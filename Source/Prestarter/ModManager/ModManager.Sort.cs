using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Prestarter;

public partial class ModManager
{
    private void TrySortMods()
    {
        var directedAcyclicGraph = new DirectedAcyclicGraph(active.Count);

        for (var i = 0; i < active.Count; i++)
        {
            var modMetaData = ModData(active[i]);
            if (modMetaData == null) continue;

            foreach (var before in modMetaData.LoadBefore.Concat(modMetaData.ForceLoadBefore))
            {
                var beforeData = active.FirstOrDefault(m => ModData(m) is { } other && other.SamePackageId(before, ignorePostfix: true));
                if (beforeData != null)
                    directedAcyclicGraph.AddEdge(active.IndexOf(beforeData), i);
            }

            foreach (string after in modMetaData.LoadAfter.Concat(modMetaData.ForceLoadAfter))
            {
                var afterData = active.FirstOrDefault(m => ModData(m) is { } other && other.SamePackageId(after, ignorePostfix: true));
                if (afterData != null)
                    directedAcyclicGraph.AddEdge(i, active.IndexOf(afterData));
            }
        }

        var num = directedAcyclicGraph.FindCycle();
        if (num != -1)
        {
            Find.WindowStack.Add(new Dialog_MessageBox("ModCyclicDependency".Translate(ModData(active[num])!.Name)));
            return;
        }

        PushUndo();

        var newActive = new List<string>();
        foreach (int newIndex in directedAcyclicGraph.TopologicalSort())
            newActive.Add(active[newIndex]);

        active = new UniqueList<string>(newActive);

        RecacheLists();
    }
}
