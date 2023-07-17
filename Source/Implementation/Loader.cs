using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DataAssembly;
using HarmonyLib;
using Prepatcher.Process;
using Prestarter;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Verse.Steam;

namespace Prepatcher;

internal static class Loader
{
    internal static Assembly origAsm;
    internal static Assembly newAsm;
    internal static volatile bool restartGame;
    internal static bool minimalInited;

    // Run from Prestarter to apply potential mod list changes before running free patches
    private static void ReloadModAssemblies()
    {
        var assemblyNameSet = new AssemblyNameSet();
        AssemblyCollector.PopulateAssemblySet(assemblyNameSet, out _, out var modAsms);

        modAsms.Except(
            from m in LoadedModManager.RunningModsListForReading
            where m.PackageId is PrepatcherMod.PrepatcherModId or PrepatcherMod.HarmonyModId
            from a in m.assemblies.loadedAssemblies
            select a
        ).Do(asm =>
        {
            Lg.Verbose($"Setting refonly after mod list change: {assemblyNameSet.GetFriendlyName(asm.GetName().Name)}");
            UnsafeAssembly.SetReflectionOnly(asm, true);
        });

        Lg.Verbose("Reloading assemblies for changed mod list");

        // Further reloading depends only on runningMods
        LoadedModManager.runningMods.Clear();
        LoadedModManager.InitializeMods();
        foreach (var mod in LoadedModManager.RunningModsListForReading)
            mod.assemblies.ReloadAll();
    }

    internal static void Reload()
    {
        HarmonyPatches.holdLoading = true;

        try
        {
            Lg.Verbose("Reloading the game");

            origAsm = typeof(Game).Assembly;

            var set = new AssemblySet();
            AssemblyCollector.PopulateAssemblySet(set, out var asmCSharp, out var modAsms);

            using (StopwatchScope.Measure("Game processing"))
                GameProcessing.Process(set, asmCSharp!, modAsms);

            // Reload the assemblies
            Reloader.Reload(
                set,
                LoadAssembly,
                () =>
                {
                    HarmonyPatches.SetLoadingStage("Serializing assemblies"); // Point where the mod manager can get opened
                },
                () =>
                {
                    HarmonyPatches.SetLoadingStage("Reloading game"); // Point where the mod manager can get opened

                    HarmonyPatches.PatchRootMethods();
                    UnregisterWorkshopCallbacks();
                    ClearAssemblyResolve();
                }
            );

            if (GenCommandLine.CommandLineArgPassed("patchandexit"))
                Application.Quit();

            Lg.Info("Done loading");
            restartGame = true;
        }
        catch (Exception e)
        {
            Lg.Error($"Fatal error while reloading: {e}");
        }

        if (restartGame) return;

        UnsafeAssembly.UnsetRefonlys();
        Find.Root.StartCoroutine(MinimalInit());
        Find.Root.StartCoroutine(ShowLogConsole());
    }

    private static void LoadAssembly(ModifiableAssembly asm)
    {
        Lg.Verbose($"Loading assembly: {asm}");

        var loadedAssembly = Assembly.Load(asm.Bytes);
        if (loadedAssembly.GetName().Name == AssemblyCollector.AssemblyCSharp)
        {
            newAsm = loadedAssembly;
            AppDomain.CurrentDomain.AssemblyResolve += (_, _) => loadedAssembly;
        }

        if (GenCommandLine.TryGetCommandLineArg("dumpasms", out var path) && !path.Trim().NullOrEmpty())
        {
            Directory.CreateDirectory(path);
            if (asm.Modified)
                File.WriteAllBytes(Path.Combine(path, asm.AsmDefinition.Name.Name + ".dll"), asm.Bytes!);
        }
    }

    private static IEnumerator ShowLogConsole()
    {
        yield return null;

        LongEventHandler.currentEvent = null;
        Find.WindowStack.Add(new EditWindow_Log { doCloseX = false });
        UIRoot_Prestarter.showManager = false;
    }

    internal static IEnumerator MinimalInit()
    {
        yield return null;

        if (!minimalInited)
        {
            minimalInited = true;

            Lg.Verbose("Doing minimal init");

            HarmonyPatches.SilenceLogging();
            HarmonyPatches.CancelSounds();

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
            Reload();
        };
        Current.Root.uiRoot = new UIRoot_Prestarter();

        DataStore.openModManager = false;
        HarmonyPatches.holdLoading = false;
    }

    private static void UnregisterWorkshopCallbacks()
    {
        Lg.Verbose("Unregistering workshop callbacks");

        // These hold references to old code and would get called externally by Steam
        Workshop.subscribedCallback?.Unregister();
        Workshop.unsubscribedCallback?.Unregister();
        Workshop.installedCallback?.Unregister();
    }

    private static void ClearAssemblyResolve()
    {
        Lg.Verbose("Clearing AppDomain.AssemblyResolve");

        var asmResolve = AccessTools.Field(typeof(AppDomain), "AssemblyResolve");
        var del = (Delegate)asmResolve.GetValue(AppDomain.CurrentDomain);

        // Handle MonoMod's internal dynamic assemblies
        foreach (var d in del.GetInvocationList().ToList())
        {
            if (d!.Method.DeclaringType!.Namespace!.StartsWith("MonoMod.Utils"))
            {
                foreach (var f in AccessTools.GetDeclaredFields(d.Method.DeclaringType))
                {
                    if (f.FieldType == typeof(Assembly))
                    {
                        var da = (Assembly)f.GetValue(d.Target);
                        Reloader.setRefonly.Add(da);
                    }
                }
            }
        }

        asmResolve.SetValue(AppDomain.CurrentDomain, null);
    }
}
