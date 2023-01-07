using System.Collections.Generic;
using Prepatcher;
using TestTargetAssembly;

namespace Tests;

public static class TestFail
{
    [PrepatcherField]
    private static ref int FailExtern(TargetClass target) => throw new Exception();

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

    // The System assemblies aren't loaded for this test
    [PrepatcherField]
    private static extern ref int FailUnresolvable<T>(List<T> target);

    [PrepatcherField]
    private static extern ref int FailInterface(TargetInterface target);
}
