using System.Collections.Generic;
using Prepatcher;
using TestAssemblyTarget;

namespace Tests;

public static class NewFields
{
    [PrepatcherField]
    private static extern ref int MyInt(this TargetClass target);

    [PrepatcherField]
    private static extern ref int MyIntStruct(this ref TargetStruct target);

    [PrepatcherField]
    private static extern ref List<T> MyList<T>(this TargetGeneric<T> target);

    [PrepatcherField]
    private static extern ref (T,W,U) MyTriple<T,U,W>(this TargetGeneric3<T,U,W> b);

    [PrepatcherField]
    private static extern ref (T,T) MyPair<T,U,W>(this TargetGeneric3<T,U,W> b);

    public static int TestIntField(int i)
    {
        var obj = new TargetClass();
        obj.MyInt() = i;
        return obj.MyInt();
    }

    public static int TestIntStructField(int i)
    {
        TargetStruct s = default;
        s.MyIntStruct() = i;
        return s.MyIntStruct();
    }

    public static string TestGenericField1(string s)
    {
        var obj = new TargetGeneric<string>();
        obj.MyList() = new();
        obj.MyList().Add(s);
        return obj.MyList()[0];
    }

    public static string TestGenericField2(string s)
    {
        var obj = new TargetGeneric3<string, int, float>();
        obj.MyTriple() = (s, 1f, 1);
        return obj.MyTriple().Item1;
    }

    public static string TestGenericField3(string s)
    {
        var obj = new TargetGeneric3<string, int, float>();
        obj.MyPair() = (s, s);
        return obj.MyPair().Item1;
    }
}
