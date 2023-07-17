using HarmonyLib;
using RimWorld;
using Verse.Sound;

namespace Prepatcher;

internal static partial class HarmonyPatches
{
    internal static void CancelSounds()
    {
        // Cancel MusicManagerEntryUpdate because it requires SongDefOf.EntrySong != null
        harmony.Patch(
            typeof(MusicManagerEntry).GetMethod(nameof(MusicManagerEntry.MusicManagerEntryUpdate)),
            new HarmonyMethod(typeof(HarmonyPatches), nameof(Cancel))
        );

        // Sounds may not be loaded after opening the mod manager
        harmony.Patch(
            typeof(SoundStarter).GetMethod(nameof(SoundStarter.PlayOneShotOnCamera)),
            new HarmonyMethod(typeof(HarmonyPatches), nameof(Cancel))
        );
    }
}
