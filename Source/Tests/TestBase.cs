using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Prepatcher;
using Prepatcher.Process;
using TestAssemblyTarget;

namespace Tests;

public class TestBase
{
    protected TestAssemblyProcessor processor;

    protected ModifiableAssembly testAsm;
    protected ModifiableAssembly targetAsm;

    protected Assembly liveTestAsm;
    protected Assembly liveTargetAsm;

    public virtual void Setup()
    {
        Lg.InfoFunc = Console.WriteLine;
        Lg.ErrorFunc = msg =>
        {
            Console.WriteLine(msg);
            throw new LogErrorException($"{msg}");
        };

        LoadLiveAsms();

        processor = new TestAssemblyProcessor();

        targetAsm = processor.AddAssembly("TestAssemblyTarget.dll");
        targetAsm.ProcessAttributes = true;

        testAsm = processor.AddAssembly("TestAssembly.dll");
        testAsm.ProcessAttributes = true;

        var typeThingWithComps =
            targetAsm.ModuleDefinition.GetType($"{nameof(TestAssemblyTarget)}.{nameof(ThingWithComps)}");
        var typeThingComp =
            targetAsm.ModuleDefinition.GetType($"{nameof(TestAssemblyTarget)}.{nameof(ThingComp)}");

        processor.FieldAdder.AddComponentInjection(
            typeThingWithComps,
            typeThingComp,
            nameof(ThingWithComps.InitComps),
            nameof(ThingWithComps.comps)
        );
    }

    // Load actual callable instances of the test assemblies
    // They are used to test free patching
    private void LoadLiveAsms()
    {
        const string testAssemblyTargetNewName = "TestAssemblyTarget1";

        using var testAsmToBeLive = ModuleDefinition.ReadModule("TestAssembly.dll");
        using var testTargetAsmToBeLive = ModuleDefinition.ReadModule("TestAssemblyTarget.dll");

        // Rename the referenced assembly so it isn't loaded from disk and passes through AssemblyResolve
        {
            testAsmToBeLive.AssemblyReferences.First(a => a.Name == "TestAssemblyTarget").Name =
                testAssemblyTargetNewName;

            // The bodies have to be initialized because the test runtime doesn't seem to like non pinvoke extern methods
            // Mono is fine with them
            foreach (var m in FieldAdder.GetAllPrepatcherFieldAccessors(testAsmToBeLive.Types))
                Util.SetEmptyBody(m);

            var stream = new MemoryStream();
            testAsmToBeLive.Write(stream);
            liveTestAsm = Assembly.Load(stream.ToArray());
        }

        {
            testTargetAsmToBeLive.Name = testAssemblyTargetNewName;
            testTargetAsmToBeLive.Assembly.Name.Name = testAssemblyTargetNewName;

            foreach (var m in FieldAdder.GetAllPrepatcherFieldAccessors(testTargetAsmToBeLive.Types))
                Util.SetEmptyBody(m);

            var stream = new MemoryStream();
            testTargetAsmToBeLive.Write(stream);
            liveTargetAsm = Assembly.Load(stream.ToArray());
        }

        AppDomain.CurrentDomain.AssemblyResolve +=
            (_, args) => args.Name.StartsWith(testAssemblyTargetNewName) ? liveTargetAsm : null;
    }
}
