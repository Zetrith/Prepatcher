using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Prepatcher.Process;

internal static class GameProcessing
{
    private const string AssemblyCSharp = "Assembly-CSharp";

    internal static void Process()
    {
        var set = new AssemblySet();

        // Add Assembly-CSharp
        var asmCSharp = set.AddAssembly(AssemblyCSharp, typeof(Game).Assembly);

        // Add System and Unity assemblies
        foreach (var asmPath in Directory.GetFiles(Path.Combine(Application.dataPath, Util.ManagedFolderOS()), "*.dll"))
        {
            var name = AssemblyName.GetAssemblyName(asmPath);
            if (name.Name != AssemblyCSharp)
            {
                var systemAsm = set.AddAssembly($"(System) {Path.GetFileName(asmPath)}", asmPath);
                systemAsm.Modifiable = false;
            }
        }

        var modAsms = new List<Assembly>();

        // Add mod assemblies
        foreach (var (mod, modAssembly) in GetUniqueModAssemblies())
        {
            var name = modAssembly.GetName().Name;
            if (set.FindModifiableAssembly(name) != null) continue;

            var masm = set.AddAssembly($"(mod {mod.PackageIdPlayerFacing}) {name}", modAssembly);

            masm.ProcessAttributes = true;
            modAsms.Add(modAssembly);
        }

        // Other code assumes that these always get reloaded
        asmCSharp.NeedsReload = true;
        set.FindModifiableAssembly("0Harmony")!.NeedsReload = true;

        // Field addition
        var fieldAdder = new FieldAdder(set);
        RegisterInjections(fieldAdder);
        fieldAdder.ProcessAllAssemblies();

        // Free patching
        FreePatcher.RunPatches(modAsms, asmCSharp);

        // Reload the assemblies
        Reloader.Reload(set, LoadAssembly);

        if (GenCommandLine.CommandLineArgPassed("patchandexit"))
            Application.Quit();
    }

    private static void LoadAssembly(ModifiableAssembly asm)
    {
        Lg.Verbose($"Loading assembly: {asm}");

        var loadedAssembly = Assembly.Load(asm.Bytes);
        if (loadedAssembly.GetName().Name == AssemblyCSharp)
        {
            Loader.newAsm = loadedAssembly;
            AppDomain.CurrentDomain.AssemblyResolve += (_, _) => loadedAssembly;
        }

        if (GenCommandLine.TryGetCommandLineArg("dumpasms", out var path) && !path.Trim().NullOrEmpty())
        {
            Directory.CreateDirectory(path);
            if (asm.Modified)
                File.WriteAllBytes(Path.Combine(path, asm.AsmDefinition.Name.Name + ".dll"), asm.Bytes);
        }
    }

    private static void RegisterInjections(FieldAdder fieldAdder)
    {
        Lg.Verbose("Registering injections");

        fieldAdder.RegisterInjection(
            typeof(ThingWithComps),
            typeof(ThingComp),
            nameof(ThingWithComps.InitializeComps),
            nameof(ThingWithComps.comps)
        );

        fieldAdder.RegisterInjection(
            typeof(Map),
            typeof(MapComponent),
            nameof(Map.FillComponents),
            nameof(Map.components)
        );

        fieldAdder.RegisterInjection(
            typeof(World),
            typeof(WorldComponent),
            nameof(World.FillComponents),
            nameof(World.components)
        );

        fieldAdder.RegisterInjection(
            typeof(Game),
            typeof(GameComponent),
            nameof(Game.FillComponents),
            nameof(Game.components)
        );
    }

    private static IEnumerable<(ModContentPack, Assembly)> GetUniqueModAssemblies()
    {
        return
            from m in LoadedModManager.RunningModsListForReading
            from a in m.assemblies.loadedAssemblies
            select (m, a);
    }
}
