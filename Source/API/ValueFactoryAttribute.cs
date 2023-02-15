namespace Prepatcher;

/// <summary>
/// Specifies a method supplying the initial value of a PrepatcherField.
/// The method has to be in the same class as the PrepatcherField.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ValueFactoryAttribute : Attribute
{
    public ValueFactoryAttribute(string factoryMethod)
    {
    }
}
