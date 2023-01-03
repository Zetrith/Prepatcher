using System.IO;
using System.Reflection;
using Prepatcher;
using Prepatcher.Process;
using TestAssembly;

namespace Tests;

public class Tests
{
    [OneTimeSetUp]
    public void Setup()
    {
        Lg.InfoFunc = Console.WriteLine;
        Lg.ErrorFunc = msg => Console.Error.WriteLine($"Error {msg}");

        var processor = new TestAssemblyProcessor();
        processor.AddAssembly("TestTargetAssembly.dll").Processable = true;
        processor.AddAssembly("TestAssembly.dll").Processable = true;
        processor.Process();
        processor.Reload();
    }

    [Test]
    public void TestFields()
    {
        Assert.AreEqual(Test.TestIntField(1), 1);
        Assert.AreEqual(Test.TestGenericField("test"), "test");
    }
}

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
