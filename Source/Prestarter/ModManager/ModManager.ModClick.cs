using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using Verse;

namespace Prestarter;

public partial class ModManager
{
    private void HandleModClick(string mod, int index, int btn, bool shift, bool ctrl)
    {
        // Changed group
        if (selectedMods.Count > 0 && active.Contains(selectedMods[0]) != active.Contains(mod))
        {
            SetOnlySelection(mod);
            return;
        }

        // Right click action
        if (btn == 1)
        {
            var modData = ModData(mod);
            if (modData != null)
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new("Open folder", () =>
                    {
                        Application.OpenURL(modData.RootDir.FullName);
                    }),
                    new("Download update", () =>
                    {
                        if (modData.publishedFileIdInt != PublishedFileId_t.Invalid)
                            Log.Message($"Prestarter: download update {SteamUGC.DownloadItem(modData.publishedFileIdInt, true)}");
                    })
                }));

            SetOnlySelection(mod);
            return;
        }

        if (lastSelectedIndex == -1)
            SetOnlySelection(mod);

        // Control selects or deselects one
        if (ctrl)
        {
            if (selectedMods.Contains(mod))
                selectedMods.Remove(mod);
            else
                selectedMods.Add(mod);
        }

        // Shift selects contiguous from last selected to current
        if (shift)
        {
            if (!ctrl)
                selectedMods.Clear();

            var list = lastSelectedGroup == activeGroup ? filteredActive : filteredInactive;
            for (int j = lastSelectedIndex; j != index; j += Math.Sign(index - lastSelectedIndex))
                if (!selectedMods.Contains(list[j]))
                    selectedMods.Add(list[j]);

            if (!selectedMods.Contains(mod))
                selectedMods.Add(mod);
        }

        if (ctrl)
            SetLastSelected(mod);

        if (!ctrl && !shift)
            SetOnlySelection(mod);

        SortSelected();
    }
}
