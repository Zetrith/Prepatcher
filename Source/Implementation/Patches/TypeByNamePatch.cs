using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DataAssembly;
using HarmonyLib;
using Verse;

namespace Prepatcher;

// A by-name lookup returns whichever copy loaded first, which is the superseded original, so
// the mod patches a method on that while its own parameter types resolve to the patched
// assembly and Harmony won't bind the two. Mono drops the originals out of the search because
// they're reflection-only; nothing hides a loaded assembly off Mono, so redirect the result.
internal static class TypeByNamePatch
{
    internal static void Apply()
    {
        var harmony = new Harmony("prepatcher.typebyname");

        // One mod's broken patch shouldn't stop every other patch on the same method
        PatchFailureTolerancePatch.Apply(harmony);

        var target = AccessTools.Method(typeof(AccessTools), nameof(AccessTools.TypeByName));
        if (target == null)
        {
            Lg.Error("Couldn't find AccessTools.TypeByName to redirect");
            return;
        }

        harmony.Patch(target, postfix: new HarmonyMethod(typeof(TypeByNamePatch), nameof(RedirectToPatched)));
        Lg.Verbose("Redirecting AccessTools.TypeByName to the patched assemblies");

        // Class names out of XML resolve the same way and land on the same problem
        var assemblies = AccessTools.PropertyGetter(typeof(GenTypes), nameof(GenTypes.AllActiveAssemblies));
        if (assemblies == null)
        {
            Lg.Error("Couldn't find GenTypes.AllActiveAssemblies to filter");
            return;
        }

        harmony.Patch(assemblies, postfix: new HarmonyMethod(typeof(TypeByNamePatch), nameof(DropSuperseded)));
        Lg.Verbose("Filtering superseded assemblies out of GenTypes");

        // These search their own cache rather than the list above, so fix up the result
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

        // Anything already resolved was found before the redirects went on
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
