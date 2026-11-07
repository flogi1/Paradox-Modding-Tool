using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.Performance;

/// <summary>Schwellen der Regeln; die Vorgaben gelten, solange nichts anderes eingestellt ist.</summary>
/// <param name="ErrorSpamMin">Ab so vielen Fehlereinträgen an einer Stelle meldet <see cref="ErrorSpamRule"/> sie.</param>
/// <param name="ManyCallsPerYear">Mehr Aufrufe pro Spieljahr als diese meldet <see cref="ManyCallsRule"/>.</param>
/// <param name="FrequentTickMinTotal">Mindest-Gesamtzeit für Regeln zu häufig laufenden Blöcken.</param>
/// <param name="TopRows">So viele Dump-Zeilen und error.log-Stellen sehen die Regeln höchstens an.</param>
/// <param name="FrequentTickMaxPeriodDays">
/// <see cref="FrequentTickRule"/> warnt nur, wenn der wirksame Abstand (Tage je Tick mal <c>ai_tick_frequency</c>) kleiner ist.
/// </param>
public sealed record RuleThresholds(int ErrorSpamMin = 100, long ManyCallsPerYear = 100_000, double FrequentTickMinTotal = 0.5, int TopRows = 200, int FrequentTickMaxPeriodDays = 30);

/// <summary>Eine Stelle des error.log mit ihrer Anzahl Einträge.</summary>
/// <param name="PerMinute">Einträge pro Minute Spielsitzung; <c>null</c>, wenn die Sitzungslänge unbekannt oder kürzer als eine Minute ist.</param>
public sealed record ErrorPlace(string Path, int Line, int Count, double? PerMinute);

/// <summary>Eine Regel, die aus Dump und error.log Hinweise ableitet. Sie liest Dateien nur über <see cref="RuleContext.ReadFile"/>.</summary>
public interface IPerformanceRule
{
    string Id { get; }

    IEnumerable<PerformanceHint> Check(RuleContext context);
}

/// <summary>
/// Was die Regeln über einen Lauf wissen: die Hotspot-Zeilen der Mod-Seite, die Stellen des error.log und Lesezugriff auf Moddateien.
/// Entweder mit Dump (<see cref="HasDump"/>) oder nur aus einem error.log (<see cref="FromErrorLog"/>), dann bleiben die Dump-Regeln stumm.
/// </summary>
public sealed class RuleContext
{
    private readonly Func<string, string?> _readFile;
    private readonly Dictionary<string, string?> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Path, int Line), ScriptBlockInfo?> _blocks = new();

    /// <summary>Kontext mit Dump; die Stellen des error.log werden auf die Mod-Seite des Klassifizierers beschränkt.</summary>
    /// <param name="readFile">Liest eine Moddatei zum Dump-Pfad; <c>null</c>, wenn sie fehlt oder nicht lesbar ist (wirft nicht).</param>
    public RuleContext(
        ProfilingSummary summary,
        RunDuration duration,
        OriginClassifier classifier,
        PerformanceSettings? settings,
        ErrorHotspots? errors,
        Func<string, string?> readFile,
        RuleThresholds thresholds)
        : this(summary, duration, classifier, settings, errors, sessionLength: null, readFile, thresholds)
    {
    }

    private RuleContext(
        ProfilingSummary? summary,
        RunDuration? duration,
        OriginClassifier? classifier,
        PerformanceSettings? settings,
        ErrorHotspots? errors,
        TimeSpan? sessionLength,
        Func<string, string?> readFile,
        RuleThresholds thresholds)
    {
        Summary = summary;
        Duration = duration;
        Classifier = classifier;
        Settings = settings;
        Errors = errors;
        SessionLength = sessionLength;
        Thresholds = thresholds;
        _readFile = readFile;
        Rows = summary?.Rank(RankBy.Total, OriginClassifier.ModSide, thresholds.TopRows) ?? Array.Empty<RankedRow>();
        ErrorPlaces = errors is null ? Array.Empty<ErrorPlace>() : PlacesOf(errors, classifier, sessionLength, thresholds.TopRows);
    }

    /// <summary>Kontext nur aus einem error.log (jedes Spiel mit Fehlerkatalog, auch ohne Profiler): keine Zeilen, keine Monate, alle Stellen.</summary>
    /// <param name="sessionLength">Länge der Spielsitzung für <see cref="ErrorPlace.PerMinute"/>; <c>null</c>, wenn unbekannt.</param>
    public static RuleContext FromErrorLog(ErrorHotspots errors, TimeSpan? sessionLength, Func<string, string?> readFile, PerformanceSettings? settings, RuleThresholds thresholds) =>
        new(null, null, null, settings, errors, sessionLength, readFile, thresholds);

    /// <summary>Zusammenfassung des Dumps; <c>null</c> im Nur-Log-Modus.</summary>
    public ProfilingSummary? Summary { get; }

    /// <summary>Dauer des Laufs; <c>null</c> im Nur-Log-Modus.</summary>
    public RunDuration? Duration { get; }

    /// <summary>Herkunft der Dateien; <c>null</c> im Nur-Log-Modus.</summary>
    public OriginClassifier? Classifier { get; }

    /// <summary>Profiler-Einstellungen des Spiels; kann fehlen.</summary>
    public PerformanceSettings? Settings { get; }

    /// <summary>Die Fehler des error.log; <c>null</c>, wenn der Lauf keines hat.</summary>
    public ErrorHotspots? Errors { get; }

    /// <summary>Länge der Spielsitzung; mit Dump immer <c>null</c>.</summary>
    public TimeSpan? SessionLength { get; }

    public RuleThresholds Thresholds { get; }

    /// <summary>False im Nur-Log-Modus: Regeln, die Total, Calls oder Herkunft brauchen, liefern dann nichts.</summary>
    public bool HasDump => Summary is not null;

    /// <summary>Die Zeilen der Mod-Seite, absteigend nach Gesamtzeit, höchstens <see cref="RuleThresholds.TopRows"/>; leer im Nur-Log-Modus.</summary>
    public IReadOnlyList<RankedRow> Rows { get; }

    /// <summary>
    /// Die Stellen des error.log nach Anzahl absteigend (dann Pfad und Zeile ordinal), höchstens <see cref="RuleThresholds.TopRows"/>;
    /// mit Klassifizierer nur Stellen der Mod-Seite. Leer ohne error.log.
    /// </summary>
    public IReadOnlyList<ErrorPlace> ErrorPlaces { get; }

    /// <summary>Monate des Laufs; <c>null</c>, wenn unbekannt oder im Nur-Log-Modus.</summary>
    public int? Months => Duration?.Months;

    public bool HasErrorLog => Errors is not null;

    /// <summary>Inhalt der Moddatei zum Dump-Pfad; je Pfad zwischengespeichert (auch ein Fehlschlag).</summary>
    public string? ReadFile(string dumpPath)
    {
        if (!_files.TryGetValue(dumpPath, out var text))
        {
            text = _readFile(dumpPath);
            _files[dumpPath] = text;
        }

        return text;
    }

    /// <summary>Der Block auf oberster Ebene um die Zeile (<see cref="ScriptBlockLocator.Locate"/>); <c>null</c> ohne lesbare Datei. Je Stelle zwischengespeichert.</summary>
    public ScriptBlockInfo? BlockAt(string dumpPath, int line)
    {
        if (!_blocks.TryGetValue((dumpPath, line), out var block))
        {
            block = ReadFile(dumpPath) is { } text ? ScriptBlockLocator.Locate(text, line) : null;
            _blocks[(dumpPath, line)] = block;
        }

        return block;
    }

    // Erst auf die Mod-Seite filtern, dann kürzen: sonst verdrängt Vanilla-Spam die Stellen der Mod.
    private static IReadOnlyList<ErrorPlace> PlacesOf(ErrorHotspots errors, OriginClassifier? classifier, TimeSpan? sessionLength, int top)
    {
        var minutes = sessionLength is { TotalMinutes: >= 1 } length ? length.TotalMinutes : (double?)null;
        return errors.Places()
            .Where(p => classifier is null || OriginClassifier.IsModSide(classifier.Classify(p.Path)))
            .OrderByDescending(p => p.Count)
            .ThenBy(p => p.Path, StringComparer.Ordinal)
            .ThenBy(p => p.Line)
            .Take(Math.Max(top, 0))
            .Select(p => new ErrorPlace(p.Path, p.Line, p.Count, minutes is { } m ? p.Count / m : null))
            .ToList();
    }
}
