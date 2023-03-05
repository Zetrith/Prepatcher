using System.Collections.Generic;

namespace TestAssemblyTarget;

public abstract class BaseComp
{
    public BaseWithComps parent;
}

public class OtherComp : BaseComp
{
}

public class InjectionBase
{
}

public class BaseWithComps : InjectionBase
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
