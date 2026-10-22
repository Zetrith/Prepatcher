using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Prepatcher;

internal static class EngineRebindPatch
{
    private static readonly string[] Targets =
    {
        "RimWorldWaylith.LongEventState:get_EventWithoutStandardWindowRunning",
        "RimWorldWaylith.Rendering.MapRenderFeature:CaptureFrame",
        "RimWorldWaylith.Rendering.WorldRenderFeature:CaptureFrame",
        "RimWorldWaylith.Rendering.WorldRenderFeature:CollectCameras",
        "RimWorldWaylith.Rendering.WorldRenderFeature:ResolveSunDirection",
        "RimWorldWaylith.Rendering.CameraCaptureFeature:ResolveSunDirection"
    };

    private static Assembly patched = null!;
    private static Assembly original = null!;

    internal static void Apply(Harmony harmony, Assembly originalAsm, Assembly patchedAsm)
    {
        original = originalAsm;
        patched = patchedAsm;

        foreach (var target in Targets)
        {
            var split = target.Split(':');
            var type = AccessTools.TypeByName(split[0]);
            var method = type == null ? null : AccessTools.Method(type, split[1]);

            if (method == null)
            {
                Lg.Error($"Couldn't find {target} to rebind");
                continue;
            }

            harmony.Patch(method, transpiler: new HarmonyMethod(typeof(EngineRebindPatch), nameof(Rebind)));
            Lg.Verbose($"Rebound {target}");
        }
    }

    private static IEnumerable<CodeInstruction> Rebind(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var inst in instructions)
        {
            if (inst.operand is MethodInfo m && m.DeclaringType?.Assembly == original)
            {
                var moved = Translate(m);
                if (moved != null)
                    inst.operand = moved;
            }
            else if (inst.operand is FieldInfo f && f.DeclaringType?.Assembly == original)
            {
                var moved = Translate(f);
                if (moved != null)
                    inst.operand = moved;
            }

            yield return inst;
        }
    }

    private static MethodInfo? Translate(MethodInfo method)
    {
        var type = patched.GetType(method.DeclaringType!.FullName!);
        if (type == null)
            return null;

        var moved = AccessTools.Method(type, method.Name,
            method.GetParameters().Select(p => p.ParameterType).ToArray());

        if (moved == null)
            Lg.Error($"No match in the patched assembly for {method.DeclaringType.FullName}.{method.Name}");

        return moved;
    }

    private static FieldInfo? Translate(FieldInfo field)
    {
        var type = patched.GetType(field.DeclaringType!.FullName!);
        if (type == null)
            return null;

        var moved = AccessTools.Field(type, field.Name);
        if (moved == null)
            Lg.Error($"No match in the patched assembly for {field.DeclaringType.FullName}.{field.Name}");

        return moved;
    }
}
