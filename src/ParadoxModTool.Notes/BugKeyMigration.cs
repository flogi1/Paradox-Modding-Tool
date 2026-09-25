namespace ParadoxModTool.Notes;

/// <summary>
/// Schreibt die Schlüssel der Bezüge um, wenn Mods angelegt oder entfernt werden (Gegenstück zu <see cref="NoteKeyMigration"/>, das nur Notizen und
/// Lesezeichen umzieht). <c>keyMap</c>: alter Schlüssel → neuer oder <see langword="null"/> = unverändert. <c>ownerMap</c>: alte Besitzer-Mod →
/// neue, <c>""</c> = kein Projekt, <see langword="null"/> = unverändert. Doppelte Bezüge einer Meldung werden danach zusammengefasst; eine Meldung
/// ohne Besitzer übernimmt die Mod ihres ersten Mod-Bezugs. Idempotent.
/// </summary>
public static class BugKeyMigration
{
    public static bool Rekey(BugData data, Func<string, string?> keyMap, Func<string, string?>? ownerMap = null)
    {
        var changed = false;
        foreach (var report in data.Reports)
        {
            foreach (var link in report.Links)
            {
                if (keyMap(link.Key) is { } newKey && newKey != link.Key)
                {
                    link.Key = newKey;
                    changed = true;
                }
            }

            var distinct = new List<BugLink>();
            foreach (var link in report.Links.Where(link => !distinct.Any(kept => kept.SameAs(link))))
            {
                distinct.Add(link);
            }

            if (distinct.Count != report.Links.Count)
            {
                report.Links = distinct;
                changed = true;
            }

            if (ownerMap is not null && report.ModId is { } owner && ownerMap(owner) is { } newOwner && newOwner != owner)
            {
                report.ModId = newOwner.Length == 0 ? null : newOwner;
                changed = true;
            }

            if (report.ModId is null && report.Links.Select(link => BugLink.ModIdOf(link.Key)).FirstOrDefault(id => id is not null) is { } adopted)
            {
                report.ModId = adopted;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>Wendet den Umzug auf die Datei an; geschrieben wird nur, wenn sich etwas ändert. <see langword="true"/> bei einer Änderung.</summary>
    public static bool Run(string path, Func<string, string?> keyMap, Func<string, string?>? ownerMap = null)
    {
        if (!Rekey(BugReportStore.Load(path), keyMap, ownerMap))
        {
            return false;
        }

        BugReportStore.Update(path, data => Rekey(data, keyMap, ownerMap));
        return true;
    }
}
