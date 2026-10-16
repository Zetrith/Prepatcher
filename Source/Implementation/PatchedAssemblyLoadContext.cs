using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Prepatcher;

// Mono makes the superseded assemblies reflection-only so references from the patched ones
// can't bind back to them. There's no such thing off Mono, where the originals stay loadable
// and win by name, which would leave a patched mod bound to the original Assembly-CSharp.
// Loading the patched set into its own context makes those references resolve to each other.
// Anything else returns null here and still comes from the default context.
internal class PatchedAssemblyLoadContext : AssemblyLoadContext
{
    internal static readonly PatchedAssemblyLoadContext Instance = new();

    private readonly Dictionary<string, Assembly> patched = new();

    private PatchedAssemblyLoadContext() : base("Prepatcher")
    {
    }

    // Bind a name to an assembly from the default context instead of a patched copy
    internal void Share(string name, Assembly asm)
    {
        patched[name] = asm;
    }

    internal Assembly LoadPatched(byte[] bytes)
    {
        // References resolve lazily, so registering afterwards is early enough
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
