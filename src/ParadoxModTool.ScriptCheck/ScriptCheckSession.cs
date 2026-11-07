using ParadoxModTool.FlagTracker;

namespace ParadoxModTool.ScriptCheck;

/// <summary>
/// Alles, was ein Lauf braucht. Wird im Hintergrund-Task erzeugt (darf I/O machen). BuiltIns: eingebaute Effekt- und Triggernamen
/// (leer = unbekannt). Notes: Statushinweise des Aufrufers. Names: welche Namen erwähnt werden (fremde Ziele: alle). PerformanceOnly: nur die
/// Leistungsprüfung (<see cref="PerformanceAnalyzer"/>), ohne Hinweis auf fehlende eingebaute Befehle.
/// </summary>
public sealed record ScriptCheckRequest(
    FlagSourceSet Sources,
    ScriptCheckRules Rules,
    ScriptCheckStore Store,
    IReadOnlySet<string> BuiltIns,
    IReadOnlyList<string> Notes,
    ScriptCheckNames Names = ScriptCheckNames.Mod,
    bool PerformanceOnly = false);

/// <summary>Zustand der Sitzung: noch kein Ergebnis, Lauf läuft, Ergebnis bereit, letzter Lauf fehlgeschlagen.</summary>
public enum ScriptCheckState
{
    Empty,
    Building,
    Ready,
    Failed,
}

/// <summary>
/// Hält das gezeigte Ergebnis der Skriptprüfung und startet Läufe (Aufbau des Index und Auswertung im Hintergrund). Ein neuerer Lauf
/// verdrängt ältere: deren Ergebnis wird verworfen. Die Eigenschaften werden nur nach dem <c>await</c> auf dem Kontext des Aufrufers
/// gesetzt, nie im Hintergrund-Task. Muss von einem Thread mit single-threaded Synchronisationskontext (UI-Thread) aufgerufen werden;
/// ohne ihn liefen die Fortsetzungen im Thread-Pool. Ein neuerer Aufruf bricht den älteren nicht ab: Dessen
/// <see cref="CancellationToken"/> bricht der Aufrufer selbst ab.
/// </summary>
public sealed class ScriptCheckSession
{
    private int _runId;

    public ScriptCheckIndex? Index { get; private set; }

    public AnalyzerResult? Result { get; private set; }

    public ScriptCheckState State { get; private set; } = ScriptCheckState.Empty;

    /// <summary>Stand-Text oder Hinweis auf fehlgeschlagenen/abgebrochenen Lauf, dahinter die Hinweise (Notes) der Anfrage mit "; ".</summary>
    public string StatusText { get; private set; } = string.Empty;

    /// <summary>Nach jeder Zustandsänderung, auf dem Thread, der <see cref="RefreshAsync"/> erwartet hat.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Startet einen Lauf; ein neuerer Aufruf verdrängt ältere. Kehrt zurück, wenn dieser Lauf geendet hat, egal wie. Wirft nicht für
    /// Fehler und Abbruch des Laufs (sie landen in <see cref="State"/> und <see cref="StatusText"/>); Ausnahmen aus Handlern von
    /// <see cref="Changed"/> laufen dagegen zum Aufrufer durch.
    /// </summary>
    public async Task RefreshAsync(Func<ScriptCheckRequest> createRequest, IProgress<BuildProgress>? progress, CancellationToken ct)
    {
        var run = Interlocked.Increment(ref _runId);
        State = ScriptCheckState.Building;
        Changed?.Invoke(this, EventArgs.Empty);

        ScriptCheckIndex index;
        AnalyzerResult result;
        ScriptCheckRequest request;
        try
        {
            // Kein ConfigureAwait(false): die Fortsetzung soll auf dem Kontext des Aufrufers (UI-Thread) laufen.
            (index, result, request) = await Task.Run(
                () =>
                {
                    var r = createRequest();
                    var built = new ScriptCheckBuilder(r.Sources, r.Rules, r.Names).Refresh(r.Store, ct, progress);
                    var analyzed = r.PerformanceOnly
                        ? new AnalyzerResult(ScriptCheckAnalyzer.Sort(PerformanceAnalyzer.Analyze(built, r.Rules)), BlockReferencesChecked: true)
                        : ScriptCheckAnalyzer.Analyze(built, r.BuiltIns, r.Rules);
                    return (built, analyzed, r);
                },
                ct);
        }
        catch (OperationCanceledException)
        {
            if (IsCurrent(run))
            {
                State = Index is null ? ScriptCheckState.Empty : ScriptCheckState.Ready;
                StatusText = ScriptCheckTexts.StatusCancelled;
                Changed?.Invoke(this, EventArgs.Empty);
            }

            return;
        }
        catch (Exception ex)
        {
            if (IsCurrent(run))
            {
                State = ScriptCheckState.Failed;
                StatusText = ScriptCheckTexts.StatusFailed(ex.Message);
                Changed?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        if (!IsCurrent(run))
        {
            return;
        }

        Index = index;
        Result = result;
        State = ScriptCheckState.Ready;
        var parts = new List<string>
        {
            ScriptCheckTexts.Status(index.BuiltUtc.ToLocalTime(), index.FileCount, index.UnreadFiles.Count, result.Findings.Count),
        };
        if (index.ModRoot is null)
        {
            parts.Add(ScriptCheckTexts.NoActiveMod);
        }

        if (!result.BlockReferencesChecked)
        {
            parts.Add(ScriptCheckTexts.NoBuiltIns);
        }

        if (request.Rules.Performance is not { GlobalIterators.Count: > 0, PulseOnActions.Count: > 0 })
        {
            parts.Add(ScriptCheckTexts.NoPerformanceRules);
        }

        parts.AddRange(request.Notes);
        StatusText = string.Join("; ", parts);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Die Funde des Ergebnisses in dessen Reihenfolge; leer ohne Ergebnis. Ausgeblendete (<paramref name="hide"/>) fallen weg, außer bei
    /// <paramref name="showHidden"/>; <paramref name="kind"/> beschränkt auf eine Art (null = alle).
    /// </summary>
    public IReadOnlyList<Finding> Visible(ScriptCheckHideList? hide, bool showHidden, FindingKind? kind)
    {
        if (Result is null)
        {
            return Array.Empty<Finding>();
        }

        return Result.Findings
            .Where(f => (kind is null || f.Kind == kind) && (showHidden || hide is null || !hide.IsHidden(f)))
            .ToList();
    }

    private bool IsCurrent(int run) => Volatile.Read(ref _runId) == run;
}
