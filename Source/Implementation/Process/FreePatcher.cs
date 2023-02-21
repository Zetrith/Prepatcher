using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Prepatcher.Process;

internal static class FreePatcher
{
    internal static void RunPatches(IEnumerable<Assembly> assemblies, ModifiableAssembly patchedAssembly)
    {
        Lg.Verbose("Running free patches");

        foreach (var patcher in FindAllFreePatches(assemblies))
        {
            Lg.Verbose($"Running free patch: {patcher.FullDescription()}");

            try
            {
                patcher.Invoke(null, new object[] { patchedAssembly.ModuleDefinition });
            }
            catch (Exception e)
            {
                Lg.Error($"Exception running free patch {patcher.FullDescription()}: {e}");
            }
        }
    }

    private static IEnumerable<MethodInfo> FindAllFreePatches(IEnumerable<Assembly> assemblies)
    {
        return assemblies
            .SelectMany(asm => asm.GetTypes())
            .Where(AccessTools.IsStatic)
            .SelectMany(AccessTools.GetDeclaredMethods)
            .Where(m => m.IsDefined(typeof(FreePatchAttribute)));
    }
}
