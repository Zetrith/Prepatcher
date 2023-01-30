using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mono.Cecil;

namespace Prepatcher.Process;

// Assumption: there only exists one assembly with a given name (just name, not f.e. name+version pair)
public class AssemblySet
{
    internal List<ModifiableAssembly> AllAssemblies { get; } = new();
    private Dictionary<string, ModifiableAssembly> nameToAsm = new();
    private IAssemblyResolver Resolver { get; }

    public AssemblySet()
    {
        Resolver = new AssemblyResolver(this);
    }

    internal ModifiableAssembly AddAssembly(Assembly asm)
    {
        var masm = new ModifiableAssembly(asm, Resolver);
        nameToAsm[masm.AsmDefinition.ShortName()] = masm;
        AllAssemblies.Add(masm);
        return masm;
    }

    internal ModifiableAssembly AddAssembly(string asmFilePath)
    {
        var masm = new ModifiableAssembly(asmFilePath, Resolver);
        nameToAsm[masm.AsmDefinition.ShortName()] = masm;
        AllAssemblies.Add(masm);
        return masm;
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
        return AllAssemblies[0].ModuleDefinition.ImportReference(type).Resolve();
    }

    internal Dictionary<ModifiableAssembly, HashSet<ModifiableAssembly>> AllAssembliesToDependants()
    {
        var dependants = new Dictionary<ModifiableAssembly, HashSet<ModifiableAssembly>>();

        foreach (var asm in nameToAsm.Values)
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
        private AssemblySet processor;

        public AssemblyResolver(AssemblySet processor)
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
