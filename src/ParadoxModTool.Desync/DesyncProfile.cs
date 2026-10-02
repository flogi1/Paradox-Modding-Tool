using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Desync;

/// <summary>
/// Pfadmuster für Knoten eines Spielstands, Segmente durch <c>/</c> getrennt. <c>*</c> passt auf genau ein Segment, <c>**</c> auf null
/// oder mehr, <c>{id}</c> auf ein Segment aus Ziffern; ein Segment darf <c>*</c> auch als Platzhalter enthalten (z.B. <c>*_cache</c>).
/// Vor dem Vergleich verliert jedes Pfadsegment sein Vorkommen: <c>played_country#2</c> gilt als <c>played_country</c>, <c>[3]</c> und
/// <c>sub_unit=7</c> sind namenlose Listeneinträge und passen nur auf <c>*</c> bzw. <c>**</c>.
/// </summary>
public sealed class PathPattern
{
    private const string AnyRun = "**";
    private const string One = "*";
    private const string Id = "{id}";

    private static readonly Regex Occurrence = new(@"#\d+$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly string[] _segments;

    private PathPattern(string text, string[] segments)
    {
        Text = text;
        _segments = segments;
    }

    /// <summary>Das Muster so, wie es im Profil steht.</summary>
    public string Text { get; }

    public static PathPattern Parse(string pattern)
    {
        var segments = new List<string>();
        foreach (var segment in pattern.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            // Aufeinanderfolgende ** sind eines, das spart Rückverfolgung.
            if (segment == AnyRun && segments.Count > 0 && segments[^1] == AnyRun)
            {
                continue;
            }

            segments.Add(segment);
        }

        return new PathPattern(pattern, segments.ToArray());
    }

    public bool Matches(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var normalized = new string?[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            normalized[i] = Normalize(parts[i]);
        }

        return MatchFrom(0, normalized, 0);
    }

    /// <summary>Muster ohne das letzte Segment (der Behälter eines <c>…/{id}</c>-Musters); null, wenn das Muster nicht auf <c>{id}</c> endet.</summary>
    internal PathPattern? ContainerOfId() =>
        _segments.Length > 0 && _segments[^1] == Id
            ? new PathPattern(string.Join('/', _segments[..^1]), _segments[..^1])
            : null;

    /// <summary>Segment ohne Vorkommen-Zusatz; null für namenlose Listeneinträge (<c>[3]</c>, <c>sub_unit=7</c>).</summary>
    private static string? Normalize(string segment) =>
        segment.StartsWith('[') || segment.Contains('=') ? null : Occurrence.Replace(segment, string.Empty);

    private bool MatchFrom(int patternIndex, string?[] path, int pathIndex)
    {
        if (patternIndex == _segments.Length)
        {
            return pathIndex == path.Length;
        }

        var segment = _segments[patternIndex];
        if (segment == AnyRun)
        {
            for (var skip = pathIndex; skip <= path.Length; skip++)
            {
                if (MatchFrom(patternIndex + 1, path, skip))
                {
                    return true;
                }
            }

            return false;
        }

        return pathIndex < path.Length && SegmentMatches(segment, path[pathIndex]) && MatchFrom(patternIndex + 1, path, pathIndex + 1);
    }

    private static bool SegmentMatches(string pattern, string? part)
    {
        if (pattern == One)
        {
            return true;
        }

        if (part is null)
        {
            return false;
        }

        if (pattern == Id)
        {
            return part.Length > 0 && part.All(char.IsAsciiDigit);
        }

        return pattern.Contains('*') ? WildcardMatches(pattern, part) : pattern == part;
    }

    /// <summary>Vergleich mit <c>*</c> als beliebig lange Zeichenfolge innerhalb des Segments.</summary>
    internal static bool WildcardMatches(string pattern, string text)
    {
        int p = 0, t = 0, star = -1, resume = 0;
        while (t < text.Length)
        {
            if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                resume = t;
            }
            else if (p < pattern.Length && pattern[p] == text[t])
            {
                p++;
                t++;
            }
            else if (star >= 0)
            {
                p = star + 1;
                t = ++resume;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }
}

/// <summary>Erklärung einer Prüfsummen-Kategorie (z.B. <c>RANDOM_COUNT</c>): Name, Beschreibung und mögliche Ursachen je Sprache.</summary>
public sealed record ChecksumInfo(
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string> Explain,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Causes);

/// <summary>Woher der Anzeigename eines Objekts kommt: aus einer Tag-Liste oder aus einer Schlüsselliste (Index ab <c>KeyListBase</c>).</summary>
public sealed record NameSource(string? TagList, string? KeyList, int KeyListBase);

/// <summary>Art eines Feldes im Paradox-Formular (JSON: <c>text</c>, <c>multiline</c>, <c>option</c>, <c>hint</c>, <c>attachments</c>).</summary>
public enum FormFieldKind
{
    Text,
    Multiline,
    Option,
    Hint,
    Attachments,
}

/// <summary>Baustein im Code, der den Inhalt eines Formularfelds liefert (JSON: camelCase des Namens, z.B. <c>modsDisabled</c>).</summary>
public enum FormSource
{
    VerifiedFiles,
    ModsDisabled,
    Summary,
    Description,
    IssueType,
    Steps,
    GameVersion,
    Fixed,
    Dumps,
    Screenshot,
}

/// <summary>Ein Feld des Paradox-Formulars; <paramref name="Value"/> ist der feste Wert (nur bei <see cref="FormSource.Fixed"/>), sonst null.</summary>
public sealed record ParadoxFormField(string Label, FormFieldKind Kind, FormSource Source, string? Value);

/// <summary>Wodurch eine Hinweisregel ausgelöst wird.</summary>
public enum AdviceTrigger
{
    /// <summary>Eine Auffälligkeit der Zusammenfassung (<see cref="SummaryWarningKind"/>).</summary>
    Warning,

    /// <summary>Eine abweichende Prüfsummen-Kategorie.</summary>
    Checksum,

    /// <summary>Ein Hinweis aus den Skriptdateien einer Mod.</summary>
    LeadInMod,
}

/// <summary>Hinweisregel für Berichte; <paramref name="Key"/> ist der Name der <see cref="SummaryWarningKind"/> bzw. die Prüfsummen-Kategorie, bei <see cref="AdviceTrigger.LeadInMod"/> null.</summary>
public sealed record AdviceRule(AdviceTrigger Trigger, string? Key, string Text);

/// <summary>
/// Desync-Regeln eines Spiels (Datei im Profilordner, Verweis über <c>desyncRules</c>): wie Objekte in einem Spielstand erkannt, welche
/// Pfade als Rauschen oder Folge ausgeblendet und wie Objekte benannt werden. Alle Muster sind <see cref="PathPattern"/>.
/// </summary>
public sealed class DesyncProfile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IReadOnlyList<PathPattern> _containers;
    private readonly IReadOnlyList<(PathPattern Pattern, string Field)> _anonymousIds;
    private readonly IReadOnlyList<(PathPattern Pattern, int Bits)> _slotBits;

    private DesyncProfile(ProfileFile file, string fingerprint)
    {
        Fingerprint = fingerprint;
        Version = file.Version;
        IdSlotBits = file.IdSlotBits;
        IdSlotBitsOverride = file.IdSlotBitsOverride ?? new Dictionary<string, int>();
        Checksums = (file.Checksums ?? new Dictionary<string, ChecksumFile>())
            .Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToInfo(), StringComparer.Ordinal);
        Objects = Patterns(file.Objects);
        AnonymousIds = file.AnonymousIds ?? new Dictionary<string, string>();
        SizeCapBytes = file.SizeCapBytes;
        NodeDepth = file.NodeDepth;
        Noise = Patterns(file.Noise);
        Consequence = Patterns(file.Consequence);
        FloatDrift = file.FloatDrift;
        Names = (file.Names ?? new Dictionary<string, NameFile>())
            .Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => new NameSource(pair.Value.TagList, pair.Value.KeyList, pair.Value.KeyListBase));
        LabelFields = file.LabelFields ?? new List<string>();
        ParadoxForm = file.ParadoxForm is null ? null : FormFields(file.ParadoxForm);
        Advice = AdviceRules(file.Advice);
        Safety = file.Safety is null ? null : SafetyRules.Parse(file.Safety);

        var containers = new List<PathPattern>();
        foreach (var pattern in Objects)
        {
            containers.Add(pattern.ContainerOfId()
                ?? throw new InvalidDataException("Objektmuster muss auf /{id} enden: " + pattern.Text));
        }

        _containers = containers;
        _anonymousIds = AnonymousIds.Select(pair => (PathPattern.Parse(pair.Key), pair.Value)).ToList();
        _slotBits = IdSlotBitsOverride.Select(pair => (PathPattern.Parse(pair.Key), pair.Value)).ToList();
    }

    public int Version { get; }

    /// <summary>SHA-256 (Hex, Kleinbuchstaben) des JSON-Texts, aus dem das Profil gelesen wurde; ändert sich mit jeder Änderung der Datei.</summary>
    public string Fingerprint { get; }

    /// <summary>Anzahl niedriger Bits einer Objekt-Id, die den Slot angeben; die höheren sind die Generation.</summary>
    public int IdSlotBits { get; }

    /// <summary>Abweichende Slot-Bits je Behälter (Muster des Behälterpfads).</summary>
    public IReadOnlyDictionary<string, int> IdSlotBitsOverride { get; }

    public IReadOnlyDictionary<string, ChecksumInfo> Checksums { get; }

    /// <summary>Objektmuster; sie enden auf <c>/{id}</c>, der Behälter ist alles davor.</summary>
    public IReadOnlyList<PathPattern> Objects { get; }

    /// <summary>Muster einer Liste ohne eigene Ids, Wert ist das Feld, dessen Zahl das Listenelement bezeichnet.</summary>
    public IReadOnlyDictionary<string, string> AnonymousIds { get; }

    /// <summary>Größere Knoten werden nicht Feld für Feld verglichen, sondern nur gemeldet.</summary>
    public long SizeCapBytes { get; }

    /// <summary>Tiefe, bis zu der ein Unterschied einem Knoten zugeordnet wird.</summary>
    public int NodeDepth { get; }

    /// <summary>Pfade, die sich ohne Bedeutung unterscheiden (Zwischenspeicher, Metadaten).</summary>
    public IReadOnlyList<PathPattern> Noise { get; }

    /// <summary>Pfade, die nur Folge eines früheren Unterschieds sind.</summary>
    public IReadOnlyList<PathPattern> Consequence { get; }

    /// <summary>Zahlen, die sich um weniger als diesen Wert unterscheiden, gelten als gleich.</summary>
    public double FloatDrift { get; }

    /// <summary>Namensquelle je Objektmuster (Text des Musters als Schlüssel).</summary>
    public IReadOnlyDictionary<string, NameSource> Names { get; }

    /// <summary>Felder, deren Text als Beschriftung eines Objekts taugt, in dieser Reihenfolge.</summary>
    public IReadOnlyList<string> LabelFields { get; }

    /// <summary>Felder des Paradox-Formulars in Formularreihenfolge; null, wenn das Profil keinen Abschnitt <c>paradoxForm</c> hat.</summary>
    public IReadOnlyList<ParadoxFormField>? ParadoxForm { get; }

    /// <summary>Hinweisregeln für Berichte in Dateireihenfolge.</summary>
    public IReadOnlyList<AdviceRule> Advice { get; }

    /// <summary>Regeln der Mehrspieler-Sicherheitsprüfung; null, wenn das Profil keinen Abschnitt <c>safety</c> hat.</summary>
    public SafetyRules? Safety { get; }

    public static string? PathFor(string profilesDirectory, string? desyncRules) =>
        string.IsNullOrWhiteSpace(desyncRules) ? null : Path.Combine(profilesDirectory, desyncRules);

    /// <summary>Lädt die Regeln; eine fehlende oder ungültige Datei wirft (Dateifehler bzw. <see cref="JsonException"/>).</summary>
    public static DesyncProfile Load(string path) => Parse(File.ReadAllText(path));

    public static DesyncProfile Parse(string json)
    {
        var file = JsonSerializer.Deserialize<ProfileFile>(json, Options)
            ?? throw new InvalidDataException("Die Desync-Regeln sind leer.");
        return new DesyncProfile(file, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant());
    }

    /// <summary>Erklärung der Prüfsummen-Kategorie; null, wenn sie das Profil nicht kennt (das Fenster zeigt dann den allgemeinen Text).</summary>
    public ChecksumInfo? Describe(string category) => Checksums.GetValueOrDefault(category);

    public bool IsObjectContainer(string containerPath) => _containers.Any(pattern => pattern.Matches(containerPath));

    /// <summary>Slot-Bits des Behälters: der erste passende Eintrag aus <see cref="IdSlotBitsOverride"/>, sonst <see cref="IdSlotBits"/>.</summary>
    public int SlotBitsFor(string containerPath)
    {
        foreach (var (pattern, bits) in _slotBits)
        {
            if (pattern.Matches(containerPath))
            {
                return bits;
            }
        }

        return IdSlotBits;
    }

    /// <summary>Feld, das die Listenelemente unter <paramref name="listPath"/> bezeichnet; null, wenn die Liste nicht in <see cref="AnonymousIds"/> steht.</summary>
    public string? AnonymousIdFor(string listPath)
    {
        foreach (var (pattern, field) in _anonymousIds)
        {
            if (pattern.Matches(listPath))
            {
                return field;
            }
        }

        return null;
    }

    private static List<PathPattern> Patterns(List<string>? texts) =>
        (texts ?? new List<string>()).Select(PathPattern.Parse).ToList();

    private static List<ParadoxFormField> FormFields(ParadoxFormFile form)
    {
        var fields = new List<ParadoxFormField>();
        foreach (var field in form.Fields ?? new List<FormFieldFile?>())
        {
            var label = field?.Label;
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new InvalidDataException("Ein Feld von paradoxForm hat keine Beschriftung (label).");
            }

            var kind = ParseName<FormFieldKind>(field!.Kind)
                ?? throw new InvalidDataException("Unbekannte Art (kind) im Feld \"" + label + "\": " + field.Kind);
            var source = ParseName<FormSource>(field.Source)
                ?? throw new InvalidDataException("Unbekannte Quelle (source) im Feld \"" + label + "\": " + field.Source);
            fields.Add(new ParadoxFormField(label, kind, source, field.Value));
        }

        return fields;
    }

    private static List<AdviceRule> AdviceRules(List<AdviceFile?>? files)
    {
        var rules = new List<AdviceRule>();
        for (var i = 0; i < (files?.Count ?? 0); i++)
        {
            var number = i + 1;
            var file = files![i];
            if (string.IsNullOrWhiteSpace(file?.Text))
            {
                throw new InvalidDataException("Hinweisregel " + number + " hat keinen Text (text).");
            }

            var when = file.When;
            var warning = when?.Warning;
            var checksum = when?.Checksum;
            var lead = when?.LeadInMod;
            var count = (warning is null ? 0 : 1) + (checksum is null ? 0 : 1) + (lead is null ? 0 : 1);
            if (count != 1)
            {
                throw new InvalidDataException("Hinweisregel " + number + " braucht in when genau eines von warning, checksum, leadInMod.");
            }

            if (warning is not null)
            {
                if (ParseName<SummaryWarningKind>(warning) is not { } kind)
                {
                    throw new InvalidDataException("Hinweisregel " + number + ": unbekannte Auffälligkeit (warning) " + warning);
                }

                rules.Add(new AdviceRule(AdviceTrigger.Warning, kind.ToString(), file.Text));
            }
            else if (checksum is not null)
            {
                if (string.IsNullOrWhiteSpace(checksum))
                {
                    throw new InvalidDataException("Hinweisregel " + number + ": checksum ist leer.");
                }

                rules.Add(new AdviceRule(AdviceTrigger.Checksum, checksum, file.Text));
            }
            else
            {
                if (lead != true)
                {
                    throw new InvalidDataException("Hinweisregel " + number + ": leadInMod muss true sein.");
                }

                rules.Add(new AdviceRule(AdviceTrigger.LeadInMod, null, file.Text));
            }
        }

        return rules;
    }

    /// <summary>Wert eines Enums aus seinem Namen (Groß-/Kleinschreibung egal); Zahlen und unbekannte Namen ergeben null.</summary>
    internal static T? ParseName<T>(string? name)
        where T : struct, Enum
    {
        foreach (var value in Enum.GetValues<T>())
        {
            if (string.Equals(value.ToString(), name, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    // Formen der JSON-Datei; die öffentlichen Typen oben tragen die ausgewerteten Werte.
    private sealed class ProfileFile
    {
        public int Version { get; set; }
        public int IdSlotBits { get; set; } = 24;
        public Dictionary<string, int>? IdSlotBitsOverride { get; set; }
        public Dictionary<string, ChecksumFile>? Checksums { get; set; }
        public List<string>? Objects { get; set; }
        public Dictionary<string, string>? AnonymousIds { get; set; }
        public long SizeCapBytes { get; set; } = 262144;
        public int NodeDepth { get; set; } = 4;
        public List<string>? Noise { get; set; }
        public List<string>? Consequence { get; set; }
        public double FloatDrift { get; set; } = 0.001;
        public Dictionary<string, NameFile>? Names { get; set; }
        public List<string>? LabelFields { get; set; }
        public ParadoxFormFile? ParadoxForm { get; set; }
        public List<AdviceFile?>? Advice { get; set; }
        public SafetyRules.SafetyFile? Safety { get; set; }
    }

    private sealed class ParadoxFormFile
    {
        public List<FormFieldFile?>? Fields { get; set; }
    }

    private sealed class FormFieldFile
    {
        public string? Label { get; set; }
        public string? Kind { get; set; }
        public string? Source { get; set; }
        public string? Value { get; set; }
    }

    private sealed class AdviceFile
    {
        public AdviceWhenFile? When { get; set; }
        public string? Text { get; set; }
    }

    private sealed class AdviceWhenFile
    {
        public string? Warning { get; set; }
        public string? Checksum { get; set; }
        public bool? LeadInMod { get; set; }
    }

    private sealed class ChecksumFile
    {
        public Dictionary<string, string>? Name { get; set; }
        public Dictionary<string, string>? Explain { get; set; }
        public List<Dictionary<string, string>>? Causes { get; set; }

        public ChecksumInfo ToInfo() => new(
            Name ?? new Dictionary<string, string>(),
            Explain ?? new Dictionary<string, string>(),
            (Causes ?? new List<Dictionary<string, string>>()).Select(cause => (IReadOnlyDictionary<string, string>)cause).ToList());
    }

    private sealed class NameFile
    {
        public string? TagList { get; set; }
        public string? KeyList { get; set; }
        public int KeyListBase { get; set; }
    }
}
