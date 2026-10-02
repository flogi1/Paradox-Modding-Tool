using System.Text.Json;
using System.Text.RegularExpressions;

namespace ParadoxModTool.ErrorLog;

public enum ErrorSeverity
{
    Error,
    Warning,
    Info,
}

public static class LocalizedText
{
    /// <summary>Text in der Sprache, sonst Englisch, sonst Deutsch, sonst der erste vorhandene. Null, wenn keiner vorhanden ist.</summary>
    public static string? Pick(IReadOnlyDictionary<string, string> texts, string language) =>
        texts.TryGetValue(language, out var text) ? text
        : texts.TryGetValue("en", out var english) ? english
        : texts.TryGetValue("de", out var german) ? german
        : texts.Values.FirstOrDefault();
}

/// <summary>Eine Fehlerart aus dem Katalog.</summary>
public sealed class ErrorKind
{
    public required string Code { get; init; }

    public required Regex Pattern { get; init; }

    public ErrorSeverity Severity { get; init; }

    /// <summary>Die Meldung gehört zur vorigen Meldung und liefert nur einen Kandidaten (z.B. "Possible match").</summary>
    public bool AttachToPrevious { get; init; }

    public bool DefaultSuppressed { get; init; }

    public IReadOnlyDictionary<string, string> Names { get; init; } = new Dictionary<string, string>();

    public IReadOnlyDictionary<string, string> Explain { get; init; } = new Dictionary<string, string>();

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Tips { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Namen der Hinweise und Fix-Bausteine, z.B. "jump", "findDefinition", "replaceWithCandidate".</summary>
    public IReadOnlyList<string> Fixes { get; init; } = Array.Empty<string>();

    /// <summary>Spielstand-Prüfung (<c>saveCheck</c>): welche Namen im Stand gesucht werden, <c>variable</c> oder <c>flag</c>; null = keine.</summary>
    public string? SaveNames { get; init; }

    /// <summary>Effekte, die "auskommentieren" (<c>commentOutSet</c>) bearbeitet, z.B. <c>set_variable</c>.</summary>
    public IReadOnlyList<string> SetEffects { get; init; } = Array.Empty<string>();

    /// <summary>Scope-Verfolgung: Bedingungs-Vorlagen je Fehlertext (<see cref="TraceGuard"/>); leer = Spur ohne Bedingung.</summary>
    public IReadOnlyList<TraceGuard> TraceGuards { get; init; } = Array.Empty<TraceGuard>();

    public string NameIn(string language) => LocalizedText.Pick(Names, language) ?? Code;

    public string ExplainIn(string language) => LocalizedText.Pick(Explain, language) ?? string.Empty;

    public IReadOnlyList<string> TipsIn(string language) =>
        Tips.TryGetValue(language, out var tips) ? tips
        : Tips.TryGetValue("en", out var english) ? english
        : Tips.TryGetValue("de", out var german) ? german
        : Tips.Values.FirstOrDefault() ?? Array.Empty<string>();
}

public enum CatalogProblemKind
{
    FileMissing,
    InvalidJson,
    EntryWithoutCodeOrPattern,
    InvalidPattern,
    UnknownSeverity,
}

/// <summary>Problem beim Laden des Katalogs. <c>Detail</c> ist Pfad, Fehlermeldung oder Wert, kein fertiger Oberflächentext.</summary>
public sealed record CatalogProblem(CatalogProblemKind Kind, string? Code, string Detail);

/// <summary>
/// Fehlerkatalog eines Spiels (Datei im Profilordner, Verweis über <c>errorLogRules</c>). Muster prüfen nur den Meldungstext; die erste
/// passende Fehlerart gewinnt, daher stehen allgemeine Muster (z.B. GUI-000) hinter den speziellen. Ungültige Einträge werden übersprungen
/// und in <see cref="Problems"/> gemeldet, der Rest gilt weiter.
/// </summary>
public sealed class ErrorCatalog
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private ErrorCatalog(IReadOnlyList<ErrorKind> kinds, IReadOnlyList<CatalogProblem> problems, TraceRules? trace = null)
    {
        Kinds = kinds;
        Problems = problems;
        Trace = trace ?? TraceRules.Empty;
    }

    public static ErrorCatalog Empty { get; } = new(Array.Empty<ErrorKind>(), Array.Empty<CatalogProblem>());

    public IReadOnlyList<ErrorKind> Kinds { get; }

    public IReadOnlyList<CatalogProblem> Problems { get; }

    /// <summary>Regeln der Scope-Verfolgung (Abschnitt <c>trace</c>); <see cref="TraceRules.Empty"/>, wenn der Katalog keinen hat.</summary>
    public TraceRules Trace { get; }

    public static string? PathFor(string profilesDirectory, string? errorLogRules) =>
        string.IsNullOrWhiteSpace(errorLogRules) ? null : Path.Combine(profilesDirectory, errorLogRules);

    public static ErrorCatalog Load(string path)
    {
        if (!File.Exists(path))
        {
            return new ErrorCatalog(Array.Empty<ErrorKind>(), new[] { new CatalogProblem(CatalogProblemKind.FileMissing, null, path) });
        }

        return Parse(File.ReadAllText(path));
    }

    public static ErrorCatalog Parse(string json)
    {
        CatalogFile? file;
        try
        {
            file = JsonSerializer.Deserialize<CatalogFile>(json, Options);
        }
        catch (JsonException ex)
        {
            return new ErrorCatalog(Array.Empty<ErrorKind>(), new[] { new CatalogProblem(CatalogProblemKind.InvalidJson, null, ex.Message) });
        }

        var kinds = new List<ErrorKind>();
        var problems = new List<CatalogProblem>();
        foreach (var entry in file?.Entries ?? new List<CatalogEntry>())
        {
            if (string.IsNullOrWhiteSpace(entry.Code) || string.IsNullOrWhiteSpace(entry.Pattern))
            {
                problems.Add(new CatalogProblem(CatalogProblemKind.EntryWithoutCodeOrPattern, entry.Code, entry.Pattern));
                continue;
            }

            Regex pattern;
            try
            {
                pattern = new Regex(entry.Pattern, RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromMilliseconds(250));
            }
            catch (ArgumentException ex)
            {
                problems.Add(new CatalogProblem(CatalogProblemKind.InvalidPattern, entry.Code, ex.Message));
                continue;
            }

            var severity = ErrorSeverity.Error;
            if (entry.Severity is not null && !Enum.TryParse(entry.Severity, ignoreCase: true, out severity))
            {
                severity = ErrorSeverity.Error;
                problems.Add(new CatalogProblem(CatalogProblemKind.UnknownSeverity, entry.Code, entry.Severity));
            }

            kinds.Add(new ErrorKind
            {
                Code = entry.Code,
                Pattern = pattern,
                Severity = severity,
                AttachToPrevious = entry.AttachToPrevious,
                DefaultSuppressed = entry.DefaultSuppressed,
                Names = entry.Name ?? new Dictionary<string, string>(),
                Explain = entry.Explain ?? new Dictionary<string, string>(),
                Tips = (entry.Tips ?? new Dictionary<string, List<string>>()).ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value),
                Fixes = entry.Fixes ?? new List<string>(),
                SaveNames = entry.SaveNames,
                SetEffects = entry.SetEffects ?? new List<string>(),
                TraceGuards = Guards(entry, problems),
            });
        }

        return new ErrorCatalog(kinds, problems, file?.Trace is { } trace ? Rules(trace) : null);
    }

    public ErrorKind? Find(string code) => Kinds.FirstOrDefault(k => k.Code == code);

    public ErrorKind? Match(string message, out Match? match)
    {
        foreach (var kind in Kinds)
        {
            try
            {
                var candidate = kind.Pattern.Match(message);
                if (candidate.Success)
                {
                    match = candidate;
                    return kind;
                }
            }
            catch (RegexMatchTimeoutException)
            {
            }
        }

        match = null;
        return null;
    }

    private static List<TraceGuard> Guards(CatalogEntry entry, List<CatalogProblem> problems)
    {
        var guards = new List<TraceGuard>();
        foreach (var guard in entry.TraceGuards ?? new List<CatalogGuard>())
        {
            try
            {
                guards.Add(new TraceGuard(new Regex(guard.Detail, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)), guard.Limit));
            }
            catch (ArgumentException ex)
            {
                problems.Add(new CatalogProblem(CatalogProblemKind.InvalidPattern, entry.Code, ex.Message));
            }
        }

        return guards;
    }

    private static TraceRules Rules(CatalogTrace trace) => new(
        trace.EffectBlocks ?? new List<string>(),
        trace.TriggerBlocks ?? new List<string>(),
        trace.EffectFolders ?? new List<string>(),
        trace.IfChain ?? new List<string>(),
        trace.DebugLogSource ?? string.Empty,
        string.IsNullOrWhiteSpace(trace.DebugScopeSource) ? null : trace.DebugScopeSource);

    private sealed class CatalogFile
    {
        public int Version { get; set; } = 1;

        public CatalogTrace? Trace { get; set; }

        public List<CatalogEntry> Entries { get; set; } = new();
    }

    private sealed class CatalogEntry
    {
        public string Code { get; set; } = string.Empty;

        public Dictionary<string, string>? Name { get; set; }

        public string? Severity { get; set; }

        public string Pattern { get; set; } = string.Empty;

        public bool AttachToPrevious { get; set; }

        public bool DefaultSuppressed { get; set; }

        public Dictionary<string, string>? Explain { get; set; }

        public Dictionary<string, List<string>>? Tips { get; set; }

        public List<string>? Fixes { get; set; }

        public string? SaveNames { get; set; }

        public List<string>? SetEffects { get; set; }

        public List<CatalogGuard>? TraceGuards { get; set; }
    }

    private sealed class CatalogGuard
    {
        public string Detail { get; set; } = string.Empty;

        public string Limit { get; set; } = string.Empty;
    }

    private sealed class CatalogTrace
    {
        public List<string>? EffectBlocks { get; set; }

        public List<string>? TriggerBlocks { get; set; }

        public List<string>? EffectFolders { get; set; }

        public List<string>? IfChain { get; set; }

        public string? DebugLogSource { get; set; }

        public string? DebugScopeSource { get; set; }

        public string? Source { get; set; }
    }
}
