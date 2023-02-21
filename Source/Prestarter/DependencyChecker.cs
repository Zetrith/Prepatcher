using RimWorld;
using UnityEngine;
using Verse;

namespace Prestarter;

public class DependencyChecker : Mod
{
    public DependencyChecker(ModContentPack content) : base(content)
    {
    }
}

[HotSwappable]
class DepWindow : Window
{
    public override Vector2 InitialSize => new(500, 600);

    public override void DoWindowContents(Rect inRect)
    {
        SetInitialSizeAndPosition();

        using (MpStyle.Set(GameFont.Medium))
            Widgets.Label(new Rect(10, 10, 500, 150), "Missing required mods");

        using (MpStyle.Set(Color.gray))
            Widgets.DrawBox(new Rect(0, 50, inRect.width, 125));

        Widgets.Label(new Rect(10, 60, 500, 150), "Prepatcher <color=#999999>(zetrith.prepatcher)</color>");

        using (MpStyle.Set(GameFont.Tiny))
        {
            Widgets.Label(new Rect(10, 85, 500, 150), "Used by: Carousel (zetrith.carousel), RocketMan (Krkr.RocketMan)");
        }

        Widgets.ButtonText(new Rect(10, 135, 100, 30), "Steam");
        if (Widgets.ButtonText(new Rect(120, 135, 100, 30), "Download"))
            Find.WindowStack.Add(new Page_ModsConfig());

        Widgets.Label(new Rect(10, 190, 500, 60), "Install the missing mods and restart the game.\n\nThe missing mods will be activated and sorted into your mod list.");

        Widgets.ButtonText(new Rect(10, 270, 110, 30), "Mod manager");
    }
}
