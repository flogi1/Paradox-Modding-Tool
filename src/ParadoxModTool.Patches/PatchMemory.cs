namespace ParadoxModTool.Patches;

/// <summary>Ein vom Rezept gesetzter Listeneintrag mit der Tabellenzeile, zu der er gehört.</summary>
public sealed record ListEntryRecord(string File, string ListPath, string Entry, string RowKey = "");

/// <summary>
/// Gedächtnis eines Rezepts: der zuletzt erzeugte Text je Baustein (Schlüssel <c>regelId|bausteinSchlüssel</c>)
/// und die vom Rezept gesetzten Listeneinträge je Regel. Grundlage des 3-Wege-Merge mit Handänderungen.
/// </summary>
public sealed class PatchMemory
{
    public Dictionary<string, string> Units { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, List<ListEntryRecord>> ListEntries { get; set; } = new(StringComparer.Ordinal);

    public static string UnitId(string ruleId, string unitKey) => ruleId + "|" + unitKey;

    public PatchMemory Clone() => new()
    {
        Units = new Dictionary<string, string>(Units, StringComparer.Ordinal),
        ListEntries = ListEntries.ToDictionary(p => p.Key, p => p.Value.ToList(), StringComparer.Ordinal),
    };
}
