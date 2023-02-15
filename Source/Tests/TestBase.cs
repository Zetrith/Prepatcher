using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Prepatcher;
using Prepatcher.Process;
using TestAssemblyTarget;

namespace Tests;

internal class TestBase
{
    protected AssemblySet set;
    protected FieldAdder fieldAdder;

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

        set = new AssemblySet();
        fieldAdder = new FieldAdder(set);

        targetAsm = set.AddAssembly("TestAssemblyTarget.dll");
        targetAsm.ProcessAttributes = true;

        testAsm = set.AddAssembly("TestAssembly.dll");
        testAsm.ProcessAttributes = true;

        var typeThingWithComps =
            targetAsm.ModuleDefinition.GetType($"{nameof(TestAssemblyTarget)}.{nameof(ThingWithComps)}");
        var typeThingComp =
            targetAsm.ModuleDefinition.GetType($"{nameof(TestAssemblyTarget)}.{nameof(ThingComp)}");

        fieldAdder.RegisterInjection(
            typeThingWithComps,
            typeThingComp,
            nameof(ThingWithComps.InitComps),
            nameof(ThingWithComps.comps)
        );
    }

    // Load the test assemblies and make them resolvable for freepatch testing
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

    protected static void WriteAssembly(ModifiableAssembly asm)
    {
        // Replaces the actual assembly file that will get auto-loaded by the runtime
        File.WriteAllBytes(asm.AsmDefinition.ShortName() + ".dll", asm.Bytes);
    }
}
