using System.Collections.Generic;

namespace TestAssemblyTarget;

public class TargetClass
{
}

public class SecondTargetClass
{
    public readonly TargetClass inner;

    public SecondTargetClass(TargetClass inner)
    {
        this.inner = inner;
    }
}

public struct TargetStruct
{
}

public class TargetGeneric<T>
{
}

public class TargetGeneric3<T,U,W>
{
}

public interface ITarget
{
}

public abstract class BaseComp
{
    public BaseWithComps parent;
}

public class SomeComp : BaseComp
{
}

public class BaseWithComps
{
    public List<BaseComp> comps = new();
    public Type[] compTypes;

    public void InitComps()
    {
        foreach (var type in compTypes)
        {
            var comp = (BaseComp)Activator.CreateInstance(type);
            comp.parent = this;
            comps.Add(comp);
        }
    }
}

public class DerivedWithComps : BaseWithComps
{
}

public class RewriteTarget
{
    public int Method()
    {
        return 0;
    }
}
