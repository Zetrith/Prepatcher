using Mono.Cecil;
using Prepatcher;
using Prepatcher.Process;

namespace Tests;

public class TestsNegative
{
    private TestAssemblyProcessor processor;
    private TypeDefinition typeFail;

    [OneTimeSetUp]
    public void SetUp()
    {
        Lg.InfoFunc = Console.WriteLine;
        Lg.ErrorFunc = msg => throw new LogErrorException($"{msg}");

        processor = new TestAssemblyProcessor();

        var targetAsm = processor.AddAssembly("TestAssemblyTarget.dll");
        targetAsm.ProcessAttributes = true;

        var testAsm = processor.AddAssembly("TestAssembly.dll");
        testAsm.ProcessAttributes = true;

        typeFail = testAsm.ModuleDefinition.GetType($"{nameof(Tests)}.{nameof(TestFail)}");
    }

    [Test]
    public void TestBadFieldAccessors()
    {
        foreach (var accessor in FieldAdder.GetAllPrepatcherFieldAccessors(TestExts.EnumerableOf(typeFail)))
            Assert.Throws<LogErrorException>(() =>
            {
                new FieldAdder(processor).ProcessAccessor(accessor);
            }, accessor.Name);
    }
}
