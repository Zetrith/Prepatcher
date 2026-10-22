using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Prepatcher;

internal static class PatchFailureTolerancePatch
{
    internal static void Apply(Harmony harmony)
    {
        Tolerate(harmony, AccessTools.TypeByName("HarmonyLib.PatchClassProcessor"));
        Tolerate(harmony, AccessTools.TypeByName("HarmonyLib.PatchProcessor"));
    }

    private static void Tolerate(Harmony harmony, Type? processor)
    {
        if (processor == null)
            return;

        var patch = AccessTools.Method(processor, "Patch", Type.EmptyTypes);
        if (patch == null)
        {
            Lg.Error($"Couldn't find {processor.Name}.Patch to make failures non-fatal");
            return;
        }

        harmony.Patch(patch, finalizer: new HarmonyMethod(typeof(PatchFailureTolerancePatch), nameof(SwallowFailure)));
        Lg.Verbose($"Patch failures in {processor.Name} are now non-fatal");
    }

    private static readonly HashSet<string> Reported = new();

    private static Exception? SwallowFailure(Exception? __exception, object __instance, ref object? __result)
    {
        if (__exception == null)
            return null;

        var description = Describe(__instance);
        if (Reported.Add(description))
            Lg.Error($"Skipping patches from {description}: {__exception.Message}");

        __result = null;
        return null;
    }

    private static string Describe(object processor)
    {
        try
        {
            var container = AccessTools.Field(processor.GetType(), "containerType")?.GetValue(processor) as Type;
            if (container != null)
                return container.FullName ?? container.Name;

            var original = AccessTools.Field(processor.GetType(), "original")?.GetValue(processor) as MethodBase;
            if (original != null)
                return original.FullDescription();
        }
        catch
        {
        }

        return processor.GetType().Name;
    }
}
