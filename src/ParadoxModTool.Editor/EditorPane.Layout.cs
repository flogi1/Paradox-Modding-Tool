using System.Windows;
using ParadoxModTool.Settings;

namespace ParadoxModTool.Editor;

/// <summary>
/// Leiste des Arbeitsbereichs in beiden Fensterlayouts (Interface-Rework 2): klassisch mit den Dateiknöpfen, im neuen Layout stattdessen
/// mit Notizen/Lesezeichen/Bug (sie wirken immer auf DIESEN Arbeitsbereich) und dem Bereichsnamen rechts.
/// </summary>
public partial class EditorPane
{
    /// <summary>Notizen-Knopf der Leiste: das Hauptfenster öffnet die Notiz zum aktiven Dokument dieses Arbeitsbereichs.</summary>
    public event EventHandler? NoteRequested;

    /// <summary>Lesezeichen-Knopf der Leiste: Lesezeichen mit Kommentar an der Cursorstelle dieses Arbeitsbereichs.</summary>
    public event EventHandler? BookmarkRequested;

    /// <summary>Bug-Knopf der Leiste: neuer Bug zur aktiven Datei dieses Arbeitsbereichs.</summary>
    public event EventHandler? BugRequested;

    /// <summary>Name des Bereichs ("A"/"B"), rechts in der Leiste des neuen Layouts.</summary>
    public string PaneName
    {
        set => PaneNameText.Text = EditorTexts.WorkspaceName(value);
    }

    /// <summary>
    /// Stellt die Leiste auf das Fensterlayout ein: klassisch zeigt die Dateiknöpfe (Neu … ⇄), das neue Layout stattdessen die
    /// Markierungsknöpfe (bei <paramref name="marks"/>) und den Bereichsnamen. Die vier Menü-Knöpfe folgen ihren Schaltern. Die
    /// Trennlinien vor und hinter den Menü-Knöpfen stehen nur, wenn auf beiden Seiten etwas sichtbar ist.
    /// </summary>
    public void SetToolbarLayout(bool classic, bool category, bool filter, bool icons, bool sort, bool marks)
    {
        var files = Show(classic);
        NewButton.Visibility = files;
        OpenButton.Visibility = files;
        SaveButton.Visibility = files;
        SaveAllButton.Visibility = files;
        CloseDocumentButton.Visibility = files;
        MoveButton.Visibility = files;

        CategoryButton.Visibility = Show(category);
        FilterBarButton.Visibility = Show(filter);
        TabIconsButton.Visibility = Show(icons);
        SortButton.Visibility = Show(sort);

        // Reihenfolge: Dateiknöpfe | Menü-Knöpfe | Markierungsknöpfe; Datei- und Markierungsknöpfe sind nie gleichzeitig sichtbar.
        var anyMenu = category || filter || icons || sort;
        var markButtons = !classic && marks;
        FileSeparator.Visibility = Show(classic && anyMenu);
        MarkSeparator.Visibility = Show(markButtons && anyMenu);

        var markVisibility = Show(markButtons);
        NoteButton.Visibility = markVisibility;
        BookmarkButton.Visibility = markVisibility;
        BugButton.Visibility = markVisibility;

        PaneNameText.Visibility = Show(!classic);
    }

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private void ApplyMarkButtonTexts()
    {
        SetText(NoteButton, MenuTexts.WorkspaceNotes, MenuTexts.ToolbarTip(ToolbarItemIds.Notes));
        SetText(BookmarkButton, MenuTexts.ToolbarLabel(ToolbarItemIds.Bookmark), MenuTexts.ToolbarTip(ToolbarItemIds.Bookmark));
        SetText(BugButton, MenuTexts.ToolbarLabel(ToolbarItemIds.Bug), MenuTexts.ToolbarTip(ToolbarItemIds.Bug));
    }

    private void NoteButton_Click(object sender, RoutedEventArgs e) => NoteRequested?.Invoke(this, EventArgs.Empty);

    private void BookmarkButton_Click(object sender, RoutedEventArgs e) => BookmarkRequested?.Invoke(this, EventArgs.Empty);

    private void BugButton_Click(object sender, RoutedEventArgs e) => BugRequested?.Invoke(this, EventArgs.Empty);
}
