using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Prepatcher.Process;

internal static class FreePatcher
{
    internal static void RunPatches(IEnumerable<Assembly> patcherAssemblies, ModifiableAssembly patchedAssembly)
    {
        Lg.Info("Running free patches");

        foreach (var patcher in
                 patcherAssemblies.SelectMany(asm => asm.GetTypes()).Where(AccessTools.IsStatic)
                     .SelectMany(AccessTools.GetDeclaredMethods).Where(m => m.GetCustomAttribute<FreePatch>() != null))
            patcher.Invoke(null, new object[] { patchedAssembly.ModuleDefinition });
    }
}
