using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;
using HarmonyLib;
using Ionic.Crc;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using UnityEngine;
using Verse;
using cecilOpCodes = Mono.Cecil.Cil.OpCodes;
using cecilOpCode = Mono.Cecil.Cil.OpCode;
using OpCodes = System.Reflection.Emit.OpCodes;
using System.Collections;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using RimWorld;
using System.Numerics;

namespace Prepatcher
{
    [StaticConstructorOnStartup]
    static class PrepatcherStatic
    {
        static PrepatcherStatic()
        {
        }
    }

    public class PrepatcherMod : Mod
    {
        public static Harmony harmony = new Harmony("prepatcher");

        static FieldInfo MonoAssemblyField = AccessTools.Field(typeof(Assembly), "_mono_assembly");

        static Assembly origAsm;
        static Assembly newAsm;

        const string PrepatcherMarkerField = "PrepatcherMarker";
        const string AssemblyCSharp = "Assembly-CSharp.dll";
        const string AssemblyCSharpCached = "Assembly-CSharp_prepatched.dll";
        const string AssemblyCSharpCachedHash = "Assembly-CSharp_prepatched.hash";

        public static string ManagedFolder = ManagedFolderOS();

        public PrepatcherMod(ModContentPack content) : base(content)
        {
            if (!DoLoad())
                return;

            Info("Zzz...");

            try
            {
                Thread.Sleep(Timeout.Infinite);
            } catch(ThreadAbortException)
            {
                Info("Aborting the loading thread. This is harmless.");
            }
        }

        static bool DoLoad()
        {
            int existingCrc = GetExistingCRC();
            var assemblyCSharpBytes = File.ReadAllBytes(Path.Combine(Application.dataPath, ManagedFolder, AssemblyCSharp));

            List<NewFieldData> fieldsToAdd = CollectFields(
                new CRC32().GetCrc32(new MemoryStream(assemblyCSharpBytes)),
                out int fieldCrc
            );

            Info($"CRCs: {existingCrc} {fieldCrc}, refonlys: {AppDomain.CurrentDomain.ReflectionOnlyGetAssemblies().Length}");

            if (AccessTools.Field(typeof(Game), PrepatcherMarkerField) != null)
            {
                // Actually assign the enum values
                foreach (var f in fieldsToAdd.Where(f => f.isEnum))
                {
                    var targetType = GenTypes.GetTypeInAnyAssembly(f.targetType);
                    targetType.GetField(f.name).SetValue(null, f.defaultValue);
                }

                Info($"Restarted with the patched assembly, going silent.");
                return false;
            }

            origAsm = typeof(Game).Assembly;
            SetReflectionOnly(origAsm, true);

            MemoryStream stream;

            if (existingCrc != fieldCrc)
            {
                Info("Baking a new assembly");
                stream = new MemoryStream();
                BakeAsm(assemblyCSharpBytes, fieldsToAdd, stream);
                File.WriteAllText(DataPath(AssemblyCSharpCachedHash), fieldCrc.ToString(), Encoding.UTF8);
            }
            else
            {
                Info("CRC matches, loading cached");
                stream = new MemoryStream(File.ReadAllBytes(Path.Combine(Application.dataPath, ManagedFolder, AssemblyCSharpCached)));
            }

            newAsm = Assembly.Load(stream.ToArray());

            SetReflectionOnly(origAsm, false);

            DoHarmonyPatches();

            Info("Setting refonly");

            SetAllRefOnly();

            foreach (var mod in LoadedModManager.RunningModsListForReading)
                mod.assemblies.ReloadAll();

            doneLoading = true;

            return true;
        }

        static void DoHarmonyPatches()
        {
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
                new HarmonyMethod(typeof(PrepatcherMod), nameof(RootOnGUIPrefix))
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
        }

        static void SetAllRefOnly()
        {
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
                    if (!modAsm.GetName().Name.StartsWith("UnityEngine")) // Some mods include Unity's dlls, this is bad
                        SetReflectionOnly(modAsm, true);
        }

        static int GetExistingCRC()
        {
            if (!File.Exists(DataPath(AssemblyCSharpCached)) || !File.Exists(DataPath(AssemblyCSharpCachedHash)))
                return 0;

            try { return int.Parse(File.ReadAllText(DataPath(AssemblyCSharpCachedHash), Encoding.UTF8)); }
            catch { return 0; }
        }

        const string FieldsXmlFile = "Fields.xml";
        const string PrepatchesFolder = "Prepatches";

        static List<NewFieldData> CollectFields(int inCrc, out int outCrc)
        {
            outCrc = inCrc;
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
                        parsed.ownerMod = mod.Name;

                        if (success)
                            fieldsToAdd.Add(parsed);
                        else
                            ErrorXML($"{mod.Name}: Error parsing {parsed}");
                    }

                    outCrc = Gen.HashCombineInt(outCrc, new CRC32().GetCrc32(new MemoryStream(Encoding.UTF8.GetBytes(ass.xmlDoc.InnerXml))));
                }
            }

            foreach (var fieldsPerType in fieldsToAdd.Where(f => f.isEnum).GroupBy(f => f.targetType))
            {
                var targetType = GenTypes.GetTypeInAnyAssembly(fieldsPerType.Key);
                var enumType = Enum.GetUnderlyingType(targetType);
                var max = ToUInt64(enumType.GetField("MaxValue").GetRawConstantValue());
                var taken = AccessTools.GetDeclaredFields(targetType).Where(f => f.IsLiteral).Select(f => ToUInt64(f.GetRawConstantValue())).ToHashSet();

                foreach (var f in fieldsPerType)
                {
                    var free = FindNotTaken(f.enumPreferred, max, taken);
                    if (free == null)
                    {
                        ErrorXML($"{f.ownerMod}: Couldn't assign a value for {f}");
                        fieldsToAdd.Remove(f);
                        continue;
                    }

                    f.defaultValue = FromUInt64(free.Value, enumType);
                    taken.Add(free.Value);
                }
            }

            foreach (var f in fieldsToAdd)
                InfoXML($"{f.ownerMod}: Parsed {f}");

            return fieldsToAdd;
        }

        static ulong ToUInt64(object obj)
        {
            if (obj is ulong u)
                return u;
            return (ulong)Convert.ToInt64(obj);
        }

        static object FromUInt64(ulong from, Type to) => Type.GetTypeCode(to) switch
        {
            TypeCode.Byte => (byte)from,
            TypeCode.SByte => (sbyte)from,
            TypeCode.Int16 => (short)from,
            TypeCode.UInt16 => (ushort)from,
            TypeCode.Int32 => (int)from,
            TypeCode.UInt32 => (uint)from,
            TypeCode.Int64 => (long)from,
            TypeCode.UInt64 => from,
            _ => null
        };

        static ulong? FindNotTaken(ulong start, ulong max, HashSet<ulong> taken)
        {
            // TODO maybe wrap around?
            for (ulong i = start; i <= max; i++)
            {
                if (taken.Contains(i)) continue;
                return i;
            }

            return null;
        }

        const string EnumElement = "Enum";
        const string NameAttr = "Name";
        const string FieldTypeAttr = "FieldType";
        const string TargetTypeAttr = "TargetType";
        const string IsStaticAttr = "IsStatic";
        const string DefaultValueAttr = "DefaultValue";
        const string PreferredValueAttr = "PreferredValue";

        static NewFieldData ParseFieldData(XmlElement xml, out bool success)
        {
            success = true;

            var isEnum = xml.Name == EnumElement;
            bool.TryParse(xml.Attributes[IsStaticAttr]?.Value?.ToLowerInvariant(), out bool isStatic);

            var targetType = xml.Attributes[TargetTypeAttr]?.Value;
            Type targetTypeType = null;
            if (targetType == null || (targetTypeType = GenTypes.GetTypeInAnyAssembly(targetType)) == null)
                success = false;

            var fieldType = isEnum ? targetType : xml.Attributes[FieldTypeAttr]?.Value;
            Type fieldTypeType = null;
            if (fieldType == null || (fieldTypeType = GenTypes.GetTypeInAnyAssembly(fieldType)) == null)
                success = false;

            object defaultValue = null;
            var defaultValueStr = xml.Attributes[DefaultValueAttr]?.Value;

            if (fieldTypeType != null && defaultValueStr != null)
            {
                if (defaultValueStr == "new()" && fieldTypeType.GetConstructor(new Type[0]) != null)
                    defaultValue = NewFieldData.DEFAULT_VALUE_NEW_CTOR;
                else if (GetConstantOpCode(fieldTypeType) != null)
                    defaultValue = ParseHelper.FromString(defaultValueStr, fieldTypeType);
                else
                    success = false;
            }

            ulong preferredValue = 1;
            var preferredValueStr = xml.Attributes[PreferredValueAttr]?.Value;
            if (preferredValueStr != null)
            {
                preferredValue = 
                    (ulong?)new UInt64Converter().TryConvert(preferredValueStr) ??
                    (ulong?)(long?)new Int64Converter().TryConvert(preferredValueStr) ??
                    1;
            }

            return new NewFieldData()
            {
                name = xml.Attributes[NameAttr]?.Value,
                fieldType = fieldTypeType == null ? null : fieldType,
                targetType = targetTypeType == null ? null : targetType,
                isStatic = isStatic | isEnum,
                defaultValue = defaultValue,
                isEnum = isEnum,
                enumPreferred = preferredValue
            };
        }

        static void BakeAsm(byte[] sourceAsmBytes, List<NewFieldData> fieldsToAdd, MemoryStream writeTo)
        {
            using ModuleDefinition module = ModuleDefinition.ReadModule(new MemoryStream(sourceAsmBytes));

            module.GetType("Verse.Game").Fields.Add(new FieldDefinition(
                PrepatcherMarkerField,
                Mono.Cecil.FieldAttributes.Static,
                module.TypeSystem.Int32
            ));

            foreach (var newField in fieldsToAdd)
                AddField(module, newField);

            Info("Added fields");

            module.Write(writeTo);
            File.WriteAllBytes(DataPath(AssemblyCSharpCached), writeTo.ToArray());
        }

        static void AddField(ModuleDefinition module, NewFieldData newField)
        {
            var fieldType = GenTypes.GetTypeInAnyAssembly(newField.fieldType);
            var ceFieldType = module.ImportReference(fieldType);

            Info($"Patching in a new field {newField.name} of type {ceFieldType.ToStringSafe()}/{newField.fieldType} in type {newField.targetType}");

            var ceField = new FieldDefinition(
                newField.name,
                Mono.Cecil.FieldAttributes.Public,
                ceFieldType
            );

            if (newField.isEnum)
            {
                ceField.Attributes |= Mono.Cecil.FieldAttributes.InitOnly | Mono.Cecil.FieldAttributes.HasDefault;
                ceField.Constant = newField.defaultValue;
            }

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
                if (CallsAThisCtor(ctor)) continue;

                var insts = ctor.Body.Instructions;
                int insertAt = -1;
                int lastValid = -1;

                for (int k = 0; k < insts.Count; k++)
                {
                    var inst = insts[k];
                    insertAt = lastValid;

                    if (inst.OpCode == cecilOpCodes.Call && inst.Operand is MethodDefinition m && m.IsConstructor)
                        break;

                    if (inst.OpCode == cecilOpCodes.Stfld && inst.Operand is FieldDefinition f)
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
                    ilProc.InsertBefore(insertBefore, Instruction.Create(cecilOpCodes.Ldarg_0));

                if (newField.defaultValue == NewFieldData.DEFAULT_VALUE_NEW_CTOR)
                {
                    ilProc.InsertBefore(insertBefore, Instruction.Create(cecilOpCodes.Newobj, targetType.Module.ImportReference(fieldType.GetConstructor(new Type[0]))));
                }
                else
                {
                    var defaultValueInst = Instruction.Create(cecilOpCodes.Ret);
                    var op = GetConstantOpCode(newField.defaultValue).Value;
                    defaultValueInst.OpCode = op;
                    defaultValueInst.Operand = op == cecilOpCodes.Ldc_I4 ? Convert.ToInt32(newField.defaultValue) : newField.defaultValue;

                    ilProc.InsertBefore(insertBefore, defaultValueInst); 
                }

                ilProc.InsertBefore(insertBefore, Instruction.Create(newField.isStatic ? cecilOpCodes.Stsfld : cecilOpCodes.Stfld, ceNewField));
            }
        }

        static bool CallsAThisCtor(MethodDefinition method)
        {
            foreach (var inst in method.Body.Instructions)
                if (inst.OpCode == cecilOpCodes.Call && inst.Operand is MethodDefinition m && m.IsConstructor && m.DeclaringType == method.DeclaringType)
                    return true;
            return false;
        }

        static cecilOpCode? GetConstantOpCode(object c)
        {
            return GetConstantOpCode(c.GetType());
        }

        static cecilOpCode? GetConstantOpCode(Type t)
        {
            var code = Type.GetTypeCode(t);

            if (code >= TypeCode.Boolean && code <= TypeCode.UInt32)
                return cecilOpCodes.Ldc_I4;

            if (code >= TypeCode.Int64 && code <= TypeCode.UInt64)
                return cecilOpCodes.Ldc_I8;

            if (code == TypeCode.Single)
                return cecilOpCodes.Ldc_R4;

            if (code == TypeCode.Double)
                return cecilOpCodes.Ldc_R8;

            if (code == TypeCode.String)
                return cecilOpCodes.Ldstr;

            return null;
        }

        static string DataPath(string file)
        {
            return Path.Combine(Application.dataPath, ManagedFolder, file);
        }

        static bool doneLoading;
        static bool runOnce;

        static bool RootUpdatePrefix(Root __instance)
        {
            while (!doneLoading)
                Thread.Sleep(50);

            if (!runOnce)
            {
                // Done to prevent a brief flash of black
                __instance.StartCoroutine(RecreateAtEndOfFrame());
                runOnce = true;
            }
            else
            {
                RecreateComponents();
            }

            return false;
        }

        static IEnumerator RecreateAtEndOfFrame()
        {
            yield return new WaitForEndOfFrame();
            RecreateComponents();
        }

        static void RecreateComponents()
        {
            // It's important the components are iterated this way to make sure
            // they are recreated in the correct order.
            foreach (var comp in UnityEngine.Object.FindObjectsOfType<UnityEngine.Component>())
            {
                var translation = newAsm.GetType(comp.GetType().FullName);
                if (translation == null) continue;
                comp.gameObject.AddComponent(translation);
                UnityEngine.Object.Destroy(comp);
            }
        }

        static IEnumerable<CodeInstruction> EmptyTranspiler(IEnumerable<CodeInstruction> insts)
        {
            yield return new CodeInstruction(OpCodes.Ret);
        }

        static bool RootOnGUIPrefix()
        {
            return true;
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

        static string ManagedFolderOS()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "Resources/Data/Managed";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Managed";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "Managed";
            return null;
        }
    }

    public class NewFieldData
    {
        public static readonly object DEFAULT_VALUE_NEW_CTOR = new object();

        public string ownerMod;
        public string name;
        public string fieldType;
        public string targetType;
        public bool isStatic;
        public object defaultValue;
        public bool isEnum;
        public ulong enumPreferred;

        public override string ToString()
        {
            string modStr = isEnum ? "enum" : $"public{(isStatic ? " static" : "")}";
            return $"{modStr} {fieldType.ToStringSafe()} {targetType.ToStringSafe()}:{name.ToStringSafe()}{DefaultValueStr()};";
        }

        private string DefaultValueStr()
        {
            if (defaultValue == null)
                return "";
            return $" = {(defaultValue == DEFAULT_VALUE_NEW_CTOR ? "new ()" : defaultValue.ToStringSafe())}";
        }
    }

    public static class Extensions
    {
        public static void Insert<T>(this IList<T> list, int index, params T[] items)
        {
            foreach (T item in items)
                list.Insert(index++, item);
        }

        public static object TryConvert(this TypeConverter converter, string s)
        {
            try
            {
                return converter.ConvertFromInvariantString(s);
            }
            catch
            {
                return null;
            }
        }
    }
}
