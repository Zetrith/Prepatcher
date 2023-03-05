using Prepatcher;
using TestAssemblyTarget;

namespace Tests;

public static class Injections
{
    [PrepatcherField]
    [InjectComponent]
    private static extern OtherComp SomeComp(this BaseWithComps target);

    [PrepatcherField]
    [InjectComponent]
    private static extern DerivedMyComponent MyComp(this BaseWithComps target);

    [PrepatcherField]
    [InjectComponent]
    private static extern MyComponent MyCompBase(this BaseWithComps target);

    [PrepatcherField]
    [InjectComponent]
    private static extern DerivedMyComponent MyCompOnSubType(this DerivedWithComps target);

    [PrepatcherField]
    [InjectComponent]
    private static extern MyComponent MyCompBaseOnSubType(this DerivedWithComps target);

    [PrepatcherField]
    [InjectComponent]
    private static extern MyComponent MyCompBaseOnSuperType(this InjectionBase target);

    // Exact comp type, initializer type == target type
    public static BaseComp TestOtherCompInjection()
    {
        var thing = new BaseWithComps { compTypes = new[] { typeof(DerivedMyComponent), typeof(OtherComp) } };
        thing.InitComps();
        return thing.SomeComp();
    }

    // Exact comp type, initializer type == target type
    public static BaseComp TestCompInjection()
    {
        var thing = new BaseWithComps { compTypes = new[] { typeof(DerivedMyComponent), typeof(OtherComp) } };
        thing.InitComps();
        return thing.MyComp();
    }

    // Exact comp type, initializer type == target type
    public static BaseComp TestCompInjection_DoubleInit()
    {
        var thing = new BaseWithComps { compTypes = new[] { typeof(DerivedMyComponent), typeof(OtherComp) } };
        thing.InitComps();
        thing.InitComps();
        return thing.MyComp();
    }

    // Sub comp type, initializer type == target type
    public static BaseComp TestCompBaseInjection()
    {
        var thing = new BaseWithComps { compTypes = new[] { typeof(DerivedMyComponent), typeof(OtherComp) } };
        thing.InitComps();
        return thing.MyCompBase();
    }

    // Exact comp type, initializer type == super of target type
    public static BaseComp TestCompInjectionOnSubType()
    {
        var thing = new DerivedWithComps { compTypes = new[] { typeof(DerivedMyComponent), typeof(OtherComp) } };
        thing.InitComps();
        return thing.MyCompOnSubType();
    }

    // Sub comp type, initializer type == super of target type
    public static BaseComp TestCompBaseInjectionOnSubType()
    {
        var thing = new DerivedWithComps { compTypes = new[] { typeof(DerivedMyComponent), typeof(OtherComp) } };
        thing.InitComps();
        return thing.MyCompBaseOnSubType();
    }

    // Exact comp type, initializer type == sub of target type
    public static BaseComp TestCompInjectionOnSuperType()
    {
        var thing = new DerivedWithComps { compTypes = new[] { typeof(DerivedMyComponent), typeof(OtherComp) } };
        thing.InitComps();
        return thing.MyCompBaseOnSuperType();
    }
}

public class MyComponent : BaseComp
{
}

public class DerivedMyComponent : MyComponent
{
}
