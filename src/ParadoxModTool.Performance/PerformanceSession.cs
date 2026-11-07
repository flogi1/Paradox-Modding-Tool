using System.Globalization;
using System.Security.Cryptography;
using ParadoxModTool.ErrorLog;
using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.Performance;

/// <summary>
/// Die Läufe einer Mod für ein Spiel mit Auswahl (ein Lauf), Vergleich (A und B) und Rauschschwellen; das fensterlose Modell hinter dem
/// Performance-Fenster. Zusammenfassungen und Vergleich entstehen erst bei Bedarf und werden je Lauf-Id zwischengespeichert.
/// Geschrieben wird nur über den <see cref="PerformanceRunStore"/>.
/// Nicht threadsicher: Die App ändert die Sitzung nur im UI-Thread. <see cref="Changed"/> meldet jede Änderung.
/// </summary>
public sealed class PerformanceSession
{
    private readonly PerformanceRunStore _store;
    private readonly string _gameId;
    private readonly string _modKey;
    private readonly PerformanceSettings _settings;
    private readonly Func<OriginClassifier> _classifierFactory;
    private readonly Func<ErrorCatalog>? _catalogFactory;
    private readonly Dictionary<string, LoadedRun> _loaded = new(StringComparer.Ordinal);
    private OriginClassifier? _classifier;
    private IReadOnlyList<PerformanceRun> _runs = Array.Empty<PerformanceRun>();
    private NoiseSettings _noise;
    private string? _selectedId;
    private string? _aId;
    private string? _bId;
    private RunComparison? _comparison;
    private IReadOnlyList<string> _comparisonWarnings = Array.Empty<string>();

    /// <param name="classifierFactory">
    /// Liefert den Herkunfts-Klassifizierer; höchstens ein Aufruf je Sitzung, beim ersten lesbaren Dump. Alle Läufe teilen sich diesen
    /// Klassifizierer und damit seinen Zwischenspeicher je Pfad (die Herkunft hängt nur von der Mod der Sitzung ab, nicht vom Lauf).
    /// </param>
    /// <param name="catalogFactory">
    /// Liefert den Fehlerkatalog des Spiels für die Regeln, die das error.log eines Laufs auswerten; ohne ihn (<c>null</c>) laufen diese Regeln nicht.
    /// Aufruf nur, wenn Hinweise zu einem Lauf mit error.log berechnet werden, einmal je Lauf.
    /// </param>
    public PerformanceSession(
        PerformanceRunStore store,
        string gameId,
        string modKey,
        PerformanceSettings settings,
        Func<OriginClassifier> classifierFactory,
        NoiseSettings noise,
        Func<ErrorCatalog>? catalogFactory = null)
    {
        _store = store;
        _gameId = gameId;
        _modKey = modKey;
        _settings = settings;
        _classifierFactory = classifierFactory;
        _catalogFactory = catalogFactory;
        _noise = noise;
        Reload();
    }

    public event EventHandler? Changed;

    /// <summary>Die Läufe dieses Spiels und dieser Mod, neuester zuerst. Läufe anderer Spiele im selben Ordner fehlen.</summary>
    public IReadOnlyList<PerformanceRun> Runs => _runs;

    /// <summary>Rauschschwellen des Vergleichs; das Setzen baut einen vorhandenen Vergleich neu und meldet <see cref="Changed"/>.</summary>
    public NoiseSettings Noise
    {
        get => _noise;
        set
        {
            _noise = value;
            UpdateComparison(rebuild: true);
            RaiseChanged();
        }
    }

    /// <summary>Der gewählte Lauf, sonst <c>null</c>.</summary>
    public PerformanceRun? Selected => Find(_selectedId);

    /// <summary>Zusammenfassung des gewählten Laufs; <c>null</c> ohne Auswahl oder wenn der Dump nicht lesbar ist (dann steht der Grund in <see cref="SelectedWarnings"/>).</summary>
    public ProfilingSummary? SelectedSummary => Selected is { } run ? Load(run).Summary : null;

    /// <summary>Dauer des gewählten Laufs, sonst <c>null</c> wie bei <see cref="SelectedSummary"/>.</summary>
    public RunDuration? SelectedDuration => Selected is { } run ? Load(run).Duration : null;

    /// <summary>Warnungen des Dump-Lesers zum gewählten Lauf; bei nicht lesbarem Dump dessen Fehlermeldung.</summary>
    public IReadOnlyList<string> SelectedWarnings => Selected is { } run ? WarningsOf(Load(run)) : Array.Empty<string>();

    /// <summary>Der Herkunfts-Klassifizierer, mit dem <see cref="SelectedSummary"/> gebaut wurde (für den Sprung zur Moddatei); sonst <c>null</c> wie dort.</summary>
    public OriginClassifier? SelectedClassifier => Selected is { } run ? Load(run).Classifier : null;

    /// <summary>
    /// Hinweise der Regeln zum gewählten Lauf (<see cref="HintEngine.DefaultRules"/>); leer ohne Auswahl oder ohne lesbaren Dump. Sie entstehen erst beim
    /// ersten Lesen, gelten dann mit der Zusammenfassung des Laufs und fallen mit ihr weg (gleiche Liste bei jedem Lesen). Das error.log des Laufs fließt
    /// ein, wenn es existiert und die Sitzung einen Fehlerkatalog hat; ist es nicht lesbar, fehlen nur die Fehler-Regeln und <see cref="SelectedWarnings"/> nennt den Grund.
    /// </summary>
    public IReadOnlyList<PerformanceHint> SelectedHints => Selected is { } run ? HintsOf(run, Load(run)) : Array.Empty<PerformanceHint>();

    /// <summary>
    /// <see cref="PerformanceTexts.HintNoErrorLog"/>, wenn die Fehler-Regeln beim gewählten Lauf fehlen, weil er kein error.log hat (manuell importiert) oder
    /// die Sitzung keinen Fehlerkatalog kennt; sonst <c>null</c>, auch ohne Auswahl oder ohne lesbaren Dump.
    /// </summary>
    public string? SelectedHintsNote => Selected is { } run && Load(run).Summary is not null && ErrorLogOf(run) is null
        ? PerformanceTexts.HintNoErrorLog
        : null;

    public PerformanceRun? A => Find(_aId);

    public PerformanceRun? B => Find(_bId);

    /// <summary>Vergleich von <see cref="A"/> und <see cref="B"/>; <c>null</c>, solange eine Seite fehlt oder ihr Dump nicht lesbar ist.</summary>
    public RunComparison? Comparison => _comparison;

    /// <summary>
    /// Hinweise zum Vergleich (Texte aus <see cref="PerformanceTexts"/>): anderer Spielstand, andere Monatszahl, anderes Playset und geänderte
    /// Moddateien seit A. Unbekannte Werte werden null-sicher verglichen: zwei unbekannte Spielstände ergeben keinen Hinweis, ein bekannter und
    /// ein unbekannter schon. Ist ein Dump nicht lesbar, steht dessen Fehlermeldung hier.
    /// </summary>
    public IReadOnlyList<string> ComparisonWarnings => _comparisonWarnings;

    /// <summary>Liest die Läufe neu von der Platte. Läufe, die verschwunden sind, verlassen Auswahl, A und B sowie den Zwischenspeicher.</summary>
    public void Refresh()
    {
        Reload();
        RaiseChanged();
    }

    /// <summary>Wählt den Lauf; eine unbekannte Id (oder <c>null</c>) hebt die Auswahl auf.</summary>
    public void Select(string? runId)
    {
        _selectedId = Find(runId)?.Id;
        RaiseChanged();
    }

    /// <summary>Setzt A und B und baut den Vergleich. Eine unbekannte Id lässt die Seite leer; dann gibt es keinen Vergleich.</summary>
    public void Compare(string? runIdA, string? runIdB)
    {
        _aId = Find(runIdA)?.Id;
        _bId = Find(runIdB)?.Id;
        UpdateComparison(rebuild: true);
        RaiseChanged();
    }

    /// <summary>
    /// Legt den Lauf an (<see cref="PerformanceRunStore.Import"/>), zählt die Monate aus der Dump-Kopie und trägt sie als
    /// <see cref="PerformanceRun.CountedMonths"/> in <c>run.json</c> nach. Ist <see cref="PerformanceRun.Id"/> leer, vergibt die Sitzung sie:
    /// <c>yyyyMMdd-HHmmss</c> (aus <see cref="PerformanceRun.CreatedUtc"/>), ein <c>-</c> und vier zufällige Hex-Zeichen. Ein nicht lesbarer Dump
    /// lässt den Lauf ohne Monatszahl bestehen; der Grund erscheint bei <see cref="SelectedWarnings"/>, sobald er gewählt ist.
    /// </summary>
    /// <param name="measured">
    /// Der Dump entstand gerade mit dieser Mod (Standard): Die Mod kann seit der letzten Messung bearbeitet worden sein, deshalb wirft die Sitzung
    /// Klassifizierer und zwischengespeicherte Zusammenfassungen weg und baut sie bei Bedarf neu. Bei einem nachträglichen Import (<c>false</c>) bleiben sie erhalten.
    /// </param>
    /// <exception cref="IOException">Kopieren oder Schreiben scheitert, oder der Lauf existiert schon.</exception>
    public PerformanceRun Import(PerformanceRun run, string dumpPath, bool measured = true)
    {
        if (string.IsNullOrEmpty(run.Id))
        {
            run = run with { Id = NewRunId(run.CreatedUtc) };
        }

        var imported = _store.Import(run, dumpPath);
        var months = CountMonths(imported);
        if (months is not null && months != imported.CountedMonths)
        {
            imported = _store.Save(imported with { CountedMonths = months });
        }

        if (measured)
        {
            // Die Mod wurde zwischen den Messungen bearbeitet: Herkunftsklassifizierer und Zusammenfassungen und Vergleich (Herkunft je Pfad) sind veraltet.
            _classifier = null;
            _loaded.Clear();
            _comparison = null;
        }

        Reload();
        RaiseChanged();
        return imported;
    }

    public void Rename(string runId, string newName)
    {
        _store.Rename(_modKey, runId, newName);
        Reload();
        RaiseChanged();
    }

    /// <summary>Löscht den Lauf samt Dump-Kopie; war er gewählt oder A bzw. B, wird die Stelle leer.</summary>
    public void Delete(string runId)
    {
        _store.Delete(_modKey, runId);
        Reload();
        RaiseChanged();
    }

    /// <summary>
    /// Neue Lauf-Id wie bei <see cref="Import"/> mit leerer Id: <c>yyyyMMdd-HHmmss</c> aus <paramref name="createdUtc"/>, ein <c>-</c> und vier
    /// zufällige Hex-Zeichen. Wer die Id vorab vergibt, legt bei einem wiederholten Import keinen zweiten Lauf an (der Speicher meldet den Ordner).
    /// </summary>
    public static string NewRunId(DateTime createdUtc) =>
        createdUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToLowerInvariant();

    /// <summary>Die Hinweise (<see cref="Hints"/>, <see cref="HintWarning"/>) werden beim ersten Bedarf gesetzt; sie hängen am Eintrag und fallen mit ihm weg.</summary>
    private sealed record LoadedRun(ProfilingSummary? Summary, RunDuration? Duration, IReadOnlyList<string> Warnings, OriginClassifier? Classifier)
    {
        public IReadOnlyList<PerformanceHint>? Hints { get; set; }

        public string? HintWarning { get; set; }
    }

    private static IReadOnlyList<string> WarningsOf(LoadedRun loaded) =>
        loaded.HintWarning is null ? loaded.Warnings : loaded.Warnings.Append(loaded.HintWarning).ToList();

    /// <summary>Pfad des archivierten error.log, wenn die Fehler-Regeln laufen können (Datei da und Katalog vorhanden); sonst <c>null</c>.</summary>
    private string? ErrorLogOf(PerformanceRun run) =>
        _catalogFactory is null ? null : RunErrorLog.PathFor(_store.FolderOf(run.ModKey, run.Id));

    private IReadOnlyList<PerformanceHint> HintsOf(PerformanceRun run, LoadedRun loaded)
    {
        if (loaded.Hints is { } cached)
        {
            return cached;
        }

        if (loaded.Summary is not { } summary || loaded.Duration is not { } duration || loaded.Classifier is not { } classifier)
        {
            return Array.Empty<PerformanceHint>();
        }

        // SelectedHints wird auch aus Changed-Handlern gelesen (etwa während eines gemessenen Imports): Ein Fehler von Katalog oder Regel darf
        // nicht hinausfliegen. Dann bleibt die Liste leer (zwischengespeichert, kein neuer Versuch je Lesen) und SelectedWarnings nennt den Grund.
        try
        {
            ErrorHotspots? errors = null;
            if (ErrorLogOf(run) is { } logPath)
            {
                try
                {
                    errors = ErrorHotspots.Load(logPath, _catalogFactory!());
                }
                catch (Exception ex) when (IsReadError(ex))
                {
                    loaded.HintWarning = $"{RunErrorLog.FileName}: {ex.Message}";
                }
            }

            var context = new RuleContext(summary, duration, classifier, _settings, errors, path => ReadModFile(classifier, path), new RuleThresholds());
            loaded.Hints = HintEngine.Run(context, HintEngine.DefaultRules);
        }
        catch (Exception ex)
        {
            loaded.Hints = Array.Empty<PerformanceHint>();
            loaded.HintWarning = PerformanceTexts.HintComputeFailed(ex.Message);
        }

        return loaded.Hints;
    }

    private static string? ReadModFile(OriginClassifier classifier, string dumpPath)
    {
        try
        {
            return classifier.ResolveModFile(dumpPath) is { } file ? File.ReadAllText(file) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsReadError(Exception ex) => ex is ProfilingFormatException or IOException or UnauthorizedAccessException;

    private PerformanceRun? Find(string? runId) => runId is null ? null : _runs.FirstOrDefault(r => r.Id == runId);

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private void Reload()
    {
        _runs = _store.List(_modKey).Where(r => r.GameId == _gameId).ToList();
        var ids = _runs.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in _loaded.Keys.Where(id => !ids.Contains(id)).ToList())
        {
            _loaded.Remove(id);
        }

        _selectedId = Find(_selectedId)?.Id;
        _aId = Find(_aId)?.Id;
        _bId = Find(_bId)?.Id;
        UpdateComparison(rebuild: false);
    }

    private LoadedRun Load(PerformanceRun run)
    {
        if (_loaded.TryGetValue(run.Id, out var cached))
        {
            return cached;
        }

        LoadedRun loaded;
        try
        {
            var dump = ProfilingDumpReader.ReadFile(_store.DumpPathOf(run));
            var classifier = _classifier ??= _classifierFactory();
            loaded = new LoadedRun(new ProfilingSummary(dump, classifier), RunDuration.From(dump, _settings.MonthlyPulseRow), dump.Warnings, classifier);
        }
        catch (Exception ex) when (IsReadError(ex))
        {
            loaded = new LoadedRun(null, null, new[] { ex.Message }, null);
        }

        _loaded[run.Id] = loaded;
        return loaded;
    }

    /// <summary>Monate des Laufs aus der Dump-Kopie, ohne die (teure) Herkunftsprüfung der Zusammenfassung; <c>null</c> bei nicht lesbarem Dump.</summary>
    private int? CountMonths(PerformanceRun run)
    {
        try
        {
            return RunDuration.From(ProfilingDumpReader.ReadFile(_store.DumpPathOf(run)), _settings.MonthlyPulseRow).Months;
        }
        catch (Exception ex) when (IsReadError(ex))
        {
            return null;
        }
    }

    /// <summary>
    /// Stellt <see cref="Comparison"/> und <see cref="ComparisonWarnings"/> zu A und B her. Ohne <paramref name="rebuild"/> bleibt ein vorhandener
    /// Vergleich erhalten (die Dumps sind unveränderlich), nur die Hinweise werden aus den aktuellen Metadaten neu gebildet.
    /// </summary>
    private void UpdateComparison(bool rebuild)
    {
        if (A is not { } a || B is not { } b)
        {
            _comparison = null;
            _comparisonWarnings = Array.Empty<string>();
            return;
        }

        var loadedA = Load(a);
        var loadedB = Load(b);
        if (loadedA.Summary is null || loadedB.Summary is null)
        {
            _comparison = null;
            _comparisonWarnings = (loadedA.Summary is null ? loadedA.Warnings : Array.Empty<string>())
                .Concat(loadedB.Summary is null ? loadedB.Warnings : Array.Empty<string>()).ToList();
            return;
        }

        if (rebuild || _comparison is null)
        {
            _comparison = new RunComparison(loadedA.Summary, loadedB.Summary, loadedA.Duration!, loadedB.Duration!, _noise);
        }

        _comparisonWarnings = WarningsFor(a, b, _comparison);
    }

    private static IReadOnlyList<string> WarningsFor(PerformanceRun a, PerformanceRun b, RunComparison comparison)
    {
        var warnings = new List<string>();
        if (!SameName(a.SaveName, b.SaveName))
        {
            warnings.Add(PerformanceTexts.WarnSaveDiffers(a.SaveName, b.SaveName));
        }

        if (!comparison.SameMonths)
        {
            var (monthsA, monthsB) = (comparison.DurationA.Months, comparison.DurationB.Months);
            warnings.Add(monthsA is null || monthsB is null ? PerformanceTexts.MonthsUnknown : PerformanceTexts.MonthsDiffer(monthsA, monthsB));
        }

        if (!SameName(a.PlaysetName, b.PlaysetName))
        {
            warnings.Add(PerformanceTexts.WarnPlaysetDiffers(a.PlaysetName, b.PlaysetName));
        }

        if (!string.IsNullOrEmpty(a.ModFilesHash) && !string.IsNullOrEmpty(b.ModFilesHash) && a.ModFilesHash != b.ModFilesHash)
        {
            warnings.Add(PerformanceTexts.WarnModChangedSinceA);
        }

        return warnings;
    }

    /// <summary>Null und leer gelten als gleich (beides: unbekannt); sonst ohne Rücksicht auf Groß-/Kleinschreibung, wie Dateinamen unter Windows.</summary>
    private static bool SameName(string? x, string? y) =>
        string.Equals(string.IsNullOrEmpty(x) ? null : x, string.IsNullOrEmpty(y) ? null : y, StringComparison.OrdinalIgnoreCase);
}
