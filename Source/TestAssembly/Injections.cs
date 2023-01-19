using Prepatcher;
using TestAssemblyTarget;

namespace Tests;

public static class Injections
{
    [PrepatcherField]
    [InjectComponent]
    private static extern MyComponent MyComp(this ThingWithComps target);

    [PrepatcherField]
    [InjectComponent]
    private static extern ThingComp MyCompBase(this ThingWithComps target);

    [PrepatcherField]
    [InjectComponent]
    private static extern MyComponent MyCompOnSubType(this SubThingWithComps target);

    public static bool TestCompInjection()
    {
        var thing = new ThingWithComps { compType = typeof(MyComponent) };
        thing.InitComps();
        return thing.MyComp() == thing.comps[0];
    }

    public static bool TestCompBaseInjection()
    {
        var thing = new ThingWithComps { compType = typeof(MyComponent) };
        thing.InitComps();
        return thing.MyCompBase() == thing.comps[0];
    }

    public static bool TestCompInjectionOnSubType()
    {
        var thing = new SubThingWithComps { compType = typeof(MyComponent) };
        thing.InitComps();
        return thing.MyCompOnSubType() == thing.comps[0];
    }
}

public class MyComponent : ThingComp
{
}
