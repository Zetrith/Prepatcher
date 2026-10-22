using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using DataAssembly;
using HarmonyLib;
using LudeonTK;
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
        set.AddAssembly("RimWorld", AssemblyCollector.AssemblyCSharp, null, typeof(Game).Assembly);

        foreach (var (friendlyName, path) in AssemblyCollector.SystemAssemblyPaths())
        {
            var systemAsmName = AssemblyCollector.TryGetAssemblyName(path);
            if (systemAsmName == null || systemAsmName == AssemblyCollector.AssemblyCSharp)
                continue;

            var addedAsm = set.AddAssembly("System", friendlyName, path, null);
            addedAsm.AllowPatches = false;
        }

        foreach (var (modName, friendlyName, asm) in AssemblyCollector.ModAssemblies())
        {
            var name = asm.GetName().Name;

            // Don't add system assemblies packaged by mods
            if (set.HasAssembly(name)) continue;

            var addedAsm = set.AddAssembly(modName, friendlyName, null, asm);

            if (name.EndsWith("DataAssembly"))
                addedAsm.AllowPatches = false;
            else
                addedAsm.ProcessAttributes = true;
        }

        using (StopwatchScope.Measure("Game processing"))
            GameProcessing.Process(set);

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
                Application.logMessageReceivedThreaded -= Log.Notify_MessageReceivedThreadedInternal;
                UnregisterWorkshopCallbacks();
                ClearAssemblyResolve();
                ClearPendingDeferredMaterials();
            }
        );

        EngineRebindPatch.Apply(new Harmony("prepatcher.enginerebind"), origAsm, newAsm);
    }

    private static readonly HashSet<string> SharedAssemblies = new() { "0Harmony" };

    private static void LoadAssembly(ModifiableAssembly asm)
    {
        Lg.Verbose($"Loading assembly: {asm}");

        var asmName = asm.AsmDefinition.Name.Name;
        Assembly loadedAssembly;

        if (SharedAssemblies.Contains(asmName))
        {
            loadedAssembly = AppDomain.CurrentDomain.GetAssemblies().
                First(a => a.GetName().Name == asmName);
            PatchedAssemblyLoadContext.Instance.Share(asmName, loadedAssembly);
        }
        else
        {
            loadedAssembly = PatchedAssemblyLoadContext.Instance.LoadPatched(asm.Bytes!);
        }
        DataStore.PatchedAssemblies[loadedAssembly.GetName().Name!] = loadedAssembly;

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

    private static void ClearPendingDeferredMaterials()
    {
        var pendingField = AccessTools.Field(typeof(Material), "PendingDeferredMaterials");
        if (pendingField?.GetValue(null) is not IList pending)
            return;

        var resolveLock = AccessTools.Field(typeof(Material), "DeferredResolveLock")?.GetValue(null);
        if (resolveLock == null)
            return;

        lock (resolveLock)
            pending.Clear();
    }

    private static void ClearAssemblyResolve()
    {
        Lg.Verbose("Clearing AppDomain.AssemblyResolve");

        var asmResolve = AccessTools.Field(typeof(AppDomain), "AssemblyResolve");
        object? resolveTarget = AppDomain.CurrentDomain;

        if (asmResolve == null)
        {
            asmResolve = AccessTools.Field(typeof(AssemblyLoadContext), "AssemblyResolve");
            resolveTarget = null;
        }

        if (asmResolve == null)
        {
            Lg.Error("Couldn't find the AssemblyResolve backing field");
            return;
        }

        var del = (Delegate?)asmResolve.GetValue(resolveTarget);
        if (del == null)
            return;

        // Handle MonoMod's internal dynamic assemblies
        foreach (var d in del.GetInvocationList().ToList())
        {
            if (d!.Method.DeclaringType?.Namespace?.StartsWith("MonoMod.Utils") == true)
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

        asmResolve.SetValue(resolveTarget, null);
    }
}
