using System.IO;
using System.Linq;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using ParadoxModTool.Editor;
using ParadoxModTool.Notes;
using ParadoxModTool.Settings;

namespace ParadoxModTool.App;

/// <summary>
/// Mod-Bugtracker: Verdrahtung zwischen Bug-Abschnitt im rechten Panel, Werkzeugleiste, Dialog und <see cref="BugReportStore"/>.
/// Meldungen gehören zu Projekten und verweisen über Bezüge auf Dateien; Tab-Symbol und Filter zählen offene Meldungen mit Bezug auf die Datei.
/// </summary>
public partial class MainWindow
{
    private static string BugsPath => BugReportStore.GetDefaultPath();

    private BugProjects CurrentBugProjects => _mods?.CreateBugProjects() ?? BugProjects.None;

    /// <summary>Projekt der aktiven Mod; <see langword="null"/> ohne erkannte Mod.</summary>
    private string? ActiveBugProjectId => _mods?.ActiveProjectId;

    private void InitializeBugPanel()
    {
        NotesPanelControl.NewBugRequested += (_, _) => CreateBug(PanelPane, attach: false);
        NotesPanelControl.EditBugRequested += (_, report) => EditBug(report);
        NotesPanelControl.BugStatusRequested += (_, args) => SetBugStatus(args.Report, args.Status);
        NotesPanelControl.DeleteBugRequested += (_, report) => DeleteBug(report);
        NotesPanelControl.OpenBugRequested += (_, row) =>
        {
            if (row.Link is { } link)
            {
                OpenBugLink(link);
            }
        };
        NotesPanelControl.ShowDoneBugsChanged += (_, _) => RefreshNotesPanel();
        NotesPanelControl.BugScopeChanged += (_, _) =>
        {
            RefreshNotesPanel();
            UpdateNotesPanelVisibility();
        };

        foreach (var pane in new[] { LeftEditor, RightEditor })
        {
            pane.NewBugHereRequested += (_, _) => CreateBug(pane, attach: false);
            pane.AttachBugHereRequested += (_, _) => CreateBug(pane, attach: true);
        }
    }

    /// <summary>Zeilen für den Bug-Abschnitt: Meldungen der Datei oder (Umschalter) des ganzen Projekts der Datei bzw. der aktiven Mod.</summary>
    private IReadOnlyList<BugListRow> BugRowsForPanel(string? filePath)
    {
        var data = BugReportStore.Load(BugsPath);
        var projects = CurrentBugProjects;
        if (NotesPanelControl.BugsWholeProject)
        {
            var projectId = (filePath is null ? null : BugQueries.ProjectOfFile(filePath, projects)) ?? ActiveBugProjectId;
            var fileKey = filePath is null ? null : NoteKeys.ForFile(filePath);
            return BugQueries.ForProject(data, projectId, projects)
                .Select(report => new BugListRow(report, BugQueries.PreferredLink(report, fileKey, projects), showPath: true))
                .ToList();
        }

        if (filePath is null)
        {
            return Array.Empty<BugListRow>();
        }

        return BugQueries.ForFile(data, filePath, projects).Select(match => new BugListRow(match.Report, match.Link, showPath: false)).ToList();
    }

    /// <summary>Zuletzt per Tab-Symbol angesprungene Meldung je Datei - Grundlage des Weiterschaltens.</summary>
    private readonly Dictionary<string, string> _lastCycledBug = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Klick auf das Bug-Symbol im Tab: aktiviert die Datei und schaltet bei jedem Klick zur nächsten OFFENEN Meldung weiter (nach der letzten wieder
    /// zur ersten). Hat ihr Bezug auf diese Datei eine Zeile, springt der Editor dorthin; die Meldung wird im Panel markiert.
    /// </summary>
    private void CycleBugs(OpenDocument document)
    {
        if (document.FilePath is not { } filePath)
        {
            return;
        }

        var open = BugQueries.ForFile(BugReportStore.Load(BugsPath), filePath, CurrentBugProjects).Where(match => match.Report.IsOpen).ToList();
        if (open.Count == 0)
        {
            return;
        }

        var lastIndex = _lastCycledBug.TryGetValue(filePath, out var lastId) ? open.FindIndex(match => match.Report.Id == lastId) : -1;
        var (report, link) = open[(lastIndex + 1) % open.Count];
        _lastCycledBug[filePath] = report.Id;

        foreach (var pane in new[] { LeftEditor, RightEditor })
        {
            var index = pane.Documents.ToList().IndexOf(document);
            if (index < 0)
            {
                continue;
            }

            // Das Panel zeigt den zuletzt fokussierten Arbeitsbereich - vor dem Tabwechsel setzen, damit es diese Datei lädt.
            _lastFocusedPane = pane;
            pane.SelectDocumentAt(index);
            if (link.Line is int line)
            {
                pane.GoToLine(line);
            }

            RefreshNotesPanel();
            NotesPanelControl.SelectBug(report.Id);
            return;
        }
    }

    /// <summary>Werkzeugleisten-Button "Bug": neuen Bug zur aktiven Datei anlegen.</summary>
    private void NewBug_Click(object sender, RoutedEventArgs e) => CreateBug(ActivePane, attach: false);

    /// <summary>Cursorzeile bzw. Markierung des Arbeitsbereichs; Endzeile nur bei mehreren Zeilen.</summary>
    private static (int? Start, int? End) LineRangeOf(EditorPane pane) =>
        pane.CurrentLineRange is { } range ? (range.StartLine, range.EndLine > range.StartLine ? range.EndLine : null) : (null, null);

    /// <summary>
    /// Dialog "Bug" für die aktive Datei von <paramref name="pane"/>: neuer Bug oder die Stelle an eine offene Meldung des Projekts der Datei hängen
    /// (ohne Projekt: an eine offene Meldung ohne Projekt).
    /// </summary>
    private void CreateBug(EditorPane pane, bool attach)
    {
        if (pane.CurrentFilePath is not { } filePath)
        {
            MessageBox.Show(BugTexts.NoFileYet, BugTexts.HintCaption);
            return;
        }

        var (startLine, endLine) = LineRangeOf(pane);
        var projects = CurrentBugProjects;
        var candidates = BugQueries.ForProject(BugReportStore.Load(BugsPath), BugQueries.ProjectOfFile(filePath, projects), projects)
            .Where(report => report.IsOpen && !report.Removed)
            .ToList();
        var dialog = BugDialog.ForCreate(filePath, startLine is null ? null : BugTexts.LineText(startLine, endLine), candidates, attach);
        dialog.Owner = this;
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var link = BugLink.ForFile(filePath, dialog.UseLine ? startLine : null, dialog.UseLine ? endLine : null);
        if (dialog.AttachMode)
        {
            if (dialog.SelectedReportId is { } reportId)
            {
                ChangeBugs(data => BugEditing.AddLink(data, reportId, link));
            }

            return;
        }

        ChangeBugs(data => BugEditing.AddLocal(data, dialog.BugTitle, dialog.Description, BugLink.ModIdOf(link.Key), link));
    }

    /// <summary>Lokale Bugs: Titel und Text bearbeiten. Externe Meldungen gehören dem Dienst (ab Task 7 zeigt sie der Bugtracker).</summary>
    private void EditBug(BugReport report)
    {
        if (report.Source != BugSource.Local)
        {
            OpenBugTracker(report.Id);
            return;
        }

        var dialog = BugDialog.ForEdit(report.Title, report.Body);
        dialog.Owner = this;
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        ChangeBugs(data => BugEditing.UpdateLocal(data, report.Id, dialog.BugTitle, dialog.Description));
    }

    /// <summary>Status setzen. Plan 1: nur lokal; ab Plan 2 gehen externe Meldungen zuerst an den Dienst.</summary>
    private void SetBugStatus(BugReport report, BugStatus status) =>
        ChangeBugs(data => BugEditing.SetStatus(data, report.Id, status));

    private void DeleteBug(BugReport report)
    {
        if (report.Source != BugSource.Local
            || MessageBox.Show(BugTexts.DeleteConfirm(report.Title), BugTexts.HintCaption, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        ChangeBugs(data => BugEditing.Delete(data, report.Id));
    }

    /// <summary>Öffnet die Datei eines Bezugs (Branch-Kopie der aktiven Mod bevorzugt) an seiner Zeile; ohne vorhandene Datei ein Hinweis.</summary>
    private void OpenBugLink(BugLink link)
    {
        var files = CurrentBugProjects.FilesFor(link, _mods?.ActiveMod?.Id);
        if (files.Count == 0)
        {
            MessageBox.Show(BugTexts.LinkFileMissing(link.DisplayPath), BugTexts.HintCaption);
            return;
        }

        JumpToBookmarkInFile(files[0], link.Line ?? 1);
    }

    /// <summary>
    /// Lädt die Meldungen frisch von der Platte, wendet die Änderung an, speichert und zieht Tab-Symbole, Panel (und ab Task 7/8 Fenster und Zähler)
    /// nach. Bei einem Fehler bleibt die Datei unverändert.
    /// </summary>
    private void ChangeBugs(Action<BugData> change)
    {
        try
        {
            BugReportStore.Update(BugsPath, change);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(BugTexts.SaveFailed(ex.Message), BugTexts.ErrorCaption);
        }

        RefreshBugViews();
    }

    private void RefreshBugViews()
    {
        foreach (var document in AllPanes.SelectMany(p => p.Documents))
        {
            UpdateHasNote(document);
        }

        RefreshNotesPanel();
        _bugTrackerWindow?.Reload();
        UpdateBugTrackerButton();
    }

    /// <summary>Beschriftung des Bugtracker-Knopfs mit der Zahl ungesehener Meldungen des aktiven Projekts ("📋 Bugtracker (3 neu)").</summary>
    private void UpdateBugTrackerButton()
    {
        var count = BugQueries.UnseenCount(BugReportStore.Load(BugsPath), ActiveBugProjectId, CurrentBugProjects);
        var label = MenuTexts.ToolbarLabel(ToolbarItemIds.BugTracker);
        ToolbarBugTrackerButton.Content = count > 0 ? $"{label} ({BugTexts.NewCount(count)})" : label;
    }
}
