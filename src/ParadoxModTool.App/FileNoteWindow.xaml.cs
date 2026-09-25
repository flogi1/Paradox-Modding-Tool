using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ParadoxModTool.Notes;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Zeigt und bearbeitet die Notiz zu genau einer Datei (Nutzer-Feedback Runde 4) sowie (Nutzer-
/// Feedback Runde 10) deren Bookmarks (siehe FileNoteWindow.xaml, Tab "Bookmarks"). Lädt Notiz
/// UND Bookmarks beim Öffnen frisch von der Platte und schreibt sie jeweils erst beim expliziten
/// Speichern/Entfernen zurück, statt sie nur im Arbeitsspeicher zu halten - dieselbe "lade frisch,
/// schreibe frisch"-Vorsicht wie bei den übrigen JSON-Stores dieses Programms (siehe
/// ParadoxModTool.Notes.FileNoteStore/BookmarkStore), damit z. B. eine zwischenzeitlich von einer
/// anderen Programminstanz gespeicherte Notiz/ein Bookmark zu einer ANDEREN Datei nicht
/// versehentlich überschrieben wird.
/// </summary>
public partial class FileNoteWindow : Window
{
    private readonly string _filePath;
    private readonly string _notesFilePath;
    private readonly string _bookmarksFilePath;
    private readonly Action<int> _jumpToLine;

    /// <summary>Eine Zeile in <see cref="BookmarksList"/> - reines Anzeigeobjekt, das
    /// <see cref="ParadoxModTool.Notes.BookmarkEntry"/> um den fertig formatierten Anzeigetext
    /// ergänzt (<c>DisplayMemberPath</c> in der XAML erwartet eine einfache Eigenschaft).</summary>
    private sealed class BookmarkRow
    {
        public required int Line { get; init; }
        public required string DisplayText { get; init; }
    }

    /// <summary>
    /// <paramref name="bookmarksFilePath"/>/<paramref name="jumpToLine"/> kamen mit dem Bookmark-
    /// Feature hinzu (Nutzer-Feedback: "eine Codestelle bookmarken ... über den Notizen Reiter
    /// schnell hinspringen"). <paramref name="jumpToLine"/> stammt von MainWindow
    /// (JumpToBookmarkInFile) - dort (nicht hier) liegt die Kenntnis darüber, in welchem der
    /// beiden Arbeitsbereiche <paramref name="filePath"/> gerade offen ist bzw. geöffnet werden
    /// soll, siehe MainWindow.ShowNoteFor.
    /// </summary>
    public FileNoteWindow(string filePath, string notesFilePath, string bookmarksFilePath, Action<int> jumpToLine)
    {
        InitializeComponent();
        _filePath = filePath;
        _notesFilePath = notesFilePath;
        _bookmarksFilePath = bookmarksFilePath;
        _jumpToLine = jumpToLine;

        FileNameText.Text = $"Notiz zu: {Path.GetFileName(filePath)}";
        FileNameText.ToolTip = filePath;

        var notes = FileNoteStore.TryLoad(_notesFilePath);
        NoteTextBox.Text = FileNoteStore.GetNote(notes, _filePath);

        RefreshBookmarksList();

        Loaded += (_, _) =>
        {
            NoteTextBox.Focus();
            NoteTextBox.CaretIndex = NoteTextBox.Text.Length;
        };
    }

    private void RefreshBookmarksList()
    {
        var bookmarks = BookmarkStore.TryLoad(_bookmarksFilePath);
        var entries = BookmarkStore.GetBookmarks(bookmarks, _filePath);

        BookmarksList.ItemsSource = entries
            .Select(b => new BookmarkRow { Line = b.Line, DisplayText = $"Zeile {b.Line}: {b.Preview}" })
            .ToList();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Erneut frisch laden statt die beim Öffnen gelesene Kopie zu verwenden - siehe
            // Klassenkommentar: eine inzwischen im ANDEREN Arbeitsbereich oder einer zweiten
            // Programminstanz gespeicherte Notiz zu einer anderen Datei bleibt so erhalten.
            var text = NoteTextBox.Text;
            FileNoteStore.Update(_notesFilePath, notes => FileNoteStore.SetNote(notes, _filePath, text));
            DialogResult = true;
        }
        catch (IOException ex)
        {
            MessageBox.Show($"Notiz konnte nicht gespeichert werden: {ex.Message}", "Fehler");
        }
        catch (UnauthorizedAccessException ex)
        {
            MessageBox.Show($"Notiz konnte nicht gespeichert werden: {ex.Message}", "Fehler");
        }
    }

    /// <summary>
    /// Springt zur ausgewählten Bookmark-Zeile (Nutzer-Feedback: "über den Notizen Reiter schnell
    /// hinspringen") - schließt danach bewusst das Fenster (wie bei "Speichern"), damit der
    /// Editor dahinter sofort wieder bedienbar ist, statt weiterhin vom modalen Dialog verdeckt zu
    /// werden.
    /// </summary>
    private void JumpToBookmark_Click(object sender, RoutedEventArgs e) => JumpToSelectedBookmark();

    private void BookmarksList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => JumpToSelectedBookmark();

    private void JumpToSelectedBookmark()
    {
        if (BookmarksList.SelectedItem is not BookmarkRow row)
        {
            return;
        }

        _jumpToLine(row.Line);
        DialogResult = true;
    }

    private void RemoveBookmark_Click(object sender, RoutedEventArgs e)
    {
        if (BookmarksList.SelectedItem is not BookmarkRow row)
        {
            return;
        }

        try
        {
            BookmarkStore.Update(_bookmarksFilePath, bookmarks => BookmarkStore.Remove(bookmarks, _filePath, row.Line));
        }
        catch (IOException ex)
        {
            MessageBox.Show($"Bookmark konnte nicht entfernt werden: {ex.Message}", "Fehler");
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            MessageBox.Show($"Bookmark konnte nicht entfernt werden: {ex.Message}", "Fehler");
            return;
        }

        RefreshBookmarksList();
    }
}
