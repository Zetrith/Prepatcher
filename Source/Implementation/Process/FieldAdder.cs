using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;
using MonoMod.Utils;
using Verse;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodBody = Mono.Cecil.Cil.MethodBody;
using MethodImplAttributes = Mono.Cecil.MethodImplAttributes;

namespace Prepatcher.Process;

internal class FieldAdder
{
    private AssemblyProcessor processor;
    private Dictionary<(TypeDefinition targetType, TypeDefinition compType), (MethodDefinition initMethod, FieldDefinition listField)> injectionSites = new();

    public FieldAdder(AssemblyProcessor processor)
    {
        this.processor = processor;
    }

    internal void ProcessTypes(IEnumerable<TypeDefinition> inTypes)
    {
        foreach (var accessor in GetAllPrepatcherFieldAccessors(inTypes))
            ProcessAccessor(accessor);
    }

    internal void ProcessAccessor(MethodDefinition accessor)
    {
        if (CheckFieldAccessor(accessor) is { } error)
        {
            Lg.Error($"{error} for new field with accessor {accessor.MemberFullName()}");
            return;
        }

        var newField = AddField(accessor);
        PatchAccessor(accessor, newField);

        if (HasInjection(accessor))
            AddInjectionHelper(accessor, newField);
    }

    private string? CheckFieldAccessor(MethodDefinition accessor)
    {
        if (accessor.HasBody && accessor.Body.Instructions.Count() != 0)
            return "Accessor is not extern";

        if (accessor.Parameters.Count() != 1)
            return "Accessor must have exactly one parameter";

        var target = FirstParameterTypeResolved(accessor);
        if (target == null)
            return "Couldn't resolve target type";

        if (target.IsInterface)
            return "Target type can't be an interface";

        if (!GenericArgumentsOf(accessor.Parameters.First().ParameterType).SequenceEqual(accessor.GenericParameters))
            return "The generic arguments of the target type don't match the generic parameters of the accessor";

        if (!processor.FindModifiableAssembly(target)!.Modifiable)
            return "Target type is not modifiable";

        if (HasInjection(accessor))
        {
            if (GetInjectionSite(accessor) == null)
                return "Unknown injection owner type/component type pair";

            if (accessor.ReturnType.IsByReference)
                return "Injected field cannot have a setter";
        }

        return null;
    }

    private FieldDefinition AddField(MethodDefinition accessor)
    {
        var targetType = FirstParameterTypeResolved(accessor)!;
        var fieldType = ImportFieldTypeIntoTargetModule(accessor);

        Lg.Info($"Patching in a new field {FieldName(accessor)} of type {fieldType} in type {targetType}");

        var ceField = new FieldDefinition(
            FieldName(accessor),
            FieldAttributes.Public,
            fieldType
        );

        var ceTargetType = targetType.Module.Resolve(targetType);
        ceTargetType.Fields.Add(ceField);

        var targetAsm = processor.FindModifiableAssembly(targetType)!;
        targetAsm.NeedsReload = true;
        targetAsm.Modified = true;

        return ceField;
    }

    private void PatchAccessor(MethodDefinition accessor, FieldDefinition newField)
    {
        accessor.ImplAttributes &= ~MethodImplAttributes.InternalCall; // Unextern

        var body = accessor.Body = new MethodBody(accessor);
        var il = body.GetILProcessor();

        var fieldOwner = accessor.Parameters.First().ParameterType;
        if (fieldOwner.IsByReference)
            fieldOwner = ((ByReferenceType)fieldOwner).ElementType;

        var fieldRef = new FieldReference(
            FieldName(accessor),
            accessor.Module.ImportReference(newField.FieldType, accessor.Module.ImportReference(newField.DeclaringType)),
            fieldOwner
        );

        // Simple getter or ref-getter body
        // ldarg 0
        // ldflda/ldfld newfield
        // ret

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(accessor.ReturnType.IsByReference ? OpCodes.Ldflda : OpCodes.Ldfld, fieldRef);
        il.Emit(OpCodes.Ret);

        var accessorAsm = processor.FindModifiableAssembly(accessor.DeclaringType)!;
        accessorAsm.NeedsReload = true;
        accessorAsm.Modified = true;
    }

    private void AddInjectionHelper(MethodDefinition accessor, FieldDefinition newField)
    {
        // ldtoken newfield
        // ldarg 0
        // ldarg 0
        // ldfld complist
        // call InjectionHelper.TryInject

        var (initMethod, listField) = GetInjectionSite(accessor)!.Value;

        var body = initMethod.Body;
        var retInst = body.Instructions.Last();
        body.Instructions.Remove(retInst);

        body.GetILProcessor().Emit(OpCodes.Ldtoken, newField);
        body.GetILProcessor().Emit(OpCodes.Ldarg_0);
        body.GetILProcessor().Emit(OpCodes.Ldarg_0);
        body.GetILProcessor().Emit(OpCodes.Ldfld, listField);
        body.GetILProcessor().Emit(
            OpCodes.Call,
            initMethod.Module.ImportReference(AccessTools.Method(typeof(InjectionHelper), nameof(InjectionHelper.TryInject)))
        );

        body.Instructions.Add(retInst);
    }

    internal void AddComponentInjection(Type targetType, Type compType, string initMethod, string listField)
    {
        AddComponentInjection(
            processor.ReflectionToCecil(targetType),
            processor.ReflectionToCecil(compType),
            initMethod,
            listField
        );
    }

    internal void AddComponentInjection(TypeDefinition targetType, TypeDefinition compType, string initMethod, string listField)
    {
        injectionSites[(targetType, compType)] = (
            targetType.Methods.FirstOrDefault(m => m.Name == initMethod),
            targetType.Fields.FirstOrDefault(m => m.Name == listField)
        );
    }

    private (MethodDefinition, FieldDefinition)? GetInjectionSite(MethodDefinition accessor)
    {
        var possibleTypes =
            from targetType in FirstParameterTypeResolved(accessor)!.BaseTypesAndSelfResolved()
            from fieldType in FieldType(accessor).Resolve().BaseTypesAndSelfResolved()
            select (targetType, fieldType);

        // SingleOrDefault is used to throw on ambiguity
        var siteId = possibleTypes.SingleOrDefault(p => injectionSites.ContainsKey(p));
        if (siteId == default)
            return null;

        return injectionSites[siteId];
    }

    private static TypeReference FieldType(MethodDefinition accessor)
    {
        return accessor.ReturnType.IsByReference ?
            ((ByReferenceType)accessor.ReturnType).ElementType :
            accessor.ReturnType;
    }

    private static TypeReference ImportFieldTypeIntoTargetModule(MethodDefinition accessor)
    {
        var targetType = FirstParameterTypeResolved(accessor)!;
        var fieldType = FieldType(accessor);
        return targetType.Module.ImportReference(
            fieldType,
            new DummyMethodReference(accessor.Name, targetType.Module.ImportReference(accessor.DeclaringType), targetType.GenericParameters)
        );
    }

    private static TypeDefinition? FirstParameterTypeResolved(MethodDefinition methodDef)
    {
        return methodDef.Parameters.First().ParameterType.Resolve();
    }

    private static string FieldName(MethodDefinition accessor)
    {
        return accessor.DeclaringType.Module.Assembly.ShortName() + accessor.Name + accessor.MetadataToken.RID;
    }

    internal static IEnumerable<MethodDefinition> GetAllPrepatcherFieldAccessors(IEnumerable<TypeDefinition> inTypes)
    {
        return inTypes
            .Where(t => t.IsSealed && t.IsAbstract) // IsStatic
            .SelectMany(t => t.Methods)
            .Where(m => m.HasCustomAttribute(typeof(PrepatcherFieldAttribute).FullName));
    }

    private static IEnumerable<TypeReference> GenericArgumentsOf(TypeReference t)
    {
        return t is GenericInstanceType gType ? gType.GenericArguments : Enumerable.Empty<TypeReference>();
    }

    private static bool HasInjection(MethodDefinition accessor)
    {
        return accessor.HasCustomAttribute(typeof(InjectComponentAttribute).FullName);
    }
}

internal class DummyMethodReference : MethodReference
{
    private readonly Collection<GenericParameter> genericParameters;
    public override Collection<GenericParameter> GenericParameters => genericParameters;

    public DummyMethodReference(string name, TypeReference declaringType, Collection<GenericParameter> genericParameters)
    {
        Name = name;
        DeclaringType = declaringType;
        this.genericParameters = genericParameters;
    }
}
