using System.IO;
using System.Net.Http;
using System.Windows;
using ParadoxModTool.BugTracking;
using ParadoxModTool.GitIntegration;
using ParadoxModTool.ModCompat;
using ParadoxModTool.Mods;
using ParadoxModTool.Notes;
using ParadoxModTool.Settings;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Bugreports aus externen Quellen (Plan 2: GitHub): stiller Abruf beim Wechsel der aktiven Mod und beim Öffnen des Bugtrackers (höchstens einmal je
/// Minute und Projekt, Fehler nur als Warnzeichen am Knopf), manuelles Aktualisieren mit Fortschrittsfenster, Status über den Dienst.
/// </summary>
public partial class MainWindow
{
    private static readonly TimeSpan BugRefreshInterval = TimeSpan.FromMinutes(1);

    private readonly IProcessRunner _bugProcessRunner = new ProcessRunner();
    private readonly Dictionary<string, DateTime> _lastBugRefresh = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _bugRefreshErrors = new(StringComparer.Ordinal);
    private readonly HashSet<string> _bugRefreshRunning = new(StringComparer.Ordinal);

    /// <summary>Aktives Fenster des Programms (z. B. der Bugtracker) als Eigentümer von Meldungen, sonst das Hauptfenster.</summary>
    private Window DialogOwner => System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive) ?? this;

    /// <summary>Ein HttpClient für alle Discord-Aufrufe (HttpClient ist für Wiederverwendung gedacht).</summary>
    private static readonly HttpClient DiscordHttp = new() { Timeout = TimeSpan.FromSeconds(30) };

    private static string DiscordTokenPath => DiscordTokenStore.GetDefaultPath();

    /// <summary>Discord-Client mit dem gespeicherten Token; <see langword="null"/> ohne Token.</summary>
    private DiscordClient? CreateDiscordClient() => DiscordTokenStore.Load(DiscordTokenPath) is { } token ? new DiscordClient(DiscordHttp, token) : null;

    private string? FindGhPath() =>
        GhLocator.Find(new GhSettingsStore(GameContext.Repository.GameDirectory(GameContext.GameId)).Load());

    private GhClient? CreateGhClient() => FindGhPath() is { } path ? new GhClient(_bugProcessRunner, path) : null;

    private BugSyncService CreateBugSyncService() => new(new IBugSource[] { new GitHubBugSource(CreateGhClient), new DiscordBugSource(CreateDiscordClient) }, BugsPath);

    /// <summary>Quellen eines Projekts (am Hauptmod); leer ohne Register oder bei unbekanntem Projekt.</summary>
    private IReadOnlyList<BugSourceEntry> BugSourcesOf(string? projectId) =>
        projectId is not null && _mods?.Registry.Get(projectId) is { } root ? root.BugSources : Array.Empty<BugSourceEntry>();

    /// <summary>Fehlertext des letzten Abrufs eines Projekts; <see langword="null"/> ohne Fehler.</summary>
    private string? BugRefreshError(string? projectId) =>
        projectId is not null && _bugRefreshErrors.TryGetValue(projectId, out var error) ? error : null;

    private bool NeedsBugRefresh(string projectId) =>
        BugSourcesOf(projectId).Any(source => source.Enabled)
        && !_bugRefreshRunning.Contains(projectId)
        && (!_lastBugRefresh.TryGetValue(projectId, out var last) || DateTime.UtcNow - last > BugRefreshInterval);

    /// <summary>Stiller Abruf, falls fällig (Mod-Wechsel, Bugtracker öffnen).</summary>
    private void RefreshBugsInBackground(string projectId)
    {
        if (NeedsBugRefresh(projectId))
        {
            _ = RefreshBugsAsync(projectId, showProgress: false);
        }
    }

    /// <summary>Ruft alle Quellen des Projekts ab. Mit <paramref name="showProgress"/>: Fortschrittsfenster mit Abbrechen und Zusammenfassung.</summary>
    private async Task RefreshBugsAsync(string projectId, bool showProgress)
    {
        var sources = BugSourcesOf(projectId);
        if (sources.Count == 0 || !_bugRefreshRunning.Add(projectId))
        {
            return;
        }

        _lastBugRefresh[projectId] = DateTime.UtcNow;
        using var cancellation = new CancellationTokenSource();
        TaskProgressWindow? window = null;
        if (showProgress)
        {
            window = new TaskProgressWindow(BugTexts.RefreshTitle, string.Empty, SettingsTexts.Cancel, SettingsTexts.ProgressCount, cancellation.Cancel) { Owner = DialogOwner };
            window.Show();
        }

        var progress = new Progress<(int Done, int Total)>(value => window?.Report(new CompatProgress(BugTexts.RefreshProgress, value.Done, value.Total)));
        try
        {
            var summary = await CreateBugSyncService().RefreshAsync(projectId, sources, progress, cancellation.Token);
            var errors = summary.Errors.Select(outcome => BugTexts.RefreshSourceError(outcome.Source.Key, outcome.Error!)).ToList();
            if (errors.Count > 0)
            {
                _bugRefreshErrors[projectId] = string.Join("\n", errors);
            }
            else
            {
                _bugRefreshErrors.Remove(projectId);
            }

            if (window is not null)
            {
                window.CloseAfterFinished();
                window = null;
                var text = BugTexts.RefreshSummary(summary.Added, summary.Changed, summary.Removed);
                MessageBox.Show(DialogOwner, errors.Count == 0 ? text : text + "\n\n" + string.Join("\n", errors), BugTexts.RefreshTitle);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Abgebrochen: der bisherige Stand bleibt.
        }
        finally
        {
            window?.CloseAfterFinished();
            _bugRefreshRunning.Remove(projectId);
            RefreshBugViews();
        }
    }

    /// <summary>Status setzen: externe Meldungen zuerst beim Dienst, lokal nur nach Erfolg; Fehler als Meldung.</summary>
    private async Task SetBugStatusAsync(string reportId, BugStatus status)
    {
        if (BugEditing.Find(BugReportStore.Load(BugsPath), reportId) is not { } report)
        {
            return;
        }

        try
        {
            await CreateBugSyncService().SetStatusAsync(report, status, BugSourcesOf(ProjectOfReport(report)), CancellationToken.None);
        }
        catch (BugSourceException ex)
        {
            MessageBox.Show(DialogOwner, BugTexts.StatusFailed(ex.Message), BugTexts.ErrorCaption);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(DialogOwner, BugTexts.SaveFailed(ex.Message), BugTexts.ErrorCaption);
        }

        RefreshBugViews();
    }

    /// <summary>Kommentare bzw. Antworten einer externen Meldung; wirft <see cref="BugSourceException"/>.</summary>
    private Task<IReadOnlyList<BugComment>> LoadBugDiscussionAsync(BugReport report) =>
        CreateBugSyncService().LoadDiscussionAsync(report, BugSourcesOf(ProjectOfReport(report)), CancellationToken.None);

    private string? ProjectOfReport(BugReport report) => report.ModId is null ? null : CurrentBugProjects.RootOf(report.ModId);
}
