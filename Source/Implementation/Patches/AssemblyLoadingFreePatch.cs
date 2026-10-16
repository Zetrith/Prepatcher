using System.IO;
using System.Linq;
using System.Reflection;
using DataAssembly;
using HarmonyLib;
using Mono.Cecil;
using MonoMod.Utils;
using Verse;

namespace Prepatcher;

public static class AssemblyLoadingFreePatch
{
    [FreePatch]
    static void ReplaceAssemblyLoading(ModuleDefinition module)
    {
        var type = module.GetType($"{nameof(Verse)}.{nameof(ModAssemblyHandler)}");
        var method = type.FindMethod(nameof(ModAssemblyHandler.ReloadAll));

        foreach (var inst in method.Body.Instructions)
            if (inst.Operand is MethodReference { Name: nameof(Assembly.LoadFrom) })
                inst.Operand = module.ImportReference(typeof(AssemblyLoadingFreePatch).GetMethod(nameof(LoadFrom)));
    }

    public static Assembly LoadFrom(string filePath)
    {
        var asmName = AssemblyName.GetAssemblyName(filePath);

        // Off Mono the superseded original is still listed below and was loaded first,
        // so it would win the lookup. Take the patched copy if there is one.
        if (DataStore.PatchedAssemblies.TryGetValue(asmName.Name!, out var patched))
            return patched;

        var asmWithName = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == asmName.Name);

        if (asmWithName != null)
            return asmWithName;

        var rawAssembly = File.ReadAllBytes(filePath);
        var fileInfo = new FileInfo(Path.Combine(Path.GetDirectoryName(filePath)!, Path.GetFileNameWithoutExtension(filePath)) + ".pdb");
        if (fileInfo.Exists)
        {
            var rawSymbolStore = File.ReadAllBytes(fileInfo.FullName);
            return AppDomain.CurrentDomain.Load(rawAssembly, rawSymbolStore);
        }
        else
        {
            return AppDomain.CurrentDomain.Load(rawAssembly);
        }
    }
}
