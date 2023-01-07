using Prepatcher;
using Prepatcher.Process;

namespace Tests;

public class TestsPositive
{
    [OneTimeSetUp]
    public void Setup()
    {
        Lg.InfoFunc = Console.WriteLine;
        Lg.ErrorFunc = msg => throw new LogErrorException($"{msg}");

        var processor = new TestAssemblyProcessor();

        var targetAsm = processor.AddAssembly("TestAssemblyTarget.dll");
        targetAsm.ProcessAttributes = true;

        var testAsm = processor.AddAssembly("TestAssembly.dll");
        testAsm.ProcessAttributes = true;

        var typeSuccess = testAsm.ModuleDefinition.GetType($"{nameof(Tests)}.{nameof(TestSuccess)}");
        new FieldAdder(processor).ProcessTypes(TestExts.EnumerableOf(typeSuccess));
        processor.Reload();
    }

    [Test]
    public void TestNewFields()
    {
        Assert.AreEqual(TestSuccess.TestIntField(1), 1);
        Assert.AreEqual(TestSuccess.TestGenericField1("test1"), "test1");
        Assert.AreEqual(TestSuccess.TestGenericField2("test2"), "test2");
        Assert.AreEqual(TestSuccess.TestGenericField3("test3"), "test3");
    }
}
