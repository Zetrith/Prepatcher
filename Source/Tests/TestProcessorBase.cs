using Prepatcher;
using Prepatcher.Process;
using TestTargetAssembly;

namespace Tests;

public class TestProcessorBase
{
    protected TestAssemblyProcessor processor;
    protected ModifiableAssembly testAsm;
    protected ModifiableAssembly targetAsm;

    public virtual void Setup()
    {
        Lg.InfoFunc = Console.WriteLine;
        Lg.ErrorFunc = msg =>
        {
            Console.WriteLine(msg);
            throw new LogErrorException($"{msg}");
        };

        processor = new TestAssemblyProcessor();

        targetAsm = processor.AddAssembly("TestAssemblyTarget.dll");
        targetAsm.ProcessAttributes = true;

        testAsm = processor.AddAssembly("TestAssembly.dll");
        testAsm.ProcessAttributes = true;

        var typeThingWithComps =
            targetAsm.ModuleDefinition.GetType($"{nameof(TestTargetAssembly)}.{nameof(ThingWithComps)}");
        var typeThingComp =
            targetAsm.ModuleDefinition.GetType($"{nameof(TestTargetAssembly)}.{nameof(ThingComp)}");

        processor.FieldAdder.AddComponentInjection(
            typeThingWithComps,
            typeThingComp,
            nameof(ThingWithComps.InitComps),
            nameof(ThingWithComps.comps)
        );
    }
}
