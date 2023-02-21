namespace Prepatcher;

internal static class Lg
{
    internal static Action<object>? InfoFunc;
    internal static Action<object>? ErrorFunc;
    internal static Action<object>? VerboseFunc;

    internal static void Info(object msg) => InfoFunc?.Invoke(msg);

    internal static void Error(string msg) => ErrorFunc?.Invoke(msg);

    internal static void Verbose(string msg) => VerboseFunc?.Invoke(msg);
}
