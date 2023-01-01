using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using UnityEngine;
using Verse;
using FieldAttributes = Mono.Cecil.FieldAttributes;

namespace Prepatcher;

// Assumption: there only exists one assembly with a given name (just name, not f.e. name+version pair)
public class AssemblyProcessor
{
    private ModifiableAssembly asmCSharp;
    private List<ModifiableAssembly> gameAssemblies = new();
    private List<ModifiableAssembly> modAssemblies = new();

    private Dictionary<string, ModifiableAssembly> nameToAsm = new();

    public IAssemblyResolver Resolver { get; }

    internal const string PrepatcherMarkerField = "PrepatcherMarker";
    private const string AssemblyCSharp = "Assembly-CSharp.dll";
    private const string VerseGameType = "Verse.Game";

    public AssemblyProcessor()
    {
        Resolver = new AssemblyResolver(this);
    }

    public void ProcessAndReload()
    {
        Load();
        Process();
        Reload();
    }

    private void Load()
    {
        asmCSharp = new ModifiableAssembly(typeof(Game).Assembly, Resolver);
        nameToAsm[asmCSharp.AsmDefinition.ShortName()] = asmCSharp;

        foreach (var asmPath in Directory.GetFiles(Path.Combine(Application.dataPath, Util.ManagedFolderOS()), "*.dll"))
            if (Path.GetFileName(asmPath) != AssemblyCSharp)
            {
                var asm = new ModifiableAssembly(asmPath, Resolver) { Modifiable = false };
                gameAssemblies.Add(asm);
                nameToAsm[asm.AsmDefinition.ShortName()] = asm;
            }

        foreach (var modAssembly in GetUniqueModAssemblies())
        {
            if (nameToAsm.ContainsKey(modAssembly.GetName().Name)) continue;

            var asm = new ModifiableAssembly(modAssembly, Resolver);
            modAssemblies.Add(asm);
            nameToAsm[asm.AsmDefinition.ShortName()] = asm;
        }
    }

    private void Process()
    {
        // Mark as visited
        asmCSharp.ModuleDefinition.GetType(VerseGameType).Fields.Add(new FieldDefinition(
            PrepatcherMarkerField,
            FieldAttributes.Static,
            asmCSharp.ModuleDefinition.TypeSystem.Int32
        ));

        // Add requested fields
        foreach (var asm in modAssemblies)
            new FieldAdder(this).ProcessModAssembly(asm);
    }

    private void Reload()
    {
        MarkForReloading();

        Lg.Info("Writing new assemblies");

        asmCSharp.PrepareByteArray();
        foreach (var modAssembly in modAssemblies.Where(modAssembly => modAssembly.NeedsReload))
            modAssembly.PrepareByteArray();

        Lg.Info("Setting refonly");

        UnsafeAssembly.SetReflectionOnly(asmCSharp.SourceAssembly!, true);
        foreach (var modAssembly in modAssemblies.Where(modAssembly => modAssembly.NeedsReload))
            UnsafeAssembly.SetReflectionOnly(modAssembly.SourceAssembly!, true);

        Lg.Info("Loading new assemblies");

        Loader.newAsm = Assembly.Load(asmCSharp.Bytes);
        AppDomain.CurrentDomain.AssemblyResolve += (_, _) => Loader.newAsm;

        foreach (var modAssembly in modAssemblies.Where(modAssembly => modAssembly.NeedsReload))
            Assembly.Load(modAssembly.Bytes);
    }

    private void MarkForReloading()
    {
        var assembliesToReloadStart = modAssemblies
            .Where(m => m.NeedsReload)
            .Append(asmCSharp)
            .Append(FindModifiableAssembly("0Harmony")!);

        var assemblyToDependants = AssembliesToDependants(nameToAsm.Values);

        foreach (var asm in Util.BFS(assembliesToReloadStart, asm => assemblyToDependants.GetValueOrDefault(asm) ?? Enumerable.Empty<ModifiableAssembly>()))
            asm.NeedsReload = true;
    }

    private ModifiableAssembly? FindModifiableAssembly(string name)
    {
        return nameToAsm.GetValueOrDefault(name);
    }

    public ModifiableAssembly? FindModifiableAssembly(TypeDefinition typeDef)
    {
        return FindModifiableAssembly(typeDef.Module.Assembly.ShortName());
    }

    private static IEnumerable<Assembly> GetUniqueModAssemblies()
    {
        return LoadedModManager.RunningModsListForReading.SelectMany(m => m.assemblies.loadedAssemblies).Distinct();
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
