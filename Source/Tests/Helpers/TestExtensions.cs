using System.Collections.Generic;

namespace Tests;

public static class TestExtensions
{
    internal static IEnumerable<T> EnumerableOf<T>(T obj)
    {
        yield return obj;
    }
}
