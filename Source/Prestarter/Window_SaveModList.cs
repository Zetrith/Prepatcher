using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Prestarter;

public class Window_SaveModList : Window
{
    public override Vector2 InitialSize => new(700f, 600f);

    private ModManager manager;
    private Vector2 scroll;
    private string listNameInput = "";

    public Window_SaveModList(ModManager manager)
    {
        this.manager = manager;
        closeOnClickedOutside = true;
        doCloseButton = true;
    }

    public override void PreOpen()
    {
        base.PreOpen();
        ModLists.Load();
    }

    public override void DoWindowContents(Rect rect)
    {
        Layouter.BeginArea(rect with { yMax = rect.yMax - 50 });

        Layouter.BeginHorizontal();
        Layouter.BeginScroll(ref scroll, spacing: 0f);
        {
            var lists = ModLists.Lists;
            if (lists != null)
                DrawModLists(lists);
            else
                Layouter.Label("Loading mod lists...");
        }
        Layouter.EndScroll();
        Layouter.EndHorizontal();

        // Save current list
        Layouter.BeginHorizontal();
        {
            listNameInput = Widgets.TextField(Layouter.FlexibleWidth(), listNameInput);
            if (Widgets.ButtonText(Layouter.Rect(100, 30), "Save"))
            {
                var fileName = GenFile.SanitizedFileName(listNameInput);
                var modList = new ModList
                {
                    fileName = fileName,
                    ids = manager.active.ToList(),
                    names = manager.active.Select(m => ModManager.ModData(m)?.Name ?? m).ToList()
                };

                SaveModList(modList, GenFilePaths.AbsFilePathForModList(fileName));
                ModLists.Load();
            }
        }
        Layouter.EndHorizontal();

        Layouter.EndArea();
    }

    private static void SaveModList(ModList modList, string absFilePath)
    {
        try
        {
            modList.fileName = Path.GetFileNameWithoutExtension(absFilePath);
            SafeSaver.Save(absFilePath, "savedModList", delegate
            {
                ScribeMetaHeaderUtility.WriteMetaHeader();
                Scribe_Deep.Look(ref modList, "modList");
            });

            Messages.Message($"Saved mod list as: {modList.fileName}", MessageTypeDefOf.SilentInput);
        }
        catch (Exception ex)
        {
            Log.Error($"Exception while saving mod list: {ex}");
            Messages.Message($"Error saving mod list: {modList.fileName}", MessageTypeDefOf.SilentInput);
        }
    }

    private void DrawModLists(List<ModListData> lists)
    {
        int item = 0;
        foreach (var (file, list, version) in lists)
        {
            Layouter.BeginHorizontal();

            // Alternating item background
            if (item++ % 2 == 0)
                Widgets.DrawAltRect(Layouter.GroupRect());

            // Adhoc space
            Layouter.Rect(0, 30);

            // List name
            using (MpStyle.Set(TextAnchor.MiddleLeft))
                Layouter.Label(list.fileName, true);

            // todo List actions
            // MpLayout.Button("Rename", 100, 30);
            // MpLayout.Button("Overwrite", 100, 30);

            using (MpStyle.Set(GameFont.Tiny))
            using (MpStyle.Set(SaveFileInfo.UnimportantTextColor))
            {
                using (MpStyle.Set(TextAnchor.MiddleRight))
                    Widgets.Label(Layouter.FixedWidth(100), $"{list.ids.Count} mods");

                using (MpStyle.Set(TextAnchor.MiddleLeft))
                    Widgets.Label(Layouter.FixedWidth(100), $"{file.LastWriteTime:yyyy-MM-dd HH:mm}\n{version}");
            }

            // List delete
            if (Widgets.ButtonImage(Layouter.Rect(30, 30), TexButton.Delete, Color.white, GenUI.SubtleMouseoverColor))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmDelete".Translate(list.fileName), delegate
                {
                    file.Delete();
                    ModLists.Load();
                }, destructive: true));
            }

            Layouter.EndHorizontal();
        }
    }
}
