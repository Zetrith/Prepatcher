using HarmonyLib;
using Mono.Cecil;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Prepatcher.Process;

internal static class ExecutionOrderFixer
{
    internal static void ApplyExecutionOrderAttributes(ModuleDefinition asmCSharp)
    {
        // Extracted using AssetRipper
        // These are the only scripts with non-zero executionOrder in .cs.meta
        var vanillaOrder = new[]
        {
            (typeof(LatestVersionGetter), -80),
            (typeof(WorldCameraDriver), -50),
            (typeof(BlackScreenFixer), -100),
            (typeof(CameraDriver), -60),
            (typeof(Root), -30),
            (typeof(Root_Entry), -29),
            (typeof(Root_Play), -28),
            (AccessTools.TypeByName("RuntimeAudioClipLoader.Manager") ?? null, -70)
        };

        var attrCtor = asmCSharp.ImportReference(
            typeof(DefaultExecutionOrder).GetConstructor(new[] { typeof(int) })
        );

        foreach (var (t, order) in vanillaOrder)
        {
            if (t == null) continue;

            var resolved = asmCSharp.ImportReference(t).Resolve();
            var attr = new CustomAttribute(attrCtor)
            {
                ConstructorArguments =
                {
                    new CustomAttributeArgument(
                        asmCSharp.TypeSystem.Int32,
                        order
                    )
                }
            };
            resolved.CustomAttributes.Add(attr);
        }
    }
}
