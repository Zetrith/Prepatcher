using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DataAssembly;
using HarmonyLib;
using Prepatcher.Process;
using Prestarter;
using RimWorld;
using Verse;
using Verse.Sound;

namespace Prepatcher;

internal static class MinimalInit
{
    internal static bool minimalInited;

    internal static IEnumerator DoInit()
    {
        yield return null;

        if (!minimalInited)
        {
            minimalInited = true;

            Lg.Verbose("Doing minimal init");

            HarmonyPatches.SilenceLogging();
            HarmonyPatches.CancelSounds();

            // Undo Vanilla Framework Expanded patches which break the mod manager
            new Harmony("prepatcher").UnpatchAll("OskarPotocki.VFECore");

            // LongEventHandler wants to show tips after uiRoot != null but none are loaded
            LongEventHandler.currentEvent.showExtraUIInfo = false;

            // Remove the queued InitializingInterface event
            LongEventHandler.ClearQueuedEvents();
            LongEventHandler.toExecuteWhenFinished.Clear();

            LanguageDatabase.InitAllMetadata();

            // ScreenshotTaker requires KeyBindingDefOf.TakeScreenshot
            KeyPrefs.data = new KeyPrefsData();
            foreach (var f in typeof(KeyBindingDefOf).GetFields())
                f.SetValue(null, new KeyBindingDef());

            // Used by the mod manager
            MessageTypeDefOf.SilentInput = new MessageTypeDef();

            Current.Root.soundRoot = new SoundRoot(); // Root.Update requires soundRoot

            PrestarterInit.Init();
        }

        Lg.Verbose("Setting Prestarter UI root");

        // Start Prestarter
        PrestarterInit.DoLoad = () =>
        {
            ReloadModAssemblies();
            Loader.Reload();
        };
        Current.Root.uiRoot = new UIRoot_Prestarter();

        DataStore.openModManager = false;
        HarmonyPatches.holdLoading = false;
    }

    // Run from Prestarter to apply potential mod list changes before running free patches
    private static void ReloadModAssemblies()
    {
        List<Assembly> modAsms = new();
        Dictionary<string, string> friendlyAssemblyNames = new()
        {
            [AssemblyCollector.AssemblyCSharp] = AssemblyCollector.AssemblyCSharp
        };

        foreach (var (friendlyName, path) in AssemblyCollector.SystemAssemblyPaths())
        {
            friendlyAssemblyNames[AssemblyName.GetAssemblyName(path).Name] = friendlyName;
        }

        foreach (var (_, friendlyName, asm) in AssemblyCollector.ModAssemblies())
        {
            // Don't add system assemblies packaged by mods
            if (friendlyAssemblyNames.TryAdd(asm.GetName().Name, friendlyName))
                modAsms.Add(asm);
        }

        static bool IgnoreRefonly(string id) =>
            id == PrepatcherMod.PrepatcherModId ||
            id == PrepatcherMod.PrepatcherModId + ModMetaData.SteamModPostfix ||
            id == PrepatcherMod.HarmonyModId ||
            id == PrepatcherMod.HarmonyModId + ModMetaData.SteamModPostfix;

        modAsms.Except(
            from m in LoadedModManager.RunningModsListForReading
            where IgnoreRefonly(m.PackageId)
            from a in m.assemblies.loadedAssemblies
            select a
        ).Do(asm =>
        {
            Lg.Verbose($"Setting refonly after mod list change: {friendlyAssemblyNames[asm.GetName().Name]}");
            UnsafeAssembly.SetReflectionOnly(asm, true);
        });

        Lg.Verbose("Reloading assemblies for changed mod list");

        // Further reloading depends only on runningMods
        LoadedModManager.runningMods.Clear();
        LoadedModManager.InitializeMods();
        foreach (var mod in LoadedModManager.RunningModsListForReading)
            mod.assemblies.ReloadAll();
    }
}
