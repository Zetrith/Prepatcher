using Verse;

namespace Prepatcher;

internal static class Lg
{
    internal static Action<object> InfoFunc;
    internal static Action<object> ErrorFunc;

    internal static void Info(object msg) => InfoFunc(msg);

    internal static void Error(string msg) => ErrorFunc(msg);
}
