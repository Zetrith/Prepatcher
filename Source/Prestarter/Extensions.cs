using System.Collections.Generic;
using System.Linq;

namespace Prestarter;

public static class Extensions
{
    public static IEnumerable<T> NotNull<T>(this IEnumerable<T?> e)
    {
        return e.Where(el => el != null)!;
    }
}
