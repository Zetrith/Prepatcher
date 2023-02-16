namespace Prepatcher;

/// <summary>
/// Specifies a method supplying the initial value of a PrepatcherField.
/// The method has to be in the same class as the PrepatcherField.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ValueInitializerAttribute : Attribute
{
    public ValueInitializerAttribute(string initializerMethod)
    {
    }
}
