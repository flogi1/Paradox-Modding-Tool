using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ParadoxModTool.Editor.Controls;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Blocks;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Dialog "Als Baustein speichern" (docs/superpowers/specs/2026-09-27-gui-building-blocks-design.md): Name, Gruppe, Beschreibung, Ebene und der Code,
/// in den der Modder <c>$…$</c> und <c>$repeat x$ … $end x$</c> schreibt. Eine Tabelle zeigt die erkannten Platzhalter und Wiederholungen mit
/// Beschriftung, Vorgabe, Art bzw. Anzahl; daneben die Vorschau der späteren Eingaben. Gespeichert wird nur ein fehlerfreier Baustein.
/// </summary>
public sealed class GuiSaveBlockWindow : Window
{
    private readonly Dictionary<string, KeyRow> _rows = new(StringComparer.Ordinal);
    private readonly FreeTextChoice _group = new(220, 70, 160);
    private readonly TextBox _description = new() { Margin = new Thickness(0, 2, 0, 8) };
    private readonly RadioButton _gameOption = new() { IsChecked = true, Margin = new Thickness(0, 2, 12, 2) };
    private readonly Grid _table = new();
    private readonly StackPanel _errors = new() { Margin = new Thickness(0, 6, 0, 6) };
    private readonly ContentControl _preview = new();
    private readonly Button _save = new() { MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), IsDefault = true };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private IReadOnlyList<GuiBlockDraftKey> _shownKeys = Array.Empty<GuiBlockDraftKey>();

    public GuiSaveBlockWindow(string code, IReadOnlyList<string> groups, bool modAvailable, IHighlightingDefinition? highlighting)
    {
        Title = GuiTexts.BlocksSaveTitle;
        Width = 1000;
        Height = 720;
        MinWidth = 700;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        NameBox = new TextBox { Margin = new Thickness(0, 2, 0, 8) };
        ModOption = new RadioButton { IsEnabled = modAvailable, Margin = new Thickness(0, 2, 0, 2) };
        CodeEditor = new TextEditor
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            ShowLineNumbers = true,
            SyntaxHighlighting = highlighting,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = GuiBlockDraft.FromCode(code),
        };
        CodeEditor.SetResourceReference(Control.BackgroundProperty, "Theme.EditorBackground");
        CodeEditor.SetResourceReference(Control.ForegroundProperty, "Theme.Text");
        _group.SetSuggestions(groups);
        _group.Suggestions.ToolTip = GuiTexts.BlocksSaveGroup;
        _gameOption.Content = new TextBlock { Text = GuiTexts.BlocksSaveLayerGame };
        ModOption.Content = new TextBlock { Text = GuiTexts.BlocksSaveLayerMod };

        NameBox.TextChanged += (_, _) => UpdateState();
        _group.ValueChanged += (_, _) => UpdateState();
        _description.TextChanged += (_, _) => UpdateState();
        CodeEditor.TextChanged += (_, _) =>
        {
            _timer.Stop();
            _timer.Start();
        };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            RefreshNow();
        };

        Content = Layout();
        RefreshNow();
        Loaded += (_, _) => NameBox.Focus();
    }

    internal TextBox NameBox { get; }

    internal RadioButton ModOption { get; }

    internal TextEditor CodeEditor { get; }

    public string BlockName => NameBox.Text.Trim();

    public GuiBlockLayer Layer => ModOption.IsChecked == true ? GuiBlockLayer.Mod : GuiBlockLayer.Game;

    /// <summary>Vollständiger Dateitext mit erzeugtem Kopf.</summary>
    public string ComposedText { get; private set; } = string.Empty;

    /// <summary>Name vorhanden und der zusammengesetzte Text ohne Fehler.</summary>
    public bool CanSave { get; private set; }

    public IReadOnlyList<GuiBlockDraftKey> DetectedKeys => _shownKeys;

    /// <summary>Fragt vor dem Überschreiben, wenn es den Namen in der Ebene schon als eigene Datei gibt; sonst ohne Frage ja.</summary>
    public static bool ConfirmOverwrite(GuiBlockLibrary library, string name, GuiBlockLayer layer, Func<string, bool> ask) =>
        !library.Exists(name, layer) || ask(name);

    /// <summary>Schlüssel neu erkennen (sonst nach 200 ms Ruhe im Code-Feld) und Tabelle, Fehler und Vorschau auffrischen.</summary>
    internal void RefreshNow()
    {
        var keys = GuiBlockDraft.KeysIn(Body);
        foreach (var key in keys.Where(k => !_rows.ContainsKey(k.Key)))
        {
            _rows[key.Key] = new KeyRow(key.Key, key.IsRepeat);
        }

        if (!keys.SequenceEqual(_shownKeys))
        {
            _shownKeys = keys;
            BuildTable();
        }

        UpdateState();
    }

    /// <summary>Werte einer Tabellenzeile setzen (wie Eingaben in der Tabelle).</summary>
    internal void SetRow(string key, string? label = null, string? defaultValue = null, int? count = null, GuiBlockParameterKind? kind = null)
    {
        var row = _rows[key];
        row.Label = label ?? row.Label;
        row.Default = defaultValue ?? row.Default;
        row.Count = count ?? row.Count;
        row.Kind = kind ?? row.Kind;
        BuildTable();
        UpdateState();
    }

    /// <summary>
    /// Übernimmt gewählte Vorschläge (Plan-2b-Ruling 8): ersetzt den Code als ein Rückgängig-Schritt, legt die neuen Tabellenzeilen mit Beschriftung,
    /// Vorgabe und Anzahl an und baut die Tabelle einmal auf.
    /// </summary>
    internal void ApplySuggestions(GuiBlockSuggestionResult result)
    {
        var document = CodeEditor.Document;
        document.BeginUpdate();
        try
        {
            document.Replace(0, document.TextLength, result.Body);
        }
        finally
        {
            document.EndUpdate();
        }

        _timer.Stop();
        var keys = GuiBlockDraft.KeysIn(Body);
        foreach (var key in keys.Where(k => !_rows.ContainsKey(k.Key)))
        {
            _rows[key.Key] = new KeyRow(key.Key, key.IsRepeat);
        }

        foreach (var parameter in result.Parameters.Where(p => _rows.ContainsKey(p.Key)))
        {
            var row = _rows[parameter.Key];
            row.Label = parameter.Label;
            row.Default = parameter.Default;
            row.Kind = parameter.Kind;
        }

        foreach (var repeat in result.Repeats.Where(r => _rows.ContainsKey(r.Key)))
        {
            _rows[repeat.Key].Label = repeat.Label;
            _rows[repeat.Key].Count = repeat.Count;
        }

        _shownKeys = keys;
        BuildTable();
        UpdateState();
    }

    /// <summary>Knopf "Platzhalter vorschlagen": Vorschläge für den aktuellen Code, Auswahl im eigenen Fenster.</summary>
    private void SuggestPlaceholders()
    {
        var body = Body;
        var suggestions = GuiBlockSuggester.Suggest(body);
        if (suggestions.Count == 0)
        {
            MessageBox.Show(this, GuiTexts.SuggestNone, GuiTexts.SuggestTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var window = new GuiBlockSuggestWindow(suggestions) { Owner = this };
        if (window.ShowDialog() == true && window.Chosen.Count > 0)
        {
            ApplySuggestions(GuiBlockSuggester.Apply(body, window.Chosen));
        }
    }

    private string Body
    {
        get
        {
            var text = CodeEditor.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
            return text.EndsWith('\n') || text.Length == 0 ? text : text + "\n";
        }
    }

    private void UpdateState()
    {
        var body = Body;
        var rows = _shownKeys.Select(k => _rows[k.Key]).ToList();
        ComposedText = GuiBlockDraft.Compose(
            BlockName,
            _group.Value,
            _description.Text,
            rows.Where(r => !r.IsRepeat).Select(r => new GuiBlockDraftParameter(r.Key, r.Label, r.Default, r.Kind)).ToList(),
            rows.Where(r => r.IsRepeat).Select(r => new GuiBlockDraftRepeat(r.Key, r.Label, r.Count)).ToList(),
            body);
        var block = GuiBlockParser.Parse(ComposedText, GuiBlockLayer.Game, null);
        var headerLines = ComposedText[..^body.Length].Count(c => c == '\n');

        _errors.Children.Clear();
        if (BlockName.Length == 0)
        {
            _errors.Children.Add(ErrorText(GuiTexts.BlocksSaveNameMissing));
        }

        foreach (var error in block.Errors)
        {
            var line = error.Line > headerLines ? error.Line - headerLines : error.Line;
            _errors.Children.Add(ErrorText(GuiTexts.BlocksErrorLine(line, error.Message)));
        }

        CanSave = BlockName.Length > 0 && block.IsValid;
        _save.IsEnabled = CanSave;
        _preview.Content = block.IsValid ? new GuiBlockInputs(block, GuiBlockValues.Defaults(block), block.Parameters, block.Repeats) : null;
    }

    private static TextBlock ErrorText(string text)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");
        return block;
    }

    /// <summary>Tabelle der erkannten Schlüssel; Eingaben schreiben in die Zeile, ohne die Tabelle neu aufzubauen.</summary>
    private void BuildTable()
    {
        _table.Children.Clear();
        _table.RowDefinitions.Clear();
        _table.ColumnDefinitions.Clear();
        foreach (var width in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), new GridLength(1, GridUnitType.Star), GridLength.Auto })
        {
            _table.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        }

        AddRow(new UIElement[]
        {
            Heading(GuiTexts.BlocksSaveKey),
            Heading(GuiTexts.BlocksSaveLabel),
            Heading(GuiTexts.BlocksSaveDefault + " / " + GuiTexts.BlocksSaveRepeatCount),
            Heading(GuiTexts.BlocksSaveKind),
        });
        foreach (var key in _shownKeys)
        {
            var row = _rows[key.Key];
            var name = new TextBlock { Text = (row.IsRepeat ? "$repeat " : "$") + row.Key + "$", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 8, 2) };
            var label = Box(row.Label, text => row.Label = text);
            if (row.IsRepeat)
            {
                var count = Box(row.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), text =>
                {
                    if (int.TryParse(text, out var value))
                    {
                        row.Count = Math.Clamp(value, 0, GuiBlockValues.MaxCount);
                    }
                });
                AddRow(new UIElement[] { name, label, count });
            }
            else
            {
                var kind = new ComboBox
                {
                    ItemsSource = new[] { GuiTexts.BlocksSaveKindText, GuiTexts.BlocksSaveKindModifier },
                    SelectedIndex = row.Kind == GuiBlockParameterKind.Modifier ? 1 : 0,
                    Margin = new Thickness(0, 2, 0, 2),
                };
                kind.SelectionChanged += (_, _) =>
                {
                    row.Kind = kind.SelectedIndex == 1 ? GuiBlockParameterKind.Modifier : GuiBlockParameterKind.Text;
                    UpdateState();
                };
                AddRow(new UIElement[] { name, label, Box(row.Default, text => row.Default = text), kind });
            }
        }
    }

    private void AddRow(IReadOnlyList<UIElement> cells)
    {
        _table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var column = 0; column < cells.Count; column++)
        {
            Grid.SetRow(cells[column], _table.RowDefinitions.Count - 1);
            Grid.SetColumn(cells[column], column);
            _table.Children.Add(cells[column]);
        }
    }

    private TextBox Box(string text, Action<string> changed)
    {
        var box = new TextBox { Text = text, Margin = new Thickness(0, 2, 4, 2) };
        box.TextChanged += (_, _) =>
        {
            changed(box.Text);
            UpdateState();
        };
        return box;
    }

    private static TextBlock Heading(string text)
    {
        var block = new TextBlock { Text = text, Margin = new Thickness(0, 0, 8, 2) };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        return block;
    }

    private UIElement Layout()
    {
        var left = new DockPanel { Margin = new Thickness(0, 0, 10, 0) };
        var fields = new StackPanel();
        fields.Children.Add(new TextBlock { Text = GuiTexts.BlocksSaveName });
        fields.Children.Add(NameBox);
        fields.Children.Add(new TextBlock { Text = GuiTexts.BlocksSaveGroup });
        _group.Margin = new Thickness(0, 2, 0, 8);
        fields.Children.Add(_group);
        fields.Children.Add(new TextBlock { Text = GuiTexts.BlocksSaveDescription });
        fields.Children.Add(_description);
        fields.Children.Add(new TextBlock { Text = GuiTexts.BlocksSaveLayer });
        var layers = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        _gameOption.GroupName = ModOption.GroupName = "layer";
        layers.Children.Add(_gameOption);
        layers.Children.Add(ModOption);
        fields.Children.Add(layers);
        var codeHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 2) };
        var suggest = new Button { Content = GuiTexts.SuggestButton, ToolTip = GuiTexts.SuggestButtonTip, Padding = new Thickness(8, 1, 8, 1) };
        suggest.Click += (_, _) => SuggestPlaceholders();
        DockPanel.SetDock(suggest, Dock.Right);
        codeHeader.Children.Add(suggest);
        codeHeader.Children.Add(new TextBlock { Text = GuiTexts.BlocksSaveCode, VerticalAlignment = VerticalAlignment.Bottom });
        fields.Children.Add(codeHeader);
        DockPanel.SetDock(fields, Dock.Top);
        left.Children.Add(fields);
        var help = new TextBlock { Text = GuiTexts.BlocksSaveHelp, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
        help.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        DockPanel.SetDock(help, Dock.Bottom);
        left.Children.Add(help);
        var codeBorder = new Border { BorderThickness = new Thickness(1), Child = CodeEditor };
        codeBorder.SetResourceReference(Border.BorderBrushProperty, "Theme.ControlBorder");
        left.Children.Add(codeBorder);

        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Place(right, new TextBlock { Text = GuiTexts.BlocksSaveKeys, FontWeight = FontWeights.SemiBold }, 0);
        Place(right, new ScrollViewer { Content = _table, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, 1);
        Place(right, _errors, 2);
        Place(right, new TextBlock { Text = GuiTexts.BlocksSavePreview, FontWeight = FontWeights.SemiBold }, 3);
        Place(right, new ScrollViewer { Content = _preview, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, 4);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(left);
        Grid.SetColumn(right, 1);
        body.Children.Add(right);

        _save.Content = GuiTexts.BlocksSaveButton;
        _save.Click += (_, _) =>
        {
            RefreshNow();
            if (CanSave)
            {
                DialogResult = true;
            }
        };
        var cancel = new Button { Content = GuiTexts.BlocksSaveCancel, IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(_save);
        buttons.Children.Add(cancel);

        var root = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(body);
        return root;
    }

    private static void Place(Grid grid, UIElement element, int row)
    {
        Grid.SetRow(element, row);
        grid.Children.Add(element);
    }

    /// <summary>Eingaben zu einem erkannten Schlüssel; bleiben erhalten, solange der Schlüssel im Code vorkommt oder wieder auftaucht.</summary>
    private sealed class KeyRow
    {
        public KeyRow(string key, bool isRepeat)
        {
            Key = key;
            IsRepeat = isRepeat;
        }

        public string Key { get; }

        public bool IsRepeat { get; }

        public string Label { get; set; } = string.Empty;

        public string Default { get; set; } = string.Empty;

        public GuiBlockParameterKind Kind { get; set; } = GuiBlockParameterKind.Text;

        public int Count { get; set; } = 1;
    }
}
