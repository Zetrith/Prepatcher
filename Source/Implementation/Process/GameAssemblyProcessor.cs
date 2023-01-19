using System.IO;
using System.Reflection;
using Mono.Cecil;
using Verse;
using FieldAttributes = Mono.Cecil.FieldAttributes;

namespace Prepatcher.Process;

internal class GameAssemblyProcessor : AssemblyProcessor
{
    internal ModifiableAssembly asmCSharp;

    private const string AssemblyCSharp = "Assembly-CSharp";
    private const string VerseGameType = "Verse.Game";
    internal const string PrepatcherMarkerField = "PrepatcherMarker";

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
        asmCSharp.Modified = true;

        FindModifiableAssembly("0Harmony")!.NeedsReload = true;

        base.Process();
    }

    protected override void LoadAssembly(ModifiableAssembly asm)
    {
        var loadedAssembly = Assembly.Load(asm.Bytes);
        if (loadedAssembly.GetName().Name == AssemblyCSharp)
        {
            Loader.newAsm = loadedAssembly;
            AppDomain.CurrentDomain.AssemblyResolve += (_, _) => loadedAssembly;
        }

        if (GenCommandLine.TryGetCommandLineArg("dumpasms", out var path))
        {
            Directory.CreateDirectory(path);
            if (asm.Modified)
                File.WriteAllBytes(Path.Combine(path, asm.AsmDefinition.Name.Name + ".dll"), asm.Bytes);
        }
    }
}
