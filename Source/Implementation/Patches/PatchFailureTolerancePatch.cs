using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Prepatcher;

// Harmony 3 rejects a patch whose injected parameter types don't line up with the method it
// patches, where 2.x passed the object across unchecked. The rejection throws out of the whole
// PatchAll, so one bad patch takes down every other patch in the same class and whichever mod
// happened to trigger the patching dies with it.
//
// Letting the bad patch fail on its own keeps the rest of the mod working. The patch itself is
// genuinely wrong and only its author can fix it, so it stays unapplied either way.
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
            Lg.Error($"Skipping patches from {description}, the rest of the mod is unaffected: {__exception.Message}");

        // A null result reads as "nothing was patched", which is what happened
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
            // Best effort only
        }

        return processor.GetType().Name;
    }
}
