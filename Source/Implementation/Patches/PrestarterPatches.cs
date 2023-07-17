using System.Threading;
using DataAssembly;
using HarmonyLib;
using Prestarter;
using UnityEngine;
using Verse;

namespace Prepatcher;

internal static partial class HarmonyPatches
{
    internal static void PatchGUI()
    {
        harmony.Patch(
            AccessTools.Method(typeof(GUIUtility), "GetControlID", new[] { typeof(int), typeof(FocusType), typeof(Rect) }),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(GetControlIDPostfix))
        );

        harmony.Patch(
            AccessTools.Method(typeof(LongEventHandler), nameof(LongEventHandler.DrawLongEventWindowContents)),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(DrawPrestarterInfo))
        );

        harmony.Patch(
            AccessTools.Method(typeof(DeepProfiler), nameof(DeepProfiler.Start)),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(InitAllMetadataPrefix))
        );

        harmony.Patch(
            AccessTools.Method(typeof(LoadedModManager), nameof(LoadedModManager.ParseAndProcessXML)),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(PrefixParseXML)),
            finalizer: new HarmonyMethod(typeof(HarmonyPatches), nameof(FinalizeParseXML))
        );

        harmony.Patch(
            AccessTools.Method(typeof(DirectXmlLoader), nameof(DirectXmlLoader.DefFromNode)),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(PrefixDefFromNode))
        );
    }

    private static bool parsingXML;

    private static void PrefixParseXML()
    {
        parsingXML = true;
    }

    private static void FinalizeParseXML()
    {
        parsingXML = false;
    }

    private static void PrefixDefFromNode()
    {
        if (parsingXML)
            SetLoadingStage("Loading defs");
    }

    private static void InitAllMetadataPrefix(string label)
    {
        if (label == "Load language metadata.")
            SetLoadingStage("Initializing data");

        if (label == "LoadModXML()")
            SetLoadingStage("Loading XML");

        if (label == "ApplyPatches()")
            SetLoadingStage("Applying patches");

        if (label == "ParseAndProcessXML()")
            SetLoadingStage("Parsing XML");

        if (label == "XmlInheritance.Resolve()")
            SetLoadingStage("Resolving inheritance");

        if (label == "Short hash giving.")
            SetLoadingStage(null); // The last stage
    }

    internal static volatile bool holdLoading = true;

    internal static void SetLoadingStage(string? stage)
    {
        DataStore.loadingStage = stage;

        if (DataStore.openModManager)
        {
            Find.Root.StartCoroutine(Loader.MinimalInit());

            // Thread abortion counts as a crash
            Prefs.data.resetModsConfigOnCrash = false;

            while (holdLoading)
                Thread.Sleep(50);

            Thread.CurrentThread.Abort();
        }
    }

    private static float pulseTimer;

    internal static void DrawPrestarterInfo(Rect rect)
    {
        if (DataStore.loadingStage == null)
            return;

        rect.y += rect.height + 20;
        rect.height = 45f;
        rect = rect.ExpandedBy(10, 0);

        Widgets.DrawWindowBackground(rect);

        pulseTimer += Time.deltaTime;
        var pulse = 0.5f + 0.5f * Mathf.Sin(pulseTimer * 2 * Mathf.PI / 7f) * Mathf.Sin(pulseTimer * 2 * Mathf.PI / 7f);

        using (MpStyle.Set(TextAnchor.MiddleCenter))
            using (MpStyle.Set(new Color(pulse, pulse, pulse)))
                Widgets.Label(rect,
                    DataStore.openModManager ?
                    $"Waiting to open mod manager\n({DataStore.loadingStage})" :
                    $"Press Space to open mod manager\n({DataStore.loadingStage})");

        if (!DataStore.openModManager && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space)
            DataStore.openModManager = true;
    }

    private static void GetControlIDPostfix(ref int __result)
    {
        if (ModManager.nextControlCount-- > 0)
        {
            __result = ModManager.nextControlId + ModManager.nextControlCount;
        }
    }
}
