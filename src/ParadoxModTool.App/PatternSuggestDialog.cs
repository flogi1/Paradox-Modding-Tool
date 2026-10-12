using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Localization;
using Button = System.Windows.Controls.Button;
using ListBox = System.Windows.Controls.ListBox;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace ParadoxModTool.App;

/// <summary>Welches Muster der Vorschlag-Dialog baut: <c>read.keys</c>, eine Pfad-Vorlage, ein Block- oder ein Lokalisierungsmuster.</summary>
internal enum PatternKind
{
    Keys,
    File,
    Block,
    Localization,
}

/// <summary>
/// "Vorschlag" in der Einlesen-Maske (Spec Teil 5b, Abschnitt 1): Schlüsselwert, Beispiel-Liste mit Suchfeld (Dateien, oberste Blöcke einer Datei
/// oder englische Lokalisierungsschlüssel der Mod) und der daraus gebaute Vorschlag.
/// </summary>
internal sealed class PatternSuggestDialog : Window
{
    private readonly PatternKind _kind;
    private readonly string _modRoot;
    private readonly string _keyField;
    private readonly string? _keyFieldPattern;
    private readonly TextBox _keyValue;
    private readonly TextBox _file = new() { VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBox _search = new() { Margin = new Thickness(0, 4, 0, 4) };
    private readonly ListBox _list = new();
    private readonly TextBlock _result = new() { TextWrapping = TextWrapping.Wrap, FontFamily = new System.Windows.Media.FontFamily("Consolas") };
    private readonly Button _ok;
    private IReadOnlyList<string> _examples = Array.Empty<string>();
    private string? _pattern;

    private PatternSuggestDialog(PatternKind kind, string modRoot, string keyValue, string keyField, string? keyFieldPattern, string? file)
    {
        _kind = kind;
        _modRoot = modRoot;
        _keyField = keyField;
        _keyFieldPattern = keyFieldPattern;
        Title = PackageEditorTexts.SuggestTitle;
        Width = 640;
        Height = 560;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var panel = new DockPanel { Margin = new Thickness(14) };
        var top = new StackPanel();
        _keyValue = new TextBox { Text = keyValue, ToolTip = PackageEditorTexts.SuggestKeyValueTip, VerticalContentAlignment = VerticalAlignment.Center };
        _keyValue.TextChanged += (_, _) => Update();
        top.Children.Add(Labeled(PackageEditorTexts.SuggestKeyValue, PackageEditorTexts.SuggestKeyValueTip, _keyValue));
        if (kind == PatternKind.Block)
        {
            _file.Text = file ?? string.Empty;
            _file.ToolTip = PackageEditorTexts.SuggestFileTip;
            _file.LostFocus += (_, _) => LoadExamples();
            var choose = new Button { Content = new TextBlock { Text = PackageEditorTexts.SuggestChooseFile }, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(6, 0, 0, 0) };
            choose.Click += (_, _) => ChooseFile();
            var row = new DockPanel();
            DockPanel.SetDock(choose, Dock.Right);
            row.Children.Add(choose);
            row.Children.Add(_file);
            top.Children.Add(Labeled(PackageEditorTexts.SuggestFile, PackageEditorTexts.SuggestFileTip, row));
        }

        top.Children.Add(new TextBlock { Text = PackageEditorTexts.SuggestExamples, Margin = new Thickness(0, 8, 0, 0) });
        top.Children.Add(_search);
        DockPanel.SetDock(top, Dock.Top);
        panel.Children.Add(top);

        var bottom = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        bottom.Children.Add(new TextBlock { Text = PackageEditorTexts.SuggestResult, FontWeight = FontWeights.SemiBold });
        bottom.Children.Add(_result);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        _ok = new Button { Content = new TextBlock { Text = CommonTexts.Ok }, Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0), IsEnabled = false };
        _ok.Click += (_, _) => DialogResult = true;
        var cancel = new Button { Content = new TextBlock { Text = CommonTexts.Cancel }, Width = 80, IsCancel = true };
        buttons.Children.Add(_ok);
        buttons.Children.Add(cancel);
        bottom.Children.Add(buttons);
        DockPanel.SetDock(bottom, Dock.Bottom);
        panel.Children.Add(bottom);
        panel.Children.Add(_list);
        Content = panel;

        _search.TextChanged += (_, _) => Filter();
        _list.SelectionChanged += (_, _) => Update();
        _list.MouseDoubleClick += (_, _) =>
        {
            if (_pattern is not null)
            {
                DialogResult = true;
            }
        };
        LoadExamples();
        Loaded += (_, _) => _search.Focus();
    }

    /// <summary>Zeigt den Dialog; gibt das vorgeschlagene Muster zurück oder <see langword="null"/> bei Abbruch.</summary>
    public static string? Ask(Window owner, PatternKind kind, string modRoot, string keyValue, string keyField, string? keyFieldPattern, string? file = null)
    {
        var dialog = new PatternSuggestDialog(kind, modRoot, keyValue, keyField, keyFieldPattern, file) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog._pattern : null;
    }

    private static FrameworkElement Labeled(string label, string tip, FrameworkElement input)
    {
        var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
        var text = new TextBlock { Text = label, Width = 150, ToolTip = tip, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(text, Dock.Left);
        row.Children.Add(text);
        row.Children.Add(input);
        return row;
    }

    private void ChooseFile()
    {
        var files = PatternExamples.Files(_modRoot).Where(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)).ToList();
        if (PickListDialog.Ask(this, PackageEditorTexts.SuggestChooseFile, PackageEditorTexts.SuggestFileTip, files, multi: false) is [var index])
        {
            _file.Text = files[index];
            LoadExamples();
        }
    }

    private void LoadExamples()
    {
        _examples = _kind switch
        {
            PatternKind.Block => PatternExamples.BlockKeys(_modRoot, _file.Text.Trim()),
            PatternKind.Localization => PatternExamples.LocalizationKeys(_modRoot),
            _ => PatternExamples.Files(_modRoot),
        };
        Filter();
    }

    private void Filter()
    {
        var filter = _search.Text.Trim();
        _list.ItemsSource = filter.Length == 0 ? _examples : _examples.Where(e => e.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        Update();
    }

    private void Update()
    {
        _pattern = null;
        if (_list.SelectedItem is not string example)
        {
            _result.Text = PackageEditorTexts.SuggestNone;
            _ok.IsEnabled = false;
            return;
        }

        var key = _keyValue.Text.Trim();
        var suggestion = _kind switch
        {
            PatternKind.Keys => PatternSuggester.Keys(example, key, _keyFieldPattern),
            PatternKind.File => PatternSuggester.FileTemplate(example, key, _keyField),
            _ => PatternSuggester.Numbered(example, key, _keyField),
        };
        _pattern = suggestion.Pattern;
        _result.Text = suggestion.Pattern ?? (suggestion.Failure == SuggestFailure.NumberMissing ? PackageEditorTexts.SuggestNumberMissing : PackageEditorTexts.SuggestKeyMissing);
        _result.SetResourceReference(TextBlock.ForegroundProperty, suggestion.Pattern is null ? "Theme.Danger" : "Theme.Text");
        _ok.IsEnabled = _pattern is not null;
    }
}
