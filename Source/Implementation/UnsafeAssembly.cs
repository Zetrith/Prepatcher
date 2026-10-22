using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Verse;

namespace Prepatcher;

internal class UnsafeAssembly
{
    private static readonly FieldInfo? MonoAssemblyField = AccessTools.Field(AccessTools.TypeByName("System.Reflection.RuntimeAssembly"), "_mono_assembly");

    internal static bool IsMonoRuntime => MonoAssemblyField != null;

    private static List<Assembly> refOnly = new();

    // Loading two assemblies with the same name and version isn't possible with Unity's Mono.
    // It IS possible in .Net and has been fixed in more recent versions of Mono
    // but the change hasn't been backported by Unity yet.

    // This sets the assembly-to-be-duplicated as ReflectionOnly to
    // make sure it is skipped by the internal assembly searcher during loading.
    // That allows for the duplication to happen.
    internal static unsafe void SetReflectionOnly(Assembly asm, bool value)
    {
        // Silently skip on non-Mono runtimes
        if (MonoAssemblyField == null)
            return;

        if (asm == null)
            throw new NullReferenceException("Settings refonly on a null assembly");

        *(int*)((IntPtr)MonoAssemblyField.GetValue(asm) + 0x74) = value ? 1 : 0;
        if (value)
            refOnly.Add(asm);
    }

    // Used for error recovery
    internal static void UnsetRefonlys()
    {
        foreach (var asm in refOnly.ToList())
            SetReflectionOnly(asm, false);
    }

    internal static unsafe byte[] GetRawData(Assembly asm)
    {
        if (MonoAssemblyField == null)
            return GetRawDataFromDisk(asm);

        var image = *(long*)((IntPtr)MonoAssemblyField.GetValue(asm) + 0x60);
        var rawData = *(long*)((IntPtr)image + 0x10);
        var rawDataLength = *(uint*)((IntPtr)image + 0x18);

        var arr = new byte[rawDataLength];
        Marshal.Copy((IntPtr)rawData, arr, 0, (int)rawDataLength);

        return arr;
    }

    private static byte[] GetRawDataFromDisk(Assembly asm)
    {
        var path = FindAssemblyFile(asm);
        if (path == null)
            throw new Exception($"Couldn't find a file on disk for assembly {asm.GetName().Name}");

        return File.ReadAllBytes(path);
    }

    private static string? FindAssemblyFile(Assembly asm)
    {
        if (!string.IsNullOrEmpty(asm.Location) && File.Exists(asm.Location))
            return asm.Location;

        var name = asm.GetName().Name;
        var mod = LoadedModManager.RunningModsListForReading.
            FirstOrDefault(m => m.assemblies.loadedAssemblies.Contains(asm));
        if (mod == null)
            return null;

        foreach (var folder in mod.foldersToLoadDescendingOrder)
        {
            var candidate = Path.Combine(folder, "Assemblies", name + ".dll");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
