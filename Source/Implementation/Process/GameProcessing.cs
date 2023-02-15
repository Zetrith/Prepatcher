using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonoMod.Utils;
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

        foreach (var mp in new[]
                 {
                     ("RimWorld.Building_GeneExtractor", "Finish"),
("RimWorld.ITab_Pawn_Visitor", "FillTab"),
("RimWorld.FloatMenuMakerMap", "AddDraftedOrders"),
("RimWorld.FloatMenuMakerMap", "AddHumanlikeOrders"),
("RimWorld.Dialog_BillConfig", "DoWindowContents"),
("RimWorld.Building_AncientMechRemains", "Tick"),
                 })
        {
            var m = asmCSharp.ModuleDefinition.GetType(mp.Item1).FindMethod(mp.Item2);
            using (StopwatchScope.Measure(m.FullName))
                Lg.Info(m.Body.Instructions.Count() + "");
        }

        // Other code assumes that these always get reloaded
        asmCSharp.NeedsReload = true;
        set.FindModifiableAssembly("0Harmony")!.NeedsReload = true;

        // Field addition
        var fieldAdder = new FieldAdder(set);
        RegisterInjections(fieldAdder);
        fieldAdder.ProcessAllAssemblies();

        // Free patching
        //FreePatcher.RunPatches(modAsms, asmCSharp);

        // Reload the assemblies
        Reloader.Reload(set, LoadAssembly);

        if (GenCommandLine.CommandLineArgPassed("patchandexit"))
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

    private static void RegisterInjections(FieldAdder fieldAdder)
    {
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

    private static IEnumerable<Assembly> GetUniqueModAssemblies()
    {
        return LoadedModManager.RunningModsListForReading.SelectMany(
            m => m.assemblies.loadedAssemblies
        ).Distinct();
    }
}
