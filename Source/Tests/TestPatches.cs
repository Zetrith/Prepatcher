using System.Collections.Generic;
using System.IO;
using Prepatcher;
using Prepatcher.Process;
using TestAssemblyTarget;
using Verse;

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
            testAsm.ModuleDefinition.GetType($"{nameof(Tests)}.{nameof(Injections)}")
        });

        FreePatcher.RunPatches(new []{ liveTestAsm }, targetAsm);
        Reloader.Reload(set, WriteAssembly);
    }

    [Test]
    public void TestNewFields()
    {
        Assert.AreEqual(NewFields.TestIntField(1), 1);
        Assert.AreEqual(NewFields.TestIntStructField(1), 1);
        Assert.AreEqual(NewFields.TestGenericField1("test1"), "test1");
        Assert.AreEqual(NewFields.TestGenericField2("test2"), "test2");
        Assert.AreEqual(NewFields.TestGenericField3("test3"), "test3");
    }

    [Test]
    public void TestInjections()
    {
        Assert.True(Injections.TestCompInjection());
        Assert.True(Injections.TestCompBaseInjection());
        Assert.True(Injections.TestCompInjectionOnSubType());
    }

    [Test]
    public void TestDefaultValues()
    {
        var targetObj = new TargetClass();

        Assert.AreEqual(targetObj.MyIntDefault(), 1);
        Assert.AreEqual(targetObj.MyStringDefault(), "a");

        Assert.AreEqual(targetObj.MyIntParameterlessFactory(), DefaultValues.IntParameterlessFactory());

        Assert.AreEqual(targetObj.MyIntThisFactory(), DefaultValues.IntThisFactory(targetObj));
        Assert.AreEqual(targetObj.MyObjectThisFactory(), targetObj.MyObjectThisFactory());
        Assert.AreEqual(targetObj.MyObjectThisFactory().inner, targetObj);
    }

    [Test]
    public void TestFreePatching()
    {
        Assert.AreEqual(new RewriteTarget().Method(), 1);
    }
}
