using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Prepatcher.Process;
using Prestarter;
using UnityEngine;
using Verse;
using Verse.Steam;

namespace Prepatcher;

internal static class Loader
{
    internal static Assembly origAsm;
    internal static Assembly newAsm;
    internal static volatile bool restartGame;

    internal static void Reload()
    {
        HarmonyPatches.holdLoading = true;

        try
        {
            Lg.Verbose("Reloading the game");
            DoReload();

            if (GenCommandLine.CommandLineArgPassed("patchandexit"))
                Application.Quit();

            Lg.Info("Done loading");
            restartGame = true;
        }
        catch (Exception e)
        {
            Lg.Error($"Fatal error while reloading: {e}");
        }

        if (restartGame) return;

        UnsafeAssembly.UnsetRefonlys();
        Find.Root.StartCoroutine(MinimalInit.DoInit());
        Find.Root.StartCoroutine(ShowLogConsole());
    }

    private static void DoReload()
    {
        origAsm = typeof(Game).Assembly;

        var set = new AssemblySet();
        var modAsms = new List<Assembly>();
        var asmCSharp =
            set.AddAssembly(AssemblyCollector.AssemblyCSharp, null, typeof(Game).Assembly);

        AssemblyCollector.CollectSystem((friendlyName, path) =>
        {
            if (AssemblyName.GetAssemblyName(path).Name == AssemblyCollector.AssemblyCSharp)
                return;

            var addedAsm = set.AddAssembly(friendlyName, path, null);
            addedAsm.AllowPatches = false;
        });

        AssemblyCollector.CollectMods((friendlyName, asm) =>
        {
            var name = asm.GetName().Name;

            // Don't add system assemblies packaged by mods
            if (set.HasAssembly(name)) return;

            var addedAsm = set.AddAssembly(friendlyName, null, asm);
            addedAsm.ProcessAttributes = true;

            modAsms.Add(asm);
        });

        using (StopwatchScope.Measure("Game processing"))
            GameProcessing.Process(set, asmCSharp, modAsms);

        // Reload the assemblies
        Reloader.Reload(
            set,
            LoadAssembly,
            () =>
            {
                HarmonyPatches.SetLoadingStage("Serializing assemblies"); // Point where the mod manager can get opened
            },
            () =>
            {
                HarmonyPatches.SetLoadingStage("Reloading game"); // Point where the mod manager can get opened

                HarmonyPatches.PatchRootMethods();
                UnregisterWorkshopCallbacks();
                ClearAssemblyResolve();
            }
        );
    }

    private static void LoadAssembly(ModifiableAssembly asm)
    {
        Lg.Verbose($"Loading assembly: {asm}");

        var loadedAssembly = Assembly.Load(asm.Bytes);
        if (loadedAssembly.GetName().Name == AssemblyCollector.AssemblyCSharp)
        {
            newAsm = loadedAssembly;
            AppDomain.CurrentDomain.AssemblyResolve += (_, _) => loadedAssembly;
        }

        if (GenCommandLine.TryGetCommandLineArg("dumpasms", out var path) && !path.Trim().NullOrEmpty())
        {
            Directory.CreateDirectory(path);
            if (asm.Modified)
                File.WriteAllBytes(Path.Combine(path, asm.AsmDefinition.Name.Name + ".dll"), asm.Bytes!);
        }
    }

    private static IEnumerator ShowLogConsole()
    {
        yield return null;

        LongEventHandler.currentEvent = null;
        Find.WindowStack.Add(new EditWindow_Log { doCloseX = false });
        UIRoot_Prestarter.showManager = false;
    }

    private static void UnregisterWorkshopCallbacks()
    {
        Lg.Verbose("Unregistering workshop callbacks");

        // These hold references to old code and would get called externally by Steam
        Workshop.subscribedCallback?.Unregister();
        Workshop.unsubscribedCallback?.Unregister();
        Workshop.installedCallback?.Unregister();
    }

    private static void ClearAssemblyResolve()
    {
        Lg.Verbose("Clearing AppDomain.AssemblyResolve");

        var asmResolve = AccessTools.Field(typeof(AppDomain), "AssemblyResolve");
        var del = (Delegate)asmResolve.GetValue(AppDomain.CurrentDomain);

        // Handle MonoMod's internal dynamic assemblies
        foreach (var d in del.GetInvocationList().ToList())
        {
            if (d!.Method.DeclaringType!.Namespace!.StartsWith("MonoMod.Utils"))
            {
                foreach (var f in AccessTools.GetDeclaredFields(d.Method.DeclaringType))
                {
                    if (f.FieldType == typeof(Assembly))
                    {
                        var da = (Assembly)f.GetValue(d.Target);
                        Reloader.setRefonly.Add(da);
                    }
                }
            }
        }

        asmResolve.SetValue(AppDomain.CurrentDomain, null);
    }
}
