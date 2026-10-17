using Mono.Cecil;
using MonoMod.Utils;
using RimWorld.Planet;
using UnityEngine;

namespace Prepatcher;

public static class WorldCameraFreePatch
{
    // [FreePatch]  // disabled: on-disk assembly must not reference PrepatcherImpl
    static void ReplaceAddComponent(ModuleDefinition module)
    {
        var type = module.GetType($"RimWorld.Planet.{nameof(WorldCameraManager)}");
        var method = type.FindMethod(nameof(WorldCameraManager.CreateWorldCamera));

        // For some reason, AddComponent treats WorldCameraDriver by name causing it to be loaded from old Assembly-CSharp
        foreach (var inst in method.Body.Instructions)
        {
            if (inst.Operand is MethodReference { Name: nameof(GameObject.AddComponent) })
                inst.Operand = module.ImportReference(typeof(WorldCameraFreePatch).GetMethod(nameof(AddComponent)));
        }
    }

    public static Component AddComponent(GameObject gameObject)
    {
        return gameObject.AddComponent<WorldCameraDriver2>();
    }
}
