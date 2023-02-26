using Prepatcher;
using TestAssemblyTarget;

namespace Tests;

public static class DefaultValues
{
    [PrepatcherField]
    [DefaultValue(int.MinValue)]
    public static extern ref int MyIntDefaultMin(this TargetClass target);

    [PrepatcherField]
    [DefaultValue(int.MaxValue)]
    public static extern ref int MyIntDefaultMax(this TargetClass target);

    [PrepatcherField]
    [DefaultValue(null)]
    public static extern ref int MyIntDefaultNull(this TargetClass target);

    [PrepatcherField]
    [DefaultValue(uint.MinValue)]
    public static extern ref uint MyUIntDefaultMin(this TargetClass target);

    [PrepatcherField]
    [DefaultValue(uint.MaxValue)]
    public static extern ref uint MyUIntDefaultMax(this TargetClass target);

    [PrepatcherField]
    [DefaultValue(null)]
    public static extern ref string MyStringDefaultNull(this TargetClass target);

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

    [PrepatcherField]
    [ValueInitializer(nameof(CounterInitializer))]
    public static extern ref int MyIntCounter(this DerivedCtorsClass target);

    public static int IntParameterlessInitializer() => 1;

    public static int IntThisInitializer(TargetClass? obj) => obj != null ? 1 : -1;

    public static SecondTargetClass ObjectThisInitializer(TargetClass obj) => new(obj);

    public static int CounterInitializer(DerivedCtorsClass ctors) => ++ctors.counter;
}
