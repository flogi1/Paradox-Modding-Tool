using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using UserControl = System.Windows.Controls.UserControl;
using ParadoxModTool.Editor;
using ParadoxModTool.Notes;

namespace ParadoxModTool.App;

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

        HeaderText.Text = filePath is null ? "Notizen & Lesezeichen" : Path.GetFileName(filePath);
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

        HasContent = !string.IsNullOrEmpty(note) || bookmarks.Count > 0 || bugsVisible;
        EmptyHint.Visibility = HasContent || ScopeAvailable ? Visibility.Collapsed : Visibility.Visible;
    }

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
            ScopeCurrentText.Text = "Scope: außerhalb eines Objekts";
            ScopeCurrentText.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
            ScopeFieldText.Text = string.Empty;
        }
        else
        {
            ScopeCurrentText.Text = context.Describe();
            ScopeCurrentText.SetResourceReference(TextBlock.ForegroundProperty, context.Confident ? "Theme.Text" : "Theme.TextMuted");
            ScopeFieldText.Text = context.FieldPath is null
                ? string.Empty
                : $"Feld: {context.FieldPath}" + (context.StartScope is null ? string.Empty : $"  (Start-Scope {context.StartScope}, Zustimmung {context.StartSupport:P0})");
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
        ScopeIssuesHeader.Text = $"⚠ Warnungen ({issues.Count})";
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
