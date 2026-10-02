using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ParadoxModTool.Settings;

namespace ParadoxModTool.Editor.Controls;

/// <summary>Eintrag einer <see cref="SearchableChoice"/>; nicht wählbare Einträge erscheinen ausgegraut, mit Tooltip.</summary>
public sealed record ChoiceItem(string Text, object? Tag, bool Enabled = true, object? ToolTip = null);

/// <summary>
/// Auswahlliste mit eigenem Suchfeld davor, für lange Listen (Nutzerregel: lange Dropdowns bekommen immer ein Suchfeld). Das Suchfeld
/// filtert die Einträge als Teilstring ohne Groß-/Kleinschreibung; die ersten <c>pinned</c> Einträge (z.B. "Alle Länder") und der gewählte
/// Eintrag bleiben immer sichtbar. Enter wählt den ersten wählbaren Treffer, Escape leert die Suche. <see cref="SelectionChanged"/> kommt nur,
/// wenn der Nutzer wählt, nicht beim Füllen oder Filtern.
/// Die Einträge sind <see cref="ChoiceItem"/>-Daten (angezeigt über <c>DisplayMemberPath</c>): ComboBoxItems mit TextBlock als Inhalt ließen
/// Fenster mit SizeToContent endlos wachsen (GUI-Bausteine Teil 1). Unterstriche bleiben sichtbar, weil die Theme-Vorlagen keine Zugriffstasten
/// lesen. Die Liste virtualisiert, damit auch Tausende Einträge (Modifier) flüssig bleiben.
/// </summary>
public sealed class SearchableChoice : StackPanel
{
    private IReadOnlyList<ChoiceItem> _items = Array.Empty<ChoiceItem>();
    private int _pinned;
    private object? _selected;
    private bool _updating;

    public SearchableChoice(double searchWidth = 90, double choiceWidth = 180)
    {
        Orientation = Orientation.Horizontal;
        Search = new TextBox { Width = searchWidth, Margin = new Thickness(0, 0, 4, 0), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = SettingsTexts.ListSearchTip };
        Choice = new ComboBox { Width = choiceWidth, DisplayMemberPath = nameof(ChoiceItem.Text) };
        Choice.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
        VirtualizingPanel.SetIsVirtualizing(Choice, true);
        VirtualizingPanel.SetVirtualizationMode(Choice, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(Choice, true);
        Choice.ItemContainerStyle = ContainerStyle();
        Children.Add(Search);
        Children.Add(Choice);
        Search.TextChanged += (_, _) => Rebuild();
        Search.PreviewKeyDown += (_, e) => e.Handled = PressInSearch(e.Key);
        Choice.SelectionChanged += (_, _) =>
        {
            if (_updating || Choice.SelectedItem is not ChoiceItem item)
            {
                return;
            }

            _selected = item.Tag;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler? SelectionChanged;

    public TextBox Search { get; }

    public ComboBox Choice { get; }

    /// <summary>Tag des gewählten Eintrags (null bei einem Eintrag ohne Tag, z.B. "Alle").</summary>
    public object? SelectedTag => _selected;

    public void SetItems(IReadOnlyList<(string Text, object? Tag)> items, object? selected, int pinned = 0) =>
        SetItems(items.Select(i => new ChoiceItem(i.Text, i.Tag)).ToList(), selected, pinned);

    /// <summary>
    /// Füllt die Liste neu und wählt den Eintrag mit <paramref name="selected"/>; gibt es ihn nicht, den ersten festen Eintrag bzw. keinen.
    /// Meldet keine Änderung.
    /// </summary>
    public void SetItems(IReadOnlyList<ChoiceItem> items, object? selected, int pinned = 0)
    {
        _items = items;
        _pinned = Math.Clamp(pinned, 0, items.Count);
        _selected = items.Any(i => Equals(i.Tag, selected)) ? selected : _pinned > 0 ? items[0].Tag : null;
        Rebuild();
    }

    /// <summary>Tastendruck im Suchfeld: Enter wählt den ersten Treffer, Escape leert die Suche. Liefert, ob die Taste verarbeitet wurde.</summary>
    public bool PressInSearch(Key key)
    {
        if (key == Key.Escape && Search.Text.Length > 0)
        {
            Search.Text = string.Empty;
            return true;
        }

        if (key == Key.Enter && Search.Text.Length > 0)
        {
            var match = _items.Skip(_pinned).FirstOrDefault(i => i.Enabled && Matches(i.Text));
            if (match is not null && !Equals(match.Tag, _selected))
            {
                _selected = match.Tag;
                Rebuild();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }

            return true;
        }

        return false;
    }

    /// <summary>Freigabe und Tooltip je Eintrag; baut auf der Theme-Vorlage für ComboBoxItem auf, sofern die Anwendung eine hat.</summary>
    private static Style ContainerStyle()
    {
        var style = new Style(typeof(ComboBoxItem), Application.Current?.TryFindResource(typeof(ComboBoxItem)) as Style);
        style.Setters.Add(new Setter(IsEnabledProperty, new Binding(nameof(ChoiceItem.Enabled))));
        style.Setters.Add(new Setter(ToolTipProperty, new Binding(nameof(ChoiceItem.ToolTip))));
        style.Setters.Add(new Setter(ToolTipService.ShowOnDisabledProperty, true));
        return style;
    }

    private bool Matches(string text) => text.Contains(Search.Text.Trim(), StringComparison.OrdinalIgnoreCase);

    private void Rebuild()
    {
        _updating = true;
        try
        {
            var shown = new List<ChoiceItem>();
            ChoiceItem? selected = null;
            for (var i = 0; i < _items.Count; i++)
            {
                var entry = _items[i];
                if (i >= _pinned && !Equals(entry.Tag, _selected) && !Matches(entry.Text))
                {
                    continue;
                }

                shown.Add(entry);
                if (selected is null && Equals(entry.Tag, _selected))
                {
                    selected = entry;
                }
            }

            Choice.ItemsSource = shown;
            Choice.SelectedItem = selected;
        }
        finally
        {
            _updating = false;
        }
    }
}
