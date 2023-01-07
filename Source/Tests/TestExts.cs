using System.Collections.Generic;

namespace Tests;

public class TestExts
{
    internal static IEnumerable<T> EnumerableOf<T>(T obj)
    {
        yield return obj;
    }
}
