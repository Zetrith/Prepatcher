using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using MonoMod.Utils;

namespace Prepatcher.Process;

internal partial class FieldAdder
{
    private void PatchCtorsWithDefault(FieldDefinition newField, CustomAttribute attribute)
    {
        Lg.Verbose("Patching the ctors with field constant default");

        var defaultValueObj = attribute.ConstructorArguments.First().Value;
        var defaultValue = defaultValueObj is CustomAttributeArgument arg ? arg.Value : defaultValueObj;

        if (defaultValue == null)
            return;

        ReplaceRetsInCtors(newField.DeclaringType, () => new []
        {
            Instruction.Create(OpCodes.Ldarg_0),
            Type.GetTypeCode(defaultValue.GetType()) switch
            {
                >= TypeCode.Boolean and <= TypeCode.UInt32 => new Instruction(OpCodes.Ldc_I4, (int)Convert.ToInt64(defaultValue)),
                TypeCode.Int64 => new Instruction(OpCodes.Ldc_I8, defaultValue),
                TypeCode.UInt64 => new Instruction(OpCodes.Ldc_I8, (long)(ulong)defaultValue),
                TypeCode.Single => new Instruction(OpCodes.Ldc_R4, defaultValue),
                TypeCode.Double => new Instruction(OpCodes.Ldc_R8, defaultValue),
                TypeCode.String => new Instruction(OpCodes.Ldstr, defaultValue),
                _ => throw new Exception($"Unknown constant default type {defaultValue.GetType()}")
            },
            Instruction.Create(OpCodes.Stfld, newField),
            Instruction.Create(OpCodes.Ret)
        });
    }

    private void PatchCtorsWithInitializer(MethodDefinition accessor, FieldDefinition newField, CustomAttribute attribute)
    {
        Lg.Verbose("Patching the ctors with field initializer");

        var initializerMethodName = (string)attribute.ConstructorArguments.First().Value;
        var initializer = accessor.DeclaringType.FindMethod(initializerMethodName);

        ReplaceRetsInCtors(newField.DeclaringType, () => new []
        {
            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(initializer.Parameters.Count() == 1 ? OpCodes.Ldarg_0 : OpCodes.Nop),
            Instruction.Create(OpCodes.Call, newField.Module.ImportReference(initializer)),
            Instruction.Create(OpCodes.Stfld, newField),
            Instruction.Create(OpCodes.Ret)
        });
    }

    private void ReplaceRetsInCtors(TypeDefinition typeDef, Func<IEnumerable<Instruction>> replacementGetter)
    {
        foreach (var ctor in typeDef.GetConstructors().Where(c => !c.IsStatic))
        {
            if (CallsAThisCtor(ctor)) continue;

            var replacementList = replacementGetter().ToList();
            var firstReplacement = replacementList[0];
            replacementList.RemoveAt(0);

            var insts = ctor.Body.Instructions;

            for (int i = insts.Count() - 1; i >= 0; i--)
            {
                var inst = insts.ElementAt(i);
                if (inst.OpCode != OpCodes.Ret) continue;

                // Preserve jumps to Ret
                inst.OpCode = firstReplacement.OpCode;
                inst.Operand = firstReplacement.Operand;

                insts.InsertRange(i + 1, replacementList);
            }
        }
    }

    private static CustomAttribute? GetExplicitDefaultValue(MethodDefinition accessor)
    {
        return accessor.GetCustomAttribute(typeof(DefaultValueAttribute).FullName);
    }

    private static CustomAttribute? GetValueInitializer(MethodDefinition accessor)
    {
        return accessor.GetCustomAttribute(typeof(ValueInitializerAttribute).FullName);
    }

    private static bool CallsAThisCtor(MethodDefinition method)
    {
        foreach (var inst in method.Body.Instructions)
            if (inst.OpCode == OpCodes.Call && inst.Operand is MethodDefinition { IsConstructor: true } m && m.DeclaringType == method.DeclaringType)
                return true;
        return false;
    }
}
