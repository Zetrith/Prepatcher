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

    internal static IEnumerable<(string, string)> SystemAssemblyPaths()
    {
        // Collect System and Unity assemblies
        return
            Directory.GetFiles(SystemAssemblyFolder(), "*.dll").
                Select(asmPath => ($"(System) {Path.GetFileName(asmPath)}", asmPath));
    }

    private static string SystemAssemblyFolder()
    {
        var gameAsmPath = typeof(Game).Assembly.Location;
        if (!string.IsNullOrEmpty(gameAsmPath) && File.Exists(gameAsmPath))
            return Path.GetDirectoryName(gameAsmPath)!;

        return Path.Combine(Application.dataPath, Util.ManagedFolderOS());
    }

    internal static string? TryGetAssemblyName(string path)
    {
        try
        {
            return AssemblyName.GetAssemblyName(path).Name;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    internal static IEnumerable<(string, string, Assembly)> ModAssemblies()
    {
        foreach (var (mod, modAssembly) in GetModAssemblies())
        {
            var name = modAssembly.GetName().Name;
            yield return (mod.Name, $"(mod {mod.PackageIdPlayerFacing}) {name}", modAssembly);
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
