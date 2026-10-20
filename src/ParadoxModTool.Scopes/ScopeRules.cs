using System.Text.Json;
using ParadoxModTool.Storage;

namespace ParadoxModTool.Scopes;

/// <summary>Gelernter Start-Scope eines Feldpfads (z. B. <c>bureaucracies/*/potential</c>).</summary>
public sealed class FieldRule
{
    /// <summary>Der Start-Scope; <see langword="null"/>, wenn die Vanilla-Dateien keinen eindeutigen ergeben (dann nur Diagnose).</summary>
    public string? Scope { get; set; }

    /// <summary>Anteil der Statements im Feld, die diesen Scope zulassen (0-1).</summary>
    public double Support { get; set; }

    /// <summary>Anzahl der gezählten Statements.</summary>
    public int Total { get; set; }
}

/// <summary>Der Scope eines Feldes hängt von einem Geschwisterfeld des Objekts ab (z. B. <c>type = character</c> in <c>customizable_localization</c>).</summary>
public sealed class DiscriminatorRule
{
    public string Key { get; set; } = string.Empty;

    /// <summary>Wert des Geschwisterfeldes auf Scope.</summary>
    public Dictionary<string, string> Map { get; set; } = new();
}

/// <summary>Das Ergebnis des Lernens: Start-Scopes je Feldpfad für ein Spiel.</summary>
public sealed class ScopeRules
{
    public const int CurrentVersion = 3;

    /// <summary>Mindestzustimmung, ab der ein gelernter Scope als sicher gilt.</summary>
    public const double ConfidentSupport = 0.9;

    public int Version { get; set; } = CurrentVersion;

    public DateTime LearnedUtc { get; set; }

    /// <summary>Fingerabdruck der gelesenen Vanilla-Dateien (Pfad, Größe, Änderungszeit); ändert er sich, wird neu gelernt.</summary>
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>Kennung des Imports der Script-Datenbank, auf dem das Lernen beruht.</summary>
    public int DocsImportId { get; set; }

    public int FilesScanned { get; set; }

    /// <summary>
    /// Art der Events ohne Feld <c>type</c> (Spielprofil <c>defaultEventType</c>, CK III: <c>character_event</c>); <see langword="null"/> = solche Events
    /// haben keinen Pfad. Mitgelernt, damit ein geänderter Wert im Profil neu lernen lässt.
    /// </summary>
    public string? DefaultEventType { get; set; }

    /// <summary>
    /// Events, deren Art im Blocknamen steht (Spielprofil <c>eventBlockScopes</c>, Stellaris: <c>country_event = { … }</c>): Blockname auf Root-Scope;
    /// "none" = Event ohne Root-Scope. Leer = keine solchen Events. Mitgelernt, damit ein geänderter Wert im Profil neu lernen lässt.
    /// </summary>
    public Dictionary<string, string> EventBlockScopes { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Wert in <see cref="EventBlockScopes"/>: Der Blockname ist die Event-Art, der Root-Scope kommt aus den gelernten Regeln wie bei Events mit
    /// <c>type</c> (Hearts of Iron IV, EU IV: Charakter- und Länder-Schlüssel in Events wechseln den Scope, ein fester Root-Scope gälte auch darunter).
    /// </summary>
    public const string LearnedRootScope = "learned";

    /// <summary>
    /// Zahlen als Blockschlüssel wechseln den Scope (Spielprofil <c>numericKeysChangeScope</c>: Staats-IDs in HOI IV, Provinz-IDs in EU IV), außer in
    /// <c>random_list</c>. Mitgelernt, damit eine geänderte Einstellung neu lernen lässt.
    /// </summary>
    public bool NumericKeysChangeScope { get; set; }

    public Dictionary<string, FieldRule> Fields { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, DiscriminatorRule> Discriminators { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gelernte Typen gespeicherter Scopes je Feld: Feldpfad der obersten Ebene (<c>country_interactions/*/potential</c>) auf Name (<c>actor</c>) und Scope.
    /// Nur sichere Regeln (mindestens 5 Statements, eindeutig, 90 % Zustimmung).
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> SavedFields { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gelernter Typ je Name über alle Felder, falls der Name überall zum selben Scope führt (<c>actor</c> → <c>country</c>); Rückfall, wenn das Feld keine eigene Regel hat.</summary>
    public Dictionary<string, string> SavedNames { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Namen der Vanilla-Script-Values (<see cref="ScriptValueNames"/>), sortiert: Wert-Vorschläge im Scope Finder. Sie werden mit den Regeln neu gelesen, also
    /// auch automatisch nach einem Spiel-Update (der Fingerabdruck umfasst <c>common/script_values</c>).
    /// </summary>
    public List<string> ScriptValues { get; set; } = new();

    public int ConfidentCount => Fields.Values.Count(f => f.Scope is not null && f.Support >= ConfidentSupport);

    public int UnresolvedCount => Fields.Values.Count(f => f.Scope is null || f.Support < ConfidentSupport);
}

/// <summary>Speichert die gelernten Regeln eines Spiels (<c>games\&lt;id&gt;\scopes\rules.json</c>). Eine fehlende oder beschädigte Datei ergibt <see langword="null"/>: dann wird neu gelernt.</summary>
public sealed class ScopeRulesStore
{
    private static readonly JsonSerializerOptions Options = new();

    private readonly string _path;

    public ScopeRulesStore(string path)
    {
        _path = path;
    }

    public ScopeRules? TryLoad()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var rules = JsonSerializer.Deserialize<ScopeRules>(File.ReadAllText(_path), Options);
            return rules is { Version: ScopeRules.CurrentVersion } ? rules : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Save(ScopeRules rules) => SharedFile.WriteAllTextAtomic(_path, JsonSerializer.Serialize(rules, Options));
}
