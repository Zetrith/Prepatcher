using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using RimWorld;
using UnityEngine;
using UnityEngine.Networking;
using Verse;

namespace Prestarter;

public partial class ModManager
{
    private IEnumerator PasteModsCoroutine(string text)
    {
        foreach (var error in PasteMods(text))
        {
            if (error is string errMsg)
                Messages.Message($"Pasting failed: {errMsg}", MessageTypeDefOf.SilentInput);
            else if (error == null)
                Messages.Message($"Pasted successfully.", MessageTypeDefOf.SilentInput);

            yield return error;
        }
    }

    // Non-null string represents an error
    private IEnumerable PasteMods(string text)
    {
        if (text.Contains("<activeMods>"))
        {
            yield return HandleXmlList(text);
            yield break;
        }

        if (text.Contains("!!! note Mod list length"))
        {
            yield return HandleMarkdownList(text);
            yield break;
        }

        if (Regex.IsMatch(text, @"^https:\/\/rentry\.co\/\w{5}$"))
        {
            var req = UnityWebRequest.Get($"{text}/raw");
            yield return req.SendWebRequest();

            if (req.error != null)
            {
                yield return "Error making request";
                yield break;
            }

            yield return HandleMarkdownList(req.downloadHandler.text);
            yield break;
        }

        yield return "Unknown format";
    }

    private string? HandleXmlList(string list)
    {
        try
        {
            var mods =
                XDocument.Parse(list).
                    Element("ModsConfigData")!.
                    Element("activeMods")!.
                    Elements().
                    Select(m => m.Value);

            SetActive(mods.ToList());
        }
        catch (Exception e)
        {
            Log.Warning($"Exception when pasting from ModsConfig.xml: {e}");
            return "Parsing ModsConfig.xml failed";
        }

        return null;
    }

    private string? HandleMarkdownList(string list)
    {
        try
        {
            var mods =
                Regex.Matches(list, @"packageId: (.*?\..*?)[})]", RegexOptions.Multiline).
                    Cast<Match>().
                    Select(m => m.Groups[1].Value);

            SetActive(mods.ToList());
        }
        catch (Exception e)
        {
            Log.Warning($"Exception when pasting from markdown: {e}");
            return "Parsing Markdown failed";
        }

        return null;
    }

    private class ModsConfigData
    {
        public string? version;
        public List<string> activeMods = new List<string>();
    }

    private void CopyMods()
    {
        XDocument xDocument = new XDocument();
        XElement content = DirectXmlSaver.XElementFromObject(new ModsConfigData()
        {
            version = VersionControl.CurrentVersionStringWithRev,
            activeMods = active.ToList()
        }, typeof(ModsConfigData));
        xDocument.Add(content);

        GUIUtility.systemCopyBuffer = xDocument.ToString();
    }
}
