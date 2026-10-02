using System.IO;
using System.Linq;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using ParadoxModTool.Editor;
using ParadoxModTool.Localization;
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

        NotesPanelControl.OpenReplaceWarningRequested += (_, hit) => OpenReplaceWarning(hit);
        NotesPanelControl.ClearReplaceWarningsRequested += (_, _) => ShowReplaceWarnings(Array.Empty<BlockWarningHit>());
        NotesPanelControl.RefreshReplaceWarningsRequested += (_, _) => RefreshReplaceWarnings();
        ReplaceWarningHub.Reported += ShowReplaceWarnings;
        Closed += (_, _) => ReplaceWarningHub.Reported -= ShowReplaceWarnings;

        InitializeBugPanel();
        RefreshNotesPanel();
    }

    /// <summary>Zeigt Scope, gespeicherte Scopes und Warnungen des Panel-Arbeitsbereichs im Abschnitt "Scope" (oder blendet ihn aus).</summary>
    /// <summary>
    /// Warnungen nach dem Ersetzen (OR-/NOR-/AND-Blöcke mit höchstens einem Eintrag): standardmäßig im Abschnitt des Notiz-Panels, mit der Einstellung
    /// "in eigenem Fenster" oder bei ausgeschaltetem Panel in einem eigenen Fenster. Eine leere Liste räumt die Anzeige ab.
    /// </summary>
    private void ShowReplaceWarnings(IReadOnlyList<BlockWarningHit> hits) => ShowReplaceWarnings(hits, autoRefresh: false);

    /// <param name="autoRefresh">Die Liste kommt vom automatischen Aktualisieren beim Speichern: Ein offenes Warnfenster wird dann nur ersetzt, wenn sich die Liste geändert hat.</param>
    private void ShowReplaceWarnings(IReadOnlyList<BlockWarningHit> hits, bool autoRefresh)
    {
        // Eine Stelle im aktiven Dokument bekommt dessen Pfad, damit das spätere Aktualisieren und das Öffnen auch nach einem Tabwechsel die richtige Datei treffen.
        var activePath = ActivePane.CurrentFilePath;
        hits = hits.Select(hit => hit.FilePath is null && activePath is not null ? hit with { FilePath = activePath } : hit).ToList();
        var unchanged = hits.SequenceEqual(_replaceWarnings);
        _replaceWarnings = hits;
        var inWindow = _settings.ReplaceWarningsInWindow || !_settings.NotesPanelVisible;
        NotesPanelControl.SetReplaceWarnings(inWindow ? Array.Empty<BlockWarningHit>() : hits);
        UpdateNotesPanelVisibility();
        // Das Speichern öffnet kein Fenster, das der Nutzer geschlossen hat; es ersetzt nur ein offenes, wenn sich die Liste geändert hat.
        if (!inWindow || (autoRefresh && (unchanged || _warningWindow is null)))
        {
            return;
        }

        // Ein neuer Stand ersetzt das Fenster des vorigen (sonst blieben veraltete Listen offen); ohne Fundstellen schließt es sich nur.
        _warningWindow?.Close();
        var window = BlockWarningWindow.ShowIfAny(this, hits, OpenReplaceWarning);
        _warningWindow = window;
        if (window is not null)
        {
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(_warningWindow, window))
                {
                    _warningWindow = null;
                }
            };
        }
    }

    private BlockWarningWindow? _warningWindow;

    private IReadOnlyList<BlockWarningHit> _replaceWarnings = Array.Empty<BlockWarningHit>();

    /// <summary>
    /// Nach dem Speichern von <paramref name="savedPath"/>: Hat das letzte Ersetzen dort (oder im Dokument ohne Pfad) Warnungen hinterlassen, werden alle
    /// Warnungen gegen den aktuellen Text aktualisiert wie mit dem Knopf. Ohne passende Warnungen geschieht nichts.
    /// </summary>
    private void RefreshReplaceWarningsAfterSave(string savedPath)
    {
        if (ReplaceWarningsAffectedBySave(_replaceWarnings, savedPath))
        {
            RefreshReplaceWarnings(autoRefresh: true);
        }
    }

    /// <summary>Ob unter den Warnungen eine Stelle der gespeicherten Datei (oder ohne Pfad) steht.</summary>
    internal static bool ReplaceWarningsAffectedBySave(IReadOnlyList<BlockWarningHit> warnings, string savedPath) =>
        warnings.Any(hit => hit.FilePath is null || string.Equals(hit.FilePath, savedPath, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Aktualisiert die Warnungen gegen den aktuellen Text: Stellen, die inzwischen behoben (oder rückgängig gemacht) sind, fallen weg, die übrigen bekommen
    /// ihre aktuelle Zeile. Gelesen wird der offene Tab (auch ungespeichert), sonst die Datei auf der Platte; eine nicht lesbare Datei lässt ihre Stellen stehen.
    /// </summary>
    private void RefreshReplaceWarnings(bool autoRefresh = false)
    {
        var refreshed = new List<BlockWarningHit>();
        foreach (var group in _replaceWarnings.GroupBy(hit => hit.FilePath ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            var text = group.Key.Length == 0
                ? ActivePane.CurrentText
                : LeftEditor.TryGetOpenText(group.Key) ?? RightEditor.TryGetOpenText(group.Key) ?? ReadTextOrNull(group.Key);
            if (text is null)
            {
                refreshed.AddRange(group);
                continue;
            }

            var hits = group.ToList();
            var still = ParadoxModTool.SearchReplace.SingleEntryBlockFinder.StillPresent(hits.Select(hit => hit.Block).ToList(), text);
            var display = hits[0].File;
            refreshed.AddRange(still.Select(block => new BlockWarningHit(display, block, group.Key.Length == 0 ? null : group.Key)));
        }

        ShowReplaceWarnings(refreshed, autoRefresh);
    }

    private static string? ReadTextOrNull(string path)
    {
        try
        {
            return File.ReadAllText(path, System.Text.Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Öffnet die Stelle einer Warnung: die Datei an der Stelle, ohne Datei das aktive Dokument.</summary>
    private void OpenReplaceWarning(BlockWarningHit hit)
    {
        if (hit.FilePath is { } path)
        {
            OpenSearchResult(path, hit.Block.Offset, hit.Block.Keyword.Length);
        }
        else
        {
            SelectInActiveDocument(hit.Block.Offset, hit.Block.Keyword.Length);
        }
    }

    private void RefreshScopeSection()
    {
        NotesPanelControl.SetScope(ParadoxModTool.Editor.ScopeSettings.PanelEnabled ? PanelPane.ScopeInsight : null);
        UpdateNotesPanelVisibility();
    }

    /// <summary>Lädt Notiz, Lesezeichen und Bugs des aktiven Dokuments ins Panel und blendet es bei Bedarf ein/aus.</summary>
    private void RefreshNotesPanel()
    {
        NotesPanelControl.Refresh(PanelPane.CurrentFilePath, BugRowsForPanel(PanelPane.CurrentFilePath));
        NotesPanelControl.SetErrors(ErrorItemsFor(PanelPane.CurrentFilePath));
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
                MainTexts.NoFileYet(MainTexts.NoFileYetLesezeichen),
                CommonTexts.Notice);
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
            MessageBox.Show(MainTexts.BookmarkSaveFailed(ex.Message), CommonTexts.Error);
        }

        foreach (var document in AllPanes.SelectMany(p => p.Documents).Where(d => EditorPane.PathsEqual(d.FilePath, filePath)))
        {
            UpdateHasNote(document);
        }

        RefreshNotesPanel();
    }
}
