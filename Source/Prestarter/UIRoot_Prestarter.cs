using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

    public UIRoot_Prestarter()
    {
        KeyPrefs.KeyPrefsData.keyPrefs[KeyBindingDefOf.Accept] = new KeyBindingData(KeyCode.Return, KeyCode.None);
    }

    public override void UIRootOnGUI()
    {
        base.UIRootOnGUI();
        UIMenuBackgroundManager.background.BackgroundOnGUI();

        ReorderableWidget.ReorderableWidgetOnGUI_BeforeWindowStack();

        const float managerWidth = 800f;
        const float managerHeight = 700f;
        var managerRect = new Rect(UI.screenWidth / 2f - managerWidth / 2, UI.screenHeight / 2f - managerHeight / 2,
            managerWidth, managerHeight);

        manager.Draw(managerRect);

        windows.WindowStackOnGUI();

        ReorderableWidget.ReorderableWidgetOnGUI_AfterWindowStack();
    }
}
