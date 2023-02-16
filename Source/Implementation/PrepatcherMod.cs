using System.Threading;
using DataAssembly;
using Verse;

namespace Prepatcher;

internal class PrepatcherMod : Mod
{
    public PrepatcherMod(ModContentPack content) : base(content)
    {
        Lg.InfoFunc = msg => Log.Message($"Prepatcher: {msg}");
        Lg.ErrorFunc = msg => Log.Error($"Prepatcher Error: {msg}");

        HarmonyPatches.PatchModLoading();

        if (DataStore.startedOnce)
        {
            Lg.Info($"Restarted with the patched assembly, going silent.");
            return;
        }

        DataStore.startedOnce = true;
        Lg.Info("Starting...");

        Loader.PreLoad();

        if (GenCommandLine.CommandLineArgPassed("noprestarter"))
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
}
