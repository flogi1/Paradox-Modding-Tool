using ParadoxModTool.ModCompat;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

/// <summary>Beitrag einer Seite zu einer Adresse: die Einträge der obersten Ebene (bei GUI-Typen Kopfzeile plus Rumpf) mit Herkunft.</summary>
/// <remarks>
/// <see cref="Entries"/> hat die dateilokalen <c>@</c>-Konstanten bereits eingesetzt (Vergleich und Ausgabe brauchen keine Konstanten).
/// <see cref="SourceEntries"/> sind dann die Originaleinträge mit Textstelle; ohne Konstanten ist es <see langword="null"/>.
/// <see cref="ContainerEnd"/> ist die Position der schließenden Klammer des umgebenden Blocks (<c>types</c>-Block bzw. Elternblock
/// bei Defines), damit neue Geschwister hineingesetzt werden können.
/// </remarks>
public sealed record KeyPart(
    MergeSide Side,
    string RelativePath,
    string FullPath,
    OverrideOperator Operator,
    EntryAddress Address,
    IReadOnlyList<ScriptEntry> Entries,
    IReadOnlyList<ScriptEntry>? SourceEntries = null,
    int? ContainerEnd = null)
{
    /// <summary>Die Einträge mit ihrer Textstelle in der Datei.</summary>
    public IReadOnlyList<ScriptEntry> Original => SourceEntries ?? Entries;
}

/// <summary>Eine gelesene Datei einer Seite: Schlüsselbeiträge, Dateirest und Text.</summary>
public sealed class FileParts
{
    public required MergeSide Side { get; init; }

    public required string RelativePath { get; init; }

    public required string FullPath { get; init; }

    public required RuleMatch Rule { get; init; }

    /// <summary><see langword="false"/> = nur als ganze Datei vergleichbar (Nicht-Skript, Ordnerregel WholeFile).</summary>
    public bool IsScript { get; init; }

    /// <summary>Parserfehler; die Datei ist dann nur als ganze Datei vergleichbar.</summary>
    public string? Error { get; init; }

    /// <summary>Dateiinhalt ohne BOM (Grundlage der Textstellen <see cref="ScriptEntry.SourceStart"/>).</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Kodierung der Datei ("utf-8" oder "windows-1252"), siehe <see cref="ParsedScript.Decode"/>.</summary>
    public string EncodingName { get; init; } = "utf-8";

    public List<KeyPart> Keys { get; } = new();

    public List<ScriptEntry> Rest { get; } = new();
}
