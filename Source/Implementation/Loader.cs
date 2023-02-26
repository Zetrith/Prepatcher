using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Prepatcher.Process;
using Prestarter;
using RimWorld;
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

    internal static void Reload()
    {
        PrepatcherMod.abortEvent.Set();

        try
        {
            Lg.Verbose("Reloading the game");

            origAsm = typeof(Game).Assembly;

            Lg.Verbose("Reloading active mods");

            // Reinit after potential mod list changes from Prestarter
            LoadedModManager.runningMods.Clear();
            LoadedModManager.InitializeMods();
            foreach (var mod in LoadedModManager.RunningModsListForReading)
                mod.assemblies.ReloadAll();

            Lg.Verbose("Patching and clearing");

            HarmonyPatches.PatchRootMethods();
            UnregisterWorkshopCallbacks();
            ClearAssemblyResolve();

            using (StopwatchScope.Measure("Game processing"))
                GameProcessing.Process();

            // Only restart if no errors were logged
            if (!EditWindow_Log.wantsToOpen)
            {
                Lg.Info("Done loading");
                restartGame = true;
            }
        }
        catch (Exception e)
        {
            Lg.Error($"Exception while reloading: {e}");
        }

        if (!restartGame)
        {
            UnsafeAssembly.UnsetRefonlys();
            Find.Root.StartCoroutine(MinimalInit());
            Find.Root.StartCoroutine(ShowLogConsole());
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

        if (minimalInited)
            yield break;

        minimalInited = true;

        Lg.Verbose("Doing minimal init");

        HarmonyPatches.DoHarmonyPatchesForMinimalInit();

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

        Current.Root.soundRoot = new SoundRoot(); // Root.Update requires soundRoot

        Lg.Verbose("Setting Prestarter UI root");

        // Start Prestarter
        PrestarterInit.DoLoad = Reload;
        Current.Root.uiRoot = new UIRoot_Prestarter();

        PrestarterInit.Init();

        PrepatcherMod.abortEvent.Set();
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
            if (d.Method.DeclaringType.Namespace.StartsWith("MonoMod.Utils"))
            {
                foreach (var f in AccessTools.GetDeclaredFields(d.Method.DeclaringType))
                {
                    if (f.FieldType == typeof(Assembly))
                    {
                        var da = (Assembly)f.GetValue(d.Target);
                        UnsafeAssembly.SetReflectionOnly(da, true);
                    }
                }
            }
        }

        asmResolve.SetValue(AppDomain.CurrentDomain, null);
    }
}
