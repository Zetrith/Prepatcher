using System.IO;
using System.Threading;
using DataAssembly;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Prepatcher;

internal class PrepatcherMod : Mod
{
    private const string CmdArgVerbose = "verbose";

    internal const string PrepatcherModId = "zetrith.prepatcher";
    internal const string HarmonyModId = "brrainz.harmony";

    public PrepatcherMod(ModContentPack content) : base(content)
    {
        InitLg();

        HarmonyPatches.PatchModLoading();
        HarmonyPatches.AddVerboseProfiling();
        HarmonyPatches.PatchGUI();

        HarmonyPatches.SetLoadingStage("Initializing Prepatcher");

        // Getting here at all means the patched assemblies loaded, so there's nothing to do
        // beyond the Harmony fix below.
        if (OnDisk.UpToDate())
        {
            OnDisk.MarkStarted();
            Lg.Info("Running on the patched assemblies");
            PatchFailureTolerancePatch.Apply(new Harmony("prepatcher.tolerance"));
            return;
        }

        // Installed, asked for a restart, and came back to a game that doesn't match what we
        // built. Put the originals back rather than leave a mismatched pair in place.
        if (OnDisk.RestartFailed)
        {
            Lg.Error("The patched assemblies don't match this game, restoring the originals");
            OnDisk.MarkStarted();
            OnDisk.Restore();
            PatchFailureTolerancePatch.Apply(new Harmony("prepatcher.tolerance"));
            return;
        }

        DataStore.startedOnce = true;
        Lg.Info($"Starting... (vanilla load took {Time.realtimeSinceStartup}s)");

        HarmonyPatches.SilenceLogging();

        try
        {
            Loader.GenerateToDisk();
            var installed = OnDisk.Install();
            Lg.Info($"Installed {installed.Count} patched assemblies, restarting");
        }
        catch (Exception e)
        {
            Lg.Error($"Couldn't prepare the patched assemblies: {e}");
            OnDisk.Restore();
            PatchFailureTolerancePatch.Apply(new Harmony("prepatcher.tolerance"));
            return;
        }

        Prefs.data.resetModsConfigOnCrash = false;
        GenCommandLine.Restart();
    }

    private static void InitLg()
    {
        Lg._infoFunc = msg => Log.Message($"Prepatcher: {msg}");
        Lg._errorFunc = msg => Log.Error($"Prepatcher Error: {msg}");

        if (GenCommandLine.CommandLineArgPassed(CmdArgVerbose))
            Lg._verboseFunc = msg => Log.Message($"Prepatcher Verbose: {msg}");
    }

    public override string SettingsCategory()
    {
        return "Prepatcher";
    }
}
