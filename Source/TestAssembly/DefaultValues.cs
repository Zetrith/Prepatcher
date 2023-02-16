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
    [ValueInitializer(nameof(IntParameterlessInitializer))]
    public static extern ref int MyIntParameterless(this TargetClass target);

    [PrepatcherField]
    [ValueInitializer(nameof(IntThisInitializer))]
    public static extern ref int MyIntFromThis(this TargetClass target);

    [PrepatcherField]
    [ValueInitializer(nameof(ObjectThisInitializer))]
    public static extern ref SecondTargetClass MyObjectFromThis(this TargetClass target);

    public static int IntParameterlessInitializer() => 1;

    public static int IntThisInitializer(TargetClass? obj) => obj != null ? 1 : -1;

    public static SecondTargetClass ObjectThisInitializer(TargetClass obj) => new(obj);
}
