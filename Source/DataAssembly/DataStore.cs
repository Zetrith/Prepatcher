// This assembly doesn't have any dependencies (apart from system ones) so won't get reloaded
// and can be used to pass data across the reloading barrier

using System.Collections.Generic;
using System.Reflection;

namespace DataAssembly;

public static class DataStore
{
    public static bool startedOnce;

    // Patched assemblies by simple name. On Mono the originals are marked reflection-only
    // and so drop out of AppDomain.GetAssemblies(); CoreCLR has no such concept, so the
    // superseded copies stay visible and have to be filtered out by identity instead.
    public static readonly Dictionary<string, Assembly> PatchedAssemblies = new();

    public static volatile bool openModManager;
    public static volatile string? loadingStage = null;
}
