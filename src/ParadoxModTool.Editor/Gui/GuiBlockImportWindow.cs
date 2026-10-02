using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Blocks;

namespace ParadoxModTool.Editor.Gui;

/// <summary>Zeile der Import-Vorschau: Häkchen, Status ("neu", "gibt es schon", "fehlerhaft") und "Überschreiben" bei Namenskonflikt.</summary>
internal sealed class GuiBlockImportRow : INotifyPropertyChanged
{
    private bool _checked;
    private bool _overwrite;
    private bool _isConflict;

    public GuiBlockImportRow(GuiBlockPackageItem item)
    {
        Item = item;
        _checked = item.Block.IsValid;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public GuiBlockPackageItem Item { get; }

    public string Name => Item.Block.Name;

    public string Text => $"{Name} · {Item.Block.Group}";

    public string Status => !Item.Block.IsValid ? GuiTexts.ShareStatusErrors : IsConflict ? GuiTexts.ShareStatusConflict : GuiTexts.ShareStatusOk;

    public string OverwriteText => GuiTexts.ShareOverwrite;

    public bool Checked
    {
        get => _checked;
        set => Set(ref _checked, value, nameof(Checked));
    }

    public bool Overwrite
    {
        get => _overwrite;
        set => Set(ref _overwrite, value, nameof(Overwrite));
    }

    public bool IsConflict
    {
        get => _isConflict;
        set
        {
            Set(ref _isConflict, value, nameof(IsConflict));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
        }
    }

    private void Set(ref bool field, bool value, string name)
    {
        if (field != value)
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

/// <summary>
/// "Bausteine importieren" (Plan-2b-Ruling 12): Vorschau mit Name, Gruppe und Status; fehlerhafte Bausteine vorab nicht angehakt, bei Namenskonflikt
/// "Überschreiben" je Zeile (Vorgabe aus = überspringen); Ziel alle Mods des Spiels oder nur die aktive Mod; Warnung bei einem Paket eines anderen Spiels.
/// </summary>
public sealed class GuiBlockImportWindow : Window
{
    private readonly GuiBlockLibrary _library;
    private readonly List<GuiBlockImportRow> _rows;
    private readonly TextBox _search = new() { Margin = new Thickness(0, 0, 0, 6) };
    private readonly ListBox _list = new();
    private readonly RadioButton _gameOption = new() { IsChecked = true, Margin = new Thickness(0, 2, 12, 2) };

    public GuiBlockImportWindow(GuiBlockPackageContent content, string? currentGame, GuiBlockLibrary library)
    {
        _library = library;
        _rows = content.Items.Select(i => new GuiBlockImportRow(i)).ToList();
        Title = GuiTexts.ShareImportTitle;
        Width = 640;
        Height = 520;
        MinWidth = 440;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        ModOption = new RadioButton { IsEnabled = library.DirectoryOf(GuiBlockLayer.Mod) is not null, Margin = new Thickness(0, 2, 0, 2) };
        _gameOption.Content = new TextBlock { Text = GuiTexts.BlocksSaveLayerGame };
        ModOption.Content = new TextBlock { Text = GuiTexts.BlocksSaveLayerMod };
        _gameOption.GroupName = ModOption.GroupName = "importLayer";
        _gameOption.Checked += (_, _) => UpdateConflicts();
        ModOption.Checked += (_, _) => UpdateConflicts();
        UpdateConflicts();

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
        GameWarningShown = content.Game is { } game && currentGame is not null && !string.Equals(game, currentGame, StringComparison.OrdinalIgnoreCase);
        if (GameWarningShown)
        {
            header.Children.Add(Warning(GuiTexts.ShareOtherGame(content.Game!)));
        }

        if (content.ManifestProblem)
        {
            header.Children.Add(Warning(GuiTexts.ShareManifestProblem));
        }

        if (content.Skipped.Count > 0)
        {
            header.Children.Add(Warning(GuiTexts.ShareSkipped(string.Join(", ", content.Skipped.Take(20)) + (content.Skipped.Count > 20 ? ", …" : string.Empty))));
        }

        header.Children.Add(new TextBlock { Text = GuiTexts.BlocksSaveLayer, Margin = new Thickness(0, 4, 0, 0) });
        var layers = new StackPanel { Orientation = Orientation.Horizontal };
        layers.Children.Add(_gameOption);
        layers.Children.Add(ModOption);
        header.Children.Add(layers);

        _search.ToolTip = GuiTexts.BlocksSearchTip;
        _search.TextChanged += (_, _) => Filter();
        _list.ItemTemplate = RowTemplate();
        VirtualizingPanel.SetIsVirtualizing(_list, true);
        Filter();

        var import = new Button { Content = GuiTexts.ShareImport, IsDefault = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2) };
        import.Click += (_, _) => DialogResult = true;
        Content = GuiBlockExportWindow.ShareLayout(_search, _list, () => SetAll(true), () => SetAll(false), import, header);
    }

    internal RadioButton ModOption { get; }

    internal bool GameWarningShown { get; }

    internal IReadOnlyList<GuiBlockImportRow> Rows => _rows;

    public GuiBlockLayer Layer => ModOption.IsChecked == true ? GuiBlockLayer.Mod : GuiBlockLayer.Game;

    /// <summary>Angehakte Bausteine mit der Wahl "Überschreiben".</summary>
    public IReadOnlyList<(GuiBlockPackageItem Item, bool Overwrite)> Chosen => _rows.Where(r => r.Checked).Select(r => (r.Item, r.Overwrite)).ToList();

    /// <summary>Übernimmt die Bausteine in die Ebene; ein Namenskonflikt ohne "Überschreiben" und ein Schreibfehler zählen als übersprungen.</summary>
    internal static (int Imported, int Skipped) Import(GuiBlockLibrary library, IReadOnlyList<(GuiBlockPackageItem Item, bool Overwrite)> chosen, GuiBlockLayer layer)
    {
        var imported = 0;
        var skipped = 0;
        foreach (var (item, overwrite) in chosen)
        {
            if (library.Exists(item.Block.Name, layer) && !overwrite)
            {
                skipped++;
                continue;
            }

            try
            {
                library.Save(item.Text, item.Block.Name, layer);
                imported++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                skipped++;
            }
        }

        return (imported, skipped);
    }

    internal void SetAll(bool value)
    {
        foreach (var row in _list.Items.Cast<GuiBlockImportRow>())
        {
            row.Checked = value;
        }
    }

    private void UpdateConflicts()
    {
        foreach (var row in _rows)
        {
            row.IsConflict = _library.Exists(row.Name, Layer);
        }
    }

    private void Filter()
    {
        var filter = _search.Text.Trim();
        _list.ItemsSource = _rows.Where(r => filter.Length == 0 || r.Text.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static TextBlock Warning(string text)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Warning");
        return block;
    }

    /// <summary>Häkchen mit Name und Gruppe, Status, bei Konflikt "Überschreiben".</summary>
    private static DataTemplate RowTemplate()
    {
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var check = new FrameworkElementFactory(typeof(CheckBox));
        check.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(GuiBlockImportRow.Checked)) { Mode = BindingMode.TwoWay });
        check.SetValue(MarginProperty, new Thickness(0, 1, 8, 1));
        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetBinding(TextBlock.TextProperty, new Binding(nameof(GuiBlockImportRow.Text)));
        check.AppendChild(name);
        panel.AppendChild(check);

        var status = new FrameworkElementFactory(typeof(TextBlock));
        status.SetBinding(TextBlock.TextProperty, new Binding(nameof(GuiBlockImportRow.Status)));
        status.SetValue(MarginProperty, new Thickness(0, 1, 8, 1));
        status.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        panel.AppendChild(status);

        var overwrite = new FrameworkElementFactory(typeof(CheckBox));
        overwrite.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(GuiBlockImportRow.Overwrite)) { Mode = BindingMode.TwoWay });
        overwrite.SetBinding(VisibilityProperty, new Binding(nameof(GuiBlockImportRow.IsConflict)) { Converter = new BooleanToVisibilityConverter() });
        overwrite.SetValue(MarginProperty, new Thickness(0, 1, 0, 1));
        var overwriteText = new FrameworkElementFactory(typeof(TextBlock));
        overwriteText.SetBinding(TextBlock.TextProperty, new Binding(nameof(GuiBlockImportRow.OverwriteText)));
        overwrite.AppendChild(overwriteText);
        panel.AppendChild(overwrite);

        return new DataTemplate { VisualTree = panel };
    }
}
