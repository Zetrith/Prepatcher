using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Prepatcher;

internal static class HarmonyPatches
{
    private static bool runOnce;
    private static Harmony harmony = new("prepatcher");

    internal static void DoHarmonyPatchesForMinimalInit()
    {
        // Don't print thread abortion errors to log
        harmony.Patch(
            typeof(Log).GetMethod("Error", new[] { typeof(string) }),
            new HarmonyMethod(typeof(HarmonyPatches), nameof(LogErrorPrefix))
        );

        // Cancel MusicManagerEntryUpdate because it requires SongDefOf.EntrySong != null
        harmony.Patch(
            typeof(MusicManagerEntry).GetMethod("MusicManagerEntryUpdate"),
            new HarmonyMethod(typeof(HarmonyPatches), nameof(Cancel))
        );
    }

    internal static void PatchRootMethods()
    {
        Lg.Info("Patching Start");

        harmony.Patch(
            Loader.origAsm.GetType("Verse.Root_Play").GetMethod("Start"),
            transpiler: new HarmonyMethod(typeof(HarmonyPatches), nameof(EmptyTranspiler))
        );

        harmony.Patch(
            Loader.origAsm.GetType("Verse.Root_Entry").GetMethod("Start"),
            transpiler: new HarmonyMethod(typeof(HarmonyPatches), nameof(EmptyTranspiler))
        );

        harmony.Patch(
            Loader.origAsm.GetType("Verse.Root").GetMethod("OnGUI"),
            new HarmonyMethod(typeof(HarmonyPatches), nameof(RootOnGUIPrefix))
        );

        Lg.Info("Patching Update");

        harmony.Patch(
            Loader.origAsm.GetType("Verse.Root_Play").GetMethod("Update"),
            new HarmonyMethod(typeof(HarmonyPatches), nameof(RootUpdatePrefix))
        );

        harmony.Patch(
            Loader.origAsm.GetType("Verse.Root_Entry").GetMethod("Update"),
            new HarmonyMethod(typeof(HarmonyPatches), nameof(RootUpdatePrefix))
        );
    }

    private static bool RootUpdatePrefix(Root __instance)
    {
        while (!Loader.doneLoading)
            Thread.Sleep(50);

        if (!runOnce)
        {
            // Done to prevent a brief flash of black
            __instance.StartCoroutine(RecreateAtEndOfFrame());
            runOnce = true;
        }
        else
        {
            RecreateComponents();
        }

        return false;
    }

    private static IEnumerator RecreateAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        RecreateComponents();
    }

    private static void RecreateComponents()
    {
        // It's important the components are iterated this way to make sure
        // they are recreated in the correct order.
        foreach (var comp in UnityEngine.Object.FindObjectsOfType<Component>())
        {
            var translation = Loader.newAsm.GetType(comp.GetType().FullName);
            if (translation == null) continue;
            comp.gameObject.AddComponent(translation);
            UnityEngine.Object.Destroy(comp);
        }
    }

    private static IEnumerable<CodeInstruction> EmptyTranspiler(IEnumerable<CodeInstruction> _)
    {
        yield return new CodeInstruction(OpCodes.Ret);
    }

    private static bool RootOnGUIPrefix()
    {
        return true;
    }

    private static bool Cancel()
    {
        return false;
    }

    private static bool LogErrorPrefix(string text)
    {
        return !text.Contains("ThreadAbortException");
    }
}
