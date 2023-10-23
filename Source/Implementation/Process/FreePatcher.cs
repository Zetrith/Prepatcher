using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mono.Cecil;
using ICustomAttributeProvider = System.Reflection.ICustomAttributeProvider;

namespace Prepatcher.Process;

internal static class FreePatcher
{
    internal static void RunPatches(AssemblySet assemblySet, string mainAssemblyName, Action<ModifiableAssembly>? callback = null)
    {
        Lg.Verbose("Running free patches");

        var patcherAssemblies =
            from a in assemblySet.AllAssemblies
            where a.ProcessAttributes && a.SourceAssembly != null
            select a;

        var mainAssembly = assemblySet.FindAssembly(mainAssemblyName);
        if (mainAssembly == null)
            throw new Exception($"Couldn't find main assembly {mainAssemblyName} in the assembly set");

        foreach (var modifiableAssembly in patcherAssemblies)
            foreach (var patcher in FindAllFreePatches(modifiableAssembly.SourceAssembly!))
            {
                callback?.Invoke(modifiableAssembly);
                Lg.Verbose($"Running free patch: {patcher.FullDescription()}");

                if (IsDefinedSafe<FreePatchAttribute>(patcher))
                {
                    if (InvokePatcher(patcher, mainAssembly.ModuleDefinition))
                        mainAssembly.Modified = true;
                }
                else
                {
                    foreach (var asmToModify in assemblySet.AllAssemblies)
                        if (asmToModify.AllowPatches && InvokePatcher(patcher, asmToModify.ModuleDefinition))
                            asmToModify.Modified = true;
                }
            }
    }

    private static bool InvokePatcher(MethodInfo patcher, ModuleDefinition moduleToPatch)
    {
        try
        {
            var ret = patcher.Invoke(null, new object[] { moduleToPatch });
            return ret == null || (bool)ret;
        }
        catch (Exception e)
        {
            Lg.Error($"Exception running free patch {patcher.FullDescription()}: {e}");
            return false;
        }
    }

    private static bool IsDefinedSafe<T>(ICustomAttributeProvider provider) where T : Attribute
    {
        try
        {
            return provider.IsDefined(typeof(T), false);
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (TypeLoadException)
        {
            return false;
        }
        catch (MissingMethodException)
        {
            return false;
        }
    }

    private static IEnumerable<MethodInfo> FindAllFreePatches(Assembly patcherAsm)
    {
        return
            from type in patcherAsm.GetTypes()
            where AccessTools.IsStatic(type)
            from m in AccessTools.GetDeclaredMethods(type)
            where IsDefinedSafe<FreePatchAttribute>(m) || IsDefinedSafe<FreePatchAllAttribute>(m)
            select m;
    }
}
