namespace ParadoxModTool.Desync;

/// <summary>Bestimmt, wo eine Skriptstelle läuft: auf allen Rechnern gleich (synchron), nur lokal oder nicht bestimmbar.</summary>
public static class ScriptContext
{
    /// <summary>Ordnerpfad der Datei für den Abgleich: klein, mit <c>/</c>; das erste Segment entfällt, wenn es in <see cref="SafetyRules.RootFolders"/> steht.</summary>
    public static string FolderPath(SafetyRules rules, string relativePath)
    {
        var segments = relativePath.Replace('\\', '/').ToLowerInvariant()
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        var skip = segments.Length > 0 && rules.RootFolders.Contains(segments[0]) ? 1 : 0;
        return string.Join('/', segments.Skip(skip));
    }

    /// <summary>
    /// Kontext einer Stelle. <paramref name="ancestorKeys"/> sind die Schlüssel der umgebenden Blöcke, der äußerste zuerst.
    /// Lokale Ordner gelten vor lokalen Blöcken; sonst sind <c>common</c>, <c>events</c> und <c>setup</c> synchron, alles andere unbekannt.
    /// </summary>
    public static SafetyContext Classify(SafetyRules rules, string relativePath, IReadOnlyList<string> ancestorKeys)
    {
        var folderPath = FolderPath(rules, relativePath);

        if (rules.LocalFolders.Any(folder => IsUnder(folderPath, folder)))
        {
            return SafetyContext.Local;
        }

        foreach (var block in rules.LocalBlocks)
        {
            if (IsUnder(folderPath, block.Folder) && IsLocalBlock(block, ancestorKeys))
            {
                return SafetyContext.Local;
            }
        }

        var first = folderPath.Split('/')[0];
        return first is "common" or "events" or "setup" ? SafetyContext.Synced : SafetyContext.Unknown;
    }

    /// <summary><c>*</c> macht die ganze Datei lokal; sonst entscheidet der nächste umgebende Schlüssel der Liste.</summary>
    private static bool IsLocalBlock(LocalBlock block, IReadOnlyList<string> ancestorKeys)
    {
        if (block.Keys.Contains("*"))
        {
            return true;
        }

        for (var i = ancestorKeys.Count - 1; i >= 0; i--)
        {
            if (block.Keys.Contains(ancestorKeys[i].ToLowerInvariant()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Der Pfad liegt im Ordner oder ist er selbst; verglichen wird in ganzen Segmenten.</summary>
    private static bool IsUnder(string folderPath, string folder) =>
        folderPath == folder || folderPath.StartsWith(folder + "/", StringComparison.Ordinal);
}
