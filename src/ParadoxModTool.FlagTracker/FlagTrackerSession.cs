namespace ParadoxModTool.FlagTracker;

/// <summary>
/// Alles, was ein Aufbau braucht. Wird im Hintergrund-Task erzeugt (darf I/O machen). Notes: Statushinweise wie
/// <see cref="FlagTrackerTexts.NoActiveMod"/> oder <see cref="FlagTrackerTexts.NoGameFolder"/>.
/// </summary>
public sealed record FlagTrackerRequest(FlagSourceSet Sources, FlagCommandTable Table, FlagIndexStore Store, IReadOnlyList<string> Notes);

/// <summary>Zustand der Sitzung: noch kein Index, Aufbau läuft, Index bereit, letzter Aufbau fehlgeschlagen.</summary>
public enum FlagTrackerState
{
    Empty,
    Building,
    Ready,
    Failed,
}

/// <summary>
/// Hält den gezeigten <see cref="FlagIndex"/> und startet Aufbauten. Ein neuerer Aufbau verdrängt ältere: deren Ergebnis wird verworfen.
/// Die Eigenschaften werden nur nach dem <c>await</c> auf dem Kontext des Aufrufers gesetzt, nie im Hintergrund-Task; so kann die
/// Oberfläche sie während eines Aufbaus lesen. Muss von einem Thread mit single-threaded Synchronisationskontext (UI-Thread) aufgerufen
/// werden; ohne ihn liefen die Fortsetzungen im Thread-Pool. Ein neuerer Aufruf bricht den älteren nicht ab: Dessen
/// <see cref="CancellationToken"/> bricht der Aufrufer selbst ab.
/// </summary>
public sealed class FlagTrackerSession
{
    private int _runId;

    /// <summary>Warnliste ohne ausgeblendete Namen, zwischengespeichert je Index, Regelliste und deren Version (Suche und Zähler teilen sie).</summary>
    private FlagSearchResult? _visibleWarnings;

    private FlagIndex? _visibleIndex;

    private FlagHideList? _visibleHide;

    private int _visibleVersion;

    public FlagIndex? Index { get; private set; }

    public FlagTrackerState State { get; private set; } = FlagTrackerState.Empty;

    /// <summary>Stand-Text oder Hinweis auf fehlenden/fehlgeschlagenen/abgebrochenen Aufbau, dahinter die Notes der Anfrage mit "; ".</summary>
    public string StatusText { get; private set; } = FlagTrackerTexts.StatusNoIndex;

    /// <summary>Bereich des gezeigten Index: true für „Playset“, false für „Alle installierten Mods“.</summary>
    public bool IsPlayset { get; private set; }

    /// <summary>Nach jeder Zustandsänderung, auf dem Thread, der <see cref="RefreshAsync"/> erwartet hat.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Startet einen Aufbau; ein neuerer Aufruf verdrängt ältere (deren Ergebnis wird verworfen). Kehrt zurück, wenn dieser Aufbau
    /// geendet hat, egal wie. Wirft nicht für Fehler und Abbruch des Aufbaus (sie landen in <see cref="State"/> und <see cref="StatusText"/>); Ausnahmen aus
    /// Handlern von <see cref="Changed"/> laufen dagegen zum Aufrufer durch.
    /// </summary>
    public async Task RefreshAsync(Func<FlagTrackerRequest> createRequest, IProgress<BuildProgress>? progress, CancellationToken ct)
    {
        var run = Interlocked.Increment(ref _runId);
        State = FlagTrackerState.Building;
        Changed?.Invoke(this, EventArgs.Empty);

        FlagIndex index;
        FlagTrackerRequest request;
        try
        {
            // Kein ConfigureAwait(false): die Fortsetzung soll auf dem Kontext des Aufrufers (UI-Thread) laufen.
            (index, request) = await Task.Run(
                () =>
                {
                    var r = createRequest();
                    return (new FlagIndexBuilder(r.Sources, r.Table).Refresh(r.Store, ct, progress), r);
                },
                ct);
        }
        catch (OperationCanceledException)
        {
            if (IsCurrent(run))
            {
                State = Index is null ? FlagTrackerState.Empty : FlagTrackerState.Ready;
                StatusText = Index is null ? FlagTrackerTexts.StatusCancelledNoIndex : FlagTrackerTexts.StatusCancelled;
                Changed?.Invoke(this, EventArgs.Empty);
            }

            return;
        }
        catch (Exception ex)
        {
            if (IsCurrent(run))
            {
                State = FlagTrackerState.Failed;
                StatusText = FlagTrackerTexts.StatusFailed(ex.Message);
                Changed?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        if (!IsCurrent(run))
        {
            return;
        }

        Index = index;
        IsPlayset = request.Sources.IsPlayset;
        State = FlagTrackerState.Ready;
        var status = FlagTrackerTexts.Status(index.BuiltUtc.ToLocalTime(), index.FileCount, index.UnreadFiles.Count);
        StatusText = request.Notes.Count == 0 ? status : status + "; " + string.Join("; ", request.Notes);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Sucht im gezeigten Index; <see cref="FlagTrackerView.Empty"/>, solange es keinen gibt. Leere Suche zeigt alle Namen mit Warnung
    /// (<see cref="FlagIndex.Warnings"/>); davon fallen die ausgeblendeten weg, außer bei <paramref name="showHidden"/>. Eine gezielte Suche
    /// zeigt alle Treffer, ausgeblendete mit Vermerk.
    /// </summary>
    /// <param name="hide">Ausblendregeln der aktiven Mod; null ohne aktive Mod.</param>
    public FlagTrackerView Search(string query, FlagHideList? hide = null, bool showHidden = false)
    {
        if (Index is null)
        {
            return FlagTrackerView.Empty;
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            return FlagTrackerView.From(Index.Search(query), hide);
        }

        // Ausblenden vor der Grenze von 5000 Namen: Die Warnliste füllt sich mit sichtbaren Namen.
        return showHidden || hide is null || hide.Rules.Count == 0
            ? FlagTrackerView.From(Index.Warnings(), hide)
            : FlagTrackerView.From(VisibleWarnings(Index, hide), hide);
    }

    /// <summary>
    /// Anzahl aller Warnungen (auch hinter der Grenze von 5000), auf die eine Ausblendregel passt; 0 ohne Index oder ohne Regeln.
    /// Teilt den Zwischenspeicher mit der leeren Suche (ein Durchlauf je Index, Regelliste und <see cref="FlagHideList.Version"/>).
    /// </summary>
    public int HiddenWarningCount(FlagHideList? hide) =>
        Index is null || hide is null || hide.Rules.Count == 0 ? 0 : VisibleWarnings(Index, hide).SkippedCount;

    private FlagSearchResult VisibleWarnings(FlagIndex index, FlagHideList hide)
    {
        if (_visibleWarnings is null || !ReferenceEquals(_visibleIndex, index) || !ReferenceEquals(_visibleHide, hide) || _visibleVersion != hide.Version)
        {
            _visibleWarnings = index.Warnings(hide.IsHidden);
            _visibleIndex = index;
            _visibleHide = hide;
            _visibleVersion = hide.Version;
        }

        return _visibleWarnings;
    }

    private bool IsCurrent(int run) => Volatile.Read(ref _runId) == run;
}
