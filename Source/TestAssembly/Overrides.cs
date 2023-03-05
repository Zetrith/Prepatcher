using Prepatcher;
using TestAssemblyTarget;

namespace Tests;

public static class Overrides
{
    [PrepatcherOverride]
    public static int IntNonVirtual(OverrideMid obj) => obj.IntNonVirtual();

    [PrepatcherOverride]
    public static int IntNonVirtualArg(OverrideMid obj, int a) => obj.IntNonVirtualArg(a);

    [PrepatcherOverride]
    public static int IntVirtual(OverrideMid obj) => obj.IntVirtual();
}

public class OverrideSub : OverrideMid
{
    [PrepatcherOverride]
    public new int IntNonVirtualArg_Instance(int a)
    {
        return base.IntNonVirtualArg_Instance(a) + 1;
    }
}
