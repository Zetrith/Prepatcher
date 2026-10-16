using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Prepatcher;

// On Mono the superseded assemblies are marked reflection-only, so references from a freshly
// loaded patched assembly can't bind to them and resolve to the patched set instead. CoreCLR
// has no such concept: the originals stay loadable and, having loaded first, win by-name
// resolution -- so a patched mod assembly would bind straight back to the original
// Assembly-CSharp and its types would not match the ones the patched game is running.
//
// Loading the patched set into its own context makes their references resolve to each other.
// Anything not in the set (the BCL, UnityStubs, the engine, Prepatcher's own data assembly)
// returns null here and so still comes from the default context, shared by both worlds.
internal class PatchedAssemblyLoadContext : AssemblyLoadContext
{
    internal static readonly PatchedAssemblyLoadContext Instance = new();

    private readonly Dictionary<string, Assembly> patched = new();

    private PatchedAssemblyLoadContext() : base("Prepatcher")
    {
    }

    // Bind this name to an assembly from the default context rather than a patched copy
    internal void Share(string name, Assembly asm)
    {
        patched[name] = asm;
    }

    internal Assembly LoadPatched(byte[] bytes)
    {
        // References resolve lazily, so registering after the load is early enough
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
