using System.IO;
using System.Linq;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using ParadoxModTool.Editor;
using ParadoxModTool.Notes;

namespace ParadoxModTool.App;

/// <summary>
/// Rechtes Panel "Notizen &amp; Lesezeichen" und der Lesezeichen-Button mit Kommentar (Nutzer-Feedback):
/// Verdrahtung zwischen den beiden Arbeitsbereichen, dem Panel und den Stores.
/// </summary>
public partial class MainWindow
{
    private const double DefaultNotesPanelWidth = 260;

    // Zuletzt fokussierter Arbeitsbereich: das Panel zeigt dessen aktives Dokument - auch dann, wenn der
    // Fokus gerade im Panel selbst liegt (dann wäre ActivePane immer der linke).
    private EditorPane? _lastFocusedPane;
    private double _notesPanelWidth = DefaultNotesPanelWidth;

    private EditorPane PanelPane => _lastFocusedPane ?? LeftEditor;

    private void InitializeNotesPanel()
    {
        foreach (var pane in AllPanes)
        {
            pane.ScopeInsightChanged += (_, _) =>
            {
                if (ReferenceEquals(pane, PanelPane))
                {
                    RefreshScopeSection();
                }
            };
            pane.ActiveDocumentChanged += (_, _) => RefreshNotesPanel();
            pane.GotKeyboardFocus += (_, _) =>
            {
                if (!ReferenceEquals(_lastFocusedPane, pane))
                {
                    _lastFocusedPane = pane;
                    RefreshNotesPanel();
                }
            };
        }

        NotesPanelControl.EditNoteRequested += (_, _) =>
        {
            if (PanelPane.ActiveDocument is { } document)
            {
                ShowNoteFor(document);
            }
        };
        NotesPanelControl.JumpRequested += (_, line) =>
        {
            if (PanelPane.CurrentFilePath is { } filePath)
            {
                JumpToBookmarkInFile(filePath, line);
            }
        };
        NotesPanelControl.EditBookmarkRequested += (_, entry) => EditBookmarkComment(entry);
        NotesPanelControl.RemoveBookmarkRequested += (_, entry) =>
        {
            if (PanelPane.CurrentFilePath is { } filePath)
            {
                SaveBookmarks(bookmarks => BookmarkStore.Remove(bookmarks, filePath, entry.Line), filePath);
            }
        };

        ParadoxModTool.Editor.ScopeSettings.Changed += (_, _) => RefreshScopeSection();

        InitializeBugPanel();
        RefreshNotesPanel();
    }

    /// <summary>Zeigt Scope, gespeicherte Scopes und Warnungen des Panel-Arbeitsbereichs im Abschnitt "Scope" (oder blendet ihn aus).</summary>
    private void RefreshScopeSection()
    {
        NotesPanelControl.SetScope(ParadoxModTool.Editor.ScopeSettings.PanelEnabled ? PanelPane.ScopeInsight : null);
        UpdateNotesPanelVisibility();
    }

    /// <summary>Lädt Notiz, Lesezeichen und Bugs des aktiven Dokuments ins Panel und blendet es bei Bedarf ein/aus.</summary>
    private void RefreshNotesPanel()
    {
        NotesPanelControl.Refresh(PanelPane.CurrentFilePath, BugRowsForPanel(PanelPane.CurrentFilePath));
        RefreshScopeSection();
    }

    /// <summary>
    /// Sichtbar, wenn der Nutzer es eingeschaltet hat UND es etwas anzuzeigen gibt (Auto-Ausblenden) - oder wenn
    /// "Lesezeichenleiste ständig anzeigen" gewählt ist, dann auch ohne Inhalt.
    /// </summary>
    private void UpdateNotesPanelVisibility()
    {
        var visible = _settings.NotesPanelVisible && (_settings.NotesPanelAlwaysVisible || NotesPanelControl.HasContent || NotesPanelControl.ScopeAvailable);
        var currentlyVisible = NotesPanelColumn.Width.Value > 0;

        if (visible == currentlyVisible)
        {
            return;
        }

        if (visible)
        {
            NotesPanelColumn.Width = new GridLength(_notesPanelWidth);
            NotesSplitterColumn.Width = new GridLength(4);
            NotesPanelControl.Visibility = Visibility.Visible;
            NotesSplitter.Visibility = Visibility.Visible;
        }
        else
        {
            if (NotesPanelColumn.ActualWidth > 0)
            {
                _notesPanelWidth = NotesPanelColumn.ActualWidth;
            }

            NotesPanelColumn.Width = new GridLength(0);
            NotesSplitterColumn.Width = new GridLength(0);
            NotesPanelControl.Visibility = Visibility.Collapsed;
            NotesSplitter.Visibility = Visibility.Collapsed;
        }
    }

    private void NotesPanelToggle_Click(object sender, RoutedEventArgs e) => _settings.NotesPanelVisible = NotesPanelToggle.IsChecked == true;

    private void ApplyDocumentBarVisible(bool visible)
    {
        LeftEditor.SetDocumentBarVisible(visible);
        RightEditor.SetDocumentBarVisible(visible);
    }

    /// <summary>Toolbar-Button "Lesezeichen…": Lesezeichen mit Kommentar an der Markierung bzw. Cursorzeile setzen, ändern oder entfernen.</summary>
    private void BookmarkWithComment_Click(object sender, RoutedEventArgs e)
    {
        var pane = ActivePane;

        if (pane.CurrentFilePath is not { } filePath)
        {
            MessageBox.Show(
                "Für dieses Dokument gibt es noch keine Datei auf der Platte - bitte zuerst " +
                "speichern, bevor ein Lesezeichen gesetzt werden kann.",
                "Hinweis");
            return;
        }

        if (pane.CurrentLineRange is not { } range)
        {
            return;
        }

        var bookmarks = BookmarkStore.TryLoad(BookmarkStore.GetDefaultBookmarksFilePath());
        var existing = BookmarkStore.FindContaining(bookmarks, filePath, range.StartLine);

        var startLine = existing?.Line ?? range.StartLine;
        var endLine = existing is not null ? existing.EndLine : (range.EndLine > range.StartLine ? range.EndLine : null);
        var preview = pane.GetLineText(startLine).Trim();
        var lineText = new BookmarkEntry { Line = startLine, EndLine = endLine }.LineText;

        var dialog = new BookmarkCommentWindow(lineText, preview, existing?.Comment ?? string.Empty, canRemove: existing is not null)
        {
            Owner = this,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        // Die Änderung wird auf dem beim Speichern frisch gelesenen Stand angewendet (siehe SaveBookmarks).
        var removeLine = dialog.Removed ? existing?.Line : null;
        var comment = dialog.Comment;
        SaveBookmarks(
            bookmarks =>
            {
                if (removeLine is { } line)
                {
                    BookmarkStore.Remove(bookmarks, filePath, line);
                }
                else
                {
                    BookmarkStore.Set(bookmarks, filePath, startLine, endLine, comment, preview);
                }
            },
            filePath);
    }

    /// <summary>Kommentar eines Lesezeichens aus dem Panel ändern.</summary>
    private void EditBookmarkComment(BookmarkEntry entry)
    {
        if (PanelPane.CurrentFilePath is not { } filePath)
        {
            return;
        }

        var dialog = new BookmarkCommentWindow(entry.LineText, entry.Preview, entry.Comment, canRemove: false)
        {
            Owner = this,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        SaveBookmarks(bookmarks => BookmarkStore.Set(bookmarks, filePath, entry.Line, entry.EndLine, dialog.Comment, entry.Preview), filePath);
    }

    /// <summary>Wendet die Änderung auf den frisch gelesenen Lesezeichen-Stand an, speichert und zieht Tab-Icons und Panel nach.</summary>
    private void SaveBookmarks(Action<Dictionary<string, List<BookmarkEntry>>> change, string filePath)
    {
        try
        {
            BookmarkStore.Update(BookmarkStore.GetDefaultBookmarksFilePath(), change);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"Lesezeichen konnte nicht gespeichert werden: {ex.Message}", "Fehler");
        }

        foreach (var document in AllPanes.SelectMany(p => p.Documents).Where(d => EditorPane.PathsEqual(d.FilePath, filePath)))
        {
            UpdateHasNote(document);
        }

        RefreshNotesPanel();
    }
}
