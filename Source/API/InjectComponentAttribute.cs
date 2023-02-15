namespace Prepatcher;

/// <summary>
/// Marks a PrepatcherField for automatic injection of components from target class
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class InjectComponentAttribute : Attribute
{
}
