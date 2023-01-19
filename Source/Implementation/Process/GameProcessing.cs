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
    private const string AssemblyCSharpFile = "Assembly-CSharp.dll";

    internal static void Process()
    {
        var processor = new GameAssemblyProcessor();

        // Add Assembly-CSharp
        processor.asmCSharp = processor.AddAssembly(typeof(Game).Assembly);

        // Add System and Unity assemblies
        foreach (var asmPath in Directory.GetFiles(Path.Combine(Application.dataPath, Util.ManagedFolderOS()), "*.dll"))
            if (Path.GetFileName(asmPath) != AssemblyCSharpFile)
                processor.AddAssembly(asmPath).Modifiable = false;

        var modAsms = new List<Assembly>();

        // Add mod assemblies
        foreach (var modAssembly in GetUniqueModAssemblies())
        {
            if (processor.FindModifiableAssembly(modAssembly.GetName().Name) != null) continue;
            var masm = processor.AddAssembly(modAssembly);
            masm.ProcessAttributes = true;
            modAsms.Add(modAssembly);
        }

        AddComponentInjections(processor.FieldAdder);

        processor.Process();
        FreePatcher.RunPatches(modAsms, processor.asmCSharp);

        processor.Reload();

        if (GenCommandLine.CommandLineArgPassed("dumpandexit"))
            Application.Quit();
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
