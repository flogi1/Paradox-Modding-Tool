using ParadoxModTool.Mods;
using ParadoxModTool.Notes;

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

    private void BugTracker_Click(object sender, System.Windows.RoutedEventArgs e) => OpenBugTracker();

    /// <summary>Öffnet den Bugtracker (eines je Hauptfenster) oder holt ihn nach vorn; mit <paramref name="reportId"/> ist diese Meldung gewählt.</summary>
    private void OpenBugTracker(string? reportId = null)
    {
        if (_bugTrackerWindow is null)
        {
            _bugTrackerWindow = new BugTrackerWindow(this) { Owner = this };
            _bugTrackerWindow.Closed += (_, _) => _bugTrackerWindow = null;
            _bugTrackerWindow.Show();
        }
        else
        {
            if (_bugTrackerWindow.WindowState == System.Windows.WindowState.Minimized)
            {
                _bugTrackerWindow.WindowState = System.Windows.WindowState.Normal;
            }

            _bugTrackerWindow.Activate();
        }

        if (reportId is not null)
        {
            _bugTrackerWindow.SelectReport(reportId);
        }
    }
}
