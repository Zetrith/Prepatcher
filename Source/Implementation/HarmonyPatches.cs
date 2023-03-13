using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Prepatcher;

internal static class HarmonyPatches
{
    private static bool runOnce;
    private static Harmony harmony = new("prepatcher");

    internal static void PatchModLoading()
    {
        Lg.Verbose("Patching mod loading");

        // If a mod needs to loadAfter brrainz.harmony, then also loadAfter zetrith.prepatcher
        harmony.Patch(
            typeof(ModMetaData.ModMetaDataInternal).GetMethod("InitVersionedData"),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(InitVersionedDataPostfix))
        );

        // Let Prepatcher satisfy modDependencies on brrainz.harmony
        harmony.Patch(
            typeof(ModDependency).GetProperty("IsSatisfied")!.GetGetMethod(),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(IsSatisfiedPostfix))
        );

        // Fixup already loaded mods
        foreach (var modMeta in ModLister.AllInstalledMods.Select(m => m.meta))
            InitVersionedDataPostfix(modMeta);
    }

    internal static void PatchRestarting()
    {
        harmony.Patch(
            typeof(GenCommandLine).GetMethod("Restart"),
            transpiler: new HarmonyMethod(typeof(HarmonyPatches), nameof(RestartPatch))
        );
    }

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

    internal static void DoHarmonyPatchesForMinimalInit()
    {
        // Cancel MusicManagerEntryUpdate because it requires SongDefOf.EntrySong != null
        harmony.Patch(
            typeof(MusicManagerEntry).GetMethod(nameof(MusicManagerEntry.MusicManagerEntryUpdate)),
            new HarmonyMethod(typeof(HarmonyPatches), nameof(Cancel))
        );
    }

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

    private static bool RootUpdatePrefix(Root __instance)
    {
        if (!Loader.restartGame)
            return false;

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
            if (comp.GetType().Assembly == Loader.newAsm) continue;

            var translation = Loader.newAsm.GetType(comp.GetType().FullName);
            if (translation == null) continue;

            try
            {
                comp.gameObject.AddComponent(translation);
                UnityEngine.Object.Destroy(comp);
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

    private static void InitVersionedDataPostfix(ModMetaData.ModMetaDataInternal __instance)
    {
        if (__instance.loadAfter.Any(s => s.ToLowerInvariant() == "brrainz.harmony") &&
            !__instance.loadAfter.Any(s => s.ToLowerInvariant() == "zetrith.prepatcher"))
            __instance.loadAfter.Add("zetrith.prepatcher");
    }

    private static bool IsSatisfiedPostfix(bool result, ModDependency __instance)
    {
        return result ||
               __instance.packageId.ToLowerInvariant() == "brrainz.harmony" &&
               ModLister.GetActiveModWithIdentifier("zetrith.prepatcher", ignorePostfix: true) != null;
    }

    private static IEnumerable<CodeInstruction> RestartPatch(IEnumerable<CodeInstruction> insts)
    {
        var processStart = AccessTools.Method(typeof(System.Diagnostics.Process), nameof(System.Diagnostics.Process.Start));
        foreach (var inst in insts)
        {
            if (inst.operand == processStart)
            {
                yield return new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.Method(typeof(HarmonyPatches), nameof(SetNoPrestarterEnvVariable)));
            }

            yield return inst;
        }
    }

    private static void SetNoPrestarterEnvVariable()
    {
        // The env variables will get inherited by the child process started in GenCommandLine.Restart
        Environment.SetEnvironmentVariable(PrepatcherMod.EnvVarNoPrestarter, "1");
    }
}
