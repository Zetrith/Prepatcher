using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using MonoMod.Utils;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodImplAttributes = Mono.Cecil.MethodImplAttributes;

namespace Prepatcher.Process;

internal class FieldAdder
{
    private AssemblyProcessor processor;

    public FieldAdder(AssemblyProcessor processor)
    {
        this.processor = processor;
    }

    internal void ProcessModAssembly(ModifiableAssembly assembly)
    {
        foreach (var fieldAccessor in GetAllPrepatcherFieldAccessors(assembly.ModuleDefinition))
        {
            if (CheckFieldAccessor(fieldAccessor) is { } error)
            {
                Lg.Error(error);
                continue;
            }

            var newField = AddField(fieldAccessor);
            PatchAccessor(fieldAccessor, newField);
        }
    }

    private string? CheckFieldAccessor(MethodDefinition accessor)
    {
        if ((accessor.ImplAttributes & MethodImplAttributes.InternalCall) != 0)
            return $"Accessor {accessor.MemberFullName()} is not extern";

        if (accessor.Parameters.Count() != 1)
            return $"Accessor {accessor.MemberFullName()} has wrong parameter count";

        var target = accessor.FirstParameterTypeResolved();
        if (target == null)
            return $"Couldn't resolve target type for new field with accessor {accessor.MemberFullName()}";

        if (!processor.FindModifiableAssembly(target)!.Modifiable)
            return $"Target type {target} for new field with accessor {accessor.MemberFullName()} is not modifiable";

        return null;
    }

    private FieldDefinition AddField(MethodDefinition accessor)
    {
        var targetType = accessor.FirstParameterTypeResolved();
        var fieldType = FieldTypeInResolvedTarget(accessor);

        Lg.Info($"Patching in a new field {FieldName(accessor)} of type {fieldType} in type {targetType}");

        var ceField = new FieldDefinition(
            FieldName(accessor),
            FieldAttributes.Public,
            fieldType
        );

        var ceTargetType = targetType.Module.Resolve(targetType);
        ceTargetType.Fields.Add(ceField);

        processor.FindModifiableAssembly(targetType)!.NeedsReload = true;

        return ceField;
    }

    private void PatchAccessor(MethodDefinition accessor, FieldDefinition newField)
    {
        accessor.ImplAttributes &= ~MethodImplAttributes.InternalCall; // Unextern

        var body = accessor.Body = new MethodBody(accessor);
        var il = body.GetILProcessor();

        var fieldRef = new FieldReference(
            FieldName(accessor),
            accessor.Module.ImportReference(newField.FieldType, accessor.Module.ImportReference(newField.DeclaringType)),
            accessor.Parameters.First().ParameterType
        );

        // Simple getter or ref-getter body
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(accessor.ReturnType.IsByReference ? OpCodes.Ldflda : OpCodes.Ldfld, fieldRef);
        il.Emit(OpCodes.Ret);

        processor.FindModifiableAssembly(accessor.DeclaringType)!.NeedsReload = true;
    }

    private static TypeReference FieldType(MethodDefinition accessor)
    {
        return accessor.ReturnType.IsByReference ? ((ByReferenceType)accessor.ReturnType).ElementType : accessor.ReturnType;
    }

    private static TypeReference FieldTypeInResolvedTarget(MethodDefinition accessor)
    {
        var targetType = accessor.FirstParameterTypeResolved();
        var fieldType = FieldType(accessor);
        return targetType.Module.ImportReference(
            fieldType,
            new DummyMethodReference(accessor.Name, targetType.Module.ImportReference(accessor.DeclaringType), targetType.GenericParameters)
        );
    }

    private static string FieldName(MethodDefinition accessor)
    {
        return accessor.DeclaringType.Module.Assembly.ShortName() + accessor.Name + accessor.MetadataToken.RID;
    }

    private static IEnumerable<MethodDefinition> GetAllPrepatcherFieldAccessors(ModuleDefinition module)
    {
        return module.Types
            .Where(t => t.IsSealed && t.IsAbstract) // IsStatic
            .SelectMany(t => t.Methods)
            .Where(m => m.HasCustomAttribute(typeof(PrepatcherFieldAttribute).FullName));
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
