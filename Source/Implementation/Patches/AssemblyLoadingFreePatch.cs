using System.IO;
using System.Linq;
using System.Reflection;
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
        var asmWithName = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == asmName.Name);

        if (asmWithName != null)
            return asmWithName;

        return Assembly.LoadFrom(filePath);
    }
}
