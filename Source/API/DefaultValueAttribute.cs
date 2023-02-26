namespace Prepatcher;

/// <summary>
/// Specifies the default value of a PrepatcherField
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class DefaultValueAttribute : Attribute
{
    public DefaultValueAttribute(object? defaultValue)
    {
    }
}
