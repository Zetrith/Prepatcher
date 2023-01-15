using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace Prepatcher.Process;

internal static class GameProcessing
{
    private const string AssemblyCSharpFile = "Assembly-CSharp.dll";

    internal static void Process()
    {
        var processor = new GameAssemblyProcessor();
        processor.asmCSharp = processor.AddAssembly(typeof(Game).Assembly);

        foreach (var asmPath in Directory.GetFiles(Path.Combine(Application.dataPath, Util.ManagedFolderOS()), "*.dll"))
            if (Path.GetFileName(asmPath) != AssemblyCSharpFile)
                processor.AddAssembly(asmPath).Modifiable = false;

        foreach (var modAssembly in GetUniqueModAssemblies())
        {
            if (processor.FindModifiableAssembly(modAssembly.GetName().Name) != null) continue;
            var masm = processor.AddAssembly(modAssembly);
            masm.ProcessAttributes = true;
        }

        AddComponentInjections(processor.FieldAdder);

        processor.Process();
        processor.Reload();
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
    }

    private static IEnumerable<Assembly> GetUniqueModAssemblies()
    {
        return LoadedModManager.RunningModsListForReading.SelectMany(m => m.assemblies.loadedAssemblies).Distinct();
    }
}
