using System.Collections.Generic;

namespace TestAssemblyTarget;

public class TargetClass
{
}

public class TargetGeneric<T>
{
}

public class TargetGeneric3<T,U,W>
{
}

public interface TargetInterface
{
}

public abstract class ThingComp
{
}

public class ThingWithComps
{
    public List<ThingComp> comps = new();
    public Type compType;

    public void InitComps()
    {
        comps.Add((ThingComp)Activator.CreateInstance(compType));
    }
}

public class SubThingWithComps : ThingWithComps
{
}

public class RewriteTarget
{
    public int Method()
    {
        return 0;
    }
}
