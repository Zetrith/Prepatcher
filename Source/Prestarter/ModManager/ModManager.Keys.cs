using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Prestarter;

public partial class ModManager
{
    private void HandleKeys()
    {
        if (ControlIsHeld && Event.current.keyCode == KeyCode.Z)
        {
            if (ShiftIsHeld)
                QueueUpdate(Redo);
            else
                QueueUpdate(Undo);

            Event.current.Use();
        }

        if (ControlIsHeld && Event.current.keyCode == KeyCode.V)
        {
            Find.Root.StartCoroutine(PasteModsCoroutine(GUIUtility.systemCopyBuffer));
            Event.current.Use();
        }

        if (Event.current.keyCode == KeyCode.DownArrow)
        {
            QueueUpdate(() => OnDownKey(lastSelectedGroup == 1 ? filteredActive : filteredInactive));
            Event.current.Use();
        }
        else if (Event.current.keyCode == KeyCode.UpArrow)
        {
            QueueUpdate(() => OnUpKey(lastSelectedGroup == 1 ? filteredActive : filteredInactive));
            Event.current.Use();
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
}
