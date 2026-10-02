using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Blocks;
using ParadoxModTool.GuiDesign.Index;

namespace ParadoxModTool.Editor.Gui;

/// <summary>Aufruf des Fensters "Bausteine": Vorauswahl, Vorbelegungen oberster Ebene, Beschriftung und Freigabe des Aktionsknopfs.</summary>
public sealed record GuiBlockWindowOptions(string? Preselect, IReadOnlyDictionary<string, string>? Presets, string ActionLabel, bool CanAct)
{
    /// <summary>Spiel-ID des aktiven Spiels: steht im exportierten Paket und löst beim Import eines fremden Pakets eine Warnung aus.</summary>
    public string? GameId { get; init; }

    /// <summary>PMT-Version für das Manifest exportierter Pakete.</summary>
    public string? PmtVersion { get; init; }
}

/// <summary>
/// Fenster "Bausteine" (docs/superpowers/specs/2026-09-27-gui-building-blocks-design.md): links Suche und Liste nach Gruppen, rechts Beschreibung,
/// Eingaben und Live-Vorschau des erzeugten Codes. Modal; das Ergebnis steht in <see cref="Result"/>, "Datei öffnen" in <see cref="FileToOpen"/>.
/// </summary>
public sealed class GuiBlockWindow : Window
{
    private GuiBlockLibrary _library;
    private readonly GuiIndex? _index;
    private readonly GuiBlockWindowOptions _options;
    private readonly TextBox _search = new() { Margin = new Thickness(0, 0, 0, 6) };
    private readonly ListBox _list = new();
    private readonly TextBlock _noShipped = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly TextBlock _description = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
    private readonly ContentControl _inputs = new();
    private readonly Border _missing = new() { BorderThickness = new Thickness(1), Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 6, 0, 6) };
    private readonly TextBlock _missingText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextEditor _preview = new()
    {
        IsReadOnly = true,
        FontFamily = new FontFamily("Consolas"),
        FontSize = 12,
        ShowLineNumbers = false,
        HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
    };

    private readonly Button _act = new() { MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), IsDefault = true };
    private readonly Button _openFile = new() { MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 8, 0) };
    private readonly Button _delete = new() { MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 8, 0) };
    private readonly Button _ownCopy = new() { MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 8, 0) };
    private GuiBlock? _selected;
    private GuiBlockValues? _values;

    public GuiBlockWindow(GuiBlockLibrary library, GuiIndex? index, IHighlightingDefinition? highlighting, GuiBlockWindowOptions options)
    {
        _library = library;
        _index = index;
        _options = options;
        Title = GuiTexts.BlocksTitle;
        Width = 1000;
        Height = 680;
        MinWidth = 640;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        _preview.SyntaxHighlighting = highlighting;
        _preview.SetResourceReference(Control.BackgroundProperty, "Theme.EditorBackground");
        _preview.SetResourceReference(Control.ForegroundProperty, "Theme.Text");
        _search.ToolTip = GuiTexts.BlocksSearchTip;
        _search.TextChanged += (_, _) => FillList(_selected?.Name);
        _list.SelectionChanged += (_, _) => Select((_list.SelectedItem as ListBoxItem)?.Tag as GuiBlock);
        _list.MouseDoubleClick += (_, _) => Act();
        _noShipped.Text = GuiTexts.BlocksNoShipped;
        _noShipped.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        _description.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        _missing.SetResourceReference(Border.BorderBrushProperty, "Theme.Warning");
        _missing.SetResourceReference(Border.BackgroundProperty, "Theme.NoticeBackground");
        _missing.Child = _missingText;

        var left = new DockPanel { Margin = new Thickness(0, 0, 10, 0) };
        DockPanel.SetDock(_search, Dock.Top);
        left.Children.Add(_search);
        var share = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        var export = new Button { Content = GuiTexts.ShareExport, ToolTip = GuiTexts.ShareExportTip, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 6, 0) };
        export.Click += (_, _) => ExportBlocks();
        var import = new Button { Content = GuiTexts.ShareImport, ToolTip = GuiTexts.ShareImportTip, Padding = new Thickness(8, 1, 8, 1) };
        import.Click += (_, _) => ImportBlocks();
        share.Children.Add(export);
        share.Children.Add(import);
        DockPanel.SetDock(share, Dock.Bottom);
        left.Children.Add(share);
        DockPanel.SetDock(_noShipped, Dock.Bottom);
        left.Children.Add(_noShipped);
        left.Children.Add(_list);

        var inputsScroll = new ScrollViewer { Content = _inputs, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var previewLabel = new TextBlock { Text = GuiTexts.BlocksPreview, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 2) };
        var previewBorder = new Border { BorderThickness = new Thickness(1), Child = _preview };
        previewBorder.SetResourceReference(Border.BorderBrushProperty, "Theme.ControlBorder");
        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.2, GridUnitType.Star) });
        Add(right, _description, 0);
        Add(right, inputsScroll, 1);
        Add(right, _missing, 2);
        Add(right, previewLabel, 3);
        Add(right, previewBorder, 4);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(left);
        Grid.SetColumn(right, 1);
        body.Children.Add(right);

        _act.Content = options.ActionLabel;
        _act.Click += (_, _) => Act();
        _openFile.Content = GuiTexts.BlocksOpenFile;
        _openFile.Click += (_, _) => OpenFile();
        _delete.Content = GuiTexts.BlocksDelete;
        _delete.Click += (_, _) => Delete();
        _ownCopy.Content = GuiTexts.BlocksOwnCopy;
        _ownCopy.ToolTip = GuiTexts.BlocksOwnCopyTip;
        _ownCopy.Click += (_, _) => ShowOwnCopyMenu();
        var close = new Button { Content = GuiTexts.BlocksClose, IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        var buttons = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = false };
        var ownButtons = new StackPanel { Orientation = Orientation.Horizontal };
        ownButtons.Children.Add(_ownCopy);
        ownButtons.Children.Add(_openFile);
        ownButtons.Children.Add(_delete);
        DockPanel.SetDock(ownButtons, Dock.Left);
        buttons.Children.Add(ownButtons);
        var mainButtons = new StackPanel { Orientation = Orientation.Horizontal };
        mainButtons.Children.Add(_act);
        mainButtons.Children.Add(close);
        DockPanel.SetDock(mainButtons, Dock.Right);
        buttons.Children.Add(mainButtons);

        var root = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(body);
        Content = root;

        FillList(options.Preselect);
        Loaded += (_, _) => _search.Focus();
    }

    /// <summary>Erzeugter Code nach "Einfügen" bzw. "Anlegen".</summary>
    public string? Result { get; private set; }

    /// <summary>Datei, die nach "Datei öffnen" im Editor geöffnet werden soll.</summary>
    public string? FileToOpen { get; private set; }

    private static void Add(Grid grid, UIElement element, int row)
    {
        Grid.SetRow(element, row);
        grid.Children.Add(element);
    }

    /// <summary>Füllt die Liste nach Gruppen (gefiltert durch die Suche) und wählt <paramref name="select"/> bzw. den ersten Baustein.</summary>
    private void FillList(string? select)
    {
        var filter = _search.Text.Trim();
        _list.Items.Clear();
        ListBoxItem? chosen = null;
        ListBoxItem? first = null;
        string? group = null;
        foreach (var block in _library.Effective.Where(b => Matches(b, filter)))
        {
            if (!string.Equals(block.Group, group, StringComparison.OrdinalIgnoreCase))
            {
                group = block.Group;
                var heading = new ListBoxItem { Content = new TextBlock { Text = group, FontWeight = FontWeights.SemiBold }, IsEnabled = false, Focusable = false };
                _list.Items.Add(heading);
            }

            var item = new ListBoxItem { Content = new TextBlock { Text = Label(block) }, Tag = block, ToolTip = Tooltip(block), Padding = new Thickness(12, 1, 2, 1) };
            _list.Items.Add(item);
            first ??= item;
            if (select is not null && string.Equals(block.Name, select, StringComparison.OrdinalIgnoreCase))
            {
                chosen = item;
            }
        }

        _noShipped.Visibility = _library.Effective.Any(b => b.Layer == GuiBlockLayer.Shipped && !b.IsBuiltIn) ? Visibility.Collapsed : Visibility.Visible;
        _list.SelectedItem = chosen ?? first;
        if (_list.SelectedItem is null)
        {
            Select(null);
        }
    }

    private static bool Matches(GuiBlock block, string filter) =>
        filter.Length == 0
        || block.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || block.Group.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || block.Description.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private static string Label(GuiBlock block)
    {
        var label = block.Layer switch
        {
            GuiBlockLayer.Game => $"{block.Name} {GuiTexts.BlocksLayerGame}",
            GuiBlockLayer.Mod => $"{block.Name} {GuiTexts.BlocksLayerMod}",
            _ => block.Name,
        };
        return block.IsValid ? label : "⚠ " + label;
    }

    private string Tooltip(GuiBlock block)
    {
        var hidden = _library.HiddenBy(block);
        var text = block.Description;
        if (hidden.Count > 0)
        {
            var layers = string.Join(", ", hidden.Select(h => h.Layer switch
            {
                GuiBlockLayer.Game => GuiTexts.BlocksLayerGame,
                GuiBlockLayer.Mod => GuiTexts.BlocksLayerMod,
                _ => GuiTexts.BlocksLayerShipped,
            }));
            text = (text.Length > 0 ? text + "\n" : string.Empty) + GuiTexts.BlocksHidden(layers);
        }

        return text.Length > 0 ? text : block.Name;
    }

    private void Select(GuiBlock? block)
    {
        _selected = block;
        _values = null;
        _description.Text = block?.Description ?? string.Empty;
        var own = block is not null && !block.IsBuiltIn && block.Layer != GuiBlockLayer.Shipped;
        _openFile.Visibility = _delete.Visibility = own ? Visibility.Visible : Visibility.Collapsed;
        _ownCopy.Visibility = OwnCopyOffered ? Visibility.Visible : Visibility.Collapsed;
        if (block is null)
        {
            _inputs.Content = null;
            _missing.Visibility = Visibility.Collapsed;
            _preview.Text = string.Empty;
            _act.IsEnabled = false;
            return;
        }

        if (!block.IsValid)
        {
            var errors = new StackPanel();
            var heading = new TextBlock { Text = GuiTexts.BlocksHasErrors, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
            heading.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");
            errors.Children.Add(heading);
            foreach (var error in block.Errors)
            {
                errors.Children.Add(new TextBlock { Text = GuiTexts.BlocksErrorLine(error.Line, error.Message), TextWrapping = TextWrapping.Wrap });
            }

            _inputs.Content = errors;
            _missing.Visibility = Visibility.Collapsed;
            _preview.Text = string.Empty;
            _act.IsEnabled = false;
            return;
        }

        _values = GuiBlockValues.Defaults(block);
        foreach (var (key, value) in _options.Presets ?? new Dictionary<string, string>())
        {
            if (_values.Values.ContainsKey(key))
            {
                _values.Values[key] = value;
            }
        }

        var inputs = new GuiBlockInputs(block, _values, block.Parameters, block.Repeats);
        inputs.Changed += (_, _) => UpdatePreview();
        _inputs.Content = inputs;
        var missing = GuiBlockLibrary.MissingRequirements(block, _index);
        _missingText.Text = missing.Count > 0 ? GuiTexts.BlocksMissing(string.Join(", ", missing)) : string.Empty;
        _missing.Visibility = missing.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _act.IsEnabled = _options.CanAct;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (_selected is { IsValid: true } block && _values is not null)
        {
            _preview.Text = GuiBlockRenderer.Render(block, _values);
        }
    }

    private void Act()
    {
        if (!_options.CanAct || _selected is not { IsValid: true } block || _values is null)
        {
            return;
        }

        Result = GuiBlockRenderer.Render(block, _values);
        DialogResult = true;
    }

    /// <summary>"Als eigene Kopie bearbeiten" nur für mitgelieferte Bausteine aus Dateien (Teil-2-Ruling 9).</summary>
    internal bool OwnCopyOffered => _selected is { IsBuiltIn: false, Layer: GuiBlockLayer.Shipped };

    /// <summary>Die Mod-Ebene gibt es nur mit aktiver Mod.</summary>
    internal bool OwnCopyModAvailable => _library.DirectoryOf(GuiBlockLayer.Mod) is not null;

    /// <summary>
    /// Kopiert den gewählten mitgelieferten Baustein in eine eigene Ebene und liefert den Pfad; null bei Ablehnung, fehlender Ebene oder Fehler
    /// (mit Meldung). Liest die Bibliothek vorher neu, damit eine inzwischen entstandene Datei gleichen Namens erkannt und nach Rückfrage
    /// überschrieben wird.
    /// </summary>
    internal string? MakeOwnCopy(GuiBlockLayer layer, Func<string, bool> confirmOverwrite)
    {
        if (!OwnCopyOffered || _selected is not { } block || _library.DirectoryOf(layer) is null)
        {
            return null;
        }

        var fresh = _library.Reload();
        if (fresh.Exists(block.Name, layer) && !confirmOverwrite(block.Name))
        {
            return null;
        }

        try
        {
            return fresh.CopyToLayer(block, layer);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show(this, GuiTexts.BlocksOwnCopyFailed(ex.Message), GuiTexts.BlocksTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
    }

    private void ShowOwnCopyMenu()
    {
        var menu = new ContextMenu { PlacementTarget = _ownCopy, Placement = System.Windows.Controls.Primitives.PlacementMode.Top };
        foreach (var (layer, text) in new[] { (GuiBlockLayer.Game, GuiTexts.BlocksOwnCopyGame), (GuiBlockLayer.Mod, GuiTexts.BlocksOwnCopyMod) })
        {
            var item = new MenuItem { Header = new TextBlock { Text = text }, IsEnabled = layer == GuiBlockLayer.Game || OwnCopyModAvailable };
            item.Click += (_, _) =>
            {
                var path = MakeOwnCopy(layer, name =>
                    MessageBox.Show(this, GuiTexts.BlocksOwnCopyOverwrite(name), GuiTexts.BlocksTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
                if (path is not null)
                {
                    GuiSettings.NotifyBlocksChanged();
                    FileToOpen = path;
                    DialogResult = false;
                }
            };
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
    }

    /// <summary>"Exportieren": eigene Bausteine als Paket speichern.</summary>
    private void ExportBlocks()
    {
        var own = GuiBlockExportWindow.OwnBlocks(_library);
        if (own.Count == 0)
        {
            MessageBox.Show(this, GuiTexts.ShareNoOwnBlocks, GuiTexts.ShareExportTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        new GuiBlockExportWindow(own, _options.GameId ?? string.Empty, _options.PmtVersion ?? string.Empty) { Owner = this }.ShowDialog();
    }

    /// <summary>"Importieren": Paket oder .guiblock lesen, Vorschau, übernehmen; danach Liste und Palette neu.</summary>
    private void ImportBlocks()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = GuiTexts.ShareImportFilter };
        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("gui.blocks.import");
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        GuiBlockPackageContent content;
        try
        {
            content = GuiBlockPackage.Read(dialog.FileName);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, GuiTexts.ShareFailed(ex.Message), GuiTexts.ShareImportTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var window = new GuiBlockImportWindow(content, _options.GameId, _library.Reload()) { Owner = this };
        if (window.ShowDialog() != true)
        {
            return;
        }

        var (imported, skipped) = GuiBlockImportWindow.Import(_library.Reload(), window.Chosen, window.Layer);
        MessageBox.Show(this, GuiTexts.ShareResult(imported, skipped), GuiTexts.ShareImportTitle, MessageBoxButton.OK, MessageBoxImage.Information);
        _library = _library.Reload();
        FillList(_selected?.Name);
        GuiSettings.NotifyBlocksChanged();
    }

    private void OpenFile()
    {
        if (_selected?.SourcePath is not { } path)
        {
            return;
        }

        FileToOpen = path;
        DialogResult = false;
    }

    private void Delete()
    {
        if (_selected is not { } block || block.IsBuiltIn || block.Layer == GuiBlockLayer.Shipped)
        {
            return;
        }

        if (MessageBox.Show(this, GuiTexts.BlocksDeleteConfirm(block.Name), GuiTexts.BlocksTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _library.Delete(block);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show(this, GuiTexts.BlocksDeleteFailed(ex.Message), GuiTexts.BlocksTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _library = _library.Reload();
        FillList(null);
    }
}
