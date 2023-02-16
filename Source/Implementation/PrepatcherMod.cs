using System.Threading;
using DataAssembly;
using UnityEngine;
using Verse;

namespace Prepatcher;

internal class PrepatcherMod : Mod
{
    public static Settings settings;

    public PrepatcherMod(ModContentPack content) : base(content)
    {
        Lg.InfoFunc = msg => Log.Message($"Prepatcher: {msg}");
        Lg.ErrorFunc = msg => Log.Error($"Prepatcher Error: {msg}");

        settings = GetSettings<Settings>();

        HarmonyPatches.PatchModLoading();

        if (DataStore.startedOnce)
        {
            Lg.Info($"Restarted with the patched assembly, going silent.");
            return;
        }

        DataStore.startedOnce = true;
        Lg.Info("Starting...");

        Loader.PreLoad();

        if (GenCommandLine.CommandLineArgPassed("noprestarter") || settings.disablePrestarter)
            Loader.DoLoad();

        try
        {
            Thread.CurrentThread.Abort();
        } catch (ThreadAbortException)
        {
            // Thread abortion counts as a crash
            Prefs.data.resetModsConfigOnCrash = false;
        }
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        settings.DoSettingsWindow(inRect);
    }

    public override string SettingsCategory()
    {
        return "Prepatcher";
    }
}
