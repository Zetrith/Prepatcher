namespace Prepatcher;

/// <summary>
/// Attribute marking an assembly rewriting method
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class FreePatchAttribute : Attribute
{
}
