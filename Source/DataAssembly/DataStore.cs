// This assembly doesn't have any dependencies (apart from system ones) so won't get reloaded
// and can be used to pass data across the reloading barrier

using System.Collections.Generic;
using System.Reflection;

namespace DataAssembly;

public static class DataStore
{
    public static bool startedOnce;

    // Patched assemblies by name. Mono drops the reflection-only originals out of
    // AppDomain.GetAssemblies(); off Mono they stay visible and have to be skipped by hand.
    public static readonly Dictionary<string, Assembly> PatchedAssemblies = new();

    public static volatile bool openModManager;
    public static volatile string? loadingStage = null;
}
