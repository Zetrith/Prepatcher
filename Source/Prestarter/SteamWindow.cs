using System;
using System.Collections.Generic;
using System.IO;
using Steamworks;
using UnityEngine;
using Verse;

namespace Prestarter;

public class SteamWindow : Window
{
    private Dictionary<string, long> mods = new();
    private ModManager manager;

    public override Vector2 InitialSize => new(1100f, 600f);

    public SteamWindow(ModManager manager)
    {
        this.manager = manager;

        var list = new List<ModCompatibility>();

        foreach (var s in File.ReadAllText("mpmods.json").Split(new[]{"},"}, StringSplitOptions.RemoveEmptyEntries))
            list.Add(JsonUtility.FromJson<ModCompatibility>(s + "}"));

        foreach (var compat in list)
            mods[compat.name] = compat.workshopId;

        ModLists.Load();
    }

    private Vector2 scroll;
    private CallResult<SteamUGCQueryCompleted_t>? queryCallback;
    private Dictionary<ulong, ulong> subscribers = new();

    public override void DoWindowContents(Rect inRect)
    {
        Layouter.BeginArea(inRect with { width = inRect.width - 20});

        if (Layouter.Button("Download mods", 100) && ModLists.Lists != null)
        {
            for (var i = 0; i < ModLists.Lists[0].List.ids.Count; i++)
            {
                var name = ModLists.Lists[0].List.names[i];
                if (mods.TryGetValue(name, out var workshopId))
                {
                    SteamUGC.SubscribeItem(new PublishedFileId_t((ulong)workshopId));
                    SteamUGC.DownloadItem(new PublishedFileId_t((ulong)workshopId), false);
                }
            }
        }

        Layouter.BeginScroll(ref scroll);
        Layouter.BeginHorizontal();
        if (ModLists.Lists != null)
        {
            if (queryCallback == null)
                RequestWorkshopInfo();

            Layouter.BeginVertical(stretch: false);
            for (var i = 0; i < ModLists.Lists[0].List.ids.Count; i++)
            {
                var id = ModLists.Lists[0].List.ids[i];
                var name = ModLists.Lists[0].List.names[i];

                Layouter.Label(id);
            }
            Layouter.EndVertical();

            Layouter.BeginVertical(stretch: false);
            for (var i = 0; i < ModLists.Lists[0].List.ids.Count; i++)
            {
                var id = ModLists.Lists[0].List.ids[i];
                var name = ModLists.Lists[0].List.names[i];

                Layouter.Label(name);
            }
            Layouter.EndVertical();

            Layouter.BeginVertical(stretch: false);
            for (var i = 0; i < ModLists.Lists[0].List.ids.Count; i++)
            {
                var id = ModLists.Lists[0].List.ids[i];
                var name = ModLists.Lists[0].List.names[i];

                Widgets.Label(
                    Layouter.Rect(140, 22),
                    mods.TryGetValue(name, out var workshopId) ?
                        workshopId.ToString() + (subscribers.TryGetValue((ulong)workshopId, out var subs) ? $"/{subs}" : "") : "X");
            }
            Layouter.EndVertical();
        }
        Layouter.EndHorizontal();
        Layouter.EndScroll();
        Layouter.EndArea();
    }

    private void RequestWorkshopInfo()
    {
        var queryList = new List<PublishedFileId_t>();

        for (var i = 0; i < ModLists.Lists[0].List.ids.Count; i++)
        {
            var name = ModLists.Lists[0].List.names[i];
            if (mods.TryGetValue(name, out var workshopId))
                queryList.Add(new PublishedFileId_t((ulong)workshopId));
        }

        var query = SteamUGC.CreateQueryUGCDetailsRequest(queryList.ToArray(), (uint)queryList.Count);
        var call = SteamUGC.SendQueryUGCRequest(query);

        queryCallback = CallResult<SteamUGCQueryCompleted_t>.Create((result, fail) =>
        {
            Log.Message("Query completed");
            if (result.m_handle == query)
            {
                uint i = 0;
                while (true)
                {
                    if (!SteamUGC.GetQueryUGCResult(query, i, out var details))
                        break;

                    SteamUGC.GetQueryUGCStatistic(
                        query, i, EItemStatistic.k_EItemStatistic_NumSubscriptions,
                        out var subs);
                    subscribers[(ulong)details.m_nPublishedFileId] = subs;

                    i++;
                }
            }
        });

        queryCallback.Set(call);
    }
}

[Serializable]
public struct ModCompatibility
{
    public string name;
    public long workshopId;
    public int status;
    public string notes;
}
