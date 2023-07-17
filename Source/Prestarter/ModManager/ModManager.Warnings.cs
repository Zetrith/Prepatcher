using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace Prestarter;

public partial class ModManager
{
    private Dictionary<string, string> GetModWarnings(UniqueList<string> activeMods)
    {
	    Dictionary<string, string> result = new Dictionary<string, string>();
	    for (int i = 0; i < activeMods.Count; i++)
	    {
		    int index = i;
		    var modMetaData = ModData(activeMods[index]);
            if (modMetaData == null) continue;

		    var warningBuilder = new StringBuilder("");

            var incompatible = FindConflicts(modMetaData.IncompatibleWith, null);
		    if (incompatible.Any())
                warningBuilder.AppendLine("ModIncompatibleWithTip".Translate(incompatible.ToCommaList(useAnd: true)));

            var loadBefore = FindConflicts(modMetaData.LoadBefore, (beforeMod) => activeMods.IndexOf(beforeMod) < index);
		    if (loadBefore.Any())
                warningBuilder.AppendLine("ModMustLoadBefore".Translate(loadBefore.ToCommaList(useAnd: true)));

            var forceLoadBefore = FindConflicts(modMetaData.ForceLoadBefore, (beforeMod) => activeMods.IndexOf(beforeMod) < index);
		    if (forceLoadBefore.Any())
                warningBuilder.AppendLine("ModMustLoadBefore".Translate(forceLoadBefore.ToCommaList(useAnd: true)));

            var loadAfter = FindConflicts(modMetaData.LoadAfter, (afterMod) => activeMods.IndexOf(afterMod) > index);
		    if (loadAfter.Any())
                warningBuilder.AppendLine("ModMustLoadAfter".Translate(loadAfter.ToCommaList(useAnd: true)));

            var forceLoadAfter = FindConflicts(modMetaData.ForceLoadAfter, (afterMod) => activeMods.IndexOf(afterMod) > index);
		    if (forceLoadAfter.Any())
                warningBuilder.AppendLine("ModMustLoadAfter".Translate(forceLoadAfter.ToCommaList(useAnd: true)));

            if (modMetaData.Dependencies.Any())
		    {
			    var missingDeps = UnsatisfiedDependencies(modMetaData);
			    if (missingDeps.Any())
                    warningBuilder.AppendLine("ModUnsatisfiedDependency".Translate(missingDeps.ToCommaList(useAnd: true)));
            }

            var warningString = warningBuilder.ToString().TrimEndNewlines();
            if (!warningString.NullOrEmpty())
		        result.Add(modMetaData.PackageId, warningString);
	    }

	    return result;
    }

    private List<string> FindConflicts(List<string> modsToCheck, Func<string, bool>? predicate)
    {
        var list = new List<string>();
        foreach (var item in modsToCheck)
        {
            var modIdLowercase = item.ToLowerInvariant();
            if (ModIsActiveNoPostfix(modIdLowercase) && predicate == null ||
                predicate != null &&
                (active.Contains(modIdLowercase) && predicate(modIdLowercase) ||
                 active.Contains(modIdLowercase + ModMetaData.SteamModPostfix) && predicate(modIdLowercase + ModMetaData.SteamModPostfix)))
            {
                list.Add(ModData(modIdLowercase)?.Name ?? modIdLowercase);
            }
        }
        return list;
    }

    private List<string> UnsatisfiedDependencies(ModMetaData modData)
    {
        bool IsSatisfied(ModDependency dep)
        {
            return ModIsActiveNoPostfix(dep.packageId) ||
                   dep.packageId.ToLowerInvariant() == "brrainz.harmony" &&
                   ModIsActiveNoPostfix("zetrith.prepatcher");
        }

        return modData.Dependencies
            .Where(dep => !IsSatisfied(dep))
            .Select(dep => dep.displayName)
            .ToList();
    }
}
