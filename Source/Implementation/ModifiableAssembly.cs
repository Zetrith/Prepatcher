using System.IO;
using System.Reflection;
using Mono.Cecil;

namespace Prepatcher;

public class ModifiableAssembly
{
    public Assembly? SourceAssembly { get; }
    public AssemblyDefinition AsmDefinition { get; }
    public ModuleDefinition ModuleDefinition => AsmDefinition.MainModule;
    public bool NeedsReload { get; set; }
    public bool Modifiable { get; set; } = true;
    public byte[] Bytes { get; private set; }

    public ModifiableAssembly(Assembly sourceAssembly, IAssemblyResolver resolver)
    {
        this.SourceAssembly = sourceAssembly;
        AsmDefinition = AssemblyDefinition.ReadAssembly(new MemoryStream(UnsafeAssembly.GetRawData(sourceAssembly)),
            new ReaderParameters
            {
                AssemblyResolver = resolver
            });
    }

    public ModifiableAssembly(string path, IAssemblyResolver resolver)
    {
        AsmDefinition = AssemblyDefinition.ReadAssembly(
            path,
            new ReaderParameters { AssemblyResolver = resolver, InMemory = true}
        );
    }

    // Writing and loading is split to do as little as possible after refonlys are set
    public void PrepareByteArray()
    {
        var stream = new MemoryStream();
        AsmDefinition.Write(stream);
        Bytes = stream.ToArray();
    }
}
