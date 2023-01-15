using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mono.Cecil;

namespace Prepatcher.Process;

// Assumption: there only exists one assembly with a given name (just name, not f.e. name+version pair)
public abstract class AssemblyProcessor
{
    private List<ModifiableAssembly> allAssemblies = new();
    private Dictionary<string, ModifiableAssembly> nameToAsm = new();

    private IAssemblyResolver Resolver { get; }
    internal FieldAdder FieldAdder { get; }

    public AssemblyProcessor()
    {
        Resolver = new AssemblyResolver(this);
        FieldAdder = new FieldAdder(this);
    }

    internal ModifiableAssembly AddAssembly(Assembly asm)
    {
        var masm = new ModifiableAssembly(asm, Resolver);
        nameToAsm[masm.AsmDefinition.ShortName()] = masm;
        allAssemblies.Add(masm);
        return masm;
    }

    internal ModifiableAssembly AddAssembly(string asmFilePath)
    {
        var masm = new ModifiableAssembly(asmFilePath, Resolver);
        nameToAsm[masm.AsmDefinition.ShortName()] = masm;
        allAssemblies.Add(masm);
        return masm;
    }

    internal virtual void Process()
    {
        foreach (var asm in allAssemblies.Where(a => a.ProcessAttributes))
            FieldAdder.ProcessTypes(asm.ModuleDefinition.Types);
    }

    internal void Reload()
    {
        MarkForReloading();

        // Writing and loading is split to do as little as possible after refonlys are set
        Lg.Info("Writing new assemblies");
        foreach (var toReload in allAssemblies.Where(modAssembly => modAssembly.NeedsReload))
            toReload.PrepareByteArray();

        Lg.Info("Setting refonly");
        foreach (var toReload in allAssemblies.Where(modAssembly => modAssembly.NeedsReload))
            toReload.SetSourceRefOnly();

        Lg.Info("Loading new assemblies");
        foreach (var toReload in allAssemblies.Where(modAssembly => modAssembly.NeedsReload))
            LoadAssembly(toReload);
    }

    protected abstract void LoadAssembly(ModifiableAssembly asm);

    private void MarkForReloading()
    {
        var assembliesToReloadStart = allAssemblies
            .Where(m => m.NeedsReload);

        var assemblyToDependants = AssembliesToDependants(nameToAsm.Values);

        foreach (var asm in Util.BFS(assembliesToReloadStart, asm => assemblyToDependants.GetValueSafe(asm) ?? Enumerable.Empty<ModifiableAssembly>()))
            asm.NeedsReload = true;
    }

    internal ModifiableAssembly? FindModifiableAssembly(string name)
    {
        return nameToAsm.GetValueSafe(name);
    }

    internal ModifiableAssembly? FindModifiableAssembly(TypeDefinition typeDef)
    {
        return FindModifiableAssembly(typeDef.Module.Assembly.ShortName());
    }

    internal TypeDefinition ReflectionToCecil(Type type)
    {
        // Use any assembly as they all go through the same assembly resolver
        return allAssemblies[0].ModuleDefinition.ImportReference(type).Resolve();
    }

    private Dictionary<ModifiableAssembly, HashSet<ModifiableAssembly>> AssembliesToDependants(IEnumerable<ModifiableAssembly> asms)
    {
        var dependants = new Dictionary<ModifiableAssembly, HashSet<ModifiableAssembly>>();

        foreach (var asm in asms)
        foreach (var reference in asm.ModuleDefinition.AssemblyReferences)
        {
            var refAsm = FindModifiableAssembly(reference.Name);
            if (refAsm == null) continue;

            if (!dependants.TryGetValue(refAsm, out var set))
                dependants[refAsm] = set = new HashSet<ModifiableAssembly>();

            set.Add(asm);
        }

        return dependants;
    }

    private class AssemblyResolver : IAssemblyResolver
    {
        private AssemblyProcessor processor;

        public AssemblyResolver(AssemblyProcessor processor)
        {
            this.processor = processor;
        }

        public AssemblyDefinition? Resolve(AssemblyNameReference name)
        {
            return processor.FindModifiableAssembly(name.Name)?.AsmDefinition;
        }

        public AssemblyDefinition? Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            return Resolve(name);
        }

        public void Dispose()
        {
        }
    }
}
