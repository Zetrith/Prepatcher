using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Prepatcher.Process;
using Verse;
using Verse.Steam;

namespace Prepatcher;

internal static class Loader
{
    internal static Assembly origAsm;
    internal static Assembly newAsm;
    internal static volatile bool doneLoading;

    internal static void DoLoad()
    {
        origAsm = typeof(Game).Assembly;

        HarmonyPatches.DoHarmonyPatches();
        UnregisterWorkshopCallbacks();
        ClearAssemblyResolve();

        var processor = new GameAssemblyProcessor();
        processor.Init();
        processor.Process();
        processor.Reload();

        Lg.Info("Done loading");
        doneLoading = true;
    }

    private static void UnregisterWorkshopCallbacks()
    {
        Workshop.subscribedCallback?.Unregister();
        Workshop.unsubscribedCallback?.Unregister();
        Workshop.installedCallback?.Unregister();
    }

    private static void ClearAssemblyResolve()
    {
        var asmResolve = AccessTools.Field(typeof(AppDomain), "AssemblyResolve");
        var del = (Delegate)asmResolve.GetValue(AppDomain.CurrentDomain);

        // Handle MonoMod's internal dynamic assemblies
        foreach (var d in del.GetInvocationList().ToList())
        {
            if (d.Method.DeclaringType.Namespace.StartsWith("MonoMod.Utils"))
            {
                foreach (var f in AccessTools.GetDeclaredFields(d.Method.DeclaringType))
                {
                    if (f.FieldType == typeof(Assembly))
                    {
                        var da = (Assembly)f.GetValue(d.Target);
                        UnsafeAssembly.SetReflectionOnly(da, true);
                    }
                }
            }
        }

        asmResolve.SetValue(AppDomain.CurrentDomain, null);
    }
}
