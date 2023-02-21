using System.Linq;
using HarmonyLib;

namespace Prepatcher.Process;

internal static class Reloader
{
    internal static void Reload(AssemblySet set, Action<ModifiableAssembly> loadAssemblyAction)
    {
        PropagateNeedsReload(set);

        // Writing and loading is split to do as little as possible after refonlys are set
        Lg.Info("Writing patched assemblies");
        using (StopwatchScope.Measure("Writing"))
            foreach (var toReload in set.AllAssemblies.Where(modAssembly => modAssembly.NeedsReload))
                toReload.PrepareByteArray();

        Lg.Info("Setting refonly");
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
            asm.NeedsReload = true;
    }
}
