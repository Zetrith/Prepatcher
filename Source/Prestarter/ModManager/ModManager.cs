using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Prestarter;

// Based on the vanilla mod manager
public partial class ModManager
{
    private UniqueList<string> inactive;
    internal UniqueList<string> active; // Mod ids, lowercase with _steam postfixes
    private UniqueList<string>? comparison;

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
    private List<UniqueList<string>> undoStack = new();

    private static bool ShiftIsHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    private static bool ControlIsHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

    private QuickSearchWidget inactiveSearch = new();
    private QuickSearchWidget activeSearch = new();
    private QuickSearchWidget dummySearch = new();

    private string inactiveFilter = "";
    private string activeFilter = "";

    private bool compact;
    private float modItemWidth;

    private static Queue<Action> updates = new();

    private float ItemHeight => compact ? 18f : 24f;

    internal ModManager()
    {
        active = new UniqueList<string>(ModsConfig.ActiveModsInLoadOrder.Select(m => m.PackageId));
        RecacheLists();
    }

    private void RecacheLists()
    {
        ClearSelection();

        inactive =
            new UniqueList<string>(from mod in ModLister.AllInstalledMods
                where !active.Contains(mod.PackageId)
                orderby mod.Official descending, mod.ShortName
                select mod.PackageId);

        bool SatisfiesFilter(string modId, string filter)
        {
            return modId.Contains(filter) || ModData(modId) is { } data && data.Name.Contains(filter);
        }

        filteredInactive = inactive.Where(m => SatisfiesFilter(m, inactiveFilter)).ToList();
        filteredActive = active.Where(m => SatisfiesFilter(m, activeFilter)).ToList();

        modWarnings = GetModWarnings(active);

        foreach (var mod in ModLister.AllInstalledMods)
        {
            var aboutXmlPath =
                GenFile.ResolveCaseInsensitiveFilePath(mod.RootDir.FullName + Path.DirectorySeparatorChar + "About", "About.xml");
            if (!new FileInfo(aboutXmlPath).Exists)
                missingAboutXml.Add(mod.PackageId);
        }
    }

    public static int nextControlId;
    public static int nextControlCount;

    internal void Draw(Rect rect)
    {
        Layouter.BeginFrame();

        if (Event.current.type == EventType.Layout)
        {
            foreach (var update in updates)
                update();
            updates.Clear();
        }

        Widgets.DrawWindowBackground(rect);

        if (Event.current.type == EventType.KeyDown && mouseoverList == null)
            HandleKeys();

        if (Event.current.type == EventType.KeyDown && ControlIsHeld && Event.current.keyCode == KeyCode.Space)
            Log.Message(Layouter.DebugString());

        DrawInner(rect.ContractedBy(15));
    }

    private const string PasteDesc = "Paste a mod list from the clipboard.\n\nContent can be:\n- Text in ModsConfig.xml format\n- Text in RimPy's markdown format\n- Link to rentry.co with markdown in RimPy's format\n\nShortcut: Ctrl+V";
    private const string CopyDesc = "Copy the current mod list to the clipboard in ModsConfig.xml format";

    private void DrawInner(Rect rect)
    {
        Layouter.BeginArea(rect);
        Layouter.BeginHorizontal();
        {
            if (Layouter.Button("Load list", 100, 30))
                OpenModListLoader();

            if (Layouter.Button("Save list", 100, 30))
                Find.WindowStack.Add(new Window_SaveModList(this));

            if (Layouter.Button("Paste list", 100, 30))
                Find.Root.StartCoroutine(PasteModsCoroutine(GUIUtility.systemCopyBuffer));

            TooltipHandler.TipRegion(Layouter.LastRect(), PasteDesc);

            if (Layouter.Button("Copy list", 100, 30))
                CopyMods();

            TooltipHandler.TipRegion(Layouter.LastRect(), CopyDesc);

            if (Layouter.Button("Auto-sort", 100, 30))
                TrySortMods();

            // if (Layouter.Button("Steam info", 100, 30))
            //     Find.WindowStack.Add(new SteamWindow(this));

            // var compactBox = autoSortBtn with { x = autoSortBtn.x + 120, width = 90 };
            // Widgets.CheckboxLabeled(compactBox, "Compact", ref compact);
        }
        Layouter.EndHorizontal();

        Layouter.BeginHorizontal();
        {
            // Mod list drawing has to be postponed because its button count varies depending on layout
            Layouter.PostponeFlexible(r =>
                DoList("Disabled", r, filteredInactive, ref inactiveScroll, ref inactiveGroup,
                ref inactiveFilter, inactiveSearch, (_, _) => { }));

            Layouter.PostponeFlexible(r =>
            {
                if (mouseoverList == null)
                    DoList("Enabled", r, filteredActive, ref activeScroll, ref activeGroup,
                        ref activeFilter, activeSearch, OnReorder);
                else
                    DoListPreview($"{mouseoverList.fileName} preview", r,
                        mouseoverList.ids);
            });

            Layouter.PostponeFlexible(DrawModDescription);

            ReorderableLinePatch.dontDrawForGroup = inactiveGroup;
            ReorderableWidget.NewMultiGroup(new List<int> { inactiveGroup, activeGroup }, OnCrossReorder);
        }
        Layouter.EndHorizontal();

        Layouter.BeginHorizontalCenter();
        {
            if (Layouter.Button("Launch", 100, 30))
                Launch();

            if (KeyBindingDefOf.Accept.KeyDownEvent)
                Launch();
        }
        Layouter.EndHorizontalCenter();
        Layouter.EndArea();

        HandleDrag();

        // Dragging mods changes control counts which breaks the button
        // Assigning a fixed, non-sequential id fixes it
        nextControlId = "ButtonInvisible".GetHashCode();
        nextControlCount = 1;

        if (Widgets.ButtonInvisible(rect, doMouseoverSound: false) && !ShiftIsHeld && !ControlIsHeld)
            ClearSelection();

        UpdateModListLoader();
    }

    internal static ModMetaData? ModData(string modId)
    {
        return ModLister.GetModWithIdentifier(modId);
    }

    private static ContentSource ModSource(string modId)
    {
        return ModData(modId)?.Source ??
               (modId.EndsWith(ModMetaData.SteamModPostfix) ? ContentSource.SteamWorkshop : ContentSource.ModsFolder);
    }

    private string ModShortName(string modId)
    {
        return ModLister.GetModWithIdentifier(modId)?.ShortName ??
               mouseoverList?.names.ElementAtOrDefault(mouseoverList.ids.IndexOf(modId)) ??
               modId;
    }

    private bool ModIsActiveNoPostfix(string modId)
    {
        modId = modId.ToLowerInvariant();
        return active.Contains(modId) || active.Contains(modId + ModMetaData.SteamModPostfix);
    }

    internal void SetComparison(List<string> list)
    {
        comparison = new UniqueList<string>(list);
    }

    private void SetActive(List<string> newActive)
    {
        PushUndo();
        ClearSelection();

        active = new UniqueList<string>(newActive);

        if (!ModIsActiveNoPostfix("zetrith.prepatcher"))
            active.InsertRange(0, new[] { "zetrith.prepatcher" });

        if (!ModIsActiveNoPostfix("ludeon.rimworld"))
            active.InsertRange(0, new[] { "ludeon.rimworld" });

        RecacheLists();
    }

    internal static void QueueUpdate(Action action)
    {
        updates.Enqueue(action);
    }
}
