using System.IO;
using System.Net.Http;
using ParadoxModTool.BugTracking;
using ParadoxModTool.GitIntegration;
using ParadoxModTool.ModOverview;
using ParadoxModTool.Notes;

namespace ParadoxModTool.App;

/// <summary>
/// Bugreports der Übersicht "Meine Mods": Abruf aller Projekte aller Spiele (beim Öffnen und bei "Aktualisieren"), Bug-Zahlen, neue Reports und
/// Hinweise zu nicht erreichbaren Quellen.
/// </summary>
internal sealed partial class MyModsWindow
{
    /// <summary>Ein HttpClient für alle Discord-Aufrufe der Übersicht.</summary>
    private static readonly HttpClient DiscordHttp = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly IProcessRunner _processRunner = new ProcessRunner();
    private BugData _bugData = new();
    private IReadOnlyList<OverviewBugGroup> _bugGroups = Array.Empty<OverviewBugGroup>();
    private IReadOnlyList<OverviewBugSourceError> _sourceErrors = Array.Empty<OverviewBugSourceError>();
    private readonly HashSet<string> _expandedBugGroups = new(StringComparer.Ordinal);

    private static string BugsPath => BugReportStore.GetDefaultPath();

    /// <summary>Dienst für ein Spiel: GitHub über dessen gh-Pfad (games\&lt;id&gt;\github.json), Discord mit dem globalen Token.</summary>
    private BugSyncService BugServiceFor(string gameId)
    {
        var gameDirectory = GameContext.Repository.GameDirectory(gameId);
        GhClient? Gh() => GhLocator.Find(new GhSettingsStore(gameDirectory).Load()) is { } path ? new GhClient(_processRunner, path) : null;
        DiscordClient? Discord() => DiscordTokenStore.Load(DiscordTokenStore.GetDefaultPath()) is { } token ? new DiscordClient(DiscordHttp, token) : null;
        return new BugSyncService(new IBugSource[] { new GitHubBugSource(Gh), new DiscordBugSource(Discord) }, BugsPath);
    }

    /// <summary>Ruft alle Quellen ab (Fortschritt in der Statuszeile), liest die Bug-Datei danach neu und zeigt das Ergebnis.</summary>
    private async Task RefreshBugsAsync()
    {
        if (_snapshot is not { } snapshot)
        {
            return;
        }

        var status = _statusText.Text;
        var progress = new Progress<(int Done, int Total)>(value =>
        {
            if (value.Total > 0)
            {
                _statusText.Text = OverviewTexts.BugsFetching(value.Done, value.Total);
            }
        });
        try
        {
            _sourceErrors = await OverviewBugRefresher.RefreshAllAsync(snapshot, BugServiceFor, progress, CancellationToken.None);
        }
        finally
        {
            _statusText.Text = status;
        }

        ApplyBugData(await Task.Run(() => BugReportStore.Load(BugsPath)));
    }

    /// <summary>Übernimmt den Stand der Bug-Datei: Zahlen, neue Reports, Hinweise; baut danach neu.</summary>
    private void ApplyBugData(BugData data)
    {
        if (_snapshot is not { } snapshot)
        {
            return;
        }

        _bugData = data;
        _bugCounts = OverviewBugCounts.Compute(snapshot, data);
        _bugGroups = OverviewBugAlerts.New(snapshot, data);
        _alerts = OverviewAlerts.Compute(snapshot).Concat(OverviewAlerts.ForSourceErrors(_sourceErrors)).ToList();
        Rebuild();
    }

    /// <summary>Liest die Bug-Datei neu (nach "Gesehen" hier oder im Spiel-PMT) und zeigt sie.</summary>
    private async Task ReloadBugDataAsync() => ApplyBugData(await Task.Run(() => BugReportStore.Load(BugsPath)));

    /// <summary>
    /// Zeilen der neuen Bugreports im Hinweisbereich: oben "N neue Bugreports" mit "Alle gesehen", dann je Report eine Zeile; ab mehr als drei Reports
    /// einer Mod eine zusammengeklappte Zeile, die sich per Klick öffnet. Gibt die Zahl der gezeigten neuen Reports zurück.
    /// </summary>
    private int AddBugRows(IReadOnlySet<string> hiddenGames)
    {
        var groups = _bugGroups.Where(g => !hiddenGames.Contains(g.Root.GameId)).ToList();
        var total = groups.Sum(g => g.Reports.Count);
        if (total == 0)
        {
            return 0;
        }

        var ids = groups.SelectMany(g => g.Reports).Select(r => r.Id).ToList();
        _alertsPanel.Children.Add(InfoRow(OverviewTexts.BugsNew(total), (OverviewTexts.BugsAllSeen, null, () => _ = MarkSeenAsync(ids))));
        foreach (var group in groups)
        {
            var rootId = group.Root.Mod.Id;
            var name = group.Root.Mod.Name;
            if (group.Collapsed && !_expandedBugGroups.Contains(rootId))
            {
                _alertsPanel.Children.Add(AlertRow("🐞", "Theme.Danger", name, OverviewTexts.BugsCollapsed(group.Reports.Count), () => ToggleBugGroup(rootId)));
                continue;
            }

            if (group.Collapsed)
            {
                _alertsPanel.Children.Add(AlertRow("🐞", "Theme.Danger", name, OverviewTexts.BugsCollapse, () => ToggleBugGroup(rootId)));
            }

            foreach (var report in group.Reports)
            {
                _alertsPanel.Children.Add(AlertRow("🐞", "Theme.Danger", name, $"{BugTexts.Source(report.Source)} · {report.Title}", () => ShowBugDetail(group.Root, report)));
            }
        }

        return total;
    }

    private void ToggleBugGroup(string rootId)
    {
        if (!_expandedBugGroups.Remove(rootId))
        {
            _expandedBugGroups.Add(rootId);
        }

        Rebuild();
    }

    /// <summary>Markiert die Reports als gesehen und liest die Bug-Datei neu.</summary>
    private async Task MarkSeenAsync(IReadOnlyList<string> reportIds)
    {
        try
        {
            await Task.Run(() => OverviewBugRefresher.MarkSeen(BugsPath, reportIds));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show(this, ex.Message, OverviewTexts.WindowTitle, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        await ReloadBugDataAsync();
    }
}
