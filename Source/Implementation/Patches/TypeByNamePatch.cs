using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DataAssembly;
using HarmonyLib;
using Verse;

namespace Prepatcher;

internal static class TypeByNamePatch
{
    internal static void Apply()
    {
        var harmony = new Harmony("prepatcher.typebyname");

        PatchFailureTolerancePatch.Apply(harmony);

        var target = AccessTools.Method(typeof(AccessTools), nameof(AccessTools.TypeByName));
        if (target == null)
        {
            Lg.Error("Couldn't find AccessTools.TypeByName to redirect");
            return;
        }

        harmony.Patch(target, postfix: new HarmonyMethod(typeof(TypeByNamePatch), nameof(RedirectToPatched)));
        Lg.Verbose("Redirecting AccessTools.TypeByName to the patched assemblies");

        var assemblies = AccessTools.PropertyGetter(typeof(GenTypes), nameof(GenTypes.AllActiveAssemblies));
        if (assemblies == null)
        {
            Lg.Error("Couldn't find GenTypes.AllActiveAssemblies to filter");
            return;
        }

        harmony.Patch(assemblies, postfix: new HarmonyMethod(typeof(TypeByNamePatch), nameof(DropSuperseded)));
        Lg.Verbose("Filtering superseded assemblies out of GenTypes");

        foreach (var lookup in new[] { nameof(GenTypes.GetTypeInAnyAssembly), "GetTypeInAnyAssemblyInt", "GetTypeInAnyAssemblyRaw" })
        {
            var method = AccessTools.Method(typeof(GenTypes), lookup);
            if (method == null)
            {
                Lg.Error($"Couldn't find GenTypes.{lookup} to redirect");
                continue;
            }

            harmony.Patch(method, postfix: new HarmonyMethod(typeof(TypeByNamePatch), nameof(RedirectToPatched)));
            Lg.Verbose($"Redirecting GenTypes.{lookup} to the patched assemblies");
        }

        GenTypes.ClearCache();

    }

    private static void DropSuperseded(ref IEnumerable<Assembly> __result)
    {
        __result = __result.Where(IsCurrent);
    }

    private static bool IsCurrent(Assembly asm)
    {
        var name = asm.GetName().Name;
        if (name == null || !DataStore.PatchedAssemblies.TryGetValue(name, out var patched))
            return true;

        return patched == asm;
    }

    private static void RedirectToPatched(ref Type __result)
    {
        if (__result?.FullName == null)
            return;

        var name = __result.Assembly.GetName().Name;
        if (name == null || !DataStore.PatchedAssemblies.TryGetValue(name, out var patched))
            return;

        if (patched == __result.Assembly)
            return;

        __result = patched.GetType(__result.FullName) ?? __result;
    }
}
