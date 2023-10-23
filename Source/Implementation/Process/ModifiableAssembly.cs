using System.IO;
using System.Reflection;
using Mono.Cecil;

namespace Prepatcher.Process;

public class ModifiableAssembly
{
    public string OwnerName { get; }
    public string FriendlyName { get; }

    public Assembly? SourceAssembly { get; set; }
    public AssemblyDefinition AsmDefinition { get; }
    public ModuleDefinition ModuleDefinition => AsmDefinition.MainModule;

    public bool ProcessAttributes { get; set; }
    public bool NeedsReload => needsReload || Modified;
    public bool Modified { get; set; }
    public bool AllowPatches { get; set; } = true;

    public byte[]? Bytes { get; private set; }
    private byte[]? RawBytes { get; }

    private bool needsReload;

    public ModifiableAssembly(string ownerName, string friendlyName, Assembly sourceAssembly, IAssemblyResolver resolver)
    {
        OwnerName = ownerName;
        FriendlyName = friendlyName;
        SourceAssembly = sourceAssembly;

        RawBytes = UnsafeAssembly.GetRawData(sourceAssembly);
        AsmDefinition = AssemblyDefinition.ReadAssembly(
            new MemoryStream(RawBytes),
            new ReaderParameters
            {
                AssemblyResolver = resolver
            });
    }

    public ModifiableAssembly(string ownerName, string friendlyName, string path, IAssemblyResolver resolver)
    {
        OwnerName = ownerName;
        FriendlyName = friendlyName;
        AsmDefinition = AssemblyDefinition.ReadAssembly(
            path,
            new ReaderParameters { AssemblyResolver = resolver, InMemory = true }
        );
    }

    public void SerializeToByteArray()
    {
        if (RawBytes != null && !Modified)
        {
            Lg.Verbose($"Assembly not modified, skipping serialization: {FriendlyName}");
            Bytes = RawBytes;
            return;
        }

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

    public void SetNeedsReload()
    {
        needsReload = true;
    }

    public override string ToString()
    {
        return FriendlyName;
    }
}
