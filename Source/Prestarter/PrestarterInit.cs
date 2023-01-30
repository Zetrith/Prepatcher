using System;
using HarmonyLib;

namespace Prestarter;

public class PrestarterInit
{
    private static Harmony harmony = new Harmony("prestarter");
    public static Action DoLoad;

    public static void Init()
    {
        harmony.PatchAll();
    }
}
