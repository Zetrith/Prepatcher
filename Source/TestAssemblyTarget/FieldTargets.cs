namespace TestAssemblyTarget;

public class TargetClass
{
}

public class SecondTargetClass
{
    public readonly TargetClass inner;

    public SecondTargetClass(TargetClass inner)
    {
        this.inner = inner;
    }
}

public struct TargetStruct
{
}

public class TargetGeneric<T>
{
}

public class TargetGeneric3<T,U,W>
{
}

public interface ITarget
{
}
