using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Prestarter;

public partial class ModManager
{
    private Vector2? loadListOpenAt;
    private ModList? mouseoverList;

    private void OpenModListLoader()
    {
        ModLists.Load();

        var btnRect = Layouter.LastRect();
        loadListOpenAt = UI.GUIToScreenPoint(btnRect.min + new Vector2(0, btnRect.height));
    }

    private void UpdateModListLoader()
    {
        if (loadListOpenAt is { } pos)
        {
            DoModListLoader(pos);
        }
    }

    private void CloseModListLoader()
    {
        loadListOpenAt = null;
        mouseoverList = null;
    }

    private void DoModListLoader(Vector2 pos)
    {
        var height = Math.Min(ModLists.Lists?.Count * ModListItemHeight ?? 150f, 450f);
        height = Math.Max(height, 100f);

        var windowRect = new Rect(
            pos,
            new Vector2(250, height)
        );

        Find.WindowStack.ImmediateWindow(
            "LoadModList".GetHashCode(),
            windowRect,
            WindowLayer.Super,
            () =>
            {
                Find.WindowStack.Notify_ManuallySetFocus(Find.WindowStack.currentlyDrawnWindow);

                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                {
                    CloseModListLoader();
                    Event.current.Use();
                }

                if (!Layouter.BeginArea(windowRect.AtZero(), spacing: 0f))
                    return;

                if (ModLists.Lists == null || ModLists.Lists.Count == 0)
                {
                    var rect = Layouter.FlexibleSpace();

                    using (MpStyle.Set(FloatMenuOption.ColorBGActive))
                        GUI.DrawTexture(rect, BaseContent.WhiteTex);
                    Widgets.DrawAtlas(rect, TexUI.FloatMenuOptionBG);

                    Widgets.Label(
                        rect.ContractedBy(10),
                        ModLists.Lists?.Count == 0 ? "No mod lists" : "Loading mod lists..."
                    );
                }
                else
                {
                    DrawModLists(ModLists.Lists);
                }

                Layouter.EndArea();
            },
            doBackground: false,
            doClickOutsideFunc: () =>
            {
                CloseModListLoader();
                Event.current.Use();
            }
        );
    }

    private const float ModListItemHeight = 30f;

    private Vector2 loaderScroll;

    private void DrawModLists(List<ModListData> lists)
    {
        if (Event.current.type != EventType.Layout)
            mouseoverList = null;

        Layouter.BeginScroll(ref loaderScroll, spacing: 0f);

        foreach (var list in lists)
        {
            Layouter.BeginHorizontal();
            {
                var mouseover = Mouse.IsOver(Layouter.GroupRect());

                using (MpStyle.Set(mouseover ? FloatMenuOption.ColorBGActiveMouseover : FloatMenuOption.ColorBGActive))
                    GUI.DrawTexture(Layouter.GroupRect(), BaseContent.WhiteTex);

                Widgets.DrawAtlas(Layouter.GroupRect(), TexUI.FloatMenuOptionBG);

                Layouter.Rect(0, ModListItemHeight);
                using (MpStyle.Set(TextAnchor.MiddleLeft))
                {
                    var labelRect = Layouter.FlexibleWidth();
                    if (mouseover)
                        labelRect.x += 4;
                    Widgets.Label(labelRect, list.List.fileName);
                }

                if (Widgets.ButtonInvisible(Layouter.GroupRect()))
                {
                    SetActive(list.List.ids);
                    CloseModListLoader();
                }

                if (mouseover)
                    mouseoverList = list.List;
            }
            Layouter.EndHorizontal();
        }

        Layouter.EndScroll();
    }
}
