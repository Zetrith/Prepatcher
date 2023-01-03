using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;

namespace Prepatcher;

internal class UnsafeAssembly
{
    private static readonly FieldInfo? MonoAssemblyField = AccessTools.Field(typeof(Assembly), "_mono_assembly");

    // Loading two assemblies with the same name and version isn't possible with Unity's Mono.
    // It IS possible in .Net and has been fixed in more recent versions of Mono
    // but the change hasn't been backported by Unity yet.

    // This sets the assembly-to-be-duplicated as ReflectionOnly to
    // make sure it is skipped by the internal assembly searcher during loading.
    // That allows for the duplication to happen.
    internal static unsafe void SetReflectionOnly(Assembly asm, bool value)
    {
        if (MonoAssemblyField != null)
            *(int*)((IntPtr)MonoAssemblyField.GetValue(asm) + 0x74) = value ? 1 : 0;
    }

    internal static unsafe byte[] GetRawData(Assembly asm)
    {
        if (MonoAssemblyField == null)
            throw new Exception("Not available on non-Mono runtime");

        var image = *(long*)((IntPtr)MonoAssemblyField.GetValue(asm) + 0x60);
        var rawData = *(long*)((IntPtr)image + 0x10);
        var rawDataLength = *(uint*)((IntPtr)image + 0x18);

        var arr = new byte[rawDataLength];
        Marshal.Copy((IntPtr)rawData, arr, 0, (int)rawDataLength);

        return arr;
    }
}
