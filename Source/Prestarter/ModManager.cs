using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Prestarter;

// Based on the vanilla mod manager
[HotSwappable]
internal class ModManager
{
    private List<string> inactive = new();
    private List<string> active;

    private List<string> filteredInactive;
    private List<string> filteredActive;

    private int inactiveGroup;
    private int activeGroup;

    private Vector2 inactiveScroll;
    private Vector2 activeScroll;

    private bool checkScroll;

    private int lastSelectedIndex = -1;
    private int lastSelectedGroup = 1;

    private List<string> selectedMods = new();
    private string? draggedMod;

    private List<string>? DraggedMods =>
        ReorderableWidget.Dragging
            ? selectedMods.Contains(draggedMod) ? selectedMods : new List<string> { draggedMod }
            : null;

    private int? undoneIndex;
    private List<List<string>> undoStack = new();

    private static bool ShiftIsHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    private static bool ControlIsHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

    private QuickSearchWidget inactiveSearch = new();
    private QuickSearchWidget activeSearch = new();

    private string inactiveFilter = "";
    private string activeFilter = "";

    private float modItemWidth;

    internal ModManager()
    {
        active = ModsConfig.ActiveModsInLoadOrder.Select(m => m.PackageId).ToList();
        RecacheLists();
    }

    private void RecacheLists()
    {
        ClearSelection();

        inactive =
            (from mod in ModLister.AllInstalledMods
                where !active.Contains(mod.PackageId)
                orderby mod.Official descending, mod.ShortName
                select mod.PackageId).ToList();

        filteredInactive = inactive.Where(m => m.Contains(inactiveFilter)).ToList();
        filteredActive = active.Where(m => m.Contains(activeFilter)).ToList();
    }

    internal void Draw(Rect rect)
    {
        Widgets.DrawWindowBackground(rect);

        HandleKeys();

        rect = rect.ContractedBy(15);

        Rect listRects = rect;
        listRects.yMax -= 50;

        DoList("Disabled", listRects.LeftPart(0.49f), filteredInactive, ref inactiveScroll, ref inactiveGroup, ref inactiveFilter, inactiveSearch, (_, _) => { });
        DoList("Enabled",listRects.RightPart(0.49f), filteredActive, ref activeScroll, ref activeGroup, ref activeFilter, activeSearch, OnReorder);

        ReorderableLinePatch.dontDrawForGroup = inactiveGroup;
        ReorderableWidget.NewMultiGroup(new List<int> { inactiveGroup, activeGroup }, OnCrossReorder);

        if (Widgets.ButtonText(new Rect(0, rect.yMax - 30, 100, 30).CenteredOnXIn(rect), "Launch"))
            LaunchAsync();

        if (KeyBindingDefOf.Accept.KeyDownEvent)
            LaunchAsync();

        if (Event.current.type == EventType.Repaint)
            HandleDrag();
    }

    private void HandleKeys()
    {
        if (Event.current.type == EventType.KeyDown)
        {
            if (ControlIsHeld && Event.current.keyCode == KeyCode.Z)
            {
                if (ShiftIsHeld)
                    Redo();
                else
                    Undo();

                Event.current.Use();
            }

            if (Event.current.keyCode == KeyCode.DownArrow)
            {
                OnDownKey(lastSelectedGroup == 1 ? filteredActive : filteredInactive);
                Event.current.Use();
            }
            else if (Event.current.keyCode == KeyCode.UpArrow)
            {
                OnUpKey(lastSelectedGroup == 1 ? filteredActive : filteredInactive);
                Event.current.Use();
            }
        }
    }

    private void OnDownKey(List<string> currentList)
    {
        if (lastSelectedIndex + 1 < currentList.Count)
        {
            SetOnlySelection(currentList[lastSelectedIndex + 1]);
            checkScroll = true;
        }
    }

    private void OnUpKey(List<string> currentList)
    {
        if (lastSelectedIndex - 1 >= 0)
        {
            SetOnlySelection(currentList[lastSelectedIndex - 1]);
            checkScroll = true;
        }
    }

    private void HandleDrag()
    {
        draggedMod = null;

        if (ReorderableWidget.Dragging && ReorderableWidget.GetDraggedIndex >= 0)
        {
            var modList = ReorderableWidget.GetDraggedFromGroupID == activeGroup ? filteredActive : filteredInactive;
            if (!modList.NullOrEmpty() && modList.Count > ReorderableWidget.GetDraggedIndex)
                draggedMod = modList[ReorderableWidget.GetDraggedIndex];
        }

        if (!DraggedMods.NullOrEmpty())
        {
            var mousePos = UI.MousePositionOnUIInverted;
            var absRect = ReorderableWidget.reorderables[ReorderableWidget.draggingReorderable].absRect;
            var mouseToCornerAtStart = absRect.min - ReorderableWidget.dragStartPos;

            Find.WindowStack.ImmediateWindow(
                12345,
                new Rect(mousePos.x + mouseToCornerAtStart.x, mousePos.y - 13f, modItemWidth, 26f * Math.Min(DraggedMods.Count, 30)),
                WindowLayer.Super, DoDraggedMods, doBackground: false, shadowAlpha: 0f
            );
        }
    }

    private void DoDraggedMods()
    {
        if (DraggedMods is not { Count: > 0 })
            return;

        int index = 0;
        Widgets.DrawWindowBackground(new Rect(0f, 0f, modItemWidth, 26f * Math.Min(DraggedMods.Count, 30)));

        foreach (var mod in DraggedMods)
        {
            if (index > 30)
                break;
            Rect r = new Rect(0f, index * 26f, modItemWidth, 26f);
            DoModRow(r, mod, index, true);
            index++;
        }
    }

    private void DoList(string label, Rect rect, List<string> list, ref Vector2 scroll, ref int group, ref string filter, QuickSearchWidget search, Action<int, int> onReorder)
    {
        Widgets.Label(rect with {height = 30f}, label + $" ({list.Count})");
        rect.yMin += 35f;

        search.OnGUI(rect with {height = 30f});
        rect.yMin += 35f;

        if (search.filter.Text != filter)
        {
            ClearSelection();
            filter = search.filter.Text;
            RecacheLists();
        }

        const float itemHeight = 26f;
        Widgets.BeginScrollView(rect, ref scroll, new Rect(0, 0, rect.width - 16, list.Count * itemHeight));

        float viewInOutStart = scroll.y - 26f;
        float viewInOutEnd = scroll.y + rect.height;

        if (Event.current.type == EventType.Repaint)
            group = ReorderableWidget.NewGroup(onReorder, ReorderableDirection.Vertical, rect);

        int itemIndex = 0;

        foreach (var item in list)
        {
            var itemRect = new Rect(0, itemIndex * 26f, rect.width - 16f, itemHeight);
            ReorderableWidget.Reorderable(group, itemRect, highlightDragged: false);

            if (itemRect.y >= viewInOutStart && itemRect.y <= viewInOutEnd)
                DoModRow(itemRect, item, itemIndex, false);

            if (checkScroll && lastSelectedIndex == itemIndex)
            {
                var dir = !(itemRect.y <= scroll.y) ? 1 : -1;
                while (itemRect.y <= viewInOutStart || itemRect.y + 26f > viewInOutEnd)
                {
                    scroll.y += 26f * dir;
                    viewInOutStart = scroll.y - 26f;
                    viewInOutEnd = scroll.y + rect.height;
                }

                checkScroll = false;
            }

            itemIndex++;
        }

        Widgets.EndScrollView();
    }

    private void DoModRow(Rect rect, string mod, int index, bool isDragged)
    {
        modItemWidth = rect.width;

        if (!isDragged && DraggedMods != null && DraggedMods.Contains(mod))
            return;

        if (selectedMods.Contains(mod) && !ReorderableWidget.Dragging)
            Widgets.DrawHighlightSelected(rect);

        if (index % 2 == 1)
            Widgets.DrawLightHighlight(rect);

        var modData = ModData(mod);

        ContentSourceUtility.DrawContentSource(rect, modData.Source);
        rect.xMin += 28f;

        Widgets.Label(rect, modData.ShortName);

        if (isDragged)
            return;

        if (!ReorderableWidget.Dragging)
            Widgets.DrawHighlightIfMouseover(rect);

        if (Widgets.ButtonInvisible(rect))
            HandleModClick(mod, index);
    }

    private void HandleModClick(string mod, int index)
    {
        // Changed group
        if (selectedMods.Count > 0 && active.Contains(selectedMods[0]) != active.Contains(mod))
        {
            SetOnlySelection(mod);
            return;
        }

        if (lastSelectedIndex == -1)
            SetOnlySelection(mod);

        // Control selects or deselects one
        if (ControlIsHeld)
        {
            if (selectedMods.Contains(mod))
                selectedMods.Remove(mod);
            else
                selectedMods.Add(mod);
        }

        // Shift selects contiguous from last selected to current
        if (ShiftIsHeld)
        {
            if (!ControlIsHeld)
                selectedMods.Clear();

            var list = lastSelectedGroup == activeGroup ? filteredActive : filteredInactive;
            for (int j = lastSelectedIndex; j != index; j += Math.Sign(index - lastSelectedIndex))
                if (!selectedMods.Contains(list[j]))
                    selectedMods.Add(list[j]);

            if (!selectedMods.Contains(mod))
                selectedMods.Add(mod);
        }

        if (ControlIsHeld)
            SetLastSelected(mod);

        if (!ControlIsHeld && !ShiftIsHeld)
            SetOnlySelection(mod);

        SortSelected();
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
        SortSelected();
    }

    private void Undo()
    {
        ClearSelection();

        if (undoneIndex > 0)
        {
            undoneIndex -= 1;
            active = undoStack[undoneIndex.Value];
        }

        if (undoneIndex is null && undoStack.Count > 0)
        {
            undoStack.Add(active);
            undoneIndex = undoStack.Count - 2;
            active = undoStack[undoneIndex.Value];
        }

        RecacheLists();
    }

    private void Redo()
    {
        ClearSelection();

        if (undoneIndex < undoStack.Count - 1)
        {
            undoneIndex += 1;
            active = undoStack[undoneIndex.Value];
        }

        RecacheLists();
    }

    private void PushUndo()
    {
        if (undoneIndex != null)
        {
            undoStack.RemoveRange(undoneIndex.Value, undoStack.Count - undoneIndex.Value);
            undoneIndex = null;
        }

        undoStack.Add(new List<string>(active));
    }

    private void SortSelected()
    {
        if (selectedMods.Count == 0 || active.Contains(selectedMods[0]))
            return;

        selectedMods = (from m in selectedMods.Select(ModData)
            orderby m.Official descending, m.ShortName
            select m.PackageId).ToList();
    }

    private void ClearSelection()
    {
        selectedMods.Clear();
        lastSelectedIndex = -1;
    }

    private void SetOnlySelection(string mod)
    {
        SetLastSelected(mod);
        selectedMods.Clear();
        selectedMods.Add(mod);
    }

    private void SetLastSelected(string mod)
    {
        lastSelectedIndex = active.Contains(mod) ? filteredActive.IndexOf(mod) : filteredInactive.IndexOf(mod);
        lastSelectedGroup = active.Contains(mod) ? activeGroup : inactiveGroup;
    }

    private static ModMetaData ModData(string modId)
    {
        return ModLister.GetModWithIdentifier(modId);
    }

    private void LaunchAsync()
    {
        ModsConfig.SetActiveToList(active);
        ModsConfig.Save();

        LongEventHandler.QueueLongEvent(PrestarterInit.DoLoad, "", true, null, false);
    }
}
