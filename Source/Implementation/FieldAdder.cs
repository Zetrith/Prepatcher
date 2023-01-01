using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodImplAttributes = Mono.Cecil.MethodImplAttributes;

namespace Prepatcher;

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

            AddField(fieldAccessor);
            PatchAccessor(fieldAccessor);
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

    private void AddField(MethodDefinition accessor)
    {
        var targetType = accessor.FirstParameterTypeResolved();
        var fieldType = accessor.ReturnType.IsByReference ? accessor.ReturnType.GetElementType() : accessor.ReturnType;
        var ceFieldType = targetType.Module.ImportReference(fieldType);

        Lg.Info($"Patching in a new field {FieldNameFromAccessor(accessor)} of type {ceFieldType} in type {targetType}");

        var ceField = new FieldDefinition(
            FieldNameFromAccessor(accessor),
            FieldAttributes.Public,
            ceFieldType
        );

        var ceTargetType = targetType.Module.Resolve(targetType);
        ceTargetType.Fields.Add(ceField);

        processor.FindModifiableAssembly(targetType)!.NeedsReload = true;
    }

    private void PatchAccessor(MethodDefinition accessor)
    {
        accessor.ImplAttributes &= ~MethodImplAttributes.InternalCall; // Unextern

        var targetType = accessor.FirstParameterTypeResolved();
        var fieldType = accessor.ReturnType.IsByReference ? accessor.ReturnType.GetElementType() : accessor.ReturnType;
        var fieldRef =
            accessor.Module.ImportReference(new FieldReference(FieldNameFromAccessor(accessor), fieldType, targetType));

        var body = accessor.Body = new MethodBody(accessor);
        var il = body.GetILProcessor();

        // Simple getter or ref-getter body
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(accessor.ReturnType.IsByReference ? OpCodes.Ldflda : OpCodes.Ldfld, fieldRef);
        il.Emit(OpCodes.Ret);

        processor.FindModifiableAssembly(accessor.DeclaringType)!.NeedsReload = true;
    }

    private static string FieldNameFromAccessor(MethodDefinition accessor)
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
