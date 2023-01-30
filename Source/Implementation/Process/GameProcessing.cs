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
    private const string AssemblyCSharpFile = "Assembly-CSharp.dll";

    internal static void Process()
    {
        var set = new AssemblySet();

        // Add Assembly-CSharp
        var asmCSharp = set.AddAssembly(typeof(Game).Assembly);

        // Add System and Unity assemblies
        foreach (var asmPath in Directory.GetFiles(Path.Combine(Application.dataPath, Util.ManagedFolderOS()), "*.dll"))
            if (Path.GetFileName(asmPath) != AssemblyCSharpFile)
                set.AddAssembly(asmPath).Modifiable = false;

        var modAsms = new List<Assembly>();

        // Add mod assemblies
        foreach (var modAssembly in GetUniqueModAssemblies())
        {
            if (set.FindModifiableAssembly(modAssembly.GetName().Name) != null) continue;
            var masm = set.AddAssembly(modAssembly);
            masm.ProcessAttributes = true;
            modAsms.Add(modAssembly);
        }

        asmCSharp.NeedsReload = true;
        set.FindModifiableAssembly("0Harmony")!.NeedsReload = true;

        var fieldAdder = new FieldAdder(set);
        AddComponentInjections(fieldAdder);
        fieldAdder.ProcessAllAssemblies();

        FreePatcher.RunPatches(modAsms, asmCSharp);

        Reloader.Reload(set, LoadAssembly);

        if (GenCommandLine.CommandLineArgPassed("dumpandexit"))
            Application.Quit();
    }

    private static void LoadAssembly(ModifiableAssembly asm)
    {
        var loadedAssembly = Assembly.Load(asm.Bytes);
        if (loadedAssembly.GetName().Name == AssemblyCSharp)
        {
            Loader.newAsm = loadedAssembly;
            AppDomain.CurrentDomain.AssemblyResolve += (_, _) => loadedAssembly;
        }

        if (GenCommandLine.TryGetCommandLineArg("dumpasms", out var path))
        {
            Directory.CreateDirectory(path);
            if (asm.Modified)
                File.WriteAllBytes(Path.Combine(path, asm.AsmDefinition.Name.Name + ".dll"), asm.Bytes);
        }
    }

    private static void AddComponentInjections(FieldAdder fieldAdder)
    {
        fieldAdder.AddComponentInjection(
            typeof(ThingWithComps),
            typeof(ThingComp),
            nameof(ThingWithComps.InitializeComps),
            nameof(ThingWithComps.comps)
        );

        fieldAdder.AddComponentInjection(
            typeof(Map),
            typeof(MapComponent),
            nameof(Map.FillComponents),
            nameof(Map.components)
        );

        fieldAdder.AddComponentInjection(
            typeof(World),
            typeof(WorldComponent),
            nameof(World.FillComponents),
            nameof(World.components)
        );

        fieldAdder.AddComponentInjection(
            typeof(Game),
            typeof(GameComponent),
            nameof(Game.FillComponents),
            nameof(Game.components)
        );
    }

    private static IEnumerable<Assembly> GetUniqueModAssemblies()
    {
        return LoadedModManager.RunningModsListForReading.SelectMany(
            m => m.assemblies.loadedAssemblies
        ).Distinct();
    }
}
