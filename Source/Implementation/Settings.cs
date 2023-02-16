using UnityEngine;
using Verse;

namespace Prepatcher;

public class Settings : ModSettings
{
    public bool disablePrestarter;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref disablePrestarter, "disablePrestarter");
    }

    public void DoSettingsWindow(Rect rect)
    {
        var listing = new Listing_Standard();
        listing.Begin(rect);
        listing.ColumnWidth = 220f;

        listing.CheckboxLabeled("Disable Prestarter", ref disablePrestarter);

        listing.End();
    }
}
