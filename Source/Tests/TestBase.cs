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
    private ModifiableAssembly targetAsm;

    private Assembly liveTestAsm;
    private Assembly liveTargetAsm;

    public virtual void Setup()
    {
        Lg._infoFunc = Console.WriteLine;
        Lg._errorFunc = msg =>
        {
            Console.WriteLine(msg);
            throw new LogErrorException($"{msg}");
        };

        LoadLiveAsms();

        set = new AssemblySet();
        fieldAdder = new FieldAdder(set);

        targetAsm = set.AddAssembly("Test","TestAssemblyTarget.dll", "TestAssemblyTarget.dll", null);
        targetAsm.ProcessAttributes = true;

        testAsm = set.AddAssembly("Test", "TestAssembly.dll", "TestAssembly.dll", null);
        testAsm.ProcessAttributes = true;
        testAsm.SourceAssembly = liveTestAsm;

        var typeThingWithComps =
            targetAsm.ModuleDefinition.GetType($"{nameof(TestAssemblyTarget)}.{nameof(BaseWithComps)}");
        var typeThingComp =
            targetAsm.ModuleDefinition.GetType($"{nameof(TestAssemblyTarget)}.{nameof(BaseComp)}");

        fieldAdder.RegisterInjection(
            typeThingWithComps,
            typeThingComp,
            nameof(BaseWithComps.InitComps),
            nameof(BaseWithComps.comps)
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
        File.WriteAllBytes(asm.AsmDefinition.ShortName() + ".dll", asm.Bytes!);
    }
}
