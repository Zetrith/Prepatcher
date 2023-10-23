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

        FreePatcher.RunPatches(set, "TestAssemblyTarget");
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
        Injections.TestOtherCompInjection().Do(c => Assert.That(c, Is.EqualTo(c.parent.comps[1])));
        Injections.TestCompInjection().Do(c => Assert.That(c, Is.EqualTo(c.parent.comps[0])));
        Injections.TestCompInjection_DoubleInit().Do(c => Assert.That(c, Is.EqualTo(c.parent.comps[0])));
        Injections.TestCompBaseInjection().Do(c => Assert.That(c, Is.EqualTo(c.parent.comps[0])));
        Injections.TestCompInjectionOnSubType().Do(c => Assert.That(c, Is.EqualTo(c.parent.comps[0])));
        Injections.TestCompBaseInjectionOnSubType().Do(c => Assert.That(c, Is.EqualTo(c.parent.comps[0])));
        Injections.TestCompInjectionOnSuperType().Do(c => Assert.That(c, Is.EqualTo(c.parent.comps[0])));
    }

    [Test]
    public void TestDefaultValues()
    {
        var targetObj = new TargetClass();

        Assert.That(targetObj.MyBoolDefaultFalse(), Is.EqualTo(false));
        Assert.That(targetObj.MyBoolDefaultTrue(), Is.EqualTo(true));

        Assert.That(targetObj.MyIntDefaultMin(), Is.EqualTo(int.MinValue));
        Assert.That(targetObj.MyIntDefaultMax(), Is.EqualTo(int.MaxValue));
        Assert.That(targetObj.MyIntDefaultNull(), Is.EqualTo(0));

        Assert.That(targetObj.MyUIntDefaultMin(), Is.EqualTo(uint.MinValue));
        Assert.That(targetObj.MyUIntDefaultMax(), Is.EqualTo(uint.MaxValue));
        Assert.That(targetObj.MyUIntDefaultNull(), Is.EqualTo(0u));

        Assert.That(targetObj.MyLongDefaultMin(), Is.EqualTo(long.MinValue));
        Assert.That(targetObj.MyLongDefaultMax(), Is.EqualTo(long.MaxValue));
        Assert.That(targetObj.MyLongDefaultNull(), Is.EqualTo(0L));

        Assert.That(targetObj.MyULongDefaultMin(), Is.EqualTo(ulong.MinValue));
        Assert.That(targetObj.MyULongDefaultMax(), Is.EqualTo(ulong.MaxValue));
        Assert.That(targetObj.MyULongDefaultNull(), Is.EqualTo(0UL));

        Assert.That(targetObj.MyFloatDefaultMin(), Is.EqualTo(float.MinValue));
        Assert.That(targetObj.MyFloatDefaultMax(), Is.EqualTo(float.MaxValue));
        Assert.That(targetObj.MyFloatDefaultNull(), Is.EqualTo(0f));

        Assert.That(targetObj.MyDoubleDefaultMin(), Is.EqualTo(double.MinValue));
        Assert.That(targetObj.MyDoubleDefaultMax(), Is.EqualTo(double.MaxValue));
        Assert.That(targetObj.MyDoubleDefaultNull(), Is.EqualTo(0d));

        Assert.That(targetObj.MyStringDefault(), Is.EqualTo("a"));
        Assert.That(targetObj.MyStringDefaultNull(), Is.EqualTo(null));
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

        Assert.That(new DerivedCtorsClass().MyIntCounter(), Is.EqualTo(1));
        Assert.That(new DerivedCtorsClass(1).MyIntCounter(), Is.EqualTo(1));
        Assert.That(new DerivedCtorsClass(0, 0).MyIntCounter(), Is.EqualTo(1));
        Assert.That(new DerivedCtorsClass(1, 0).MyIntCounter(), Is.EqualTo(1));
        Assert.That(new DerivedCtorsClass(2, 0).MyIntCounter(), Is.EqualTo(1));
        Assert.That(new DerivedCtorsClass("a").MyIntCounter(), Is.EqualTo(1));
    }

    [Test]
    public void TestFreePatching()
    {
        Assert.That(new RewriteTarget().Method(), Is.EqualTo(1));
        Assert.That(new RewriteTarget().Method2(), Is.EqualTo("b"));
    }
}
