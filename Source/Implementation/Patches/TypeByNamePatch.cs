using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DataAssembly;
using HarmonyLib;
using Verse;

namespace Prepatcher;

// Mods that look their patch target up by name get whichever copy loaded first, which is the
// superseded original. They then patch a method on that, while their own parameter types
// resolve to the patched assembly, and Harmony refuses to bind one to the other because the
// two classes really are unrelated despite sharing a name.
//
// Mono never hit this: the originals are reflection-only there and drop out of the search.
// Nothing hides a loaded assembly off Mono, so redirect the lookup instead.
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

        // The game resolves class names out of XML the same way, so it can hand back a type
        // from a superseded assembly whose base class is the old game assembly's.
        var assemblies = AccessTools.PropertyGetter(typeof(GenTypes), nameof(GenTypes.AllActiveAssemblies));
        if (assemblies == null)
        {
            Lg.Error("Couldn't find GenTypes.AllActiveAssemblies to filter");
            return;
        }

        harmony.Patch(assemblies, postfix: new HarmonyMethod(typeof(TypeByNamePatch), nameof(DropSuperseded)));
        Lg.Verbose("Filtering superseded assemblies out of GenTypes");

        // The game's own by-name lookup finds types through its own cached search rather than
        // the list above, so redirect what it returns instead of what it searches.
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

        Lg.Info($"Patched assembly registry holds {DataStore.PatchedAssemblies.Count} entries");
        foreach (var probe in new[] { "CozyRainMod", "SC_Filth_Reducer", "LunarFramework" })
            Lg.Info($"  registry has {probe}: {DataStore.PatchedAssemblies.ContainsKey(probe)}");
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
