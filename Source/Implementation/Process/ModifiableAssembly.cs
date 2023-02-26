using System.IO;
using System.Reflection;
using Mono.Cecil;

namespace Prepatcher.Process;

public class ModifiableAssembly
{
    public string FriendlyName { get; }
    public Assembly? SourceAssembly { get; set; }
    public AssemblyDefinition AsmDefinition { get; }
    public ModuleDefinition ModuleDefinition => AsmDefinition.MainModule;
    public bool NeedsReload { get; set; }
    public bool Modified { get; set; } // Used to dump modified assemblies
    public bool Modifiable { get; set; } = true;
    public bool ProcessAttributes { get; set; }
    public byte[] Bytes { get; private set; }

    public ModifiableAssembly(string friendlyName, Assembly sourceAssembly, IAssemblyResolver resolver)
    {
        FriendlyName = friendlyName;
        SourceAssembly = sourceAssembly;
        AsmDefinition = AssemblyDefinition.ReadAssembly(
            new MemoryStream(UnsafeAssembly.GetRawData(sourceAssembly)),
            new ReaderParameters
            {
                AssemblyResolver = resolver
            });
    }

    public ModifiableAssembly(string friendlyName, string path, IAssemblyResolver resolver)
    {
        FriendlyName = friendlyName;
        AsmDefinition = AssemblyDefinition.ReadAssembly(
            path,
            new ReaderParameters { AssemblyResolver = resolver, InMemory = true}
        );
    }

    public void SerializeToByteArray()
    {
        Lg.Verbose($"Serializing: {FriendlyName}");
        var stream = new MemoryStream();
        AsmDefinition.Write(stream);
        Bytes = stream.ToArray();
    }

    public void SetSourceRefOnly()
    {
        Lg.Verbose($"Setting refonly: {FriendlyName}");
        UnsafeAssembly.SetReflectionOnly(SourceAssembly!, true);
    }

    public override string ToString()
    {
        return FriendlyName;
    }
}
