using System.Threading;
using DataAssembly;
using UnityEngine;
using Verse;

namespace Prepatcher;

internal class PrepatcherMod : Mod
{
    public static Settings settings;

    private const string CmdArgNoPrestarter = "noprestarter";
    private const string CmdArgVerbose = "verbose";

    internal static ManualResetEvent abortEvent = new(false);

    public PrepatcherMod(ModContentPack content) : base(content)
    {
        InitLg();
        settings = GetSettings<Settings>();

        HarmonyPatches.PatchModLoading();

        if (DataStore.startedOnce)
        {
            Lg.Info($"Restarted with the patched assembly, going silent.");
            return;
        }

        DataStore.startedOnce = true;
        Lg.Info("Starting...");

        HarmonyPatches.SilenceLogging();

        if (GenCommandLine.CommandLineArgPassed(CmdArgNoPrestarter) || settings.disablePrestarter)
        {
            Loader.Reload();
        }
        else
        {
            // Init on main thread
            Find.Root.StartCoroutine(Loader.MinimalInit());
        }

        try
        {
            abortEvent.WaitOne();

            Lg.Verbose("Aborting loading thread");
            Thread.CurrentThread.Abort();
        } catch (ThreadAbortException)
        {
            // Thread abortion counts as a crash
            Prefs.data.resetModsConfigOnCrash = false;
        }
    }

    private static void InitLg()
    {
        Lg.InfoFunc = msg => Log.Message($"Prepatcher: {msg}");
        Lg.ErrorFunc = msg => Log.Error($"Prepatcher Error: {msg}");

        if (GenCommandLine.CommandLineArgPassed(CmdArgVerbose))
            Lg.VerboseFunc = msg => Log.Message($"Prepatcher Verbose: {msg}");
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
