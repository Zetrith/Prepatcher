using HarmonyLib;
using Verse;

namespace Prestarter;

[HarmonyPatch(typeof(ReorderableWidget), nameof(ReorderableWidget.DrawLine))]
internal static class ReorderableLinePatch
{
    internal static int? dontDrawForGroup;

    static bool Prefix(int groupID) => groupID != dontDrawForGroup;
}
