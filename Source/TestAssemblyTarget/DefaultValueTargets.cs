namespace TestAssemblyTarget;

public class CtorsClass
{
    public int counter;

    public CtorsClass()
    {
    }

    public CtorsClass(int a)
    {
    }
}

public class DerivedCtorsClass : CtorsClass
{
    public DerivedCtorsClass()
    {
    }

    public DerivedCtorsClass(int a)
    {
    }

    public DerivedCtorsClass(int a, int b) : base(1)
    {
        for (; b < 10; b++)
            if (a == 0 && b == 9)
                return;

        if (a == 1)
        {
            Console.WriteLine("1");
            return;
        }

        if (a == 2)
        {
            Console.WriteLine("2");
            return;
        }

        throw new Exception();
    }

    public DerivedCtorsClass(string c) : this(1)
    {
    }
}
