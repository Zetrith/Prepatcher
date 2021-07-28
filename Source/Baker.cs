using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Prepatcher
{
    public class Baker
    {
        public static void BakeAsm(byte[] sourceAsmBytes, List<NewFieldData> fieldsToAdd, MemoryStream writeTo, string cachePath)
        {
            var clock1 = Stopwatch.StartNew();
            using ModuleDefinition module = ModuleDefinition.ReadModule(new MemoryStream(sourceAsmBytes));
            PrepatcherMod.Info($"Reading took {clock1.ElapsedMilliseconds}");

            module.GetType("Verse.Game").Fields.Add(new FieldDefinition(
                PrepatcherMod.PrepatcherMarkerField,
                FieldAttributes.Static,
                module.TypeSystem.Int32
            ));

            foreach (var newField in fieldsToAdd)
                AddField(module, newField);

            PrepatcherMod.Info("Added fields");

            var clock2 = Stopwatch.StartNew();
            module.Write(writeTo);
            PrepatcherMod.Info($"Write to memory took {clock2.ElapsedMilliseconds}");

            var clock3 = Stopwatch.StartNew();
            File.WriteAllBytes(cachePath, writeTo.ToArray());
            PrepatcherMod.Info($"Write to file took {clock3.ElapsedMilliseconds}");
        }

        static void AddField(ModuleDefinition module, NewFieldData newField)
        {
            var fieldType = GenTypes.GetTypeInAnyAssembly(newField.fieldType);
            var ceFieldType = module.ImportReference(fieldType);

            PrepatcherMod.Info($"Patching in a new field {newField.name} of type {ceFieldType.ToStringSafe()}/{newField.fieldType} in type {newField.targetType}");

            var ceField = new FieldDefinition(
                newField.name,
                Mono.Cecil.FieldAttributes.Public,
                ceFieldType
            );

            if (newField.isStatic)
                ceField.Attributes |= Mono.Cecil.FieldAttributes.Static;

            var targetType = module.GetType(newField.targetType);
            targetType.Fields.Add(ceField);

            if (newField.defaultValue != null)
                WriteFieldInitializers(newField, ceField, fieldType);
        }

        static void WriteFieldInitializers(NewFieldData newField, FieldDefinition ceNewField, Type fieldType)
        {
            var targetType = ceNewField.DeclaringType;
            var i = targetType.Fields.IndexOf(ceNewField);

            foreach (var ctor in targetType.GetConstructors().Where(c => c.IsStatic == newField.isStatic))
            {
                if (Util.CallsAThisCtor(ctor)) continue;

                var insts = ctor.Body.Instructions;
                int insertAt = -1;
                int lastValid = -1;

                for (int k = 0; k < insts.Count; k++)
                {
                    var inst = insts[k];
                    insertAt = lastValid;

                    if (inst.OpCode == OpCodes.Call && inst.Operand is MethodDefinition m && m.IsConstructor)
                        break;

                    if (inst.OpCode == OpCodes.Stfld && inst.Operand is FieldDefinition f)
                    {
                        if (targetType.Fields.IndexOf(f) > i)
                            break;

                        lastValid = k;
                    }
                }

                insertAt++;

                var ilProc = ctor.Body.GetILProcessor();
                var insertBefore = insts[insertAt];

                if (!newField.isStatic)
                    ilProc.InsertBefore(insertBefore, Instruction.Create(OpCodes.Ldarg_0));

                if (newField.defaultValue == NewFieldData.DEFAULT_VALUE_NEW_CTOR)
                {
                    ilProc.InsertBefore(insertBefore, Instruction.Create(OpCodes.Newobj, targetType.Module.ImportReference(fieldType.GetConstructor(new Type[0]))));
                }
                else
                {
                    var defaultValueInst = Instruction.Create(OpCodes.Ret);
                    var op = Util.GetConstantOpCode(newField.defaultValue).Value;
                    defaultValueInst.OpCode = op;
                    defaultValueInst.Operand = op == OpCodes.Ldc_I4 ? Convert.ToInt32(newField.defaultValue) : newField.defaultValue;

                    ilProc.InsertBefore(insertBefore, defaultValueInst);
                }

                ilProc.InsertBefore(insertBefore, Instruction.Create(newField.isStatic ? OpCodes.Stsfld : OpCodes.Stfld, ceNewField));
            }
        }
    }
}
