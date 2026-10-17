using Mono.Cecil;
using MonoMod.Utils;
using RimWorld.Planet;
using UnityEngine;

namespace Prepatcher;

// Only needed for the in-process reload: AddComponent resolves WorldCameraDriver by
// name and picks up the old assembly. An on-disk assembly must not call back into
// PrepatcherImpl, which isn't loaded yet when mod content loads.
public static class WorldCameraFreePatch
{
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
