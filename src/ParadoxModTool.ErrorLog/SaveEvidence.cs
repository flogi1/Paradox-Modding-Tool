using ParadoxModTool.SaveGames;

namespace ParadoxModTool.ErrorLog;

/// <summary>
/// Befund aus dem Spielstand für einen Namen einer Fehlermeldung: bei wie vielen Objekten er gesetzt ist, bis zu drei Beispiele und, wenn er
/// fehlt, der ähnlichste gesetzte Name (Tippfehler-Kandidat). <see cref="SaveLabel"/> ist das Spieldatum oder der Dateiname des Stands.
/// </summary>
public sealed record SaveEvidence(string SaveLabel, string Name, int ObjectCount, IReadOnlyList<SaveObjectRef> Examples, string? SimilarName, int SimilarCount)
{
    private const int MaxExamples = 3;
    private const int MaxDistance = 2;

    /// <summary><paramref name="saveNames"/> "flag" sucht Flag-Werte, sonst Variablen und Variablenlisten (Katalogfeld <c>saveNames</c>).</summary>
    public static SaveEvidence For(SaveSnapshot save, string name, string saveNames)
    {
        var kinds = saveNames == "flag" ? new[] { SaveNameKind.FlagValue } : new[] { SaveNameKind.Variable, SaveNameKind.List };
        var owners = kinds.SelectMany(kind => save.EntriesFor(name, kind)).Select(e => e.OwnerPath).Distinct(StringComparer.Ordinal).ToList();
        var examples = owners
            .Select(save.Objects.Resolve)
            .OrderBy(o => o.Kind.Order)
            .ThenBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(MaxExamples)
            .ToList();
        var label = save.File.GameDate ?? Path.GetFileName(save.File.Path);
        if (owners.Count > 0)
        {
            return new SaveEvidence(label, name, owners.Count, examples, null, 0);
        }

        var similar = save.Names
            .Where(n => kinds.Contains(n.Kind) && n.Name != name && Math.Abs(n.Name.Length - name.Length) <= MaxDistance)
            .Where(n => SymbolIndex.Distance(n.Name, name, MaxDistance) <= MaxDistance)
            .GroupBy(n => n.Name, StringComparer.Ordinal)
            .Select(g => (Name: g.Key, Count: g.Max(n => n.ObjectCount)))
            .OrderByDescending(n => n.Count)
            .ThenBy(n => n.Name, StringComparer.Ordinal)
            .FirstOrDefault();
        return new SaveEvidence(label, name, 0, examples, similar.Name, similar.Name is null ? 0 : similar.Count);
    }
}
