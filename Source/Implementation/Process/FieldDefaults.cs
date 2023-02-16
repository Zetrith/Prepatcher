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
        var obj = attribute.ConstructorArguments.First().Value;
        var defaultValue = obj is CustomAttributeArgument arg ? arg.Value : obj;

        if (defaultValue == null)
            return;

        var opcode = GetConstantOpCode(defaultValue.GetType())!.Value;

        InsertDefaultValueInstructions(newField.DeclaringType, new []
        {
            Instruction.Create(OpCodes.Ldarg_0),
            new Instruction(
                opcode,
                opcode == OpCodes.Ldc_I4 ? Convert.ToInt32(defaultValue) : defaultValue
            ),
            Instruction.Create(OpCodes.Stfld, newField)
        });
    }

    private void PatchCtorsWithInitializer(MethodDefinition accessor, FieldDefinition newField, CustomAttribute attribute)
    {
        var initializer = accessor.DeclaringType.FindMethod((string)attribute.ConstructorArguments.First().Value);

        InsertDefaultValueInstructions(newField.DeclaringType, new []
        {
            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(initializer.Parameters.Count() == 1 ? OpCodes.Ldarg_0 : OpCodes.Nop),
            Instruction.Create(OpCodes.Call, newField.Module.ImportReference(initializer)),
            Instruction.Create(OpCodes.Stfld, newField)
        });
    }

    private void InsertDefaultValueInstructions(TypeDefinition typeDef, IEnumerable<Instruction> newInsts)
    {
        foreach (var ctor in typeDef.GetConstructors().Where(c => !c.IsStatic))
        {
            if (CallsAThisCtor(ctor)) continue;

            var insts = ctor.Body.Instructions;

            for (int i = insts.Count() - 1; i >= 0; i--)
            {
                var inst = insts.ElementAt(i);
                if (inst.OpCode != OpCodes.Ret) continue;
                foreach (var newInst in newInsts.Reverse())
                    insts.Insert(i, newInst);
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

    private static OpCode? GetConstantOpCode(Type t)
    {
        return Type.GetTypeCode(t) switch
        {
            >= TypeCode.Boolean and <= TypeCode.UInt32 => OpCodes.Ldc_I4,
            >= TypeCode.Int64 and <= TypeCode.UInt64 => OpCodes.Ldc_I8,
            TypeCode.Single => OpCodes.Ldc_R4,
            TypeCode.Double => OpCodes.Ldc_R8,
            TypeCode.String => OpCodes.Ldstr,
            _ => null
        };
    }
}
