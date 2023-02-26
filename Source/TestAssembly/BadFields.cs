using System.Collections.Generic;
using Prepatcher;
using TestAssemblyTarget;

namespace Tests;

public static class BadFields
{
    [PrepatcherField]
    private static extern ref T FailGenericMethod<T>(TargetClass target);

    [PrepatcherField]
    private static extern ref T FailNestedGeneric<T>(TargetGeneric<List<T>> target);

    [PrepatcherField]
    private static extern ref int FailConcreteGeneric(TargetGeneric<int> target);

    [PrepatcherField]
    private static extern ref int FailGenericCount<T, U>(TargetGeneric<T> target);

    [PrepatcherField]
    private static extern ref int FailGenericsDontMatch<T>(TargetGeneric3<T, T, T> target);

    [PrepatcherField]
    private static extern ref int FailGenericsDontMatch<T, U, W>(TargetGeneric3<T, W, U> target);

    // The parameter type is List because it isn't resolvable in the test environment
    // (System assembly isn't provided)
    [PrepatcherField]
    private static extern ref int FailUnresolvable<T>(List<T> target);

    [PrepatcherField]
    private static extern ref int FailInterface(ITarget target);

    [PrepatcherField]
    [InjectComponent]
    private static extern ref BaseComp FailInjectionByRef(BaseWithComps target);

    [PrepatcherField]
    [InjectComponent]
    private static extern BaseComp FailUnknownInjection(TargetClass target);
}
