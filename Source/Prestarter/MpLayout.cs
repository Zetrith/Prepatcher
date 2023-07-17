using UnityEngine;
using Verse;

namespace Prestarter;

[HotSwappable]
public static class MpLayout
{
    public static void Label(string text, bool inheritHeight = false)
    {
        GUI.Label(inheritHeight ? Layouter.FlexibleWidth() : Layouter.ContentRect(text), text, Text.CurFontStyle);
    }

    public static bool Button(string text, float width, float height = 35f)
    {
        return Widgets.ButtonText(Layouter.Rect(width, height), text);
    }

    public static void BeginHorizCenter()
    {
        Layouter.BeginHorizontal();
        Layouter.FlexibleWidth();
    }

    public static void EndHorizCenter()
    {
        Layouter.FlexibleWidth();
        Layouter.EndHorizontal();
    }
}
