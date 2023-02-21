using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using Verse;
using UnityEngine.Yoga;

namespace Prestarter;

public class UIRoot_Prestarter : UIRoot
{
    private ModManager manager = new();

    public static bool showManager = true;

    public UIRoot_Prestarter()
    {
        KeyPrefs.KeyPrefsData.keyPrefs[KeyBindingDefOf.Accept] = new KeyBindingData(KeyCode.Return, KeyCode.None);
        KeyPrefs.KeyPrefsData.keyPrefs[KeyBindingDefOf.Cancel] = new KeyBindingData(KeyCode.Escape, KeyCode.None);

        ColoredText.ColonistCountRegex = new Regex("\\d+\\.?\\d* (colonist|colonists)");
        ColoredText.DateTimeRegexes = new List<Regex>();
    }

    const float managerWidth = 800f;
    const float managerHeight = 700f;

    public override void UIRootOnGUI()
    {
        base.UIRootOnGUI();
        UIMenuBackgroundManager.background.BackgroundOnGUI();

        ReorderableWidget.ReorderableWidgetOnGUI_BeforeWindowStack();

        if (showManager)
        {
            var managerRect = new Rect(UI.screenWidth / 2f - managerWidth / 2, UI.screenHeight / 2f - managerHeight / 2,
                managerWidth, managerHeight);

            manager.Draw(managerRect);
        }

        windows.WindowStackOnGUI();

        ReorderableWidget.ReorderableWidgetOnGUI_AfterWindowStack();
    }
}
