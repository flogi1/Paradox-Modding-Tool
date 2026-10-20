using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Classic;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Auswahl eines vorhandenen Sprites (klassische Spiele) mit Suche und Vorschau samt Frame-Teilung. Gelistet wird jeder Name einmal, mit dem Eintrag, der laut
/// Ladereihenfolge gilt. HOI IV hat rund 30 000 Sprites; die Liste virtualisiert, die Suche filtert den ganzen Bestand ohne Verzögerung beim Tippen.
/// </summary>
public sealed class ClassicSpritePickerWindow : Window
{
    private readonly ClassicAssets _assets;
    private readonly IReadOnlyList<Row> _all;
    private readonly TextBox _search = new() { Margin = new Thickness(0, 2, 0, 6) };
    private readonly ListView _list = new() { Height = 300, MinWidth = 640, SelectionMode = SelectionMode.Single };
    private readonly TextBlock _count = new() { Margin = new Thickness(0, 4, 0, 0) };
    private readonly ClassicFramePreview _preview = new(width: 200, height: 120);
    private readonly TextBlock _detail = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 420, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
    private readonly Button _ok = new() { Content = GuiTexts.ClassicSpritePickerOk, IsDefault = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), IsEnabled = false };
    private IReadOnlyList<Row> _shown;

    /// <param name="assets">Die Sprite-Tabelle des aktuellen Stands.</param>
    /// <param name="current">Der Name im Eingabefeld (mit oder ohne Anführungszeichen) oder <see langword="null"/>; ist er in der Tabelle, ist er vorgewählt.</param>
    public ClassicSpritePickerWindow(ClassicAssets assets, string? current)
    {
        _assets = assets;
        _all = RowsOf(assets);
        _shown = _all;
        Title = GuiTexts.ClassicSpritePickerTitle;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        _search.ToolTip = GuiTexts.ClassicSpritePickerSearch;
        _count.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        _detail.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");

        var grid = new GridView();
        grid.Columns.Add(Column(GuiTexts.ClassicSpritePickerColName, nameof(Row.Name), 250));
        grid.Columns.Add(Column(GuiTexts.ClassicSpritePickerColKind, nameof(Row.Kind), 100));
        grid.Columns.Add(Column(GuiTexts.ClassicSpritePickerColFrames, nameof(Row.Frames), 60));
        grid.Columns.Add(Column(GuiTexts.ClassicSpritePickerColFile, nameof(Row.File), 230));
        _list.View = grid;
        VirtualizingPanel.SetIsVirtualizing(_list, true);
        VirtualizingPanel.SetVirtualizationMode(_list, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(_list, true);
        _list.SelectionChanged += (_, _) => OnSelectionChanged();
        _list.MouseDoubleClick += OnDoubleClick;
        _search.TextChanged += (_, _) => ApplyFilter();

        _ok.Click += (_, _) => Accept();
        var cancel = new Button { Content = GuiTexts.ClassicSpriteCancel, IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(_ok);
        buttons.Children.Add(cancel);

        var below = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
        below.Children.Add(_preview);
        below.Children.Add(_detail);
        var footer = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(buttons, Dock.Right);
        footer.Children.Add(buttons);

        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(_search);
        panel.Children.Add(_list);
        panel.Children.Add(_count);
        panel.Children.Add(below);
        panel.Children.Add(footer);
        Content = panel;

        ApplyFilter();
        var name = (current ?? string.Empty).Trim().Trim('"');
        if (name.Length > 0 && assets.Sprites.Find(name).Sprite is { } found)
        {
            SelectedName = found.Name;
        }

        Loaded += (_, _) =>
        {
            _search.Focus();
            if (_list.SelectedItem is { } selected)
            {
                _list.ScrollIntoView(selected);
            }
        };
    }

    /// <summary>Der gewählte Spritename; leer, solange nichts gewählt wurde.</summary>
    public string Chosen { get; private set; } = string.Empty;

    internal string SearchText
    {
        get => _search.Text;
        set => _search.Text = value;
    }

    /// <summary>Die Namen der gezeigten (gefilterten) Zeilen in Listenreihenfolge.</summary>
    internal IReadOnlyList<string> ShownNames => _shown.Select(r => r.Name).ToList();

    internal string? SelectedName
    {
        get => (_list.SelectedItem as Row)?.Name;
        set
        {
            var row = _shown.FirstOrDefault(r => string.Equals(r.Name, value, StringComparison.Ordinal));
            _list.SelectedItem = row;
        }
    }

    internal Button OkButton => _ok;

    internal ListView List => _list;

    /// <summary>Übernimmt die gewählte Zeile als <see cref="Chosen"/>; <see langword="false"/>, wenn nichts gewählt ist.</summary>
    internal bool Choose()
    {
        if (_list.SelectedItem is not Row row)
        {
            return false;
        }

        Chosen = row.Name;
        return true;
    }

    private void Accept()
    {
        if (Choose() && IsVisible)
        {
            DialogResult = true;
        }
    }

    private void OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Nur ein Doppelklick auf eine Zeile wählt (nicht auf Spaltenköpfe oder die Rollleiste).
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(_list, source) is ListViewItem item && item.Content is Row row)
        {
            _list.SelectedItem = row;
            Accept();
        }
    }

    private void ApplyFilter()
    {
        var text = _search.Text.Trim();
        _shown = text.Length == 0 ? _all : _all.Where(r => r.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        var selected = SelectedName;
        _list.ItemsSource = _shown;
        if (selected is not null)
        {
            SelectedName = selected;
        }

        _count.Text = GuiTexts.ClassicSpritePickerCount(_shown.Count, _all.Count);
        OnSelectionChanged();
    }

    private void OnSelectionChanged()
    {
        _ok.IsEnabled = _list.SelectedItem is Row;
        if (_list.SelectedItem is not Row { Sprite: var sprite })
        {
            _preview.Show(null, 1);
            _detail.Text = string.Empty;
            return;
        }

        if (sprite.TexturePath is not { Length: > 0 } texture)
        {
            _preview.Show(null, 1);
            _detail.Text = GuiTexts.ClassicSpritePickerNoTexture;
            return;
        }

        var located = _assets.Textures.Locate(texture);
        _preview.Show(located, sprite.Frames);
        _detail.Text = located is null ? GuiTexts.ClassicSpritePickerTextureMissing(texture) : GuiTexts.ClassicSpritePickerTexture(texture);
    }

    private static GridViewColumn Column(string header, string member, double width) =>
        new() { Header = header, Width = width, DisplayMemberBinding = new System.Windows.Data.Binding(member) };

    /// <summary>Jeder Name einmal, mit dem wirksamen Eintrag, nach Name sortiert.</summary>
    private static IReadOnlyList<Row> RowsOf(ClassicAssets assets)
    {
        var rows = new List<Row>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sprite in assets.Sprites.Sprites)
        {
            if (seen.Add(sprite.Name) && assets.Sprites.Find(sprite.Name).Sprite is { } winner)
            {
                rows.Add(new Row(winner.Name, winner.Kind.ToString(), winner.Frames, winner.File.Label + ": " + winner.File.RelativePath, winner));
            }
        }

        rows.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return rows;
    }

    /// <summary>Eine Zeile der Liste. Öffentliche Eigenschaften, weil die Spalten daran binden.</summary>
    internal sealed record Row(string Name, string Kind, int Frames, string File, ClassicSprite Sprite);
}
