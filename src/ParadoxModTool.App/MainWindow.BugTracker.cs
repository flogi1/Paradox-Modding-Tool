using ParadoxModTool.Mods;
using ParadoxModTool.BugTracking;
using ParadoxModTool.Notes;
using ParadoxModTool.Settings;

namespace ParadoxModTool.App;

/// <summary>Was das Bugtracker-Fenster vom Hauptfenster braucht.</summary>
internal interface IBugTrackerHost
{
    BugProjects BugProjects { get; }

    /// <summary>Wählbare Projekte (Id des Hauptmods, Name), nach Name sortiert. "Ohne Projekt" ergänzt das Fenster selbst.</summary>
    IReadOnlyList<(string Id, string Name)> ProjectChoices();

    /// <summary>Projekt der aktiven Datei bzw. Mod; <see langword="null"/> ohne Projekt.</summary>
    string? CurrentProjectId { get; }

    /// <summary>Bezug auf die aktive Datei mit Cursorzeile oder Markierung; <see langword="null"/> ohne aktive Datei mit Pfad.</summary>
    BugLink? CurrentFileLink();

    /// <summary>Status setzen (extern zuerst beim Dienst); Fehler meldet das Hauptfenster selbst.</summary>
    Task SetBugStatusAsync(string reportId, BugStatus status);

    /// <summary>Alle Quellen des Projekts mit Fortschrittsfenster abrufen.</summary>
    Task RefreshBugsAsync(string projectId);

    /// <summary>Stiller Abruf, falls der letzte älter als eine Minute ist.</summary>
    void RefreshBugsInBackground(string projectId);

    bool HasBugSources(string projectId);

    /// <summary>Fehler des letzten Abrufs; <see langword="null"/> ohne Fehler.</summary>
    string? BugRefreshError(string projectId);

    /// <summary>Kommentare bzw. Antworten; wirft <see cref="ParadoxModTool.BugTracking.BugSourceException"/>.</summary>
    Task<IReadOnlyList<BugComment>> LoadDiscussionAsync(BugReport report);

    /// <summary>Quellen-Dialog des Projekts; nach dem Speichern folgt ein Abruf mit Fortschrittsfenster.</summary>
    void OpenBugSources(string projectId);

    void OpenBugLink(BugLink link);

    void ChangeBugs(Action<BugData> change);
}

public partial class MainWindow : IBugTrackerHost
{
    private BugTrackerWindow? _bugTrackerWindow;

    BugProjects IBugTrackerHost.BugProjects => CurrentBugProjects;

    IReadOnlyList<(string Id, string Name)> IBugTrackerHost.ProjectChoices()
    {
        if (_mods is null)
        {
            return Array.Empty<(string, string)>();
        }

        return _mods.Registry.Mods
            .Select(mod => ModProjects.Root(_mods.Registry, mod))
            .DistinctBy(root => root.Id)
            .OrderBy(root => root.Name, StringComparer.OrdinalIgnoreCase)
            .Select(root => (root.Id, root.Name))
            .ToList();
    }

    string? IBugTrackerHost.CurrentProjectId =>
        (ActivePane.CurrentFilePath is { } filePath ? BugQueries.ProjectOfFile(filePath, CurrentBugProjects) : null) ?? ActiveBugProjectId;

    BugLink? IBugTrackerHost.CurrentFileLink()
    {
        if (ActivePane.CurrentFilePath is not { } filePath)
        {
            return null;
        }

        var (startLine, endLine) = LineRangeOf(ActivePane);
        return BugLink.ForFile(filePath, startLine, endLine);
    }

    void IBugTrackerHost.OpenBugLink(BugLink link) => OpenBugLink(link);

    void IBugTrackerHost.ChangeBugs(Action<BugData> change) => ChangeBugs(change);

    Task IBugTrackerHost.SetBugStatusAsync(string reportId, BugStatus status) => SetBugStatusAsync(reportId, status);

    Task IBugTrackerHost.RefreshBugsAsync(string projectId) => RefreshBugsAsync(projectId, showProgress: true);

    void IBugTrackerHost.RefreshBugsInBackground(string projectId) => RefreshBugsInBackground(projectId);

    bool IBugTrackerHost.HasBugSources(string projectId) => BugSourcesOf(projectId).Count > 0;

    string? IBugTrackerHost.BugRefreshError(string projectId) => BugRefreshError(projectId);

    Task<IReadOnlyList<BugComment>> IBugTrackerHost.LoadDiscussionAsync(BugReport report) => LoadBugDiscussionAsync(report);

    void IBugTrackerHost.OpenBugSources(string projectId)
    {
        if (_mods?.Registry.Get(projectId) is not { } root)
        {
            return;
        }

        var registry = _mods.Registry;
        var suggestions = ModProjects.Members(registry, root)
            .SelectMany(member => member.GitHubRepos)
            .Select(repo => repo.Repo)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var labels = new GitHubBugSource(CreateGhClient);
        var dialog = new BugSourcesDialog(
            root.Name,
            root.BugSources,
            suggestions,
            repo => labels.LoadLabelsAsync(repo, CancellationToken.None),
            () => OpenSettingsAt(SettingsCategory.BugTracker),
            owner =>
            {
                if (CreateDiscordClient() is not { } discord)
                {
                    System.Windows.MessageBox.Show(owner, BugTexts.DiscordNotSetUp, BugTexts.HintCaption);
                    OpenSettingsAt(SettingsCategory.BugTracker);
                    return null;
                }

                var forum = new DiscordForumDialog(discord) { Owner = owner };
                return forum.ShowDialog() == true ? forum.Result : null;
            })
        {
            Owner = DialogOwner,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var before = root.BugSources.ToList();
        try
        {
            registry.SetBugSources(root.Id, dialog.Sources);
        }
        catch (ArgumentException ex)
        {
            System.Windows.MessageBox.Show(DialogOwner, ex.Message, BugTexts.ErrorCaption);
            return;
        }

        var removed = before
            .Where(old => !dialog.Sources.Any(kept => kept.Kind == old.Kind && kept.Key.Equals(old.Key, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (removed.Count > 0)
        {
            ChangeBugs(data =>
            {
                foreach (var source in removed)
                {
                    BugSync.MarkSourceRemoved(data, BugSyncService.SourceOf(source.Kind), source.Key);
                }
            });
        }

        _ = RefreshBugsAsync(root.Id, showProgress: true);
    }

    private void BugTracker_Click(object sender, System.Windows.RoutedEventArgs e) => OpenBugTracker();

    /// <summary>
    /// Öffnet den Bugtracker (eines je Hauptfenster) oder holt ihn nach vorn; mit <paramref name="projectId"/> ist dieses Projekt, mit
    /// <paramref name="reportId"/> diese Meldung gewählt.
    /// </summary>
    private void OpenBugTracker(string? reportId = null, string? projectId = null)
    {
        if (_bugTrackerWindow is null)
        {
            _bugTrackerWindow = new BugTrackerWindow(this) { Owner = this };
            _bugTrackerWindow.Closed += (_, _) => _bugTrackerWindow = null;
            _bugTrackerWindow.Show();
            if ((projectId ?? ((IBugTrackerHost)this).CurrentProjectId) is { } refreshId)
            {
                RefreshBugsInBackground(refreshId);
            }
        }
        else
        {
            if (_bugTrackerWindow.WindowState == System.Windows.WindowState.Minimized)
            {
                _bugTrackerWindow.WindowState = System.Windows.WindowState.Normal;
            }

            _bugTrackerWindow.Activate();
        }

        if (projectId is not null)
        {
            _bugTrackerWindow.SelectProject(projectId);
        }

        if (reportId is not null)
        {
            _bugTrackerWindow.SelectReport(reportId);
        }
    }
}
