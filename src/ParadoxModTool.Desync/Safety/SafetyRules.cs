using System.Text.Json;

namespace ParadoxModTool.Desync;

/// <summary>Wie bedenklich ein Fund für den Mehrspielerbetrieb ist (JSON: <c>allowed</c>, <c>check</c>, <c>risk</c>); die Reihenfolge ist die Strenge.</summary>
public enum SafetySeverity
{
    Allowed,
    Check,
    Risk,
}

/// <summary>Wo ein Fund steht: in Skript, das auf allen Rechnern gleich läuft, nur auf dem lokalen Rechner, oder nicht bestimmbar.</summary>
public enum SafetyContext
{
    Synced,
    Local,
    Unknown,
}

/// <summary>Art einer MP-Regel (JSON: <c>scriptKey</c>, <c>consoleCommand</c>, <c>consoleDynamic</c>).</summary>
public enum SafetyRuleKind
{
    /// <summary>Schlüssel oder Listeneintrag in einer Skriptdatei.</summary>
    ScriptKey,

    /// <summary>Name eines Konsolenbefehls in <c>ExecuteConsoleCommand('…')</c> einer <c>.gui</c>-Datei.</summary>
    ConsoleCommand,

    /// <summary><c>ExecuteConsoleCommand(</c> mit einem Argument, das kein einzelner Text in Anführungszeichen ist.</summary>
    ConsoleDynamic,
}

/// <summary>Schlüssel eines Ordners, deren Inhalt nur lokal ausgewertet wird; <paramref name="Folder"/> klein mit <c>/</c>, ein Schlüssel <c>*</c> meint die ganze Datei.</summary>
public sealed record LocalBlock(string Folder, IReadOnlyList<string> Keys);

/// <summary>
/// Eine MP-Regel. <paramref name="Patterns"/> stehen so, wie sie im Profil geschrieben sind (der Vergleich schreibt klein);
/// bei <see cref="SafetyRuleKind.ConsoleDynamic"/> sind sie leer. <paramref name="Explain"/> ist der Text je Sprache.
/// </summary>
public sealed record SafetyRule(
    string Id,
    SafetyRuleKind Kind,
    IReadOnlyList<string> Patterns,
    SafetySeverity Synced,
    SafetySeverity Local,
    IReadOnlyDictionary<string, string> Explain)
{
    /// <summary>Strenge im Kontext; unbekannt nimmt die strengere der beiden, also mindestens <see cref="SafetySeverity.Check"/>, wenn nicht beide erlaubt sind.</summary>
    public SafetySeverity SeverityIn(SafetyContext context) => context switch
    {
        SafetyContext.Synced => Synced,
        SafetyContext.Local => Local,
        _ => (SafetySeverity)Math.Max((int)Synced, (int)Local),
    };
}

/// <summary>Abschnitt <c>safety</c> des Desync-Profils: Regeln für die Prüfung, ob eine Mod im Mehrspielerbetrieb sicher ist.</summary>
public sealed class SafetyRules
{
    private SafetyRules()
    {
    }

    /// <summary>Erstes Pfadsegment, das beim Abgleich der Ordner entfällt (klein).</summary>
    public IReadOnlyList<string> RootFolders { get; private init; } = Array.Empty<string>();

    /// <summary>Ordner, deren Dateien nur lokal ausgewertet werden (klein, <c>/</c>).</summary>
    public IReadOnlyList<string> LocalFolders { get; private init; } = Array.Empty<string>();

    /// <summary>Blöcke innerhalb synchroner Ordner, die nur lokal ausgewertet werden.</summary>
    public IReadOnlyList<LocalBlock> LocalBlocks { get; private init; } = Array.Empty<LocalBlock>();

    /// <summary>Regeln in Dateireihenfolge; die erste passende gilt.</summary>
    public IReadOnlyList<SafetyRule> Rules { get; private init; } = Array.Empty<SafetyRule>();

    /// <summary>Begriffe, die bei der Suche nach Verdächtigen nie zählen (klein).</summary>
    public IReadOnlySet<string> StopTerms { get; private init; } = new HashSet<string>();

    /// <summary>Vorgesehen für einen späteren Ausbau (Verwalter → Ordner und Schlüssel); wird gelesen und nicht ausgewertet.</summary>
    public IReadOnlyDictionary<string, JsonElement> AreaMap { get; private init; } = new Dictionary<string, JsonElement>();

    /// <summary>Erste <see cref="SafetyRuleKind.ScriptKey"/>-Regel, deren Muster dem Schlüssel gleicht (ohne Beachtung der Schreibung).</summary>
    public SafetyRule? ScriptKeyRule(string key) =>
        Rules.FirstOrDefault(r => r.Kind == SafetyRuleKind.ScriptKey
            && r.Patterns.Any(p => string.Equals(p, key, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Erste <see cref="SafetyRuleKind.ConsoleCommand"/>-Regel, die auf den Befehl passt (Schreibung egal): ein Muster mit <c>.</c> am Ende ist ein Anfang,
    /// ein Muster mit <c>*</c> steht für beliebige Zeichen, jedes andere muss dem ganzen Befehl gleichen.
    /// </summary>
    public SafetyRule? ConsoleRule(string command)
    {
        var text = command.ToLowerInvariant();
        return Rules.FirstOrDefault(r => r.Kind == SafetyRuleKind.ConsoleCommand
            && r.Patterns.Any(p => ConsoleMatches(p.ToLowerInvariant(), text)));
    }

    /// <summary>Erste <see cref="SafetyRuleKind.ConsoleDynamic"/>-Regel: für <c>ExecuteConsoleCommand(</c> mit einem Argument, das kein einzelner Text ist.</summary>
    public SafetyRule? DynamicRule => Rules.FirstOrDefault(r => r.Kind == SafetyRuleKind.ConsoleDynamic);

    private static bool ConsoleMatches(string pattern, string command)
    {
        if (pattern.Contains('*'))
        {
            return PathPattern.WildcardMatches(pattern, command);
        }

        return pattern.EndsWith('.') ? command.StartsWith(pattern, StringComparison.Ordinal) : pattern == command;
    }

    internal static SafetyRules Parse(SafetyFile file)
    {
        var blocks = new List<LocalBlock>();
        var blockFiles = file.LocalBlocks ?? new List<LocalBlockFile?>();
        for (var i = 0; i < blockFiles.Count; i++)
        {
            var block = blockFiles[i];
            if (string.IsNullOrWhiteSpace(block?.Folder))
            {
                throw new InvalidDataException("MP-Ordnerblock " + (i + 1) + " hat keinen Ordner (folder).");
            }

            blocks.Add(new LocalBlock(Folder(block.Folder), Lower(block.Keys)));
        }

        var rules = new List<SafetyRule>();
        var ruleFiles = file.Rules ?? new List<SafetyRuleFile?>();
        for (var i = 0; i < ruleFiles.Count; i++)
        {
            rules.Add(ParseRule(i + 1, ruleFiles[i]));
        }

        return new SafetyRules
        {
            RootFolders = Lower(file.RootFolders),
            LocalFolders = (file.LocalFolders ?? new List<string?>()).Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => Folder(f!)).ToList(),
            LocalBlocks = blocks,
            Rules = rules,
            StopTerms = new HashSet<string>(Lower(file.StopTerms), StringComparer.Ordinal),
            AreaMap = file.AreaMap ?? new Dictionary<string, JsonElement>(),
        };
    }

    private static SafetyRule ParseRule(int number, SafetyRuleFile? file)
    {
        if (string.IsNullOrWhiteSpace(file?.Id))
        {
            throw new InvalidDataException("MP-Regel " + number + " hat keine Id (id).");
        }

        var kind = DesyncProfile.ParseName<SafetyRuleKind>(file.Kind)
            ?? throw new InvalidDataException("MP-Regel " + number + ": unbekannte Art (kind) " + file.Kind);
        var synced = DesyncProfile.ParseName<SafetySeverity>(file.Synced)
            ?? throw new InvalidDataException("MP-Regel " + number + ": unbekannte Strenge (synced) " + file.Synced);
        var local = DesyncProfile.ParseName<SafetySeverity>(file.Local)
            ?? throw new InvalidDataException("MP-Regel " + number + ": unbekannte Strenge (local) " + file.Local);
        var patterns = Patterns(number, file.Match);
        if (kind != SafetyRuleKind.ConsoleDynamic && patterns.Count == 0)
        {
            throw new InvalidDataException("MP-Regel " + number + " braucht Muster (match).");
        }

        return new SafetyRule(file.Id, kind, patterns, synced, local, file.Explain ?? new Dictionary<string, string>());
    }

    /// <summary><c>match</c> ist ein Text oder eine Liste von Texten.</summary>
    private static List<string> Patterns(int number, JsonElement? match)
    {
        var patterns = new List<string>();
        if (match is not { } element || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return patterns;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            patterns.Add(element.GetString()!);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException("MP-Regel " + number + ": match darf nur Texte enthalten.");
                }

                patterns.Add(item.GetString()!);
            }
        }
        else
        {
            throw new InvalidDataException("MP-Regel " + number + ": match muss ein Text oder eine Liste von Texten sein.");
        }

        patterns.RemoveAll(string.IsNullOrWhiteSpace);
        return patterns;
    }

    private static List<string> Lower(List<string?>? texts) =>
        (texts ?? new List<string?>()).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!.ToLowerInvariant()).ToList();

    private static string Folder(string folder) => folder.Replace('\\', '/').Trim('/').ToLowerInvariant();

    // Formen der JSON-Datei; die öffentlichen Typen oben tragen die ausgewerteten Werte.
    internal sealed class SafetyFile
    {
        public List<string?>? RootFolders { get; set; }
        public List<string?>? LocalFolders { get; set; }
        public List<LocalBlockFile?>? LocalBlocks { get; set; }
        public List<SafetyRuleFile?>? Rules { get; set; }
        public List<string?>? StopTerms { get; set; }
        public Dictionary<string, JsonElement>? AreaMap { get; set; }
    }

    internal sealed class LocalBlockFile
    {
        public string? Folder { get; set; }
        public List<string?>? Keys { get; set; }
    }

    internal sealed class SafetyRuleFile
    {
        public string? Id { get; set; }
        public string? Kind { get; set; }
        public JsonElement? Match { get; set; }
        public string? Synced { get; set; }
        public string? Local { get; set; }
        public Dictionary<string, string>? Explain { get; set; }
    }
}
