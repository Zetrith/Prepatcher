using Verse;

namespace Prepatcher;

internal static class Lg
{
    internal static void Info(object msg)
    {
        Log.Message($"Prepatcher: {msg}");
    }

    internal static void Error(string msg)
    {
        Log.Error($"Prepatcher Error: {msg}");
    }
}
