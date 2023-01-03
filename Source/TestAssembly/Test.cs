using System.Collections.Generic;
using Prepatcher;
using TestTargetAssembly;

namespace TestAssembly;

public static class Test
{
    [PrepatcherField]
    private static extern ref int MyInt(this TargetClass target);

    [PrepatcherField]
    private static extern ref List<T> MyField<T>(this TargetGeneric<T> target);

    public static int TestIntField(int i)
    {
        var obj = new TargetClass();
        obj.MyInt() = i;
        return obj.MyInt();
    }

    public static string TestGenericField(string s)
    {
        var obj = new TargetGeneric<string>();
        obj.MyField() = new();
        obj.MyField().Add(s);
        return obj.MyField()[0];
    }
}
