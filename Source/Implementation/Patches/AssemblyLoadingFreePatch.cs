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

        var replaced = 0;
        foreach (var inst in method.Body.Instructions)
            if (inst.Operand is MethodReference { Name: nameof(Assembly.LoadFrom) })
            {
                inst.Operand = module.ImportReference(typeof(AssemblyLoadingFreePatch).GetMethod(nameof(LoadFrom)));
                replaced++;
            }

        Lg.Verbose($"ReplaceAssemblyLoading: replaced {replaced} Assembly.LoadFrom call(s) in {module.Assembly.Name.Name}");
    }

    public static Assembly LoadFrom(string filePath)
    {
        var asmName = AssemblyName.GetAssemblyName(filePath);

        // Prefer the patched copy. On CoreCLR the superseded original is still listed by
        // AppDomain.GetAssemblies() and, being loaded first, would otherwise win here --
        // handing the patched game types that derive from the old Assembly-CSharp.
        Lg.Verbose($"LoadFrom: {asmName.Name} (registry holds {DataStore.PatchedAssemblies.Count})");

        if (DataStore.PatchedAssemblies.TryGetValue(asmName.Name!, out var patched))
        {
            Lg.Verbose($"LoadFrom: returning patched {asmName.Name}");
            return patched;
        }

        var asmWithName = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == asmName.Name);

        if (asmWithName != null)
        {
            Lg.Verbose($"LoadFrom: returning already-loaded {asmName.Name} (NOT from registry)");
            return asmWithName;
        }

        Lg.Verbose($"LoadFrom: loading {asmName.Name} fresh from disk");

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
