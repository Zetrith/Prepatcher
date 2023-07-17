using System;
using UnityEngine;
using Verse;

namespace Prestarter;

public partial class ModManager
{
    private void HandleDrag()
    {
        if (draggedMod == null && ReorderableWidget.Dragging && ReorderableWidget.GetDraggedIndex >= 0)
        {
            var modList = ReorderableWidget.GetDraggedFromGroupID == activeGroup ? filteredActive : filteredInactive;
            if (!modList.NullOrEmpty() && modList.Count > ReorderableWidget.GetDraggedIndex)
                draggedMod = modList[ReorderableWidget.GetDraggedIndex];
        }

        if (draggedMod != null && !ReorderableWidget.Dragging)
            draggedMod = null;

        if (Event.current.type == EventType.Repaint && DraggedMods.Count > 0)
        {
            var mousePos = UI.MousePositionOnUIInverted;
            var absRect = ReorderableWidget.reorderables[ReorderableWidget.draggingReorderable].absRect;
            var mouseToCornerAtStart = absRect.min - ReorderableWidget.dragStartPos;

            Find.WindowStack.ImmediateWindow(
                12345,
                new Rect(
                    mousePos.x + mouseToCornerAtStart.x,
                    mousePos.y + mouseToCornerAtStart.y - DraggedMods.IndexOf(draggedMod!) * ItemHeight,
                    modItemWidth,
                    ItemHeight * Math.Min(DraggedMods.Count, 30)
                ),
                WindowLayer.Super, DoDraggedMods, doBackground: false, shadowAlpha: 0f
            );
        }
    }

    private void DoDraggedMods()
    {
        if (DraggedMods.Count == 0)
            return;

        int index = 0;
        Widgets.DrawWindowBackground(new Rect(0f, 0f, modItemWidth, ItemHeight * Math.Min(DraggedMods.Count, 30)));

        foreach (var mod in DraggedMods)
        {
            if (index > 30)
                break;
            Rect r = new Rect(0f, index * ItemHeight, modItemWidth, ItemHeight);
            DoModRow(r, mod, index, true, false);
            index++;
        }
    }

    private void OnReorder(int from, int to)
    {
        PushUndo();

        if (!selectedMods.Contains(filteredActive[from]))
            SetOnlySelection(filteredActive[from]);

        string? firstNotSelectedAboveTo = null;
        for (int i = to - 1; i >= 0; i--)
        {
            firstNotSelectedAboveTo = active[i];
            if (!selectedMods.Contains(firstNotSelectedAboveTo))
                break;
        }

        foreach (var selected in selectedMods)
            active.Remove(selected);

        active.InsertRange(
            firstNotSelectedAboveTo == null ? 0 : active.IndexOf(firstNotSelectedAboveTo) + 1,
            selectedMods
        );

        RecacheLists();
    }

    private void OnCrossReorder(int from, int fromGroup, int to, int toGroup)
    {
        var fromList = fromGroup == activeGroup ? filteredActive : filteredInactive;
        if (!selectedMods.Contains(fromList[from]))
            SetOnlySelection(fromList[from]);

        if (fromGroup == activeGroup)
        {
            PushUndo();

            foreach (var selected in selectedMods)
                active.Remove(selected);
        }
        else if (fromGroup == inactiveGroup)
        {
            PushUndo();
            active.InsertRange(to, selectedMods);
        }

        RecacheLists();
    }
}
