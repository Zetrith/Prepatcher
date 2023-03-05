namespace TestAssemblyTarget;

public class OverrideBase
{
    public int IntNonVirtual()
    {
        return 1;
    }

    public virtual int IntVirtual()
    {
        return 1;
    }

    public int IntNonVirtualArg(int a)
    {
        return a;
    }

    public int IntNonVirtual_Instance()
    {
        return 1;
    }

    public int IntNonVirtualArg_Instance(int a)
    {
        return a;
    }
}

public class OverrideMid : OverrideBase
{
}
