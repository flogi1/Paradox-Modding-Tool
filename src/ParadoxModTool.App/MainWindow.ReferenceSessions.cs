using ParadoxModTool.FlagTracker;
using ParadoxModTool.ReferenceTree;
using ParadoxModTool.ScriptCheck;

namespace ParadoxModTool.App;

/// <summary>
/// Verweisbaum, Teil Sitzungen: Abruf aus dem geteilten Register (der Baum ist dort ein eigener Nutzer), Warten auf einen laufenden Aufbau,
/// Freigabe beim Zurücksetzen und Beobachten auf neue Daten. Der Baum bricht nie einen Aufbau ab und ruft während eines Aufbaus nie
/// <c>RefreshAsync</c> auf.
/// </summary>
public partial class MainWindow
{
    /// <summary>Gemeinsame Sitzungen von Flag-Tracker, Skript-Prüfung und Verweisbaum (ein Register je Hauptfenster, also je Spiel).</summary>
    private readonly SharedSessions _sharedSessions = new();

    /// <summary>Nutzer-Kennung des Baums im Register (die Fenster melden sich mit sich selbst an).</summary>
    private readonly object _referenceTreeUser = new();

    /// <summary>Vom Baum genutzte Sitzungen und die Indizes, die er zeigt (für den Hinweis "Daten aktualisiert").</summary>
    private FlagTrackerSession? _referenceFlags;
    private FlagIndex? _referenceFlagIndex;
    private ScriptCheckSession? _referenceChecks;
    private ScriptCheckIndex? _referenceCheckIndex;

    /// <summary>Der Baum nutzt keine Sitzung mehr (Reset, kein Werkzeug, keine Mod): beobachten beenden und im Register freigeben.</summary>
    private void ReleaseReferenceSessions()
    {
        WatchReferenceSessions(null, null);
        _sharedSessions.Release(_referenceTreeUser);
    }

    /// <summary>
    /// Sitzung für den Baum: bei Bedarf eigener Aufbau, sonst auf einen laufenden warten. Endet ein fremder Aufbau abgebrochen ohne Index
    /// (Sitzung leer, Status "abgebrochen"), startet der Baum seinen eigenen wie beim ersten Mal, statt das fremde "abgebrochen" zu zeigen;
    /// mit Index (bereit) nimmt er den Index. Der Baum selbst bricht nie ab. <paramref name="wanted"/> false (Schlüssel gewechselt): aufhören.
    /// </summary>
    private async Task<FlagTrackerSession?> LoadFlagSessionAsync(FlagTrackerWindowHost host, string mod, string? playset, bool reload, IProgress<BuildProgress> progress, Func<bool> wanted)
    {
        var session = _sharedSessions.Flags(SharedSessions.FlagKey(mod, playset, isPlayset: true), _referenceTreeUser);
        for (var firstPass = true; firstPass || wanted(); firstPass = false)
        {
            if (ReferenceTreeRules.StartsOwnBuild(session.State == FlagTrackerState.Building, session.State == FlagTrackerState.Empty,
                    session.State == FlagTrackerState.Failed, reload, firstPass))
            {
                // Wie FlagTrackerWindow.CreateRequestFactory: Tabelle mit den Namen aus den ScriptDocs (gleicher Fingerabdruck wie im Fenster).
                var table = FlagCommandTable.For(host.GameId, host.ScriptDocNames());
                await session.RefreshAsync(AnalysisRequests.Flags(host, isPlayset: true, playset, mod, table), progress, CancellationToken.None);
            }

            // Hat ein Fenster den Aufbau verdrängt, läuft er noch: auf dessen Ende warten.
            await WhenSettledAsync(() => session.State == FlagTrackerState.Building, h => session.Changed += h, h => session.Changed -= h);
            if (session.State != FlagTrackerState.Empty)
            {
                break;
            }
        }

        return session;
    }

    /// <summary>Wie <see cref="LoadFlagSessionAsync"/> für die Skript-Prüfung.</summary>
    private async Task<ScriptCheckSession?> LoadCheckSessionAsync(ScriptCheckWindowHost host, string mod, string? playset, bool reload, IProgress<BuildProgress> progress, Func<bool> wanted)
    {
        var session = _sharedSessions.Checks(SharedSessions.CheckKey(mod, playset), _referenceTreeUser);
        for (var firstPass = true; firstPass || wanted(); firstPass = false)
        {
            if (ReferenceTreeRules.StartsOwnBuild(session.State == ScriptCheckState.Building, session.State == ScriptCheckState.Empty,
                    session.State == ScriptCheckState.Failed, reload, firstPass))
            {
                await session.RefreshAsync(AnalysisRequests.Checks(host, mod, playset), progress, CancellationToken.None);
            }

            await WhenSettledAsync(() => session.State == ScriptCheckState.Building, h => session.Changed += h, h => session.Changed -= h);
            if (session.State != ScriptCheckState.Empty)
            {
                break;
            }
        }

        return session;
    }

    /// <summary>Wartet, bis <paramref name="building"/> nicht mehr gilt (geprüft bei jedem <c>Changed</c> der Sitzung).</summary>
    private static Task WhenSettledAsync(Func<bool> building, Action<EventHandler> subscribe, Action<EventHandler> unsubscribe)
    {
        if (!building())
        {
            return Task.CompletedTask;
        }

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            if (!building())
            {
                unsubscribe(handler!);
                done.TrySetResult();
            }
        };
        subscribe(handler);
        return done.Task;
    }

    /// <summary>Beobachtet die vom Baum genutzten Sitzungen (null: keine); ein neuer Index darunter zeigt "Daten aktualisiert".</summary>
    private void WatchReferenceSessions(FlagTrackerSession? flags, ScriptCheckSession? checks)
    {
        if (_referenceFlags is not null)
        {
            _referenceFlags.Changed -= ReferenceSession_Changed;
        }

        if (_referenceChecks is not null)
        {
            _referenceChecks.Changed -= ReferenceSession_Changed;
        }

        _referenceFlags = flags;
        _referenceFlagIndex = flags?.Index;
        _referenceChecks = checks;
        _referenceCheckIndex = checks?.Index;
        if (flags is not null)
        {
            flags.Changed += ReferenceSession_Changed;
        }

        if (checks is not null)
        {
            checks.Changed += ReferenceSession_Changed;
        }
    }

    private void ReferenceSession_Changed(object? sender, EventArgs e)
    {
        if ((_referenceFlags?.Index is { } flags && !ReferenceEquals(flags, _referenceFlagIndex))
            || (_referenceChecks?.Index is { } checks && !ReferenceEquals(checks, _referenceCheckIndex)))
        {
            References.MarkDataUpdated();
        }
    }
}
