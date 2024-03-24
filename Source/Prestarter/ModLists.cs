using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace Prestarter;

internal record ModListData(FileInfo File, ModList List, string Version);

internal static class ModLists
{
    internal static List<ModListData>? Lists
    {
        get;
        private set;
    }

    internal static void Load()
    {
        Lists = null;

        Task.Run(() =>
        {
            var buildingList = new List<ModListData>();

            foreach (var modListFile in GenFilePaths.AllModListFiles)
            {
                var text = GenFilePaths.AbsFilePathForModList(Path.GetFileNameWithoutExtension(modListFile.FullName));
                try
                {
                    var version = ScribeMetaHeaderUtility.GameVersionOf(modListFile);
                    Scribe.loader.InitLoadingMetaHeaderOnly(text);
                    ScribeMetaHeaderUtility.LoadGameDataHeader(
                        ScribeMetaHeaderUtility.ScribeHeaderMode.ModList,
                        logVersionConflictWarning: false);
                    if (Scribe.mode != LoadSaveMode.Inactive)
                        Scribe.loader.FinalizeLoading();
                    if (GameDataSaveLoader.TryLoadModList(text, out var modList))
                        buildingList.Add(new ModListData(modListFile, modList, version));
                }
                catch (Exception ex)
                {
                    Log.Warning("Exception loading " + text + ": " + ex);
                    Scribe.ForceStop();
                }
            }

            ModManager.QueueUpdate(() => Lists = buildingList);
        });
    }
}
