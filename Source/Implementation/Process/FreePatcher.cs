﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ICustomAttributeProvider = System.Reflection.ICustomAttributeProvider;

namespace Prepatcher.Process;

internal static class FreePatcher
{
    internal static void RunPatches(IEnumerable<Assembly> assemblies, ModifiableAssembly assemblyToModify)
    {
        Lg.Verbose("Running free patches");

        foreach (var patcher in FindAllFreePatches(assemblies))
        {
            Lg.Verbose($"Running free patch: {patcher.FullDescription()}");

            try
            {
                assemblyToModify.Modified = true;
                patcher.Invoke(null, new object[] { assemblyToModify.ModuleDefinition });
            }
            catch (Exception e)
            {
                Lg.Error($"Exception running free patch {patcher.FullDescription()}: {e}");
            }
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

    private static IEnumerable<MethodInfo> FindAllFreePatches(IEnumerable<Assembly> assemblies)
    {
        return
            from asm in assemblies
            from type in asm.GetTypes()
            where AccessTools.IsStatic(type)
            from m in AccessTools.GetDeclaredMethods(type)
            where IsDefinedSafe<FreePatchAttribute>(m)
            select m;
    }
}
