using System.Collections.Generic;
using System.Reflection;

namespace Prepatcher.Process;

public static class InjectionHelper
{
    public static void TryInject(RuntimeFieldHandle fieldHandle, object target, IEnumerable<object> comps)
    {
        var field = FieldInfo.GetFieldFromHandle(fieldHandle);

        if (!field.DeclaringType!.IsInstanceOfType(target)) return;
        if (field.GetValue(target) != null) return;

        foreach (var comp in comps)
            if (field.FieldType.IsInstanceOfType(comp))
            {
                field.SetValue(target, comp);
                break;
            }
    }
}
