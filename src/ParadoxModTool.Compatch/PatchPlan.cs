namespace ParadoxModTool.Compatch;

public enum PatchForm
{
    /// <summary><c>INJECT:key = { nur die Ergänzungen }</c> in der PMT-eigenen Datei.</summary>
    Inject,

    /// <summary><c>REPLACE:key = { … }</c> in der PMT-eigenen Datei; bei Lokalisierung eine Zeile im <c>replace/</c>-Ordner.</summary>
    Replace,

    /// <summary>Schlüssel ohne Präfix in der PMT-eigenen Datei (Name gewinnt die Ladereihenfolge).</summary>
    PlainKey,

    /// <summary>Ganze Datei unter dem Originalpfad.</summary>
    WholeFile,
}

public enum PatchTarget
{
    Compatch,
    BaseModA,
    BaseModB,
}

/// <summary>
/// Ein Eintrag für eine PMT-eigene Patch-Datei. <see cref="Text"/> mit <c>\n</c>, inklusive <c># PMT offen</c>-Zeilen.
/// <see cref="InlineText"/> = derselbe Eintrag ohne types-/Elternblock-Hülle und ohne Kommentare vor dem ersten Eintrag, für das Ersetzen an einer vorhandenen Textstelle.
/// </summary>
public sealed record PatchEntryOp(EntryAddress Address, PatchForm Form, string TargetFile, string Text, IReadOnlyList<string> OpenNotes, bool DependsOnLoadOrder, bool Uncertain, string InlineText = "");

/// <summary>
/// Eine ganze Datei unter <see cref="RelativePath"/>. Entweder <see cref="Text"/> (mit den Zeilenenden der Quelldatei, ohne BOM;
/// <see cref="Utf8Bom"/> sagt, ob beim Schreiben ein BOM davor gehört) oder <see cref="CopyFrom"/> (Datei unverändert kopieren).
/// <see cref="Normalized"/> = aus Einträgen neu geschrieben, Leerzeilen zwischen Einträgen gehen verloren.
/// </summary>
public sealed record PatchFileOp(string RelativePath, string? Text, string? CopyFrom, bool Utf8Bom, IReadOnlyList<EntryAddress> Addresses, bool Normalized, bool DependsOnLoadOrder, string EncodingName = "utf-8");

public sealed class PatchPlan
{
    public List<PatchEntryOp> Entries { get; } = new();

    public List<PatchFileOp> Files { get; } = new();

    /// <summary>Adressen, deren gewünschter Zustand dem Spielzustand ohne Patch entspricht.</summary>
    public HashSet<EntryAddress> NotNeeded { get; } = new();

    /// <summary>Wählbare Formen je Schlüssel-Adresse (erste = Empfehlung).</summary>
    public Dictionary<EntryAddress, IReadOnlyList<PatchForm>> AvailableForms { get; } = new();
}
