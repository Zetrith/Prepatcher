using System.Linq;
using System.Net;
using RimWorld;
using UnityEngine;
using Verse;

namespace Prestarter;

public partial class ModManager
{
    private Vector2 modDescScroll;

    private void DrawModDescription(Rect r)
    {
        var group = lastSelectedGroup == 1 ? filteredActive : filteredInactive;
        var selectedMod = lastSelectedIndex == -1 ? active[0] : group[lastSelectedIndex];
        var data = ModData(selectedMod);

        Layouter.BeginArea(r);
        {
            TryDrawPreviewImage(data?.PreviewImage);

            using (MpStyle.Set(GameFont.Medium))
                Layouter.Label(data?.Name ?? "Name unknown");

            Layouter.BeginVertical(spacing: 0f, stretch: false);
            {
                Layouter.Label($"{"Id".Colorize(Color.gray)}: {selectedMod}");
                Layouter.Label($"{"Author".Colorize(Color.gray)}: {data?.AuthorsString ?? "Unknown"}");

                if (data is not { IsCoreMod: true })
                {
                    var version = data != null ? VersionString(data) : "Unknown";
                    Layouter.Label($"{"Game versions".Colorize(Color.gray)}: {version}");
                }
            }
            Layouter.EndVertical();

            var descRect = Layouter.FlexibleSpace();
            var modDesc = WebUtility.HtmlDecode(WebUtility.HtmlDecode(data?.Description ?? "No description."));
            var descHeight = Text.CalcHeight(modDesc, descRect.width);
            var viewRect = new Rect(0, 0, descRect.width - 16f, descHeight);

            Widgets.BeginScrollView(descRect, ref modDescScroll, viewRect);
            {
                using (MpStyle.Set(TextAnchor.UpperLeft))
                    Widgets.Label(viewRect, modDesc);
            }
            Widgets.EndScrollView();
        }
        Layouter.EndArea();
    }

    private static string VersionString(ModMetaData data)
    {
        return data.SupportedVersionsReadOnly.
            Select(v =>
            {
                var color = VersionControl.IsCompatible(v) ? Color.green : Color.red;
                return v.Build > 0
                        ? $"{v.Major.ToString()}.{v.Minor.ToString()}.{v.Build.ToString()}".Colorize(color)
                        : $"{v.Major.ToString()}.{v.Minor.ToString()}".Colorize(color);
            }).ToCommaList();
    }

    private static void TryDrawPreviewImage(Texture? tex)
    {
        if (tex != null)
            GUI.DrawTexture(Layouter.AspectRect((float)tex.width / tex.height), tex, ScaleMode.ScaleToFit);
    }
}
