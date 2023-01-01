using System.Threading;
using HarmonyLib;
using Verse;

namespace Prepatcher;

internal class PrepatcherMod : Mod
{
    public static Harmony harmony = new Harmony("prepatcher");

    public PrepatcherMod(ModContentPack content) : base(content)
    {
        if (AccessTools.Field(typeof(Game), AssemblyProcessor.PrepatcherMarkerField) != null)
        {
            Lg.Info($"Restarted with the patched assembly, going silent.");
            return;
        }

        Loader.DoLoad();

        try
        {
            Thread.CurrentThread.Abort();
        } catch (ThreadAbortException)
        {
            Prefs.data.resetModsConfigOnCrash = false;
            HarmonyPatches.stopLoggingThread = Thread.CurrentThread.ManagedThreadId;
        }
    }
}
