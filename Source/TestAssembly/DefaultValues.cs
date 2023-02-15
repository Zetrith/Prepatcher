using Prepatcher;
using TestAssemblyTarget;

namespace Tests;

public static class DefaultValues
{
    [PrepatcherField]
    [DefaultValue(1)]
    public static extern ref int MyIntDefault(this TargetClass target);

    [PrepatcherField]
    [DefaultValue("a")]
    public static extern ref string MyStringDefault(this TargetClass target);

    [PrepatcherField]
    [ValueFactory(nameof(IntParameterlessFactory))]
    public static extern ref int MyIntParameterlessFactory(this TargetClass target);

    [PrepatcherField]
    [ValueFactory(nameof(IntThisFactory))]
    public static extern ref int MyIntThisFactory(this TargetClass target);

    [PrepatcherField]
    [ValueFactory(nameof(ObjectThisFactory))]
    public static extern ref SecondTargetClass MyObjectThisFactory(this TargetClass target);

    public static int IntParameterlessFactory() => 1;

    public static int IntThisFactory(TargetClass? obj) => obj != null ? 1 : -1;

    public static SecondTargetClass ObjectThisFactory(TargetClass obj) => new(obj);
}
