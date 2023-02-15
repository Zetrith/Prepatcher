using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Prestarter;

// Based on the vanilla mod manager
[HotSwappable]
internal class ModManager
{
    private List<string> inactive = new();
    private List<string> active; // Mod ids with postfixes

    private List<string> filteredInactive;
    private List<string> filteredActive;

    private Dictionary<string, string> modWarnings = new();
    private HashSet<string> missingAboutXml = new();

    private int inactiveGroup;
    private int activeGroup;

    private Vector2 inactiveScroll;
    private Vector2 activeScroll;

    private bool checkScroll;

    private int lastSelectedIndex = -1;
    private int lastSelectedGroup = 1;

    private List<string> selectedMods = new();
    private string? draggedMod;

    private static List<string> emptyList = new();

    private List<string> DraggedMods =>
        draggedMod != null
            ? selectedMods.Contains(draggedMod) ? selectedMods : new List<string> { draggedMod }
            : emptyList;

    private int? undoneIndex;
    private List<List<string>> undoStack = new();

    private static bool ShiftIsHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    private static bool ControlIsHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

    private QuickSearchWidget inactiveSearch = new();
    private QuickSearchWidget activeSearch = new();
    private QuickSearchWidget dummySearch = new();

    private string inactiveFilter = "";
    private string activeFilter = "";

    private bool compact;
    private float modItemWidth;

    private float ItemHeight => compact ? 18f : 24f;

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

        modWarnings = GetModWarnings(active.Select(m => ModData(m)!).ToList());

        foreach (var mod in ModLister.AllInstalledMods)
        {
            var aboutXmlPath =
                GenFile.ResolveCaseInsensitiveFilePath(mod.RootDir.FullName + Path.DirectorySeparatorChar + "About", "About.xml");
            if (!new FileInfo(aboutXmlPath).Exists)
                missingAboutXml.Add(mod.PackageId);
        }
    }

    internal void Draw(Rect rect)
    {
        ModLists.Update(this);

        Widgets.DrawWindowBackground(rect);

        if (ModLists.CurrentList == null)
            HandleKeys();

        rect = rect.ContractedBy(15);

        var modListsBtn = rect with { width = 100, height = 30 };
        if (Widgets.ButtonText(modListsBtn, "Mod lists"))
        {
            ModLists.Load();

            var menu = new FloatMenu(ModLists.floatMenuOptions);
            Find.WindowStack.Add(menu);
            menu.options = ModLists.floatMenuOptions;
        }

        var autoSortBtn = rect with { x = modListsBtn.x + 110, width = 100, height = 30 };
        if (Widgets.ButtonText(autoSortBtn, "Auto-sort"))
            TrySortMods();

        var compactBox = autoSortBtn with { x = autoSortBtn.x + 120, width = 90 };
        Widgets.CheckboxLabeled(compactBox, "Compact", ref compact);

        var listRects = rect;
        listRects.yMin += 40;
        listRects.yMax -= 50;

        DoList("Disabled", listRects.LeftPart(0.49f), filteredInactive, ref inactiveScroll, ref inactiveGroup, ref inactiveFilter, inactiveSearch, (_, _) => { });

        if (ModLists.CurrentList == null)
            DoList("Enabled",listRects.RightPart(0.49f), filteredActive, ref activeScroll, ref activeGroup, ref activeFilter, activeSearch, OnReorder);
        else
            DoListPreview($"{ModLists.CurrentList.fileName} preview",listRects.RightPart(0.49f), ModLists.CurrentList.ids);

        ReorderableLinePatch.dontDrawForGroup = inactiveGroup;
        ReorderableWidget.NewMultiGroup(new List<int> { inactiveGroup, activeGroup }, OnCrossReorder);

        if (Widgets.ButtonText(new Rect(0, rect.yMax - 30, 100, 30).CenteredOnXIn(rect), "Launch"))
            Launch();

        if (KeyBindingDefOf.Accept.KeyDownEvent)
            Launch();

        HandleDrag();

        ModLists.PostUpdate();
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
                new Rect(mousePos.x + mouseToCornerAtStart.x, mousePos.y - 13f, modItemWidth, ItemHeight * Math.Min(DraggedMods.Count, 30)),
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

        Widgets.BeginScrollView(rect, ref scroll, new Rect(0, 0, rect.width - 16, list.Count * ItemHeight));

        var viewInOutStart = scroll.y - ItemHeight;
        var viewInOutEnd = scroll.y + rect.height;

        if (Event.current.type == EventType.Repaint)
            group = ReorderableWidget.NewGroup(onReorder, ReorderableDirection.Vertical, rect);

        var itemIndex = 0;

        foreach (var item in list)
        {
            var itemRect = new Rect(0, itemIndex * ItemHeight, rect.width - 16f, ItemHeight);
            ReorderableWidget.Reorderable(group, itemRect, highlightDragged: false);

            if (itemRect.y >= viewInOutStart && itemRect.y <= viewInOutEnd)
                DoModRow(itemRect, item, itemIndex, false, false);

            if (checkScroll && lastSelectedIndex == itemIndex)
            {
                var dir = !(itemRect.y <= scroll.y) ? 1 : -1;
                while (itemRect.y <= viewInOutStart || itemRect.y + ItemHeight > viewInOutEnd)
                {
                    scroll.y += ItemHeight * dir;
                    viewInOutStart = scroll.y - ItemHeight;
                    viewInOutEnd = scroll.y + rect.height;
                }

                checkScroll = false;
            }

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

        if (selectedMods.Contains(mod) && !ReorderableWidget.Dragging && ModLists.CurrentList == null)
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
            HandleModClick(mod, index);
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
            return "Missing About.xml\n\nThe mod is possibly wrongly installed.";

        var tooltip = $"Id: {metaData.PackageIdPlayerFacing}\nAuthor: {metaData.AuthorsString}";
        if (modWarnings.ContainsKey(mod))
            tooltip += "\n\n" + modWarnings[mod];

        if (!metaData.VersionCompatible)
            tooltip += "\n\nThis mod is incompatible with current version of the game.";

        return tooltip;
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
    }

    private void Undo()
    {
        ClearSelection();

        // Currently up the undo stack
        if (undoneIndex > 0)
        {
            undoneIndex -= 1;
            active = undoStack[undoneIndex.Value];
        }

        // Not undoing
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
        if (selectedMods.Count == 0) return;

        var list = active.Contains(selectedMods[0]) ? active : inactive;
        selectedMods.SortBy(m => list.IndexOf(m));
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

    private static ModMetaData? ModData(string modId)
    {
        return ModLister.GetModWithIdentifier(modId);
    }

    private static ContentSource ModSource(string modId)
    {
        return ModData(modId)?.Source ??
               (modId.EndsWith("_steam") ? ContentSource.SteamWorkshop : ContentSource.ModsFolder);
    }

    private static string ModShortName(string modId)
    {
        return ModLister.GetModWithIdentifier(modId)?.ShortName ??
               ModLists.CurrentList?.names[ModLists.CurrentList.ids.IndexOf(modId)]!;
    }

    private void Launch()
    {
        LongEventHandler.QueueLongEvent(() =>
        {
            ModsConfig.SetActiveToList(active);
            ModsConfig.Save();
            PrestarterInit.DoLoad();
        }, "", true, null, false);
    }

    internal void SetActive(ModList modList)
    {
        PushUndo();
        ClearSelection();

        active.Clear();
        var missingMods = new List<int>();

        for (var j = 0; j < modList.ids.Count; j++)
        {
            var modMetaData = ModLister.AllInstalledMods.FirstOrDefault(mod => mod.PackageId == modList.ids[j]);
            if (modMetaData != null)
                active.Add(modList.ids[j]);
            else
                missingMods.Add(j);
        }

        RecacheLists();

        var missingModStrings = missingMods.Select(i => " - " + modList.names[i] + " (" + modList.ids[i] + ")");
        if (missingMods.Any())
            Find.WindowStack.Add(new Dialog_MessageBox(
                $"Mod list activated with missing mods ignored:\n\n{missingModStrings.ToLineList()}",
                "OK"
                ));
    }

    private void TrySortMods()
    {
        var list = active.Select(m => ModData(m)!).ToList();
        var directedAcyclicGraph = new DirectedAcyclicGraph(list.Count);

        for (var i = 0; i < list.Count; i++)
        {
            var modMetaData = list[i];
            foreach (var before in modMetaData.LoadBefore.Concat(modMetaData.ForceLoadBefore))
            {
                var modMetaData2 = list.FirstOrDefault(m => m.SamePackageId(before, ignorePostfix: true));
                if (modMetaData2 != null)
                    directedAcyclicGraph.AddEdge(list.IndexOf(modMetaData2), i);
            }
            foreach (string after in modMetaData.LoadAfter.Concat(modMetaData.ForceLoadAfter))
            {
                var modMetaData3 = list.FirstOrDefault(m => m.SamePackageId(after, ignorePostfix: true));
                if (modMetaData3 != null)
                    directedAcyclicGraph.AddEdge(i, list.IndexOf(modMetaData3));
            }
        }

        var num = directedAcyclicGraph.FindCycle();
        if (num != -1)
        {
            Find.WindowStack.Add(new Dialog_MessageBox("ModCyclicDependency".Translate(list[num].Name)));
            return;
        }

        PushUndo();

        var newActive = new List<string>();
        foreach (int newIndex in directedAcyclicGraph.TopologicalSort())
            newActive.Add(active[newIndex]);
        active = newActive;

        RecacheLists();
    }

    private static Dictionary<string, string> GetModWarnings(List<ModMetaData> mods)
    {
	    Dictionary<string, string> result = new Dictionary<string, string>();
	    for (int i = 0; i < mods.Count; i++)
	    {
		    int index = i;
		    var modMetaData = mods[index];
		    var warningBuilder = new StringBuilder("");

            var incompatible = FindConflicts(modMetaData.IncompatibleWith, null);
		    if (incompatible.Any())
                warningBuilder.AppendLine("ModIncompatibleWithTip".Translate(incompatible.ToCommaList(useAnd: true)));

            var loadBefore = FindConflicts(modMetaData.LoadBefore, (beforeMod) => mods.IndexOf(beforeMod) < index);
		    if (loadBefore.Any())
                warningBuilder.AppendLine("ModMustLoadBefore".Translate(loadBefore.ToCommaList(useAnd: true)));

            var forceLoadBefore = FindConflicts(modMetaData.ForceLoadBefore, (beforeMod) => mods.IndexOf(beforeMod) < index);
		    if (forceLoadBefore.Any())
                warningBuilder.AppendLine("ModMustLoadBefore".Translate(forceLoadBefore.ToCommaList(useAnd: true)));

            var forceLoadAfter = FindConflicts(modMetaData.LoadAfter, (afterMod) => mods.IndexOf(afterMod) > index);
		    if (forceLoadAfter.Any())
                warningBuilder.AppendLine("ModMustLoadAfter".Translate(forceLoadAfter.ToCommaList(useAnd: true)));

            var loadAfter = FindConflicts(modMetaData.ForceLoadAfter, (afterMod) => mods.IndexOf(afterMod) > index);
		    if (loadAfter.Any())
                warningBuilder.AppendLine("ModMustLoadAfter".Translate(loadAfter.ToCommaList(useAnd: true)));

            if (modMetaData.Dependencies.Any())
		    {
			    var missingDeps = modMetaData.UnsatisfiedDependencies();
			    if (missingDeps.Any())
                    warningBuilder.AppendLine("ModUnsatisfiedDependency".Translate(missingDeps.ToCommaList(useAnd: true)));
            }

            var warningString = warningBuilder.ToString().TrimEndNewlines();
            if (!warningString.NullOrEmpty())
		        result.Add(modMetaData.PackageId, warningString);
	    }

	    return result;
    }

    private static List<string> FindConflicts(List<string> modsToCheck, Func<ModMetaData, bool>? predicate)
    {
        var list = new List<string>();
        foreach (var item in modsToCheck)
        {
            ModMetaData activeModWithIdentifier = ModLister.GetActiveModWithIdentifier(item, ignorePostfix: true);
            if (activeModWithIdentifier != null && (predicate == null || predicate(activeModWithIdentifier)))
            {
                list.Add(activeModWithIdentifier.Name);
            }
        }
        return list;
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
