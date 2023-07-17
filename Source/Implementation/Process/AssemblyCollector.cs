using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace Prepatcher.Process;

internal class AssemblyCollector
{
    internal const string AssemblyCSharp = "Assembly-CSharp";

    internal static void PopulateAssemblySet(IAssemblySet set, out ModifiableAssembly? asmCSharp, out List<Assembly> modAsms)
    {
        // Add Assembly-CSharp
        asmCSharp = set.AddAssembly(AssemblyCSharp, typeof(Game).Assembly);

        // Add System and Unity assemblies
        foreach (var asmPath in Directory.GetFiles(Path.Combine(Application.dataPath, Util.ManagedFolderOS()), "*.dll"))
        {
            var name = AssemblyName.GetAssemblyName(asmPath);
            if (name.Name != AssemblyCSharp)
            {
                var systemAsm = set.AddAssembly($"(System) {Path.GetFileName(asmPath)}", asmPath);
                if (systemAsm != null)
                    systemAsm.AllowPatches = false;
            }
        }

        modAsms = new List<Assembly>();

        // Add mod assemblies
        foreach (var (mod, modAssembly) in GetModAssemblies())
        {
            var name = modAssembly.GetName().Name;
            if (set.HasAssembly(name)) continue;

            var masm = set.AddAssembly($"(mod {mod.PackageIdPlayerFacing}) {name}", modAssembly);
            if (masm != null)
                masm.ProcessAttributes = true;

            modAsms.Add(modAssembly);
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
