using System.Diagnostics;

namespace Prepatcher;

internal class StopwatchScope : IDisposable
{
    private Stopwatch watch = Stopwatch.StartNew();
    private string title;

    private StopwatchScope()
    {
    }

    public void Dispose()
    {
        Lg.Info($"{title} took {watch.Elapsed.TotalMilliseconds}ms");
    }

    internal static StopwatchScope Measure(string title)
    {
        return new StopwatchScope
        {
            title = title
        };
    }
}
