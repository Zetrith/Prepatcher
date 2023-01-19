namespace Prepatcher;

/// <summary>
/// Attribute marking the accessor of a requested new field
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class PrepatcherFieldAttribute : Attribute
{
}
