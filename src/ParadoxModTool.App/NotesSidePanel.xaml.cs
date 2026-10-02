using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using UserControl = System.Windows.Controls.UserControl;
using ParadoxModTool.Editor;
using ParadoxModTool.ErrorLog;
using ParadoxModTool.Notes;

namespace ParadoxModTool.App;

/// <summary>Eine Zeile im Abschnitt "⚠ Fehler": Fehlerart und Name, darunter Zeile und Meldung; <c>Line</c> = Sprungziel, falls bekannt.</summary>
public sealed record NotesErrorItem(string Title, string Detail, int? Line);

/// <summary>
/// Rechtes Panel mit Notiz und Lesezeichen des aktiven Dokuments (siehe NotesSidePanel.xaml). Liest die
/// Stores bei jedem <see cref="Refresh"/> frisch von der Platte (dieselbe Vorsicht wie das Notizfenster)
/// und löst für alle Aktionen Ereignisse aus - die eigentliche Arbeit (Fenster, Speichern, Springen)
/// erledigt das Hauptfenster.
/// </summary>
public partial class NotesSidePanel : UserControl
{
    public NotesSidePanel()
    {
        InitializeComponent();
    }

    /// <summary>Ob es für die aktuell angezeigte Datei eine Notiz, ein Lesezeichen oder einen Bug gibt (Grundlage des Auto-Ausblendens).</summary>
    public bool HasContent { get; private set; }

    /// <summary>Ob der Abschnitt "Scope" gerade sichtbar ist (die Datei hat Scopes und die Einstellung ist an); er zählt beim Auto-Ausblenden des Panels mit.</summary>
    public bool ScopeAvailable { get; private set; }

    /// <summary>Ob erledigte und verworfene Meldungen in der Liste stehen (Häkchen "Erledigte").</summary>
    public bool ShowDoneBugs
    {
        get => ShowDoneCheckBox.IsChecked == true;
        set => ShowDoneCheckBox.IsChecked = value;
    }

    /// <summary>Bug-Abschnitt zeigt das ganze Projekt statt nur die aktive Datei.</summary>
    public bool BugsWholeProject
    {
        get => BugScopeProjectRadio.IsChecked == true;
        set
        {
            BugScopeProjectRadio.IsChecked = value;
            BugScopeFileRadio.IsChecked = !value;
        }
    }

    public event EventHandler? EditNoteRequested;
    public event EventHandler<int>? JumpRequested;
    public event EventHandler<BookmarkEntry>? EditBookmarkRequested;
    public event EventHandler<BookmarkEntry>? RemoveBookmarkRequested;
    public event EventHandler? NewBugRequested;
    public event EventHandler<BugReport>? EditBugRequested;
    public event EventHandler<BugReport>? DeleteBugRequested;

    /// <summary>Doppelklick auf eine Zeile mit Bezug: die Datei des Bezugs öffnen.</summary>
    public event EventHandler<BugListRow>? OpenBugRequested;

    /// <summary>Knopf Erledigt, Verwerfen oder Wieder öffnen in einer Zeile.</summary>
    public event EventHandler<(BugReport Report, BugStatus Status)>? BugStatusRequested;

    /// <summary>Das Häkchen "Erledigte" wurde umgeschaltet; das Hauptfenster lädt das Panel neu und merkt sich die Wahl.</summary>
    public event EventHandler? ShowDoneBugsChanged;

    /// <summary>Doppelklick auf einen Fehler mit Zeile; der Wert ist die Zeile in der angezeigten Datei.</summary>
    public event EventHandler<int>? JumpToErrorRequested;

    /// <summary>Knopf "Im Fehlerprotokoll zeigen".</summary>
    public event EventHandler? ShowInErrorLogRequested;

    private bool _baseHasContent;

    /// <summary>Umschalter "Diese Datei / Ganzes Projekt" wurde geändert.</summary>
    public event EventHandler? BugScopeChanged;

    /// <summary>
    /// Lädt Notiz und Lesezeichen der Datei neu und zeigt die übergebenen Bug-Zeilen (das Hauptfenster wählt sie nach dem Umschalter aus).
    /// <see langword="null"/> (kein Dokument bzw. noch keine Datei) leert Notiz und Lesezeichen.
    /// </summary>
    public void Refresh(string? filePath, IReadOnlyList<BugListRow> bugRows)
    {
        var note = string.Empty;
        var bookmarks = new List<BookmarkEntry>();

        if (filePath is not null)
        {
            note = FileNoteStore.GetNote(FileNoteStore.TryLoad(FileNoteStore.GetDefaultNotesFilePath()), filePath);
            bookmarks = BookmarkStore.GetBookmarks(BookmarkStore.TryLoad(BookmarkStore.GetDefaultBookmarksFilePath()), filePath);
        }

        HeaderText.Text = filePath is null ? MiscWindowTexts.NotesPanelHeader : Path.GetFileName(filePath);
        HeaderText.ToolTip = filePath;

        NoteText.Text = note;
        NoteSection.Visibility = string.IsNullOrEmpty(note) ? Visibility.Collapsed : Visibility.Visible;

        BookmarksList.ItemsSource = bookmarks;
        BookmarksSection.Visibility = bookmarks.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        BookmarksRow.Height = bookmarks.Count == 0 ? new GridLength(0) : new GridLength(1, GridUnitType.Star);

        var shown = ShowDoneBugs ? bugRows : bugRows.Where(row => row.IsOpen).ToList();
        var bugsVisible = BugsWholeProject ? shown.Count > 0 : bugRows.Count > 0;
        BugsList.ItemsSource = shown;
        BugsSection.Visibility = bugsVisible ? Visibility.Visible : Visibility.Collapsed;
        BugsRow.Height = bugsVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        _baseHasContent = !string.IsNullOrEmpty(note) || bookmarks.Count > 0 || bugsVisible;
        HasContent = _baseHasContent || ErrorsSection.Visibility == Visibility.Visible || _hasWarnings;
        EmptyHint.Visibility = HasContent || ScopeAvailable ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Eine Zeile im Abschnitt "⚠ Nach dem Ersetzen".</summary>
    private sealed record WarningRow(string Text, BlockWarningHit Hit);

    private bool _hasWarnings;

    /// <summary>Doppelklick auf eine Zeile der Warnungen nach dem Ersetzen: die Stelle öffnen.</summary>
    public event EventHandler<BlockWarningHit>? OpenReplaceWarningRequested;

    /// <summary>Knopf ✕ im Abschnitt: die Liste leeren (das Hauptfenster setzt sie auf leer).</summary>
    public event EventHandler? ClearReplaceWarningsRequested;

    /// <summary>Knopf ⟳ im Abschnitt: die Liste gegen den aktuellen Text der Dateien aktualisieren (erledigte Stellen fallen weg, Zeilen werden nachgeführt).</summary>
    public event EventHandler? RefreshReplaceWarningsRequested;

    /// <summary>
    /// Zeigt die Warnungen nach dem Ersetzen (OR-/NOR-/AND-Blöcke mit höchstens einem Eintrag, alle Fundstellen des letzten Ersetzens);
    /// eine leere Liste blendet den Abschnitt aus. Sie gelten für das Panel unabhängig von der gerade angezeigten Datei.
    /// </summary>
    public void SetReplaceWarnings(IReadOnlyList<BlockWarningHit> hits)
    {
        _hasWarnings = hits.Count > 0;
        WarningsHeader.Text = SearchTexts.BlockWarningPanelHeading(hits.Count);
        WarningsClearButton.ToolTip = SearchTexts.BlockWarningPanelClearTip;
        WarningsRefreshButton.ToolTip = SearchTexts.BlockWarningPanelRefreshTip;
        WarningsList.ToolTip = SearchTexts.BlockWarningOpenHint;
        WarningsList.ItemsSource = hits.Select(hit => new WarningRow(SearchTexts.BlockWarningText(hit), hit)).ToList();
        WarningsSection.Visibility = _hasWarnings ? Visibility.Visible : Visibility.Collapsed;
        WarningsRow.Height = _hasWarnings ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        HasContent = _baseHasContent || ErrorsSection.Visibility == Visibility.Visible || _hasWarnings;
        EmptyHint.Visibility = HasContent || ScopeAvailable ? Visibility.Collapsed : Visibility.Visible;
    }

    private void WarningsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (WarningsList.SelectedItem is WarningRow row)
        {
            OpenReplaceWarningRequested?.Invoke(this, row.Hit);
        }
    }

    private void ClearReplaceWarnings_Click(object sender, RoutedEventArgs e) => ClearReplaceWarningsRequested?.Invoke(this, EventArgs.Empty);

    private void RefreshReplaceWarnings_Click(object sender, RoutedEventArgs e) => RefreshReplaceWarningsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Füllt den Abschnitt "Scope": Scope am Cursor mit dem Weg dorthin, die gespeicherten Scopes und die Warnungen der Datei.
    /// <see langword="null"/> oder eine Datei ohne Scopes blendet den Abschnitt aus.
    /// </summary>
    public void SetScope(ScopeInsight? insight)
    {
        ScopeAvailable = insight is { Available: true };
        ScopeSection.Visibility = ScopeAvailable ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Visibility = HasContent || ScopeAvailable ? Visibility.Collapsed : Visibility.Visible;
        if (insight is not { Available: true })
        {
            return;
        }

        var context = insight.Context;
        if (context is null)
        {
            ScopeCurrentText.Text = MiscWindowTexts.NotesScopeOutside;
            ScopeCurrentText.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
            ScopeFieldText.Text = string.Empty;
        }
        else
        {
            ScopeCurrentText.Text = context.Describe();
            ScopeCurrentText.SetResourceReference(TextBlock.ForegroundProperty, context.Confident ? "Theme.Text" : "Theme.TextMuted");
            ScopeFieldText.Text = context.FieldPath is null
                ? string.Empty
                : MiscWindowTexts.NotesScopeField(context.FieldPath) + (context.StartScope is null ? string.Empty : "  " + MiscWindowTexts.NotesScopeStart(context.StartScope, context.StartSupport));
        }

        var saved = new List<string>();
        if (context is not null)
        {
            saved.AddRange(context.SavedScopes.Select(s => $"scope:{s.Key} = {s.Value ?? "?"}  (save_scope_as)"));
            saved.AddRange(context.LearnedSavedScopes
                .Where(s => !context.SavedScopes.ContainsKey(s.Key))
                .OrderBy(s => s.Key, StringComparer.Ordinal)
                .Select(s => $"scope:{s.Key} = {s.Value}"));
        }

        ScopeSavedHeader.Visibility = saved.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ScopeSavedText.Visibility = saved.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ScopeSavedText.Text = string.Join("\n", saved);

        var issues = insight.Issues;
        ScopeIssuesHeader.Text = MiscWindowTexts.NotesScopeWarnings(issues.Count);
        ScopeIssuesHeader.Visibility = issues.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ScopeIssuesList.Visibility = issues.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ScopeIssuesList.ItemsSource = issues;
    }

    private void ScopeIssuesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ScopeIssuesList.SelectedItem is ScopeIssueLine issue)
        {
            JumpRequested?.Invoke(this, issue.Line);
        }
    }

    private void EditNote_Click(object sender, RoutedEventArgs e) => EditNoteRequested?.Invoke(this, EventArgs.Empty);

    private void BookmarksList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (BookmarksList.SelectedItem is BookmarkEntry entry)
        {
            JumpRequested?.Invoke(this, entry.Line);
        }
    }

    private void EditBookmark_Click(object sender, RoutedEventArgs e)
    {
        if (BookmarksList.SelectedItem is BookmarkEntry entry)
        {
            EditBookmarkRequested?.Invoke(this, entry);
        }
    }

    private void RemoveBookmark_Click(object sender, RoutedEventArgs e)
    {
        if (BookmarksList.SelectedItem is BookmarkEntry entry)
        {
            RemoveBookmarkRequested?.Invoke(this, entry);
        }
    }

    /// <summary>Markiert die Meldung mit dieser Kennung in der Liste und scrollt sie ins Bild (falls sichtbar).</summary>
    public void SelectBug(string id)
    {
        var row = BugsList.Items.OfType<BugListRow>().FirstOrDefault(r => r.Id == id);
        if (row is null)
        {
            return;
        }

        BugsList.SelectedItem = row;
        BugsList.ScrollIntoView(row);
    }

    private void NewBug_Click(object sender, RoutedEventArgs e) => NewBugRequested?.Invoke(this, EventArgs.Empty);

    private void EditBug_Click(object sender, RoutedEventArgs e)
    {
        if (BugsList.SelectedItem is BugListRow row)
        {
            EditBugRequested?.Invoke(this, row.Report);
        }
    }

    private void BugsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && IsInsideButton(source))
        {
            return;
        }

        if (BugsList.SelectedItem is BugListRow { Link: not null } row)
        {
            OpenBugRequested?.Invoke(this, row);
        }
    }

    private void BugDone_Click(object sender, RoutedEventArgs e) => RequestStatus(sender, BugStatus.Done);

    private void BugReject_Click(object sender, RoutedEventArgs e) => RequestStatus(sender, BugStatus.Rejected);

    private void BugReopen_Click(object sender, RoutedEventArgs e) => RequestStatus(sender, BugStatus.Open);

    private void RequestStatus(object sender, BugStatus status)
    {
        if (sender is Button { DataContext: BugListRow row })
        {
            BugStatusRequested?.Invoke(this, (row.Report, status));
        }
    }

    private void BugDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: BugListRow row })
        {
            DeleteBugRequested?.Invoke(this, row.Report);
        }
    }

    private void BugScope_Click(object sender, RoutedEventArgs e) => BugScopeChanged?.Invoke(this, EventArgs.Empty);

    private void ShowDone_Click(object sender, RoutedEventArgs e) => ShowDoneBugsChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Füllt den Abschnitt "⚠ Fehler"; eine leere Liste blendet ihn aus. Fehler zählen als Inhalt (Plan-2b-Ruling 4).</summary>
    public void SetErrors(IReadOnlyList<NotesErrorItem> errors)
    {
        ErrorsHeader.Text = ErrorLogTexts.NotesHeading(errors.Count);
        ShowInErrorLogButton.Content = ErrorLogTexts.NotesShowInLog;
        ShowInErrorLogButton.ToolTip = ErrorLogTexts.NotesShowInLogTip;
        ErrorsList.ToolTip = ErrorLogTexts.NotesJumpTip;
        ErrorsList.ItemsSource = errors;
        ErrorsSection.Visibility = errors.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ErrorsRow.Height = errors.Count == 0 ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        HasContent = _baseHasContent || errors.Count > 0 || _hasWarnings;
        EmptyHint.Visibility = HasContent || ScopeAvailable ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ErrorsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ErrorsList.SelectedItem is NotesErrorItem { Line: int line })
        {
            JumpToErrorRequested?.Invoke(this, line);
        }
    }

    private void ShowInErrorLog_Click(object sender, RoutedEventArgs e) => ShowInErrorLogRequested?.Invoke(this, EventArgs.Empty);

    private static bool IsInsideButton(DependencyObject element)
    {
        for (var current = element; current is not null; current = System.Windows.Media.VisualTreeHelper.GetParent(current))
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase)
            {
                return true;
            }
        }

        return false;
    }
}
