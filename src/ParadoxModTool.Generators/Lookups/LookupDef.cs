namespace ParadoxModTool.Generators.Lookups;

/// <summary>Art einer Nachschlage-Quelle: Blöcke aus Skriptordnern des Spiels oder Einträge einer JSON-Datei aus den Assets.</summary>
public enum LookupKind
{
    GameKeys,
    Json,
}

/// <summary>
/// Eine Nachschlage-Quelle aus <c>lookups</c> in package.json (Spec 2026-10-05-generator-packages-lookups, Abschnitt 1).
/// <see cref="LookupKind.GameKeys"/>: <see cref="Folder"/> unter dem Spielinhalt, Blöcke bis <see cref="Depth"/>, mit <see cref="NamedOnly"/> nur Blöcke mit Namen
/// in der Lokalisierung, Tag aus dem Dateinamen (<see cref="TagFromFile"/>, Gruppe 1) oder aus Schlüsseln im Block (<see cref="TagInBlock"/>).
/// <see cref="LookupKind.Json"/>: Datei des Assets <see cref="Asset"/>, Pfad <see cref="Items"/> (<c>[]</c> = jedes Element), <see cref="Fields"/> ordnet die
/// Eintragsfelder Eigenschaften zu (<c>../x</c> = Eigenschaft des Elternelements).
/// </summary>
public sealed record LookupSourceDef(
    string Id,
    LookupKind Kind,
    string Label,
    string? Folder = null,
    int Depth = 1,
    bool NamedOnly = false,
    string? TagFromFile = null,
    IReadOnlyList<string>? TagInBlock = null,
    string? Asset = null,
    string? Items = null,
    IReadOnlyDictionary<string, string>? Fields = null);

/// <summary>Nachschlagen an einem Feld: Quellen (Ids aus <c>lookups</c>) und welche Eintragsfelder (<c>name</c>, <c>desc</c>) welche Felder derselben Ebene füllen.</summary>
public sealed record FieldLookupDef(IReadOnlyList<string> Sources, IReadOnlyDictionary<string, string> Fills);
