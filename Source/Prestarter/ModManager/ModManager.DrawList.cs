using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Prestarter;

public partial class ModManager
{
    private const float ScrollbarWidth = 16f;

    private void DoList(string label, Rect rect, List<string> list, ref Vector2 scroll, ref int group, ref string filter, QuickSearchWidget search, Action<int, int> onReorder)
    {
        Widgets.Label(rect with {height = 30f}, $"{label} ({list.Count})");
        rect.yMin += 35f;

        search.OnGUI(rect with {height = 30f});
        rect.yMin += 35f;

        if (search.filter.Text != filter)
        {
            ClearSelection();
            filter = search.filter.Text;
            RecacheLists();
        }

        Widgets.BeginScrollView(rect, ref scroll, new Rect(0, 0, rect.width - ScrollbarWidth, list.Count * ItemHeight));

        var viewInOutStart = scroll.y - ItemHeight;
        var viewInOutEnd = scroll.y + rect.height;

        if (Event.current.type == EventType.Repaint)
            group = ReorderableWidget.NewGroup(onReorder, ReorderableDirection.Vertical, rect);

        var itemIndex = 0;

        foreach (var item in list)
        {
            var itemRect = new Rect(0, itemIndex * ItemHeight, rect.width - ScrollbarWidth, ItemHeight);
            ReorderableWidget.Reorderable(group, itemRect, highlightDragged: false);

            if (itemRect.y >= viewInOutStart && itemRect.y <= viewInOutEnd)
                DoModRow(itemRect, item, itemIndex, false, false);

            // if (checkScroll && lastSelectedIndex == itemIndex)
            // {
            //     var dir = !(itemRect.y <= scroll.y) ? 1 : -1;
            //     while (itemRect.y <= viewInOutStart || itemRect.y + ItemHeight > viewInOutEnd)
            //     {
            //         scroll.y += ItemHeight * dir;
            //         viewInOutStart = scroll.y - ItemHeight;
            //         viewInOutEnd = scroll.y + rect.height;
            //     }
            //
            //     checkScroll = false;
            // }

            itemIndex++;
        }

        Widgets.EndScrollView();
    }

    private void DoListPreview(string label, Rect rect, List<string> list)
    {
        Widgets.Label(rect with {height = 30f}, label + $" ({list.Count})");
        rect.yMin += 35f;

        dummySearch.OnGUI(rect with {height = 30f});
        rect.yMin += 35f;

        dummySearch.filter.Text = "";

        Vector2 scroll = default;
        Widgets.BeginScrollView(rect, ref scroll, new Rect(0, 0, rect.width - 16, list.Count * ItemHeight));

        var viewInOutStart = scroll.y - ItemHeight;
        var viewInOutEnd = scroll.y + rect.height;

        var itemIndex = 0;

        foreach (var item in list)
        {
            var itemRect = new Rect(0, itemIndex * ItemHeight, rect.width - 16f, ItemHeight);

            using (MpStyle.Set(Color.gray))
                if (itemRect.y >= viewInOutStart && itemRect.y <= viewInOutEnd)
                    DoModRow(itemRect, item, itemIndex, false, true);

            itemIndex++;
        }

        Widgets.EndScrollView();
    }

    private void DoModRow(Rect rect, string mod, int index, bool isDragged, bool preview)
    {
        modItemWidth = rect.width;

        if (!DraggedMods.NullOrEmpty() && !isDragged && DraggedMods.Contains(mod))
            return;

        if (selectedMods.Contains(mod) && !ReorderableWidget.Dragging && mouseoverList == null)
            Widgets.DrawHighlightSelected(rect);

        if (index % 2 == 1)
            Widgets.DrawLightHighlight(rect);

        DrawContentSource(rect, ModSource(mod), ItemHeight);
        rect.xMin += ItemHeight + 2f;

        if (!isDragged && Mouse.IsOver(rect))
            TooltipHandler.TipRegion(rect, new TipSignal(ModTooltip(mod), mod.GetHashCode() * 3311));

        using (MpStyle.Set(compact ? GameFont.Tiny : GameFont.Small))
        using (MpStyle.Set(ModColor(mod, preview) * GUI.color))
            Widgets.Label(rect, ModShortName(mod));

        if (isDragged)
            return;

        if (!ReorderableWidget.Dragging)
            Widgets.DrawHighlightIfMouseover(rect);

        if (Widgets.ButtonInvisible(rect))
        {
            var btn = Event.current.button;
            var shiftHeld = ShiftIsHeld;
            var ctrlHeld = ControlIsHeld;
            QueueUpdate(() => HandleModClick(mod, index, btn, shiftHeld, ctrlHeld));
        }
    }

    private Color ModColor(string mod, bool preview)
    {
        if (ModData(mod) is not { } metaData)
            return Color.gray;
        if (!preview && modWarnings.ContainsKey(mod))
            return Color.red;
        if (missingAboutXml.Contains(mod))
            return new Color(0.6f, 0.6f, 0f, 1f);
        if (!metaData.VersionCompatible)
            return Color.yellow;
        return Color.white;
    }

    private string ModTooltip(string mod)
    {
        if (ModData(mod) is not { } metaData)
            return $"Id: {mod}\n\nMod not installed";

        if (missingAboutXml.Contains(mod))
            return "Missing About.xml\n\nThis mod isn't installed correctly.";

        var tooltip = $"Id: {metaData.PackageIdPlayerFacing}\n\nAuthor: {metaData.AuthorsString}";
        if (modWarnings.ContainsKey(mod))
            tooltip += "\n\n" + modWarnings[mod];

        if (!metaData.VersionCompatible)
            tooltip += "\n\nThis mod is incompatible with current version of the game.";

        return tooltip;
    }

    private static void DrawContentSource(Rect r, ContentSource source, float size)
    {
        var rect = new Rect(r.x, r.y + r.height / 2f - size / 2f, size, size);
        GUI.DrawTexture(rect, source.GetIcon());
        if (Mouse.IsOver(rect))
        {
            TooltipHandler.TipRegion(rect, () => "Source".Translate() + ": " + source.HumanLabel(), (int)(r.x + r.y * 56161f));
            Widgets.DrawHighlight(rect);
        }
    }
}
