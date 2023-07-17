using Verse;

namespace Prestarter;

public partial class ModManager
{
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
}
