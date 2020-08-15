using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using HarmonyLib;
using Ionic.Crc;
using Microsoft.Reflection;
using UnityEngine;
using Verse;
using dnOpCode = dnlib.DotNet.Emit.OpCode;
using dnOpCodes = dnlib.DotNet.Emit.OpCodes;
using OpCodes = System.Reflection.Emit.OpCodes;

namespace Prepatcher
{
    public class NewFieldData
    {
        public static readonly object DEFAULT_VALUE_NEW_CTOR = new object();

        public string name;
        public string fieldType;
        public string targetType;
        public bool isStatic;
        public object defaultValue; // This is default(fieldType), not null for value types

        public override string ToString()
        {
            return $"public{(isStatic ? " static" : "")} {fieldType.ToStringSafe()} {targetType.ToStringSafe()}:{name.ToStringSafe()} = {(defaultValue == DEFAULT_VALUE_NEW_CTOR ? "new()" : defaultValue.ToStringSafe())};";
        }
    }

    public class PrepatcherMod : Mod
    {
        public static Harmony harmony = new Harmony("prepatcher");

        static FieldInfo MonoAssemblyField = AccessTools.Field(typeof(Assembly), "_mono_assembly");

        static Assembly newAsm;
        static IntPtr newAsmPtr;
        static IntPtr newAsmName;

        const string PrepatcherMarkerField = "PrepatcherMarker";
        const string AssemblyCSharp = "Assembly-CSharp.dll";
        const string AssemblyCSharpCached = "Assembly-CSharp_prepatched.dll";
        const string AssemblyCSharpCachedHash = "Assembly-CSharp_prepatched.hash";

        public static string ManagedFolder = Native.OSSpecifics();

        public PrepatcherMod(ModContentPack content) : base(content)
        {
            int existingCrc = 0;

            if (File.Exists(DataPath(AssemblyCSharpCached)) && File.Exists(DataPath(AssemblyCSharpCachedHash)))
                try
                {
                    existingCrc = int.Parse(File.ReadAllText(DataPath(AssemblyCSharpCachedHash), Encoding.UTF8));
                }
                catch { }

            var assemblyCSharpBytes = File.ReadAllBytes(Path.Combine(Application.dataPath, ManagedFolder, AssemblyCSharp));

            (int fieldCrc, List<NewFieldData> fieldsToAdd) =
                CollectFields(new CRC32().GetCrc32(new MemoryStream(assemblyCSharpBytes)));

            Info($"CRCs: {existingCrc} {fieldCrc}, refonlys: {AppDomain.CurrentDomain.ReflectionOnlyGetAssemblies().Length}");

            if (AccessTools.Field(typeof(Game), PrepatcherMarkerField) != null)
            {
                Info("Restarted with the patched assembly, going silent.");
                return;
            }

            Native.EarlyInit();

            var origAsm = typeof(Game).Assembly;
            SetReflectionOnly(origAsm, true);

            MemoryStream stream;

            if (existingCrc != fieldCrc)
            {
                Info("Baking a new assembly");
                BakeAsm(assemblyCSharpBytes, fieldsToAdd, stream = new MemoryStream());
                File.WriteAllText(DataPath(AssemblyCSharpCachedHash), fieldCrc.ToString(), Encoding.UTF8);
            }
            else
            {
                Info("CRC matches, loading cached");
                stream = new MemoryStream(File.ReadAllBytes(Path.Combine(Application.dataPath, ManagedFolder, AssemblyCSharpCached)));
            }

            newAsm = Assembly.Load(stream.ToArray());

            newAsmPtr = (IntPtr)MonoAssemblyField.GetValue(newAsm);
            newAsmName = Native.mono_assembly_get_name(newAsmPtr);

            SetReflectionOnly(origAsm, false);

            Info("Patching Start");

            harmony.Patch(
                origAsm.GetType("Verse.Root_Play").GetMethod("Start"),
                transpiler: new HarmonyMethod(typeof(PrepatcherMod), nameof(EmptyTranspiler))
            );

            harmony.Patch(
                origAsm.GetType("Verse.Root_Entry").GetMethod("Start"),
                transpiler: new HarmonyMethod(typeof(PrepatcherMod), nameof(EmptyTranspiler))
            );

            harmony.Patch(
                origAsm.GetType("Verse.Root").GetMethod("OnGUI"),
                transpiler: new HarmonyMethod(typeof(PrepatcherMod), nameof(EmptyTranspiler))
            );

            Info("Patching Update");

            harmony.Patch(
                origAsm.GetType("Verse.Root_Play").GetMethod("Update"),
                new HarmonyMethod(typeof(PrepatcherMod), nameof(RootUpdatePrefix))
            );

            harmony.Patch(
                origAsm.GetType("Verse.Root_Entry").GetMethod("Update"),
                new HarmonyMethod(typeof(PrepatcherMod), nameof(RootUpdatePrefix))
            );

            Info("Setting refonly");

            SetReflectionOnly(origAsm, true);

            var asmResolve = AccessTools.Field(typeof(AppDomain), "AssemblyResolve");
            var del = (Delegate)asmResolve.GetValue(AppDomain.CurrentDomain);

            // Handle MonoMod's internal dynamic assemblies
            foreach (var d in del.GetInvocationList().ToList())
            {
                if (d.Method.DeclaringType.Namespace.StartsWith("MonoMod.Utils"))
                {
                    foreach (var f in AccessTools.GetDeclaredFields(d.Method.DeclaringType))
                    {
                        if (f.FieldType == typeof(Assembly))
                        {
                            var da = (Assembly)f.GetValue(d.Target);
                            SetReflectionOnly(da, true);
                        }
                    }

                    Delegate.Remove(del, d);
                }
            }

            foreach (var mod in LoadedModManager.RunningModsListForReading)
                foreach (var modAsm in mod.assemblies.loadedAssemblies)
                    SetReflectionOnly(modAsm, true);

            foreach (var mod in LoadedModManager.RunningModsListForReading)
                mod.assemblies.ReloadAll();

            doneLoading = true;

            Info("Zzz...");

            try
            {
                Thread.Sleep(Timeout.Infinite);
            } catch(ThreadAbortException)
            {
                Info("Aborting the loading thread. This is harmless.");
            }
        }

        const string FieldsXmlFile = "Fields.xml";
        const string PrepatchesFolder = "Prepatches";

        static (int, List<NewFieldData>) CollectFields(int fieldCrc)
        {
            var fieldsToAdd = new List<NewFieldData>();

            foreach (var mod in LoadedModManager.RunningModsListForReading)
            {
                var assets = DirectXmlLoader.XmlAssetsInModFolder(mod, PrepatchesFolder);
                foreach (var ass in assets)
                {
                    if (ass.name != FieldsXmlFile) continue;

                    foreach (var e in ass.xmlDoc["Fields"].OfType<XmlElement>())
                    {
                        var parsed = ParseFieldData(e, out bool success);

                        if (success)
                        {
                            fieldsToAdd.Add(parsed);
                            InfoXML($"{mod.Name}: Parsed {parsed}");
                        }
                        else
                        {
                            ErrorXML($"{mod.Name}: Error parsing {parsed}");
                        }
                    }

                    fieldCrc = Gen.HashCombineInt(fieldCrc, new CRC32().GetCrc32(new MemoryStream(Encoding.UTF8.GetBytes(ass.xmlDoc.InnerXml))));
                }
            }

            return (fieldCrc, fieldsToAdd);
        }

        const string NameAttr = "Name";
        const string FieldTypeAttr = "FieldType";
        const string TargetTypeAttr = "TargetType";
        const string IsStaticAttr = "IsStatic";
        const string DefaultValueAttr = "DefaultValue";

        static NewFieldData ParseFieldData(XmlElement xml, out bool success)
        {
            success = true;

            bool.TryParse(xml.Attributes[IsStaticAttr]?.Value?.ToLowerInvariant(), out bool isStatic);

            var fieldType = xml.Attributes[FieldTypeAttr]?.Value;
            Type fieldTypeType = null;
            if (fieldType == null || (fieldTypeType = GenTypes.GetTypeInAnyAssembly(fieldType)) == null)
                success = false;

            var targetType = xml.Attributes[TargetTypeAttr]?.Value;
            Type targetTypeType = null;
            if (targetType == null || (targetTypeType = GenTypes.GetTypeInAnyAssembly(targetType)) == null)
                success = false;

            object defaultValue = null;
            if (fieldTypeType != null)
            {
                var str = xml.Attributes[DefaultValueAttr]?.Value;
                if (str == "new()" && fieldTypeType.GetConstructor(new Type[0]) != null)
                    defaultValue = NewFieldData.DEFAULT_VALUE_NEW_CTOR;
                else if (GetConstantOpCode(fieldTypeType) != null)
                    defaultValue = ParseHelper.FromString(str, fieldTypeType);
                else if (fieldTypeType.IsValueType)
                    defaultValue = Activator.CreateInstance(fieldTypeType);
            }

            return new NewFieldData()
            {
                name = xml.Attributes[NameAttr]?.Value,
                fieldType = fieldTypeType == null ? null : fieldType,
                targetType = targetTypeType == null ? null : targetType,
                isStatic = isStatic,
                defaultValue = defaultValue
            };
        }

        static void BakeAsm(byte[] sourceAsmBytes, List<NewFieldData> fieldsToAdd, Stream writeTo)
        {
            using var dnOrigAsm = ModuleDefMD.Load(sourceAsmBytes, new ModuleContext(new AssemblyResolver()));

            var dnCrcField = new FieldDefUser(
                PrepatcherMarkerField,
                new FieldSig(dnOrigAsm.CorLibTypes.Int32),
                dnlib.DotNet.FieldAttributes.Static
            );

            dnOrigAsm.Find("Verse.Game", true).Fields.Add(dnCrcField);

            foreach (var newField in fieldsToAdd)
                AddField(dnOrigAsm, newField);

            Info("Added fields");

            var opts = new dnlib.DotNet.Writer.ModuleWriterOptions(dnOrigAsm);
            opts.MetadataOptions.Flags |= dnlib.DotNet.Writer.MetadataFlags.PreserveAll;

            dnOrigAsm.Write(writeTo);
            dnOrigAsm.Write(DataPath(AssemblyCSharpCached));
        }

        static void AddField(ModuleDef module, NewFieldData newField)
        {
            var fieldType = GenTypes.GetTypeInAnyAssembly(newField.fieldType);
            var dnFieldType = module.Import(fieldType);

            Info($"Patching in a new field {newField.name} of type {dnFieldType.ToStringSafe()}/{newField.fieldType} in type {newField.targetType}");

            var dnNewField = new FieldDefUser(
                newField.name,
                new FieldSig(dnFieldType.ToTypeSig()),
                dnlib.DotNet.FieldAttributes.Public
            );

            if (newField.isStatic)
                dnNewField.Attributes |= dnlib.DotNet.FieldAttributes.Static;

            var targetType = module.Find(newField.targetType, true);
            targetType.Fields.Add(dnNewField);

            if (!IsNull(newField.defaultValue))
                WriteFieldInitializers(newField, dnNewField, dnFieldType);
        }

        static void WriteFieldInitializers(NewFieldData newField, FieldDef dnNewField, ITypeDefOrRef dnFieldType)
        {
            var targetType = dnNewField.DeclaringType;
            var i = targetType.Fields.IndexOf(dnNewField);

            foreach (var ctor in targetType.FindInstanceConstructors())
            {
                if (CallsAThisCtor(ctor)) continue;

                var insts = ctor.Body.Instructions;
                int insertAt = -1;

                for (int k = 0; k < insts.Count; k++)
                {
                    var inst = insts[k];

                    if (inst.OpCode == dnOpCodes.Stfld && inst.Operand is FieldDef f && targetType.Fields.IndexOf(f) > i)
                        break;

                    if (inst.OpCode == dnOpCodes.Call && inst.Operand is MethodDef m && m.IsConstructor)
                        break;

                    insertAt = k;
                }

                insertAt++;

                if (newField.defaultValue == NewFieldData.DEFAULT_VALUE_NEW_CTOR)
                {
                    insts.Insert(
                        insertAt,
                        new Instruction(dnOpCodes.Ldarg_0),
                        new Instruction(dnOpCodes.Newobj, new MemberRefUser(dnNewField.Module, ".ctor", MethodSig.CreateInstance(dnFieldType.Module.CorLibTypes.Void), dnFieldType)),
                        new Instruction(dnOpCodes.Stfld, dnNewField)
                    );
                }
                else
                {
                    insts.Insert(
                        insertAt,
                        new Instruction(dnOpCodes.Ldarg_0),
                        new Instruction(GetConstantOpCode(newField.defaultValue), newField.defaultValue),
                        new Instruction(dnOpCodes.Stfld, dnNewField)
                    );
                }
            }
        }

        static bool CallsAThisCtor(MethodDef method)
        {
            foreach (var inst in method.Body.Instructions)
                if (inst.OpCode == dnOpCodes.Call && inst.Operand is MethodDef m && m.IsConstructor && m.DeclaringType == method.DeclaringType)
                    return true;
            return false;
        }

        static bool IsNull(object val)
        {
            return val == null || (val.GetType().IsValueType && val == Activator.CreateInstance(val.GetType()));
        }

        static dnOpCode GetConstantOpCode(object c)
        {
            return GetConstantOpCode(c.GetType());
        }

        static dnOpCode GetConstantOpCode(Type t)
        {
            var code = t.GetTypeCode();

            if (code >= TypeCode.Boolean && code <= TypeCode.UInt32)
                return dnOpCodes.Ldc_I4;

            if (code >= TypeCode.Int64 && code <= TypeCode.UInt64)
                return dnOpCodes.Ldc_I8;

            if (code == TypeCode.Single)
                return dnOpCodes.Ldc_R4;

            if (code == TypeCode.Double)
                return dnOpCodes.Ldc_R8;

            if (code == TypeCode.String)
                return dnOpCodes.Ldstr;

            return null;
        }

        static string DataPath(string file)
        {
            return Path.Combine(Application.dataPath, ManagedFolder, file);
        }

        static bool doneLoading;

        static bool RootUpdatePrefix()
        {
            while (!doneLoading)
                Thread.Sleep(50);

            // It's important the components are iterated this way to make sure
            // they are recreated in the correct order.
            foreach (var comp in UnityEngine.Object.FindObjectsOfType<Component>())
            {
                var translation = newAsm.GetType(comp.GetType().FullName);
                if (translation == null) continue;
                comp.gameObject.AddComponent(translation);
                UnityEngine.Object.Destroy(comp);
            }

            return false;
        }

        static IEnumerable<CodeInstruction> EmptyTranspiler(IEnumerable<CodeInstruction> insts)
        {
            yield return new CodeInstruction(OpCodes.Ret);
        }

        // Loading two assemblies with the same name and version isn't possible with Unity's Mono.
        // It IS possible in .Net and has been fixed in more recent versions of Mono
        // but the change hasn't been backported by Unity yet.

        // This sets the assembly-to-be-duplicated as ReflectionOnly to
        // make sure it is skipped by the internal assembly searcher during loading.
        // That allows for the duplication to happen.
        public unsafe static void SetReflectionOnly(Assembly asm, bool value)
        {
            *(int*)((long)(IntPtr)MonoAssemblyField.GetValue(asm) + 0x74) = value ? 1 : 0;
        }

        static void Info(string msg) => Log.Message($"Prepatcher: {msg}");
        static void InfoXML(string msg) => Log.Message($"Prepatcher XML: {msg}");
        static void ErrorXML(string msg) => Log.Error($"Prepatcher XML: {msg}");
    }

    public static class Extensions
    {
        public static void Insert<T>(this IList<T> list, int index, params T[] items)
        {
            foreach (T item in items)
                list.Insert(index++, item);
        }
    }
}
