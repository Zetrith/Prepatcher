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
    public const string EnvVarNoPrestarter = "NoPrestarter";

    internal static volatile bool holdLoading = true;

    public PrepatcherMod(ModContentPack content) : base(content)
    {
        InitLg();
        settings = GetSettings<Settings>();

        HarmonyPatches.PatchModLoading();
        HarmonyPatches.PatchRestarting();

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

        EditWindow_Log.wantsToOpen = false;

        DataStore.startedOnce = true;
        Lg.Info($"Starting... (vanilla load took {Time.realtimeSinceStartup}s)");

        HarmonyPatches.SilenceLogging();

        if (GenCommandLine.CommandLineArgPassed(CmdArgNoPrestarter) ||
            !Environment.GetEnvironmentVariable(EnvVarNoPrestarter).NullOrEmpty() ||
            settings.disablePrestarter)
        {
            Loader.Reload();
        }
        else
        {
            // Init on main thread
            Find.Root.StartCoroutine(Loader.MinimalInit());
        }

        // Thread abortion counts as a crash
        Prefs.data.resetModsConfigOnCrash = false;

        while (holdLoading)
            Thread.Sleep(50);

        Thread.CurrentThread.Abort();
    }

    private static void InitLg()
    {
        Lg._infoFunc = msg => Log.Message($"Prepatcher: {msg}");
        Lg._errorFunc = msg => Log.Error($"Prepatcher Error: {msg}");

        if (GenCommandLine.CommandLineArgPassed(CmdArgVerbose))
            Lg._verboseFunc = msg => Log.Message($"Prepatcher Verbose: {msg}");
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
