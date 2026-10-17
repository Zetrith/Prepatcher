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

        // The game assembly on disk is already the patched one, so there's nothing to rewrite
        // and nothing to reload. Everything runs in a single assembly as the engine expects.
        if (File.Exists(Path.Combine(GenFilePaths.ConfigFolderPath, "PrepatcherDump", "USE_ONDISK")))
        {
            Lg.Info("Patched game assembly is already on disk, skipping the reload");

            // Still needed: a mod whose patch Harmony 3 rejects would otherwise take down
            // everything else patching the same method, and its own static constructor with it.
            PatchFailureTolerancePatch.Apply(new Harmony("prepatcher.tolerance"));
            return;
        }

        if (DataStore.startedOnce)
        {
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += (sender, args) =>
            {
                Lg.Verbose($"ReflectionOnlyAssemblyResolve: {args.RequestingAssembly} requested {args.Name}");
                return null;
            };

            // Runs before the other mods' classes are created, so their lookups are covered
            TypeByNamePatch.Apply();

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

        if (UnsafeAssembly.IsMonoRuntime)
        {
            Thread.CurrentThread.Abort();
        }
        else
        {
            // No Thread.Abort off Mono. The vanilla load still has to stop, and the patched
            // assemblies take over from Root.Update on the main thread, so just park it here.
            Thread.Sleep(Timeout.Infinite);
        }
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
