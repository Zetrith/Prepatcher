using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Prepatcher;

internal class PatchedAssemblyLoadContext : AssemblyLoadContext
{
    internal static readonly PatchedAssemblyLoadContext Instance = new();

    private readonly Dictionary<string, Assembly> patched = new();

    private PatchedAssemblyLoadContext() : base("Prepatcher")
    {
    }

    internal void Share(string name, Assembly asm)
    {
        patched[name] = asm;
    }

    internal Assembly LoadPatched(byte[] bytes)
    {
        var asm = LoadFromStream(new MemoryStream(bytes));
        patched[asm.GetName().Name!] = asm;
        return asm;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        return assemblyName.Name != null && patched.TryGetValue(assemblyName.Name, out var asm)
            ? asm
            : null;
    }
}
