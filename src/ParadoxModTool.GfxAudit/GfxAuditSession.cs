using ParadoxModTool.FlagTracker;

namespace ParadoxModTool.GfxAudit;

/// <summary>
/// Alles, was ein Lauf braucht. Wird im Hintergrund-Task erzeugt (darf I/O machen). Notes: Statushinweise des Aufrufers.
/// </summary>
public sealed record GfxAuditRequest(FlagSourceSet Sources, GfxAuditRules Rules, GfxAuditStore Store, IReadOnlyList<string> Notes)
{
    /// <summary>Für Tests: liest den Text einer Datei (voller Pfad) statt der Platte; null = Standard.</summary>
    internal Func<string, string>? ReadText { get; init; }
}

/// <summary>Zustand der Sitzung: noch kein Ergebnis, Lauf läuft, Ergebnis bereit, letzter Lauf fehlgeschlagen.</summary>
public enum GfxAuditState
{
    Empty,
    Building,
    Ready,
    Failed,
}

/// <summary>
/// Hält das gezeigte Ergebnis der GFX-Prüfung und startet Läufe (Aufbau des Index und Auswertung im Hintergrund). Ein neuerer Lauf
/// verdrängt ältere: deren Ergebnis wird verworfen. Die Eigenschaften werden nur nach dem <c>await</c> auf dem Kontext des Aufrufers
/// gesetzt, nie im Hintergrund-Task. Muss von einem Thread mit single-threaded Synchronisationskontext (UI-Thread) aufgerufen werden;
/// ohne ihn liefen die Fortsetzungen im Thread-Pool. Ein neuerer Aufruf bricht den älteren nicht ab: Dessen
/// <see cref="CancellationToken"/> bricht der Aufrufer selbst ab.
/// </summary>
public sealed class GfxAuditSession
{
    private int _runId;

    public GfxAuditIndex? Index { get; private set; }

    public GfxAuditResult? Result { get; private set; }

    public GfxAuditState State { get; private set; } = GfxAuditState.Empty;

    /// <summary>Stand-Text oder Hinweis auf fehlgeschlagenen/abgebrochenen Lauf, dahinter die Hinweise (Notes) der Anfrage mit "; ".</summary>
    public string StatusText { get; private set; } = string.Empty;

    /// <summary>Nach jeder Zustandsänderung, auf dem Thread, der <see cref="RefreshAsync"/> erwartet hat.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Startet einen Lauf; ein neuerer Aufruf verdrängt ältere. Kehrt zurück, wenn dieser Lauf geendet hat, egal wie. Wirft nicht für
    /// Fehler und Abbruch des Laufs (sie landen in <see cref="State"/> und <see cref="StatusText"/>); Ausnahmen aus Handlern von
    /// <see cref="Changed"/> laufen dagegen zum Aufrufer durch.
    /// </summary>
    public async Task RefreshAsync(Func<GfxAuditRequest> createRequest, IProgress<BuildProgress>? progress, CancellationToken ct)
    {
        var run = Interlocked.Increment(ref _runId);
        State = GfxAuditState.Building;
        Changed?.Invoke(this, EventArgs.Empty);

        GfxAuditIndex index;
        GfxAuditResult result;
        GfxAuditRequest request;
        try
        {
            // Kein ConfigureAwait(false): die Fortsetzung soll auf dem Kontext des Aufrufers (UI-Thread) laufen.
            (index, result, request) = await Task.Run(
                () =>
                {
                    var r = createRequest();
                    var builder = r.ReadText is { } read ? new GfxAuditBuilder(r.Sources, r.Rules, read) : new GfxAuditBuilder(r.Sources, r.Rules);
                    var built = builder.Refresh(r.Store, ct, progress);
                    return (built, GfxAuditAnalyzer.Analyze(built, r.Rules), r);
                },
                ct);
        }
        catch (OperationCanceledException)
        {
            if (IsCurrent(run))
            {
                State = Index is null ? GfxAuditState.Empty : GfxAuditState.Ready;
                StatusText = GfxAuditTexts.Cancelled;
                Changed?.Invoke(this, EventArgs.Empty);
            }

            return;
        }
        catch (Exception ex)
        {
            if (IsCurrent(run))
            {
                State = GfxAuditState.Failed;
                StatusText = GfxAuditTexts.Failed(ex.Message);
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
        State = GfxAuditState.Ready;
        var parts = new List<string> { GfxAuditTexts.Status(index.Images.Count, index.Files.Count, index.BuiltUtc) };
        if (index.UnreadFiles.Count > 0)
        {
            parts.Add(GfxAuditTexts.Unread(index.UnreadFiles.Count));
        }

        parts.AddRange(request.Notes);
        StatusText = string.Join("; ", parts);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private bool IsCurrent(int run) => Volatile.Read(ref _runId) == run;
}
