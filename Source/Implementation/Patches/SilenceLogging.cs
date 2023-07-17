using HarmonyLib;
using UnityEngine;
using Verse;

namespace Prepatcher;

internal static partial class HarmonyPatches
{
    internal static void SilenceLogging()
    {
        // Don't print thread abortion errors to log
        harmony.Patch(
            typeof(Log).GetMethod("Error", new[] { typeof(string) }),
            new HarmonyMethod(typeof(HarmonyPatches), nameof(LogErrorPrefix))
        );

        // Don't show "uninitialized DefOf" warnings in the console
        harmony.Patch(
            typeof(Log).GetMethod("Warning", new[] { typeof(string) }),
            new HarmonyMethod(typeof(HarmonyPatches), nameof(LogWarningPrefix))
        );
    }

    private static bool LogErrorPrefix(string text)
    {
        return !text.Contains("ThreadAbortException");
    }

    private static bool LogWarningPrefix(string text)
    {
        if (!text.Contains("Tried to use an uninitialized DefOf"))
            return true;
        Debug.LogWarning(text);
        return false;
    }
}
