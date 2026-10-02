namespace ParadoxModTool.Compatch;

/// <summary>Filter und Auswahlen des Merge-Fensters, die über die Stufen- und Herkunftsfilter hinausgehen (Nutzerwünsche 2026-09-29).</summary>
public static class ActionFilters
{
    /// <summary>
    /// "Doppelte Dateien ausblenden": Dateien, die in A und B am selben Pfad liegen (Dateirest- oder Ganze-Datei-Adresse) und deren Aktionen
    /// alle automatisch sind – eine Seite ersetzt die Datei einfach. Pfade wie <see cref="TreePath.FileOf"/>.
    /// </summary>
    public static IReadOnlySet<string> ReplacedFiles(IReadOnlyList<MergeAction> actions, ResolvedEntries resolved)
    {
        var samePath = resolved.Addresses.Keys
            .Where(a => a.Kind is AddressKind.FileRest or AddressKind.WholeFile)
            .Select(a => a.Scope)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return actions
            .GroupBy(TreePath.FileOf, StringComparer.OrdinalIgnoreCase)
            .Where(g => samePath.Contains(g.Key) && g.All(a => a.Stage == MergeStage.Automatic))
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Suchfeld über dem Baum: Der Text (ohne Leerzeichen am Rand, Groß-/Kleinschreibung egal) kommt im Dateipfad, in der Stelle oder im
    /// Schlüssel vor. Leerer Text passt immer. Früher waren Dateipfad ("Ordner") und Stelle/Schlüssel ("Suche") zwei Felder.
    /// </summary>
    public static bool MatchesText(MergeAction action, string? text)
    {
        var needle = text?.Trim();
        return string.IsNullOrEmpty(needle)
            || TreePath.FileOf(action).Contains(needle, StringComparison.OrdinalIgnoreCase)
            || action.DisplayPath.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || action.Address.Key.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Aktionen unter einem Baumknoten: Ordner (Pfad als Präfix), Datei (gleicher Pfad) oder Block (<see cref="EntryAddress.Id"/>).</summary>
    public static IReadOnlyList<MergeAction> Under(IEnumerable<MergeAction> actions, string treePath, TreeNodeKind kind) => kind switch
    {
        TreeNodeKind.Folder => actions.Where(a => TreePath.FileOf(a).StartsWith(treePath + "/", StringComparison.OrdinalIgnoreCase)).ToList(),
        TreeNodeKind.File => actions.Where(a => string.Equals(TreePath.FileOf(a), treePath, StringComparison.OrdinalIgnoreCase)).ToList(),
        TreeNodeKind.Block => actions.Where(a => a.Address.Id == treePath).ToList(),
        _ => actions.Where(a => a.Id == treePath).ToList(),
    };
}
