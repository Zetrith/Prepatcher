using System.IO;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;
using Verse;

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

    public byte[]? SymbolBytes { get; private set; }

    public bool SymbolsLoaded { get; private set; }

    private bool needsReload;

    public ModifiableAssembly(string ownerName, string friendlyName, Assembly sourceAssembly, IAssemblyResolver resolver)
    {
        OwnerName = ownerName;
        FriendlyName = friendlyName;
        SourceAssembly = sourceAssembly;
        RawBytes = UnsafeAssembly.GetRawData(sourceAssembly);
        if (sourceAssembly.Location != "")
        {
            try
            {
                AsmDefinition = AssemblyDefinition.ReadAssembly(sourceAssembly.Location, new ReaderParameters
                {
                    AssemblyResolver = resolver,
                    ReadSymbols = true,
                    InMemory = true
                });
                SymbolsLoaded = true;
                CheckSymbols();


            }
            catch (Exception e)
            {
                AsmDefinition = AssemblyDefinition.ReadAssembly(sourceAssembly.Location, new ReaderParameters
                {
                    AssemblyResolver = resolver,
                    ReadSymbols = false,
                    InMemory = true
                });
                SymbolsLoaded = false;
            }
        }
        else
        {
            try
            {
                AsmDefinition = AssemblyDefinition.ReadAssembly(
                    new MemoryStream(RawBytes),
                    new ReaderParameters
                    {
                        AssemblyResolver = resolver,
                        ReadSymbols = true,
                    });
                SymbolsLoaded = true;
                CheckSymbols();
            }
            catch (Exception e)
            {
                AsmDefinition = AssemblyDefinition.ReadAssembly(
                    new MemoryStream(RawBytes),
                    new ReaderParameters
                    {
                        AssemblyResolver = resolver,
                    });
                SymbolsLoaded = false;
            }

        }
    }

    public ModifiableAssembly(string ownerName, string friendlyName, string path, IAssemblyResolver resolver)
    {
        OwnerName = ownerName;
        FriendlyName = friendlyName;
        try
        {
            AsmDefinition = AssemblyDefinition.ReadAssembly(path, new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadSymbols = true,
                InMemory = true
            });
            SymbolsLoaded = true;
            CheckSymbols();
        }
        catch (Exception e)
        {
            AsmDefinition = AssemblyDefinition.ReadAssembly(path, new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadSymbols = false,
                InMemory = true
            });
            SymbolsLoaded = false;
        }
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
        if (SymbolsLoaded)
        {
            Lg.Verbose($"Serializing assembly with symbols loaded: {FriendlyName} {AsmDefinition.MainModule} {AsmDefinition.MainModule.symbol_reader.GetType()}");
            var symbolsStream = new MemoryStream();
            AsmDefinition.Write(stream,
                new WriterParameters
                {
                    SymbolStream = symbolsStream,
                    SymbolWriterProvider = new PdbWriterProvider()
                });
            SymbolBytes = symbolsStream.ToArray();

        }
        else
        {
            AsmDefinition.Write(stream);
        }
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

    private void CheckSymbols()
    {
        // embedded symbols are portable symbols compressed and inserted directly into assembly
        // Mono.Cecil doesnt support writing embedded symbols into memory so we extract the portable provider from them
        if (AsmDefinition.MainModule.symbol_reader is EmbeddedPortablePdbReader mainEmbedded)
        {
            AsmDefinition.MainModule.symbol_reader = mainEmbedded.reader;

        }

        if (!AsmDefinition.Modules.NullOrEmpty())
        {
            foreach (var module in AsmDefinition.Modules)
            {
                if (module.symbol_reader is EmbeddedPortablePdbReader embedded)
                {
                    module.symbol_reader = embedded.reader;
                }
            }
        }

        // Mono has no support loading native pdb, we ignore them
        if (AsmDefinition.MainModule.symbol_reader is NativePdbReader)
        {
            SymbolsLoaded = false;
        }
    }
}
