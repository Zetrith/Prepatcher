using System.IO;
using System.Reflection;
using Prepatcher;
using Prepatcher.Process;

namespace Tests;

public class TestAssemblyProcessor : AssemblyProcessor
{
    protected override Assembly LoadAssembly(ModifiableAssembly asm)
    {
        // The base LoadAssembly uses Assembly.Load but I couldn't get it to function in the test env
        // This just replaces the actual assembly file that will get auto-loaded by the runtime

        File.WriteAllBytes(asm.AsmDefinition.ShortName() + ".dll", asm.Bytes);
        return null;
    }
}
