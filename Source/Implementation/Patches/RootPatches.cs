using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Prepatcher;

internal static partial class HarmonyPatches
{
    internal static void PatchRootMethods()
    {
        Lg.Verbose("Patching Root methods");

        harmony.Patch(
            Loader.origAsm.GetType("Verse.Root_Play").GetMethod("Start"),
            transpiler: new HarmonyMethod(typeof(HarmonyPatches), nameof(EmptyTranspiler))
        );

        harmony.Patch(
            Loader.origAsm.GetType("Verse.Root_Entry").GetMethod("Start"),
            transpiler: new HarmonyMethod(typeof(HarmonyPatches), nameof(EmptyTranspiler))
        );

        harmony.Patch(
            Loader.origAsm.GetType("Verse.Root_Play").GetMethod("Update"),
            new HarmonyMethod(typeof(HarmonyPatches), nameof(RootUpdatePrefix))
        );

        harmony.Patch(
            Loader.origAsm.GetType("Verse.Root_Entry").GetMethod("Update"),
            new HarmonyMethod(typeof(HarmonyPatches), nameof(RootUpdatePrefix))
        );
    }

    private static bool rootUpdateRunOnce;

    private static bool RootUpdatePrefix(Root __instance)
    {
        if (!Loader.restartGame)
            return false;

        if (!rootUpdateRunOnce)
        {
            // Done to prevent a brief flash of black
            __instance.StartCoroutine(RecreateAtEndOfFrame());
            rootUpdateRunOnce = true;
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
        Lg.Verbose("Recreating comps");

        // It's important the components are iterated this way to make sure
        // they are recreated in the correct order.
        foreach (var comp in UnityEngine.Object.FindObjectsOfType<Component>())
        {
            if (comp.GetType().Assembly == Loader.newAsm) continue;

            var translation = Loader.newAsm.GetType(comp.GetType().FullName);
            if (translation == null) continue;

            try
            {
                comp.gameObject.AddComponent(translation);
                UnityEngine.Object.Destroy(comp);

                Lg.Verbose($"Recreated {comp} with new type {translation.FullName}");
            }
            catch (Exception e)
            {
                Lg.Error($"Exception recreating Unity component {comp}: {e}");
            }
        }
    }

    private static IEnumerable<CodeInstruction> EmptyTranspiler(IEnumerable<CodeInstruction> _)
    {
        yield return new CodeInstruction(OpCodes.Ret);
    }

    private static bool Cancel()
    {
        return false;
    }
}
