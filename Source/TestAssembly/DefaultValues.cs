using Prepatcher;
using TestAssemblyTarget;

namespace Tests;

public static class DefaultValues
{
    // bool defaults
    [PrepatcherField]
    [DefaultValue(false)]
    public static extern ref bool MyBoolDefaultFalse(this TargetClass target);

    [PrepatcherField]
    [DefaultValue(true)]
    public static extern ref bool MyBoolDefaultTrue(this TargetClass target);

    // int defaults
    [PrepatcherField]
    [DefaultValue(int.MinValue)]
    public static extern ref int MyIntDefaultMin(this TargetClass target);

    [PrepatcherField]
    [DefaultValue(int.MaxValue)]
    public static extern ref int MyIntDefaultMax(this TargetClass target);

    [PrepatcherField]
    [DefaultValue(null)]
    public static extern ref int MyIntDefaultNull(this TargetClass target);

    // uint defaults
    [PrepatcherField]
    [DefaultValue(uint.MinValue)]
    public static extern ref uint MyUIntDefaultMin(this TargetClass target);

    [PrepatcherField]
    [DefaultValue(uint.MaxValue)]
    public static extern ref uint MyUIntDefaultMax(this TargetClass target);

    [PrepatcherField]
    [DefaultValue(null)]
    public static extern ref uint MyUIntDefaultNull(this TargetClass target);

    // long defaults
    [PrepatcherField]
    [DefaultValue(long.MinValue)]
    public static extern ref long MyLongDefaultMin(this TargetClass target);

    [PrepatcherField]
    [DefaultValue(long.MaxValue)]
    public static extern ref long MyLongDefaultMax(this TargetClass target);

    [PrepatcherField]
    [DefaultValue(null)]
    public static extern ref long MyLongDefaultNull(this TargetClass target);

    // ulong defaults
    [PrepatcherField]
    [DefaultValue(ulong.MinValue)]
    public static extern ref ulong MyULongDefaultMin(this TargetClass target);

    [PrepatcherField]
    [DefaultValue(ulong.MaxValue)]
    public static extern ref ulong MyULongDefaultMax(this TargetClass target);

    [PrepatcherField]
    [DefaultValue(null)]
    public static extern ref ulong MyULongDefaultNull(this TargetClass target);

    // float defaults
    [PrepatcherField]
    [DefaultValue(float.MinValue)]
    public static extern ref float MyFloatDefaultMin(this TargetClass target);

    [PrepatcherField]
    [DefaultValue(float.MaxValue)]
    public static extern ref float MyFloatDefaultMax(this TargetClass target);

    [PrepatcherField]
    [DefaultValue(null)]
    public static extern ref float MyFloatDefaultNull(this TargetClass target);

    // double defaults
    [PrepatcherField]
    [DefaultValue(double.MinValue)]
    public static extern ref double MyDoubleDefaultMin(this TargetClass target);

    [PrepatcherField]
    [DefaultValue(double.MaxValue)]
    public static extern ref double MyDoubleDefaultMax(this TargetClass target);

    [PrepatcherField]
    [DefaultValue(null)]
    public static extern ref double MyDoubleDefaultNull(this TargetClass target);

    // string defaults
    [PrepatcherField]
    [DefaultValue(null)]
    public static extern ref string MyStringDefaultNull(this TargetClass target);

    [PrepatcherField]
    [DefaultValue("a")]
    public static extern ref string MyStringDefault(this TargetClass target);

    // Value initializers
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
