using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ParadoxModTool.Settings;

namespace ParadoxModTool.Editor;

/// <summary>
/// Tab-Sortierung des Arbeitsbereichs (Interface-Rework 2): "Manuell" ist die Reihenfolge des Nutzers; Name, Kategorie oder Herkunft
/// sortieren sofort und halten die Reihenfolge beim Öffnen neuer Dokumente. Ein gezogener Tab schaltet zurück auf "Manuell".
/// Alle Einfügungen in die Tab-Liste laufen über <see cref="InsertDocument"/>.
/// </summary>
public partial class EditorPane
{
    private string _tabSort = TabSorting.Manual;

    /// <summary>Größer null, solange <see cref="SortAfterwards"/> läuft.</summary>
    private int _sortDeferred;

    /// <summary>Ausgelöst, wenn sich <see cref="TabSort"/> ändert (auch durch Ziehen eines Tabs).</summary>
    public event EventHandler? TabSortChanged;

    /// <summary>
    /// Sortierung der Tabs (<see cref="TabSorting"/>-Werte; Unbekanntes wird "Manuell"). Eine neue Sortierung ordnet die offenen Tabs sofort
    /// um; das aktive Dokument bleibt aktiv.
    /// </summary>
    public string TabSort
    {
        get => _tabSort;
        set
        {
            var normalized = TabSorting.Normalize(value);
            if (normalized == _tabSort)
            {
                return;
            }

            _tabSort = normalized;
            if (normalized != TabSorting.Manual)
            {
                ApplyTabSort();
            }

            TabSortChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Menü mit den vier Sortierungen als Häkchen-Einträge (<c>Tag</c> = Wert); ein Klick setzt <see cref="TabSort"/>.</summary>
    public ContextMenu BuildSortMenu()
    {
        var menu = new ContextMenu();
        foreach (var (value, label) in new[]
                 {
                     (TabSorting.Manual, MenuTexts.WorkspaceSortManual),
                     (TabSorting.Name, MenuTexts.WorkspaceSortName),
                     (TabSorting.Category, MenuTexts.WorkspaceSortCategory),
                     (TabSorting.Origin, MenuTexts.WorkspaceSortOrigin),
                 })
        {
            var item = new MenuItem { Header = label, Tag = value, IsCheckable = true, IsChecked = value == _tabSort };
            var chosen = value;
            item.Click += (_, _) => TabSort = chosen;
            menu.Items.Add(item);
        }

        return menu;
    }

    /// <summary>
    /// Verschiebt ein Dokument dieses Arbeitsbereichs vor <paramref name="before"/> (oder ans Ende) und schaltet die Sortierung auf
    /// "Manuell" (derselbe Weg wie das Ziehen eines Tabs). Bleibt der Tab an seiner Stelle (auf sich selbst oder als letzter ans Ende
    /// abgelegt), bleibt auch die Sortierung.
    /// </summary>
    public void MoveDocument(OpenDocument document, OpenDocument? before)
    {
        var oldIndex = _documents.IndexOf(document);
        if (oldIndex < 0)
        {
            return;
        }

        var newIndex = before is null ? _documents.Count - 1 : _documents.IndexOf(before);
        if (newIndex < 0 || newIndex == oldIndex)
        {
            return;
        }

        _documents.Move(oldIndex, newIndex);
        if (_tabSort != TabSorting.Manual)
        {
            _tabSort = TabSorting.Manual;
            TabSortChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SortButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = BuildSortMenu();
        menu.PlacementTarget = SortButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    /// <summary>Fügt ein Dokument ein: manuell vor <paramref name="before"/> bzw. ans Ende, sonst an der sortierten Stelle.</summary>
    private void InsertDocument(OpenDocument document, OpenDocument? before)
    {
        int index;
        if (_tabSort == TabSorting.Manual)
        {
            index = before is null ? -1 : _documents.IndexOf(before);
            if (index < 0)
            {
                index = _documents.Count;
            }
        }
        else
        {
            index = TabSorting.InsertIndex(_documents, document, _tabSort);
        }

        _documents.Insert(index, document);
    }

    /// <summary>
    /// Ein Sortierschlüssel eines Dokuments hat sich geändert (Umbenennen, Kategorie, Herkunft): Tabs neu ordnen. Änderungen wie
    /// ungespeichert oder Notiz/Lesezeichen/Bug ordnen nichts um; während <see cref="SortAfterwards"/> wird erst am Ende sortiert.
    /// </summary>
    private void Document_PropertyChangedForSort(object? sender, PropertyChangedEventArgs e)
    {
        if (_sortDeferred == 0
            && e.PropertyName is nameof(OpenDocument.DisplayName) or nameof(OpenDocument.Category) or nameof(OpenDocument.Origin))
        {
            ApplyTabSort();
        }
    }

    /// <summary>
    /// Führt eine Änderung an vielen Dokumenten aus (Schleife über die Tabs) und sortiert erst danach einmal - ein Umsortieren je Dokument
    /// würde die Tab-Liste mitten in der Schleife verändern.
    /// </summary>
    private void SortAfterwards(Action change)
    {
        _sortDeferred++;
        try
        {
            change();
        }
        finally
        {
            _sortDeferred--;
        }

        if (_sortDeferred == 0)
        {
            ApplyTabSort();
        }
    }

    /// <summary>Ordnet die offenen Tabs nach <see cref="TabSort"/> um, mit möglichst wenigen Verschiebungen (die Auswahl bleibt erhalten).</summary>
    private void ApplyTabSort()
    {
        if (_tabSort == TabSorting.Manual)
        {
            return;
        }

        var sorted = TabSorting.Sorted(_documents, _tabSort);
        for (var i = 0; i < sorted.Count; i++)
        {
            if (!ReferenceEquals(_documents[i], sorted[i]))
            {
                _documents.Move(_documents.IndexOf(sorted[i]), i);
            }
        }
    }
}
