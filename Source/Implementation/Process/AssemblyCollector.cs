using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace Prepatcher.Process;

internal static class AssemblyCollector
{
    internal const string AssemblyCSharp = "Assembly-CSharp";

    internal static void CollectSystem(Action<string, string?, Assembly?> collector)
    {
        // Collect Assembly-CSharp
        collector(AssemblyCSharp, null, typeof(Game).Assembly);

        // Collect System and Unity assemblies
        foreach (var asmPath in Directory.GetFiles(Path.Combine(Application.dataPath, Util.ManagedFolderOS()), "*.dll"))
            collector($"(System) {Path.GetFileName(asmPath)}", asmPath, null);
    }

    internal static void CollectMods(Action<string, Assembly> collector)
    {
        // Collect mod assemblies
        foreach (var (mod, modAssembly) in GetModAssemblies())
        {
            var name = modAssembly.GetName().Name;
            collector($"(mod {mod.PackageIdPlayerFacing}) {name}", modAssembly);
        }
    }

    private static IEnumerable<(ModContentPack, Assembly)> GetModAssemblies()
    {
        return
            from m in LoadedModManager.RunningModsListForReading
            from a in m.assemblies.loadedAssemblies
            select (m, a);
    }
}
