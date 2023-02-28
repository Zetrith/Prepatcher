using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;

namespace Prepatcher.Process;

internal partial class FieldAdder
{
    private Dictionary<(TypeDefinition targetType, TypeDefinition compType), (MethodDefinition initMethod, FieldDefinition listField)> injectionSites = new();

    internal void RegisterInjection(Type targetType, Type compType, string initMethod, string listField)
    {
        RegisterInjection(
            set.ReflectionToCecil(targetType),
            set.ReflectionToCecil(compType),
            initMethod,
            listField
        );
    }

    internal void RegisterInjection(TypeDefinition targetType, TypeDefinition compType, string initMethod, string listFieldName)
    {
        var method = targetType.Methods.FirstOrDefault(m => m.Name == initMethod);
        if (method == null)
            throw new Exception($"Injection site {targetType}:{initMethod} not found");

        var listField = targetType.Fields.FirstOrDefault(m => m.Name == listFieldName);
        if (listField == null)
            throw new Exception($"Component list field {targetType}:{listFieldName} not found");

        if (method.Body.Instructions.Last().OpCode != OpCodes.Ret)
            throw new Exception($"Expected last instruction of injection site {targetType}:{initMethod} to be Ret");

        injectionSites[(targetType, compType)] = (
            method,
            listField
        );
    }

    private void PatchInjectionSite(MethodDefinition accessor, FieldDefinition newField)
    {
        Lg.Verbose("Patching the component initialization site for injection");

        // ldtoken newfield
        // ldarg 0
        // ldarg 0
        // ldfld complist
        // call InjectionHelper.TryInject

        var (initMethod, listField) = GetInjectionSite(accessor)!.Value;

        var body = initMethod.Body;

        // Set to null in the prefix
        body.Instructions.Insert(0, Instruction.Create(OpCodes.Ldarg_0));
        body.Instructions.Insert(1, Instruction.Create(OpCodes.Ldflda, newField));
        body.Instructions.Insert(2, Instruction.Create(OpCodes.Ldarg_0));
        body.Instructions.Insert(3, Instruction.Create(
            OpCodes.Call,
            new GenericInstanceMethod(initMethod.Module.ImportReference(
                AccessTools.Method(typeof(InjectionHelper), nameof(InjectionHelper.Clear))))
            {
                GenericArguments = { newField.DeclaringType, newField.FieldType }
            }
        ));

        var retInst = body.Instructions.Last();
        body.Instructions.Remove(retInst);

        // Inject in the postfix
        body.GetILProcessor().Emit(OpCodes.Ldarg_0);
        body.GetILProcessor().Emit(OpCodes.Ldflda, newField);
        body.GetILProcessor().Emit(OpCodes.Ldarg_0);
        body.GetILProcessor().Emit(OpCodes.Ldarg_0);
        body.GetILProcessor().Emit(OpCodes.Ldfld, listField);
        body.GetILProcessor().Emit(
            OpCodes.Call,
            new GenericInstanceMethod(initMethod.Module.ImportReference(
                AccessTools.Method(typeof(InjectionHelper), nameof(InjectionHelper.TryInject))))
            {
                GenericArguments = { newField.DeclaringType, newField.FieldType }
            }
        );

        body.Instructions.Add(retInst);
    }

    // Find the unique (component owner type, component type) pair for this accessor and then
    // return the corresponding injection site
    private (MethodDefinition, FieldDefinition)? GetInjectionSite(MethodDefinition accessor)
    {
        var fieldTarget = FirstParameterTypeResolved(accessor)!;

        // (field target or its base, field type or its base)
        var possibleTypes =
            from targetType in fieldTarget.BaseTypesAndSelfResolved()
            from fieldType in FieldType(accessor).Resolve().BaseTypesAndSelfResolved()
            select (targetType, fieldType);

        // (supertype of field target, field type or its base)
        possibleTypes = possibleTypes.Concat(
            from targetType in
                injectionSites.Keys
                .Select(p => p.targetType)
                .Where(t => t != fieldTarget && t.BaseTypesAndSelfResolved().Contains(fieldTarget))
            from fieldType in FieldType(accessor).Resolve().BaseTypesAndSelfResolved()
            select (targetType, fieldType)
        );

        // SingleOrDefault is used to throw on ambiguity
        var siteId = possibleTypes.SingleOrDefault(p => injectionSites.ContainsKey(p));
        if (siteId == default)
            return null;

        return injectionSites[siteId];
    }

    private static bool HasInjection(MethodDefinition accessor)
    {
        return accessor.HasCustomAttribute(typeof(InjectComponentAttribute).FullName);
    }
}
