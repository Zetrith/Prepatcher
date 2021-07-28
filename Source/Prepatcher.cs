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
using Mono.Cecil.Cil;
using UnityEngine;
using Verse;
using OpCodes = System.Reflection.Emit.OpCodes;
using System.Collections;
using Verse.Steam;
using System.Diagnostics;

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

        public const string PrepatcherMarkerField = "PrepatcherMarker";
        const string AssemblyCSharp = "Assembly-CSharp.dll";
        const string AssemblyCSharpCached = "Assembly-CSharp_prepatched.dll";
        const string AssemblyCSharpCachedHash = "Assembly-CSharp_prepatched.hash";

        static int stopLoggingThread = -1;

        public PrepatcherMod(ModContentPack content) : base(content)
        {
            if (!DoLoad())
                return;

            try
            {
                Thread.CurrentThread.Abort();
            } catch (ThreadAbortException)
            {
                Prefs.data.resetModsConfigOnCrash = false;
                stopLoggingThread = Thread.CurrentThread.ManagedThreadId;
            }
        }

        static bool CheckRestarted() => AccessTools.Field(typeof(Game), PrepatcherMarkerField) != null;

        static bool DoLoad()
        {
            var clock1 = Stopwatch.StartNew();

            int existingCrc = GetExistingCRC();
            var assemblyCSharpBytes = File.ReadAllBytes(Util.DataPath(AssemblyCSharp));

            List<NewFieldData> fieldsToAdd = CollectFields(
                new CRC32().GetCrc32(new MemoryStream(assemblyCSharpBytes)),
                out int fieldCrc
            );

            Info($"CRCs: {existingCrc} {fieldCrc}, refonlys: {AppDomain.CurrentDomain.ReflectionOnlyGetAssemblies().Length}");

            if (CheckRestarted())
            {
                Info($"Restarted with the patched assembly, going silent.");
                return false;
            }

            origAsm = typeof(Game).Assembly;
            SetReflectionOnly(origAsm, true);

            MemoryStream stream;

            if (existingCrc != fieldCrc)
            {
                var clock2 = Stopwatch.StartNew();

                Info("Baking a new assembly");
                stream = new MemoryStream();
                Baker.BakeAsm(assemblyCSharpBytes, fieldsToAdd, stream, Util.DataPath(AssemblyCSharpCached));
                File.WriteAllText(Util.DataPath(AssemblyCSharpCachedHash), fieldCrc.ToString(), Encoding.UTF8);

                Info($"Baking took: {clock2.ElapsedMilliseconds}");
            }
            else
            {
                Info("CRC matches, loading cached");
                stream = new MemoryStream(File.ReadAllBytes(Util.DataPath(AssemblyCSharpCached)));
            }

            newAsm = Assembly.Load(stream.ToArray());

            SetReflectionOnly(origAsm, false);

            DoHarmonyPatches();
            UnregisterWorkshopCallbacks();

            Info("Setting refonly");

            ClearAssemblyResolve();
            SetReflectionOnly(origAsm, true);
            SetModsRefOnly();

            // This is what RimWorld itself does later
            AppDomain.CurrentDomain.AssemblyResolve += (o, a) => newAsm;

            foreach (var mod in LoadedModManager.RunningModsListForReading)
                mod.assemblies.ReloadAll();

            Info("Done loading");
            doneLoading = true;

            Info($"Took: {clock1.ElapsedMilliseconds}");

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

            harmony.Patch(
                origAsm.GetType("Verse.Log").GetMethod("Error", new[] { typeof(string) }),
                new HarmonyMethod(typeof(PrepatcherMod), nameof(LogErrorPrefix))
            );
        }

        static void UnregisterWorkshopCallbacks()
        {
            Workshop.subscribedCallback?.Unregister();
            Workshop.unsubscribedCallback?.Unregister();
            Workshop.installedCallback?.Unregister();
        }

        static void ClearAssemblyResolve()
        {
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
                }
            }

            asmResolve.SetValue(AppDomain.CurrentDomain, null);
        }

        static void SetModsRefOnly()
        {
            var dependants = Util.AssembliesDependingOn(
                LoadedModManager.RunningModsListForReading.SelectMany(m => m.assemblies.loadedAssemblies),
                 "Assembly-CSharp", "0Harmony"
            );

            foreach (var mod in LoadedModManager.RunningModsListForReading)
                foreach (var modAsm in mod.assemblies.loadedAssemblies)
                    if (dependants.Contains(modAsm.GetName().Name))
                        SetReflectionOnly(modAsm, true);
        }

        static int GetExistingCRC()
        {
            if (!File.Exists(Util.DataPath(AssemblyCSharpCached)) || !File.Exists(Util.DataPath(AssemblyCSharpCachedHash)))
                return 0;

            try { return int.Parse(File.ReadAllText(Util.DataPath(AssemblyCSharpCachedHash), Encoding.UTF8)); }
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

            foreach (var f in fieldsToAdd)
                InfoXML($"{f.ownerMod}: Parsed {f}");

            return fieldsToAdd;
        }

        const string NameAttr = "Name";
        const string FieldTypeAttr = "FieldType";
        const string TargetTypeAttr = "TargetType";
        const string IsStaticAttr = "IsStatic";
        const string DefaultValueAttr = "DefaultValue";
        const string PreferredValueAttr = "PreferredValue";

        static NewFieldData ParseFieldData(XmlElement xml, out bool success)
        {
            success = true;

            bool.TryParse(xml.Attributes[IsStaticAttr]?.Value?.ToLowerInvariant(), out bool isStatic);

            var targetTypeStr = xml.Attributes[TargetTypeAttr]?.Value;
            Type targetType = null;
            if (targetTypeStr == null || (targetType = GenTypes.GetTypeInAnyAssembly(targetTypeStr)) == null)
                success = false;

            var fieldTypeStr = xml.Attributes[FieldTypeAttr]?.Value;
            Type fieldType = null;
            if (fieldTypeStr == null || (fieldType = GenTypes.GetTypeInAnyAssembly(fieldTypeStr)) == null)
                success = false;

            object defaultValue = null;
            var defaultValueStr = xml.Attributes[DefaultValueAttr]?.Value;

            if (fieldType != null && defaultValueStr != null)
            {
                if (defaultValueStr == "new()" && fieldType.GetConstructor(new Type[0]) != null)
                    defaultValue = NewFieldData.DEFAULT_VALUE_NEW_CTOR;
                else if (Util.GetConstantOpCode(fieldType) != null)
                    defaultValue = ParseHelper.FromString(defaultValueStr, fieldType);
                else
                    success = false;
            }

            return new NewFieldData()
            {
                name = xml.Attributes[NameAttr]?.Value,
                fieldType = fieldType == null ? null : fieldTypeStr,
                targetType = targetType == null ? null : targetTypeStr,
                isStatic = isStatic,
                defaultValue = defaultValue
            };
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

        static bool LogErrorPrefix()
        {
            return Thread.CurrentThread.ManagedThreadId != stopLoggingThread;
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

        public static void Info(string msg) => Log.Message($"Prepatcher: {msg}");
        public static void InfoXML(string msg) => Log.Message($"Prepatcher XML: {msg}");
        public static void ErrorXML(string msg) => Log.Error($"Prepatcher XML: {msg}");
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

        public override string ToString()
        {
            string modStr = $"public{(isStatic ? " static" : "")}";
            return $"{modStr} {fieldType.ToStringSafe()} {targetType.ToStringSafe()}:{name.ToStringSafe()}{DefaultValueStr()};";
        }

        private string DefaultValueStr()
        {
            if (defaultValue == null)
                return "";
            return $" = {(defaultValue == DEFAULT_VALUE_NEW_CTOR ? "new ()" : defaultValue.ToStringSafe())}";
        }
    }
}
