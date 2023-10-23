namespace Prepatcher.Process;

internal static class GameProcessing
{
    internal static void Process(AssemblySet set)
    {
        var asmCSharp = set.FindAssembly(AssemblyCollector.AssemblyCSharp)!;

        // Other code assumes that these always get reloaded
        asmCSharp.SetNeedsReload();
        set.FindAssembly("0Harmony")!.SetNeedsReload();

        var monoModUtils = set.FindAssembly("MonoMod.Utils");
        monoModUtils?.SetNeedsReload();

        // Field addition
        var fieldAdder = new FieldAdder(set);
        GameInjections.RegisterInjections(fieldAdder);
        fieldAdder.ProcessAllAssemblies();

        // Fix the update order of RimWorld's reloaded Unity components
        ExecutionOrderFixer.ApplyExecutionOrderAttributes(asmCSharp.ModuleDefinition);
        asmCSharp.Modified = true; // Mark as modified so it's serialized and new attributes are applied

        // Free patching
        FreePatcher.RunPatches(
            set,
            AssemblyCollector.AssemblyCSharp,
            asm => HarmonyPatches.SetLoadingStage($"Applying prepatches from {asm.OwnerName}")
        );
    }
}
