namespace ParadoxModTool.ErrorLog;

/// <summary>
/// Fehler gleicher Zuordnung, gleicher Fehlerart und gleichen Namens. Ohne Namen gilt die Datei aus dem Log als Gruppenname,
/// bei nicht zugeordneten Zeilen die Meldung.
/// </summary>
public sealed class ErrorGroup
{
    public required ErrorBucket Bucket { get; init; }

    public ModSource? Mod { get; init; }

    public required string Code { get; init; }

    public required string GroupName { get; init; }

    public ErrorKind? Kind { get; init; }

    public List<ErrorEntry> Entries { get; } = new();

    public string Key => $"{Bucket}|{Mod?.Directory}|{Code}|{GroupName}";
}

public static class ErrorGrouping
{
    /// <summary>Gruppen in der Reihenfolge der Zuordnung (eigene Mods zuerst), dann nach Mod-Name, dann größte Gruppe zuerst.</summary>
    public static IReadOnlyList<ErrorGroup> Group(IEnumerable<ErrorEntry> entries, Func<ErrorEntry, ErrorAttribution> attribute)
    {
        var groups = new Dictionary<string, ErrorGroup>(StringComparer.Ordinal);
        var order = new List<ErrorGroup>();
        foreach (var entry in entries)
        {
            var attribution = attribute(entry);
            var group = new ErrorGroup
            {
                Bucket = attribution.Bucket,
                Mod = attribution.Mod,
                Code = entry.Code,
                GroupName = entry.Name ?? entry.FilePath ?? (entry.Kind is null ? entry.Message : string.Empty),
                Kind = entry.Kind,
            };

            if (!groups.TryGetValue(group.Key, out var existing))
            {
                groups[group.Key] = group;
                order.Add(group);
                existing = group;
            }

            existing.Entries.Add(entry);
        }

        return order
            .OrderBy(g => g.Bucket)
            .ThenBy(g => g.Mod?.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(g => g.Entries.Count)
            .ThenBy(g => g.GroupName, StringComparer.Ordinal)
            .ToList();
    }
}
