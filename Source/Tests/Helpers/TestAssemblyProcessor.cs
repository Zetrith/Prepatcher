using System.IO;
using Prepatcher;
using Prepatcher.Process;

namespace Tests;

public class TestAssemblyProcessor : AssemblyProcessor
{
    protected override void LoadAssembly(ModifiableAssembly asm)
    {
        // Replaces the actual assembly file that will get auto-loaded by the runtime

        File.WriteAllBytes(asm.AsmDefinition.ShortName() + ".dll", asm.Bytes);
    }
}
