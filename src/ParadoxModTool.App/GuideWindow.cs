using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Border = System.Windows.Controls.Border;
using FontFamily = System.Windows.Media.FontFamily;
using ListBox = System.Windows.Controls.ListBox;
using Point = System.Windows.Point;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;

namespace ParadoxModTool.App;

/// <summary>Art eines Blocks in einer Anleitung.</summary>
public enum GuideBlockKind
{
    Title,
    Heading,
    Paragraph,
    Example,
}

/// <summary>Ein Block einer Anleitung: Titel, Überschrift, Absatz oder Beispielkasten (Consolas). <see cref="Key"/> bei Überschriften: Sprungziel für <see cref="GuideWindow.ShowSection"/>.</summary>
public sealed record GuideBlock(GuideBlockKind Kind, string Text, string? Key = null);

/// <summary>
/// Gemeinsamer Aufbau der Anleitungsfenster: Titel, Überschriften, Absätze und Beispielkästen untereinander.
/// Mit Navigation steht links ein Inhaltsverzeichnis der Überschriften mit Suchfeld; die Suche blendet Abschnitte
/// ohne Treffer im Verzeichnis aus und hebt die Treffer im Text hervor.
/// </summary>
public class GuideWindow : Window
{
    /// <summary>Ein Abschnitt: Überschrift plus alle Blöcke bis zur nächsten Überschrift.</summary>
    private sealed class Section
    {
        public required string Label { get; init; }
        public required FrameworkElement Heading { get; init; }
        public string? Key { get; init; }
        public List<TextBlock> Texts { get; } = new();
        public override string ToString() => Label;
    }

    private readonly StackPanel _panel = new() { Margin = new Thickness(18, 14, 18, 18) };
    private readonly ScrollViewer _scroll;
    private readonly List<TextBlock> _allTexts = new();
    private readonly List<Section> _sections = new();
    private readonly ListBox? _toc;
    private readonly TextBox? _search;
    private bool _syncing;

    protected GuideWindow(string title, IReadOnlyList<GuideBlock> blocks, bool withNavigation)
    {
        Title = title;
        Width = withNavigation ? 1060 : 820;
        Height = withNavigation ? 800 : 760;
        MinWidth = 520;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        Section? current = null;
        foreach (var block in blocks)
        {
            var (element, text) = Build(block);
            _panel.Children.Add(element);
            _allTexts.Add(text);
            if (block.Kind == GuideBlockKind.Heading)
            {
                current = new Section { Label = $"{_sections.Count + 1}  {block.Text}", Heading = element, Key = block.Key };
                _sections.Add(current);
            }

            current?.Texts.Add(text);
        }

        _scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _panel };
        if (!withNavigation)
        {
            Content = _scroll;
            return;
        }

        _search = new TextBox { VerticalContentAlignment = VerticalAlignment.Center };
        _search.TextChanged += (_, _) => ApplySearch();
        var searchBar = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var icon = new TextBlock { Text = "🔍", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
        DockPanel.SetDock(icon, Dock.Left);
        searchBar.Children.Add(icon);
        searchBar.Children.Add(_search);

        _toc = new ListBox { ItemsSource = _sections.ToList(), BorderThickness = new Thickness(0) };
        ScrollViewer.SetHorizontalScrollBarVisibility(_toc, ScrollBarVisibility.Disabled);
        _toc.SelectionChanged += (_, _) => JumpToSelected();
        _toc.PreviewMouseLeftButtonUp += (_, _) => JumpToSelected();
        _scroll.ScrollChanged += (_, _) => SyncSelection();

        var navigation = new DockPanel { Margin = new Thickness(8, 10, 8, 8) };
        DockPanel.SetDock(searchBar, Dock.Top);
        navigation.Children.Add(searchBar);
        navigation.Children.Add(_toc);
        var navBorder = new Border { BorderThickness = new Thickness(0, 0, 1, 0), Child = navigation };
        navBorder.SetResourceReference(Border.BorderBrushProperty, "Theme.Separator");

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_scroll, 1);
        grid.Children.Add(navBorder);
        grid.Children.Add(_scroll);
        Content = grid;
        Loaded += (_, _) => _search.Focus();
    }

    private static (FrameworkElement Element, TextBlock Text) Build(GuideBlock block)
    {
        switch (block.Kind)
        {
            case GuideBlockKind.Title:
                var title = new TextBlock { Text = block.Text, FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap };
                return (title, title);
            case GuideBlockKind.Heading:
                var heading = new TextBlock { Text = block.Text, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 6), TextWrapping = TextWrapping.Wrap };
                return (heading, heading);
            case GuideBlockKind.Example:
                var example = new TextBlock { Text = block.Text, FontFamily = new FontFamily("Consolas"), FontSize = 13, Margin = new Thickness(10, 8, 10, 8) };
                example.SetResourceReference(TextBlock.ForegroundProperty, "Theme.EditorForeground");
                var box = new Border { BorderThickness = new Thickness(1), Margin = new Thickness(0, 2, 0, 10), Child = example };
                box.SetResourceReference(Border.BackgroundProperty, "Theme.EditorBackground");
                box.SetResourceReference(Border.BorderBrushProperty, "Theme.ControlBorder");
                return (box, example);
            default:
                var paragraph = new TextBlock { Text = block.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8), LineHeight = 20 };
                return (paragraph, paragraph);
        }
    }

    private double OffsetOf(FrameworkElement element) => element.TranslatePoint(new Point(0, 0), _panel).Y;

    /// <summary>Springt zur Überschrift mit diesem Schlüssel (F1 mit Kontext); vor dem Laden erst danach. Ein unbekannter Schlüssel ändert nichts.</summary>
    public void ShowSection(string key)
    {
        if (_sections.FirstOrDefault(s => s.Key == key) is not { } section)
        {
            return;
        }

        void Jump()
        {
            UpdateLayout();
            _scroll.ScrollToVerticalOffset(Math.Max(0, OffsetOf(section.Heading) - 8));
            if (_toc is not null && _toc.Items.Contains(section))
            {
                _syncing = true;
                _toc.SelectedItem = section;
                _toc.ScrollIntoView(section);
                _syncing = false;
            }
        }

        if (IsLoaded)
        {
            Jump();
        }
        else
        {
            Loaded += (_, _) => Jump();
        }
    }

    private void JumpToSelected()
    {
        if (_syncing || _toc?.SelectedItem is not Section section)
        {
            return;
        }

        _scroll.ScrollToVerticalOffset(Math.Max(0, OffsetOf(section.Heading) - 8));
    }

    /// <summary>Markiert im Inhaltsverzeichnis den Abschnitt, in dem der Leser gerade ist, ohne dabei zu springen.</summary>
    private void SyncSelection()
    {
        if (_toc is null || !IsLoaded)
        {
            return;
        }

        var visible = _toc.Items.Cast<Section>().ToList();
        var current = visible.LastOrDefault(s => OffsetOf(s.Heading) - 30 <= _scroll.VerticalOffset) ?? visible.FirstOrDefault();
        if (ReferenceEquals(_toc.SelectedItem, current))
        {
            return;
        }

        _syncing = true;
        _toc.SelectedItem = current;
        if (current is not null)
        {
            _toc.ScrollIntoView(current);
        }

        _syncing = false;
    }

    private void ApplySearch()
    {
        var query = _search!.Text.Trim();
        foreach (var text in _allTexts)
        {
            Highlight(text, query);
        }

        var matching = query.Length == 0
            ? _sections
            : _sections.Where(s => s.Texts.Any(t => Contains(PlainText(t), query))).ToList();
        _syncing = true;
        _toc!.ItemsSource = matching.ToList();
        _syncing = false;

        if (query.Length > 0)
        {
            var first = _allTexts.FirstOrDefault(t => Contains(PlainText(t), query));
            if (first is not null)
            {
                UpdateLayout();
                _scroll.ScrollToVerticalOffset(Math.Max(0, OffsetOf(first) - 40));
            }
        }

        SyncSelection();
    }

    // Ordinal: ein Treffer ist immer so lang wie der Suchbegriff (kulturabhängig träfe "ss" auch "ß").
    private static bool Contains(string text, string query) => text.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string PlainText(TextBlock text) => (string)(text.Tag ??= text.Text);

    /// <summary>Baut den Text eines Blocks neu auf, Treffer bekommen die Farben der Auswahl.</summary>
    private static void Highlight(TextBlock block, string query)
    {
        var text = PlainText(block);
        block.Inlines.Clear();
        var start = 0;
        while (query.Length > 0)
        {
            var index = text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                break;
            }

            if (index > start)
            {
                block.Inlines.Add(new Run(text[start..index]));
            }

            var hit = new Run(text.Substring(index, query.Length));
            hit.SetResourceReference(TextElement.BackgroundProperty, "Theme.Selection");
            hit.SetResourceReference(TextElement.ForegroundProperty, "Theme.SelectionText");
            block.Inlines.Add(hit);
            start = index + query.Length;
        }

        block.Inlines.Add(new Run(text[start..]));
    }
}
