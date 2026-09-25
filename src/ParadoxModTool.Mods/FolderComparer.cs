namespace ParadoxModTool.Mods;

public enum DifferenceKind
{
    OnlyLeft,
    OnlyRight,
    Different,
    Same,
}

/// <summary>Ergebnis für einen relativen Pfad; die Seite, auf der die Datei fehlt, ist <see langword="null"/>.</summary>
public sealed record FileDifference(string RelativePath, DifferenceKind Kind, ModFileEntry? Left, ModFileEntry? Right);

/// <summary>Vergleicht zwei Datei-Indizes. Verschiedene Größe ist sofort verschieden, bei gleicher Größe entscheidet der Hash. Das Datum allein entscheidet nie.</summary>
public static class FolderComparer
{
    public static IReadOnlyList<FileDifference> Compare(ModFileIndex left, ModFileIndex right, CancellationToken cancellationToken = default)
    {
        var paths = new SortedSet<string>(left.Entries.Keys.Concat(right.Entries.Keys), StringComparer.OrdinalIgnoreCase);
        var result = new List<FileDifference>(paths.Count);
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            left.Entries.TryGetValue(path, out var leftEntry);
            right.Entries.TryGetValue(path, out var rightEntry);

            var kind = leftEntry is null ? DifferenceKind.OnlyRight
                : rightEntry is null ? DifferenceKind.OnlyLeft
                : leftEntry.Size != rightEntry.Size ? DifferenceKind.Different
                : left.HashOf(path) == right.HashOf(path) ? DifferenceKind.Same
                : DifferenceKind.Different;
            result.Add(new FileDifference(path, kind, leftEntry, rightEntry));
        }

        return result;
    }
}
