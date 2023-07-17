using System.Collections.Generic;
using System.Reflection;

namespace Prepatcher.Process;

internal static class GameProcessing
{
    internal static void Process(AssemblySet set, ModifiableAssembly asmCSharp, List<Assembly> modAsms)
    {
        // Other code assumes that these always get reloaded
        asmCSharp.NeedsReload = true;
        set.FindAssembly("0Harmony")!.NeedsReload = true;

        var monoModUtils = set.FindAssembly("MonoMod.Utils");
        if (monoModUtils != null)
            monoModUtils.NeedsReload = true;

        // Field addition
        var fieldAdder = new FieldAdder(set);
        GameInjections.RegisterInjections(fieldAdder);
        fieldAdder.ProcessAllAssemblies();

        // Fix the update order of RimWorld's reloaded Unity components
        ExecutionOrderFixer.ApplyExecutionOrderAttributes(asmCSharp.ModuleDefinition);
        asmCSharp.Modified = true; // Mark as modified so it's serialized and new attributes are applied

        // Free patching
        FreePatcher.RunPatches(modAsms, asmCSharp);
    }
}
