namespace Prepatcher;

/// <summary>
/// Marks the accessor of a requested new field
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class PrepatcherFieldAttribute : Attribute
{
    /// <summary>
    /// Specifies the default value of the field
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Specifies a method supplying the initial value of the field.
    /// The method has to be in the same class as this accessor.
    /// </summary>
    public string? ValueFactory { get; set; }
}
