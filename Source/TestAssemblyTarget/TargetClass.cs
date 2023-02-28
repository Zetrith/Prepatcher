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

public class OtherComp : BaseComp
{
}

public class BaseClass
{
}

public class BaseWithComps : BaseClass
{
    public List<BaseComp> comps = new();
    public Type[] compTypes;

    public void InitComps()
    {
        comps.Clear();

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

public class CtorsClass
{
    public int counter;

    public CtorsClass()
    {
    }

    public CtorsClass(int a)
    {
    }
}

public class DerivedCtorsClass : CtorsClass
{
    public DerivedCtorsClass()
    {
    }

    public DerivedCtorsClass(int a)
    {
    }

    public DerivedCtorsClass(int a, int b) : base(1)
    {
        for (; b < 10; b++)
            if (a == 0 && b == 9)
                return;

        if (a == 1)
        {
            Console.WriteLine("1");
            return;
        }

        if (a == 2)
        {
            Console.WriteLine("2");
            return;
        }

        throw new Exception();
    }

    public DerivedCtorsClass(string c) : this(1)
    {
    }
}
