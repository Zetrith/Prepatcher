using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Prepatcher.Process;

internal static class Reloader
{
    internal static List<Assembly> setRefonly = new();

    internal static void Reload(AssemblySet set, Action<ModifiableAssembly> loadAssemblyAction, Action? beforeSerialization = null, Action? beforeRefOnlys = null)
    {
        PropagateNeedsReload(set);

        beforeSerialization?.Invoke();

        // Serializing and loading is split to do as little as possible after refonlys are set
        Lg.Info("Serializing patched assemblies");
        using (StopwatchScope.Measure("Serializing"))
            foreach (var toReload in set.AllAssemblies.Where(modAssembly => modAssembly.NeedsReload))
                toReload.SerializeToByteArray();

        beforeRefOnlys?.Invoke();

        Lg.Info("Setting refonly");
        foreach (var toSet in setRefonly)
            UnsafeAssembly.SetReflectionOnly(toSet, true);

        foreach (var toReload in set.AllAssemblies.Where(modAssembly => modAssembly.NeedsReload))
            toReload.SetSourceRefOnly();

        Lg.Info("Loading patched assemblies");
        foreach (var toReload in set.AllAssemblies.Where(modAssembly => modAssembly.NeedsReload))
            loadAssemblyAction(toReload);
    }

    private static void PropagateNeedsReload(AssemblySet set)
    {
        var assembliesToReloadStart = set.AllAssemblies.Where(m => m.NeedsReload);
        var assemblyToDependants = set.AllAssembliesToDependants();

        foreach (var asm in Util.BFS(assembliesToReloadStart, asm => assemblyToDependants.GetValueSafe(asm) ?? Enumerable.Empty<ModifiableAssembly>()))
            asm.SetNeedsReload();
    }
}
