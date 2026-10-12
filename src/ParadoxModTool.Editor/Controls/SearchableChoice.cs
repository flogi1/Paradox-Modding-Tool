using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ParadoxModTool.Settings;

namespace ParadoxModTool.Editor.Controls;

/// <summary>Eintrag einer <see cref="SearchableChoice"/>; nicht wählbare Einträge erscheinen ausgegraut, mit Tooltip.</summary>
public sealed record ChoiceItem(string Text, object? Tag, bool Enabled = true, object? ToolTip = null);

/// <summary>
/// Auswahlliste mit Suchfeld oben im aufgeklappten Teil, für lange Listen (Nutzerregel: lange Dropdowns bekommen immer ein Suchfeld). Beim
/// Aufklappen liegt der Fokus im Suchfeld; es filtert die Einträge als Teilstring ohne Groß-/Kleinschreibung; die ersten <c>pinned</c> Einträge
/// (z.B. "Alle Länder") und der gewählte Eintrag bleiben immer sichtbar. Enter wählt den ersten wählbaren Treffer, Escape leert die Suche, beim
/// Zuklappen wird sie geleert. <see cref="SelectionChanged"/> kommt nur, wenn der Nutzer wählt, nicht beim Füllen oder Filtern.
/// Die Einträge sind <see cref="ChoiceItem"/>-Daten (angezeigt über <c>DisplayMemberPath</c>): ComboBoxItems mit TextBlock als Inhalt ließen
/// Fenster mit SizeToContent endlos wachsen (GUI-Bausteine Teil 1). Unterstriche bleiben sichtbar, weil die Theme-Vorlagen keine Zugriffstasten
/// lesen. Die Liste virtualisiert, damit auch Tausende Einträge (Modifier) flüssig bleiben.
/// </summary>
public sealed class SearchableChoice : StackPanel
{
    private readonly Grid _searchHost;
    private IReadOnlyList<ChoiceItem> _items = Array.Empty<ChoiceItem>();
    private bool _searchPlaced;
    private int _pinned;
    private object? _selected;
    private bool _updating;

    /// <summary><paramref name="searchWidth"/>: Mindestbreite des Suchfelds im aufgeklappten Teil; <paramref name="choiceWidth"/>: Breite der Liste.</summary>
    public SearchableChoice(double searchWidth = 90, double choiceWidth = 180)
    {
        Orientation = Orientation.Horizontal;
        Search = new TextBox { VerticalContentAlignment = VerticalAlignment.Center, ToolTip = SettingsTexts.ListSearchTip, IsTabStop = true };
        var placeholder = new TextBlock { Text = SettingsTexts.ListSearchPlaceholder, IsHitTestVisible = false, Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        placeholder.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        _searchHost = new Grid { MinWidth = searchWidth, Margin = new Thickness(4) };
        _searchHost.Children.Add(Search);
        _searchHost.Children.Add(placeholder);
        Choice = new ComboBox { Width = choiceWidth, DisplayMemberPath = nameof(ChoiceItem.Text), IsTextSearchEnabled = false };
        Choice.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
        VirtualizingPanel.SetIsVirtualizing(Choice, true);
        VirtualizingPanel.SetVirtualizationMode(Choice, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(Choice, true);
        Choice.ItemContainerStyle = ContainerStyle();
        Children.Add(Choice);
        Choice.Loaded += (_, _) => PlaceSearchInDropDown();
        Choice.DropDownOpened += (_, _) =>
        {
            PlaceSearchInDropDown();
            ClearSearch();
            // Nach dem Öffnen setzt die ComboBox den Fokus auf ihren Eintrag; danach ins Suchfeld.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => Keyboard.Focus(Search));
        };
        // Jedes Aufklappen beginnt mit voller Liste; beim Zuklappen leeren, damit die geschlossene Liste nicht gefiltert bleibt.
        Choice.DropDownClosed += (_, _) => ClearSearch();
        Search.TextChanged += (_, _) =>
        {
            placeholder.Visibility = Search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            Rebuild();
        };
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

    /// <summary>
    /// Tastendruck im Suchfeld: Enter wählt den ersten Treffer und schließt die Liste, Pfeil nach unten geht in die Liste, Escape leert die Suche
    /// bzw. schließt die Liste, wenn die Suche schon leer ist. Liefert, ob die Taste verarbeitet wurde.
    /// </summary>
    public bool PressInSearch(Key key)
    {
        if (key == Key.Escape)
        {
            if (Search.Text.Length > 0)
            {
                Search.Text = string.Empty;
                return true;
            }

            if (Choice.IsDropDownOpen)
            {
                Choice.IsDropDownOpen = false;
                return true;
            }

            return false;
        }

        if (key == Key.Down && Choice.IsDropDownOpen && Choice.Items.Count > 0)
        {
            var index = Math.Max(0, Choice.SelectedIndex);
            if (Choice.ItemContainerGenerator.ContainerFromIndex(index) is ComboBoxItem item)
            {
                item.Focus();
            }

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

            Choice.IsDropDownOpen = false;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Hängt das Suchfeld oben in den aufklappenden Teil der ComboBox (Popup <c>PART_Popup</c> der Vorlage): beim PMT-Theme in dessen Rahmen
    /// über die Liste, bei anderen Vorlagen mit eigenem Rahmen um den ganzen Inhalt.
    /// </summary>
    private void PlaceSearchInDropDown()
    {
        if (_searchPlaced)
        {
            return;
        }

        Choice.ApplyTemplate();
        if (Choice.Template?.FindName("PART_Popup", Choice) is not Popup { Child: { } content } popup)
        {
            return;
        }

        _searchPlaced = true;
        if (content is Border { Child: { } inner } border)
        {
            border.Child = null;
            border.Child = WithSearch(inner);
            return;
        }

        popup.Child = null;
        var frame = new Border { BorderThickness = new Thickness(1), Child = WithSearch(content) };
        frame.SetResourceReference(Border.BackgroundProperty, "Theme.PanelBackground");
        frame.SetResourceReference(Border.BorderBrushProperty, "Theme.ControlBorder");
        popup.Child = frame;
    }

    private void ClearSearch()
    {
        if (Search.Text.Length > 0)
        {
            Search.Text = string.Empty;
        }
    }

    private DockPanel WithSearch(UIElement list)
    {
        var panel = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_searchHost, Dock.Top);
        panel.Children.Add(_searchHost);
        panel.Children.Add(list);
        return panel;
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
