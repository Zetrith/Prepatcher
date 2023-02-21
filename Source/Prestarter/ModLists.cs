using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace Prestarter;

internal static class ModLists
{
    internal static List<FloatMenuOption> floatMenuOptions = new() { new FloatMenuOption("...", () => {}) };
    internal static Dictionary<string, ModList> modLists = new();
    private static FloatMenuOption? mouseoverOption;

    internal static ModList? CurrentList => mouseoverOption == null ? null : modLists[mouseoverOption.Label];

    private static ConcurrentQueue<ModList?> loadedLists = new();
    private static bool startedLoading;

    internal static void Update(ModManager manager)
    {
        while (loadedLists.TryDequeue(out var list))
        {
            if (list == null)
            {
                floatMenuOptions.RemoveAt(0);
                if (modLists.Count == 0)
                {
                    var noListsOpt = new FloatMenuOption("No mod lists", () => {});
                    noListsOpt.SetSizeMode(FloatMenuSizeMode.Normal);
                    floatMenuOptions.Add(noListsOpt);
                }

                continue;
            }

            var opt = new FloatMenuOption(
                list.fileName,
                () => manager.SetActive(list)
            );

            opt.mouseoverGuiAction = _ => { mouseoverOption = opt; };
            opt.SetSizeMode(FloatMenuSizeMode.Normal);

            floatMenuOptions.Add(opt);
            modLists[list.fileName] = list;
        }

        // A bit of a hack to make a dynamically updating FloatMenu
        foreach (var window in Find.WindowStack.Windows)
            if (window is FloatMenu menu)
                menu.windowRect.size = menu.InitialSize;
    }

    internal static void PostUpdate()
    {
        mouseoverOption = null;
    }

    internal static void Load()
    {
        if (startedLoading) return;
        startedLoading = true;

        Task.Run(() =>
        {
            foreach (var modListFile in GenFilePaths.AllModListFiles)
            {
                var text = GenFilePaths.AbsFilePathForModList(Path.GetFileNameWithoutExtension(modListFile.FullName));
                try
                {
                    Scribe.loader.InitLoadingMetaHeaderOnly(text);
                    ScribeMetaHeaderUtility.LoadGameDataHeader(ScribeMetaHeaderUtility.ScribeHeaderMode.ModList,
                        logVersionConflictWarning: false);
                    Scribe.loader.FinalizeLoading();
                    if (GameDataSaveLoader.TryLoadModList(text, out var modList))
                        loadedLists.Enqueue(modList);
                }
                catch (Exception ex)
                {
                    Log.Warning("Exception loading " + text + ": " + ex);
                    Scribe.ForceStop();
                }
            }

            loadedLists.Enqueue(null);
        });
    }
}
