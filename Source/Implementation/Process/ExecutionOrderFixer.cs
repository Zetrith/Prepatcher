using Mono.Cecil;
using RimWorld;
using RimWorld.Planet;
using RuntimeAudioClipLoader;
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
            (typeof(Manager), -70),
            (typeof(BlackScreenFixer), -100),
            (typeof(CameraDriver), -60),
            (typeof(Root), -30),
            (typeof(Root_Entry), -29),
            (typeof(Root_Play), -28)
        };

        var attrCtor = asmCSharp.ImportReference(
            typeof(DefaultExecutionOrder).GetConstructor(new[] { typeof(int) })
        );

        foreach (var (t, order) in vanillaOrder)
        {
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
