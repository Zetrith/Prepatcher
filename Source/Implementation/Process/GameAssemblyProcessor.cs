using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using UnityEngine;
using Verse;
using FieldAttributes = Mono.Cecil.FieldAttributes;

namespace Prepatcher.Process;

internal class GameAssemblyProcessor : AssemblyProcessor
{
    private ModifiableAssembly asmCSharp;
    private List<ModifiableAssembly> modAssemblies = new();

    private const string AssemblyCSharpFile = "Assembly-CSharp.dll";
    private const string AssemblyCSharp = "Assembly-CSharp";
    private const string VerseGameType = "Verse.Game";

    internal void Init()
    {
        AddAssembly(typeof(Game).Assembly);

        foreach (var asmPath in Directory.GetFiles(Path.Combine(Application.dataPath, Util.ManagedFolderOS()), "*.dll"))
            if (Path.GetFileName(asmPath) != AssemblyCSharpFile)
                AddAssembly(asmPath).Modifiable = false;

        foreach (var modAssembly in GetUniqueModAssemblies())
        {
            if (FindModifiableAssembly(modAssembly.GetName().Name) != null) continue;
            var masm = AddAssembly(modAssembly);
            masm.Processable = true;
            modAssemblies.Add(masm);
        }
    }

    internal override void Process()
    {
        // Mark as visited
        asmCSharp.ModuleDefinition.GetType(VerseGameType).Fields.Add(
        new FieldDefinition(
            PrepatcherMarkerField,
            FieldAttributes.Static,
            asmCSharp.ModuleDefinition.TypeSystem.Int32
        ));

        asmCSharp.NeedsReload = true;
        FindModifiableAssembly("0Harmony")!.NeedsReload = true;

        base.Process();
    }

    protected override Assembly LoadAssembly(ModifiableAssembly asm)
    {
        var loadedAssembly = base.LoadAssembly(asm);
        if (loadedAssembly.GetName().Name == AssemblyCSharp)
        {
            Loader.newAsm = loadedAssembly;
            AppDomain.CurrentDomain.AssemblyResolve += (_, _) => loadedAssembly;
        }

        return loadedAssembly;
    }

    private static IEnumerable<Assembly> GetUniqueModAssemblies()
    {
        return LoadedModManager.RunningModsListForReading.SelectMany(m => m.assemblies.loadedAssemblies).Distinct();
    }
}
