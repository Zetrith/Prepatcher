using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml;
using dnlib.DotNet;
using HarmonyLib;
using Ionic.Crc;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Prepatcher
{
    public class NewFieldData
    {
        public string name;
        public string ofType;
        public string inType;
        public bool isStatic;
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
            var fieldsToAdd = new List<NewFieldData>();
            int fieldCrc = new CRC32().GetCrc32(new MemoryStream(assemblyCSharpBytes));

            foreach (var mod in LoadedModManager.RunningModsListForReading)
            {
                var assets = DirectXmlLoader.XmlAssetsInModFolder(mod, "Prepatches");
                foreach (var ass in assets)
                {
                    if (ass.name == "Fields.xml")
                    {
                        foreach (var f in ass.xmlDoc["Fields"])
                        {
                            Log.Message($"Prepatcher XML: found new field {((XmlElement)f).Attributes["Name"].Value}");

                            bool.TryParse(((XmlElement)f).Attributes["IsStatic"]?.Value?.ToLowerInvariant(), out bool isStatic);

                            fieldsToAdd.Add(
                                new NewFieldData()
                                {
                                    name = ((XmlElement)f).Attributes["Name"].Value,
                                    ofType = ((XmlElement)f).Attributes["OfType"].Value,
                                    inType = ((XmlElement)f).Attributes["InType"].Value,
                                    isStatic = isStatic
                                }
                            );
                        }

                        fieldCrc = Gen.HashCombineInt(fieldCrc, new CRC32().GetCrc32(new MemoryStream(Encoding.UTF8.GetBytes(ass.xmlDoc.InnerXml))));
                    }
                }
            }

            Log.Message($"Prepatcher: {existingCrc} {fieldCrc} {AppDomain.CurrentDomain.ReflectionOnlyGetAssemblies().Length}");

            if (AccessTools.Field(typeof(Game), PrepatcherMarkerField) != null)
                return;

            Native.EarlyInit();

            var origAsm = typeof(Game).Assembly;

            SetReflectionOnly(origAsm, true);

            MemoryStream stream;

            if (existingCrc != fieldCrc)
            {
                Log.Message("Prepatcher: baking a new assembly");
                BakeAsm(assemblyCSharpBytes, fieldsToAdd, stream = new MemoryStream());
                File.WriteAllText(DataPath(AssemblyCSharpCachedHash), fieldCrc.ToString(), Encoding.UTF8);
            }
            else
            {
                Log.Message("Prepatcher: CRC matches, loading cached");
                stream = new MemoryStream(File.ReadAllBytes(Path.Combine(Application.dataPath, ManagedFolder, AssemblyCSharpCached)));
            }

            newAsm = Assembly.Load(stream.ToArray());
            newAsmPtr = (IntPtr)MonoAssemblyField.GetValue(newAsm);
            newAsmName = Native.mono_assembly_get_name(newAsmPtr);

            SetReflectionOnly(origAsm, false);

            Log.Message("Patching Start");

            harmony.Patch(
                origAsm.GetType("Verse.Root_Play").GetMethod("Start"),
                transpiler: new HarmonyMethod(typeof(PrepatcherMod), nameof(EmptyTranspiler))
            );

            harmony.Patch(
                origAsm.GetType("Verse.Root_Entry").GetMethod("Start"),
                transpiler: new HarmonyMethod(typeof(PrepatcherMod), nameof(EmptyTranspiler))
            );

            Log.Message("Patching Update");

            harmony.Patch(
                origAsm.GetType("Verse.Root_Play").GetMethod("Update"),
                new HarmonyMethod(typeof(PrepatcherMod), nameof(RootUpdatePrefix))
            );

            harmony.Patch(
                origAsm.GetType("Verse.Root_Entry").GetMethod("Update"),
                new HarmonyMethod(typeof(PrepatcherMod), nameof(RootUpdatePrefix))
            );

            harmony.Patch(
                origAsm.GetType("Verse.Root").GetMethod("OnGUI"),
                transpiler: new HarmonyMethod(typeof(PrepatcherMod), nameof(EmptyTranspiler))
            );

            Log.Message("Setting refonly");

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

            Log.Message("Zzz...");

            Thread.Sleep(Timeout.Infinite);
        }

        static void BakeAsm(byte[] asmCSharp, List<NewFieldData> fieldsToAdd, Stream writeTo)
        {
            using var dnOrigAsm = ModuleDefMD.Load(asmCSharp, new ModuleContext(new AssemblyResolver()));

            var dnCrcField = new FieldDefUser(
                PrepatcherMarkerField,
                new FieldSig(dnOrigAsm.CorLibTypes.Int32),
                dnlib.DotNet.FieldAttributes.Static
            );

            dnOrigAsm.Find("Verse.Game", true).Fields.Add(dnCrcField);

            foreach (var fieldToAdd in fieldsToAdd)
            {
                var fieldType = GenTypes.GetTypeInAnyAssembly(fieldToAdd.ofType);
                Log.Message($"Patching in a new field {fieldToAdd.name} of type {fieldType.ToStringSafe()}/{fieldToAdd.ofType} in type {fieldToAdd.inType}");

                var dnNewField = new FieldDefUser(
                    fieldToAdd.name,
                    new FieldSig(dnOrigAsm.ImportAsTypeSig(fieldType)),
                    dnlib.DotNet.FieldAttributes.Public
                );

                if (fieldToAdd.isStatic)
                    dnNewField.Attributes |= dnlib.DotNet.FieldAttributes.Static;

                dnOrigAsm.Find(fieldToAdd.inType, true).Fields.Add(dnNewField);
            }

            Log.Message("Added fields");

            var opts = new dnlib.DotNet.Writer.ModuleWriterOptions(dnOrigAsm);
            opts.MetadataOptions.Flags |= dnlib.DotNet.Writer.MetadataFlags.PreserveAll;

            dnOrigAsm.Write(writeTo);
            dnOrigAsm.Write(DataPath(AssemblyCSharpCached));
        }

        static string DataPath(string file)
        {
            return Path.Combine(Application.dataPath, ManagedFolder, file);
        }

        static bool inited;
        static bool doneLoading;

        static bool RootUpdatePrefix()
        {
            if (!inited)
            {
                //Native.mono_install_assembly_search_hook(Marshal.GetFunctionPointerForDelegate((Func<IntPtr, IntPtr, IntPtr>)AssemblySearch), IntPtr.Zero);
                inited = true;
            }

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

        static IntPtr AssemblySearch(IntPtr aname, IntPtr data)
        {
            if (StrsEqual(Native.mono_assembly_name_get_name(newAsmName), Native.mono_assembly_name_get_name(aname)))
                return newAsmPtr;

            return IntPtr.Zero;
        }

        unsafe static bool StrsEqual(IntPtr a, IntPtr b)
        {
            if (a == IntPtr.Zero || b == IntPtr.Zero)
                return a == b;

            byte* aa = (byte*)a;
            byte* bb = (byte*)b;

            while (*aa != 0 && *bb != 0)
            {
                if (*aa != *bb)
                    return false;
                aa++;
                bb++;
            }

            return *aa == *bb;
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
    }
}
