namespace ParadoxModTool.GuiDesign.DataTypes;

/// <summary>
/// Ein GUI-Datenausdruck, wie ihn der Daten-Helfer baut: Start (Typname wie <c>Country</c> oder globaler Promote wie <c>GetPlayer</c>) und Glieder
/// (Promotes, das letzte darf eine Funktion sein). <see cref="Text"/> ergibt <c>[Country.GetCapital.GetName]</c>. Unveränderlich.
/// </summary>
public sealed record DataChain(string Start, IReadOnlyList<DataTypeEntry> Links)
{
    public const string ArgumentPlaceholder = "ARG";

    public static DataChain Empty(string start) => new(start, Array.Empty<DataTypeEntry>());

    public DataChain Append(DataTypeEntry entry) => this with { Links = Links.Append(entry).ToArray() };

    public DataChain AppendRange(IEnumerable<DataTypeEntry> entries) => this with { Links = Links.Concat(entries).ToArray() };

    /// <summary>Entfernt das Glied <paramref name="index"/> und alle dahinter.</summary>
    public DataChain TruncateAt(int index) => this with { Links = Links.Take(Math.Clamp(index, 0, Links.Count)).ToArray() };

    /// <summary>Das letzte Glied ist eine Funktion: danach kann nichts mehr folgen.</summary>
    public bool IsClosed => Links.Count > 0 && Links[^1].Kind != DataTypeKind.Promote;

    /// <summary>Typ am Ende der Kette: Start-Typ bzw. Rückgabe des globalen Promotes, dann den Promotes folgend; <see langword="null"/> nach einer Funktion.</summary>
    public string? EndType(DataTypeDatabase db)
    {
        var type = db.IsType(Start) ? Start : db.GlobalPromote(Start);
        foreach (var link in Links)
        {
            if (type is null || link.Kind != DataTypeKind.Promote || !link.HasRegisteredReturn)
            {
                return null;
            }

            type = link.ReturnType;
        }

        return type;
    }

    public string Text =>
        Start.Length == 0
            ? string.Empty
            : "[" + string.Join('.', new[] { Start }.Concat(Links.Select(Part))) + "]";

    private static string Part(DataTypeEntry entry) =>
        entry.Arguments.Count == 0
            ? entry.Name
            : $"{entry.Name}( {string.Join(", ", Enumerable.Repeat(ArgumentPlaceholder, entry.Arguments.Count))} )";
}
