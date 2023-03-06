using System.Collections.Generic;

namespace Prepatcher.Process;

public static class InjectionHelper
{
    public static void Clear<T, TF>(ref TF? field, object target)
    {
        if (target is not T) return;
        field = default;
    }

    public static void TryInject<T, TF>(ref TF field, object target, IEnumerable<object> comps)
    {
        if (target is not T) return;
        if (field != null) return;

        foreach (var comp in comps)
            if (comp is TF casted)
            {
                field = casted;
                break;
            }
    }
}
