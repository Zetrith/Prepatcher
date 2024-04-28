using System.Threading;
using DataAssembly;
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

        if (DataStore.startedOnce)
        {
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += (sender, args) =>
            {
                Lg.Verbose($"ReflectionOnlyAssemblyResolve: {args.RequestingAssembly} requested {args.Name}");
                return null;
            };

            Lg.Info($"Restarted with the patched assembly, going silent.");
            return;
        }

        // EditWindow_Log.wantsToOpen = false;

        DataStore.startedOnce = true;
        Lg.Info($"Starting... (vanilla load took {Time.realtimeSinceStartup}s)");

        HarmonyPatches.SilenceLogging();
        Loader.Reload();

        // Thread abortion counts as a crash
        Prefs.data.resetModsConfigOnCrash = false;

        Thread.CurrentThread.Abort();
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
