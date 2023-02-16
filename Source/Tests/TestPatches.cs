using Prepatcher.Process;
using TestAssemblyTarget;

namespace Tests;

internal class TestPatches : TestBase
{
    [OneTimeSetUp]
    public override void Setup()
    {
        base.Setup();

        fieldAdder.ProcessTypes(new[]
        {
            testAsm.ModuleDefinition.GetType($"{nameof(Tests)}.{nameof(NewFields)}"),
            testAsm.ModuleDefinition.GetType($"{nameof(Tests)}.{nameof(DefaultValues)}"),
            testAsm.ModuleDefinition.GetType($"{nameof(Tests)}.{nameof(Injections)}")
        });

        FreePatcher.RunPatches(new []{ liveTestAsm }, targetAsm);
        Reloader.Reload(set, WriteAssembly);
    }

    [Test]
    public void TestNewFields()
    {
        Assert.That(NewFields.TestIntField(1), Is.EqualTo(1));
        Assert.That(NewFields.TestIntStructField(1), Is.EqualTo(1));
        Assert.That(NewFields.TestGenericField1("test1"), Is.EqualTo("test1"));
        Assert.That(NewFields.TestGenericField2("test2"), Is.EqualTo("test2"));
        Assert.That(NewFields.TestGenericField3("test3"), Is.EqualTo("test3"));
    }

    [Test]
    public void TestInjections()
    {
        Injections.TestSomeCompInjection().Do(c => Assert.That(c, Is.EqualTo(c.parent.comps[1])));
        Injections.TestCompInjection().Do(c => Assert.That(c, Is.EqualTo(c.parent.comps[0])));
        Injections.TestCompBaseInjection().Do(c => Assert.That(c, Is.EqualTo(c.parent.comps[0])));
        Injections.TestCompInjectionOnSubType().Do(c => Assert.That(c, Is.EqualTo(c.parent.comps[0])));
        Injections.TestCompBaseInjectionOnSubType().Do(c => Assert.That(c, Is.EqualTo(c.parent.comps[0])));
    }

    [Test]
    public void TestDefaultValues()
    {
        var targetObj = new TargetClass();
        Assert.That(targetObj.MyIntDefault(), Is.EqualTo(1));
        Assert.That(targetObj.MyStringDefault(), Is.EqualTo("a"));
    }

    [Test]
    public void TestDefaultValueInitializers()
    {
        var targetObj = new TargetClass();
        Assert.That(targetObj.MyIntParameterless(), Is.EqualTo(DefaultValues.IntParameterlessInitializer()));

        Assert.That(targetObj.MyIntFromThis(), Is.EqualTo(DefaultValues.IntThisInitializer(targetObj)));
        Assert.That(targetObj.MyObjectFromThis().inner, Is.EqualTo(targetObj));

#pragma warning disable NUnit2009
        Assert.That(targetObj.MyObjectFromThis(), Is.EqualTo(targetObj.MyObjectFromThis()));
#pragma warning restore NUnit2009
    }

    [Test]
    public void TestFreePatching()
    {
        Assert.That(new RewriteTarget().Method(), Is.EqualTo(1));
    }
}
