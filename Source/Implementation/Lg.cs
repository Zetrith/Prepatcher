namespace Prepatcher;

internal static class Lg
{
    internal static Action<object>? _infoFunc;
    internal static Action<object>? _errorFunc;
    internal static Action<object>? _verboseFunc;

    internal static void Info(object msg) => _infoFunc?.Invoke(msg);

    internal static void Error(string msg) => _errorFunc?.Invoke(msg);

    internal static void Verbose(string msg) => _verboseFunc?.Invoke(msg);
}
