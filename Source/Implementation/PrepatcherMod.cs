using System.Threading;
using HarmonyLib;
using Prepatcher.Process;
using Verse;
using Unity.Collections;

namespace Prepatcher;

internal class PrepatcherMod : Mod
{
    public PrepatcherMod(ModContentPack content) : base(content)
    {
        Lg.InfoFunc = msg => Log.Message($"Prepatcher: {msg}");
        Lg.ErrorFunc = msg => Log.Error($"Prepatcher Error: {msg}");

        if (AccessTools.Field(typeof(Game), GameAssemblyProcessor.PrepatcherMarkerField) != null)
        {
            Lg.Info($"Restarted with the patched assembly, going silent.");
            return;
        }

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
