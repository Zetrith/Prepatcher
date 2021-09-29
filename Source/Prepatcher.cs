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
using UnityEngine;
using Verse;
using OpCodes = System.Reflection.Emit.OpCodes;
using System.Collections;
using Verse.Steam;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;

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
            var assemblyCSharpBytes = File.ReadAllBytes(Util.FileInManagedFolder(AssemblyCSharp));

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

            if (true || existingCrc != fieldCrc)
            {
                var clock2 = Stopwatch.StartNew();

                Info("Baking a new assembly");
                {
                    stream = new MemoryStream();
                    Baker.BakeAsm(assemblyCSharpBytes, fieldsToAdd, stream);
                    Baker.CacheData(stream, Util.FileInManagedFolder(AssemblyCSharpCached));
                    File.WriteAllText(Util.FileInManagedFolder(AssemblyCSharpCachedHash), fieldCrc.ToString(), Encoding.UTF8);
                }
                Info($"Baking took: {clock2.ElapsedMilliseconds}");
            }
            else
            {
                Info("CRC matches, loading cached");
                stream = new MemoryStream(File.ReadAllBytes(Util.FileInManagedFolder(AssemblyCSharpCached)));
            }

            var clock3 = Stopwatch.StartNew();
            Info("Loading new assembly");
            {
                newAsm = Assembly.Load(stream.ToArray());
            }
            Info($"Loading took: {clock3.ElapsedMilliseconds}");

            SetReflectionOnly(origAsm, false);

            DoHarmonyPatches();
            UnregisterWorkshopCallbacks();

            Info("Setting refonly");

            ClearAssemblyResolve();
            SetReflectionOnly(origAsm, true);

            // This is what RimWorld itself does later
            AppDomain.CurrentDomain.AssemblyResolve += (o, a) => newAsm;

            ProcessModAssemblies();

            Info("Done loading");
            doneLoading = true;

            Info($"Took: {clock1.ElapsedMilliseconds}");

            return true;
        }

        class AssemblyResolver : DefaultAssemblyResolver
        {
            private List<ModuleDefinition> modules;

            public AssemblyResolver(List<ModuleDefinition> modules)
            {
                this.modules = modules;
                AddSearchDirectory(Path.Combine(Application.dataPath, Util.ManagedFolderOS()));
            }

            public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                return modules.FirstOrDefault(m => m.Assembly.Name.Name == name.Name)?.Assembly ?? base.Resolve(name, parameters);
            }
        }

        public static void ProcessModule(ModuleDefinition module)
        {
            foreach (var t in module.Types.SelectMany(t => t.NestedTypes))
                if (!t.IsValueType && t.HasCustomAttribute("System.Runtime.CompilerServices.CompilerGeneratedAttribute"))
                {
                    var ctor = t.GetConstructors().FirstOrDefault(c => c.IsPublic && !c.IsStatic);
                    if (ctor is { HasBody: true })
                    {
                        ctor.Body.Instructions.Insert(0, Instruction.Create(Mono.Cecil.Cil.OpCodes.Ldstr, ctor.FullName));
                        ctor.Body.Instructions.Insert(1, Instruction.Create(
                            Mono.Cecil.Cil.OpCodes.Call,
                            module.ImportReference(AccessTools.Method(typeof(Allocs), nameof(Allocs.Allocd)))
                        ));
                    }
                }
        }

        static void ProcessModAssemblies()
        {
            var modAsms = GetModAssemblies();
            var newAsms = new List<byte[]>();

            var clock4 = Stopwatch.StartNew();
            {
                var modules = new List<ModuleDefinition>();

                foreach (var asm in modAsms)
                {
                    var module = ModuleDefinition.ReadModule(new MemoryStream(GetRawData(asm)), new ReaderParameters()
                    {
                        AssemblyResolver = new AssemblyResolver(modules)
                    });
                    ProcessModule(module);
                    modules.Add(module);
                }

                foreach (var m in modules)
                {
                    var stream = new MemoryStream();
                    m.Write(stream);
                    newAsms.Add(stream.ToArray());
                }
            }
            Info($"Mod assembly processing took: {clock4.ElapsedMilliseconds}");

            foreach (var modAsm in modAsms)
                SetReflectionOnly(modAsm, true);

            foreach (var newAsm in newAsms)
                Assembly.Load(newAsm);
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
                // Obsolete signature, used for cross-version compat
                origAsm.GetType("Verse.Log").GetMethod("Error", new[] { typeof(string), typeof(bool) }),
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

        static List<Assembly> GetModAssemblies()
        {
            var asms = new HashSet<Assembly>();
            var dependants = Util.AssembliesDependingOn(
                LoadedModManager.RunningModsListForReading.SelectMany(m => m.assemblies.loadedAssemblies),
                 "Assembly-CSharp", "0Harmony"
            );

            foreach (var mod in LoadedModManager.RunningModsListForReading)
                foreach (var modAsm in mod.assemblies.loadedAssemblies)
                    if (dependants.Contains(modAsm.GetName().Name))
                    {
                        asms.Add(modAsm);
                        Info($"Mod assembly: {modAsm}");
                    }

            return asms.ToList();
        }

        static int GetExistingCRC()
        {
            if (!File.Exists(Util.FileInManagedFolder(AssemblyCSharpCached)) || !File.Exists(Util.FileInManagedFolder(AssemblyCSharpCachedHash)))
                return 0;

            try { return int.Parse(File.ReadAllText(Util.FileInManagedFolder(AssemblyCSharpCachedHash), Encoding.UTF8)); }
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
            foreach (var comp in UnityEngine.Object.FindObjectsOfType<Component>())
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
        public static unsafe void SetReflectionOnly(Assembly asm, bool value)
        {
            *(int*)((IntPtr)MonoAssemblyField.GetValue(asm) + 0x74) = value ? 1 : 0;
        }

        public static unsafe byte[] GetRawData(Assembly asm)
        {
            var image = *(long*)((IntPtr)MonoAssemblyField.GetValue(asm) + 0x60);
            var rawData = *(long*)((IntPtr)image + 0x10);
            var rawDataLength = *(uint*)((IntPtr)image + 0x18);

            var arr = new byte[rawDataLength];
            Marshal.Copy((IntPtr)rawData, arr, 0, (int)rawDataLength);

            return arr;
        }

        // Obsolete signatures, used for cross-version compat
        public static void Info(string msg) => Log.Message($"Prepatcher: {msg}", false);
        public static void InfoXML(string msg) => Log.Message($"Prepatcher XML: {msg}", false);
        public static void ErrorXML(string msg) => Log.Error($"Prepatcher XML: {msg}", false);
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
