using System.Linq;
using Verse;

namespace Prestarter;

public partial class ModManager
{
    private void Launch()
    {
        LongEventHandler.QueueLongEvent(() =>
        {
            ModsConfig.SetActiveToList(active.ToList());
            ModsConfig.Save();
            PrestarterInit.DoLoad();
        }, "", true, null, false);
    }
}
