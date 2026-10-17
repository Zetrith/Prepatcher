using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Prepatcher.Process;
using RimWorld;
using Verse;

namespace Prepatcher;

// A loaded assembly can't be rewritten in place, and loading a patched copy alongside it
// doesn't work on 1.7: the engine reads the map and camera out of Assembly-CSharp and binds to
// the original before any mod runs, so it carries on drawing the world we replaced. Writing the
// assembly out and restarting sidesteps that. One assembly, with the fields already in it.
internal static class OnDisk
{
    private const string CacheFolder = "PrepatcherCache";
    private const string StampFile = "stamp.txt";
    private const string PendingFile = "pending";
    private const string StockSuffix = ".stock";

    internal static string CachePath => Path.Combine(GenFilePaths.ConfigFolderPath, CacheFolder);

    private static string GameAssemblyPath =>
        Path.Combine(Path.GetDirectoryName(typeof(Game).Assembly.Location)!,
            AssemblyCollector.AssemblyCSharp + ".dll");

    // What the patched assemblies were built from. Anything here changing means they're stale.
    private static string CurrentStamp()
    {
        var sb = new StringBuilder();
        sb.AppendLine(VersionControl.CurrentVersionStringWithRev);

        foreach (var mod in LoadedModManager.RunningModsListForReading)
        {
            sb.AppendLine(mod.PackageId);
            foreach (var asm in mod.assemblies.loadedAssemblies.OrderBy(a => a.GetName().Name))
            {
                sb.Append(asm.GetName().Name);
                var file = AssemblyFile(mod, asm);
                sb.AppendLine(file == null ? "?" : File.GetLastWriteTimeUtc(file).Ticks.ToString());
            }
        }

        return sb.ToString();
    }

    private static string? AssemblyFile(ModContentPack mod, Assembly asm)
    {
        var name = asm.GetName().Name;
        foreach (var folder in mod.foldersToLoadDescendingOrder)
        {
            var candidate = Path.Combine(folder, "Assemblies", name + ".dll");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    internal static bool UpToDate()
    {
        var stamp = Path.Combine(CachePath, StampFile);
        return File.Exists(stamp) && File.ReadAllText(stamp) == CurrentStamp();
    }

    // Written when we install and cleared on the next start. It only tells us an install
    // happened; reaching Prepatcher at all means the assemblies themselves loaded fine.
    internal static bool InstallPending => File.Exists(Path.Combine(CachePath, PendingFile));

    internal static void MarkStarted()
    {
        var pending = Path.Combine(CachePath, PendingFile);
        if (File.Exists(pending))
            File.Delete(pending);
    }

    internal static void Write(AssemblySet set)
    {
        Directory.CreateDirectory(CachePath);

        foreach (var file in Directory.GetFiles(CachePath, "*.dll"))
            File.Delete(file);

        foreach (var asm in set.AllAssemblies.Where(a => a.Modified))
        {
            asm.SerializeToByteArray();
            File.WriteAllBytes(Path.Combine(CachePath, asm.AsmDefinition.Name.Name + ".dll"), asm.Bytes!);
            Lg.Verbose($"Cached patched {asm.AsmDefinition.Name.Name}");
        }
    }

    // Returns the mod assemblies that were patched, which still live beside the mods themselves
    internal static List<string> Install()
    {
        var installed = new List<string>();

        foreach (var cached in Directory.GetFiles(CachePath, "*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(cached);
            var target = name == AssemblyCollector.AssemblyCSharp
                ? GameAssemblyPath
                : TargetForModAssembly(name);

            if (target == null)
            {
                Lg.Error($"Couldn't find where {name} is loaded from, leaving it unpatched");
                continue;
            }

            if (!File.Exists(target + StockSuffix))
                File.Copy(target, target + StockSuffix);

            Replace(cached, target);
            installed.Add(name);
        }

        File.WriteAllText(Path.Combine(CachePath, StampFile), CurrentStamp());
        File.WriteAllText(Path.Combine(CachePath, PendingFile), "");

        return installed;
    }


    // The game has these files mapped while it runs, and writing over one corrupts the copy
    // the process is using. Renaming a new file into place leaves the running process on the
    // old one and gives the next launch the new one.
    private static void Replace(string source, string target)
    {
        var staged = target + ".new";
        File.Copy(source, staged, true);
        File.Move(staged, target, true);
    }

    private static string? TargetForModAssembly(string name)
    {
        foreach (var mod in LoadedModManager.RunningModsListForReading)
        foreach (var asm in mod.assemblies.loadedAssemblies)
            if (asm.GetName().Name == name)
                return AssemblyFile(mod, asm);

        return null;
    }

    internal static void Restore()
    {
        RestoreOne(GameAssemblyPath);

        foreach (var mod in LoadedModManager.RunningModsListForReading)
        foreach (var asm in mod.assemblies.loadedAssemblies)
        {
            var file = AssemblyFile(mod, asm);
            if (file != null)
                RestoreOne(file);
        }

        var stamp = Path.Combine(CachePath, StampFile);
        if (File.Exists(stamp))
            File.Delete(stamp);
    }

    private static void RestoreOne(string target)
    {
        if (!File.Exists(target + StockSuffix))
            return;

        Replace(target + StockSuffix, target);
        File.Delete(target + StockSuffix);
        Lg.Info($"Restored {Path.GetFileName(target)}");
    }
}
