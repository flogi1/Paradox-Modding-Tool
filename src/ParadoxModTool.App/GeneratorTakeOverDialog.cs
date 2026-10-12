using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ParadoxModTool.Editor.Controls;
using ParadoxModTool.Generators;
using ParadoxModTool.Localization;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>
/// "Tags aus Mod übernehmen" (Spec Teil 4): Quelle wählen (Mod des Spiels oder Ordner), gefundene Schlüssel mit Haken und Suche; vorhandene
/// Arbeitsstände sind als "wird ersetzt" markiert. Liefert Quelle und gewählte Schlüssel.
/// </summary>
public sealed class GeneratorTakeOverDialog : Window
{
    private readonly GeneratorPackage _package;
    private readonly ISet<string> _existing;
    private readonly StackPanel _keys = new();
    private readonly TextBox _search = new() { Margin = new Thickness(0, 0, 0, 4), ToolTip = GeneratorTexts.SearchStatesTip };
    private readonly Dictionary<string, CheckBox> _boxes = new(StringComparer.Ordinal);
    private string? _source;

    private GeneratorTakeOverDialog(GeneratorPackage package, IReadOnlyList<(string Name, string Path)> mods, ISet<string> existing)
    {
        _package = package;
        _existing = existing;
        Title = GeneratorTexts.TakeOverTitle;
        Width = 560;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var root = new DockPanel { Margin = new Thickness(12) };
        var sourceRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        sourceRow.Children.Add(new TextBlock { Text = GeneratorTexts.TakeOverSource, Width = 70, VerticalAlignment = VerticalAlignment.Center });
        var choice = new SearchableChoice(searchWidth: 120, choiceWidth: 300);
        choice.SetItems(mods.Select(m => (m.Name, (object?)m.Path)).ToList(), null);
        choice.SelectionChanged += (_, _) => SetSource(choice.SelectedTag as string);
        sourceRow.Children.Add(choice);
        var browse = new Button { Content = new TextBlock { Text = GeneratorTexts.ChooseFolder }, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(6, 0, 0, 0) };
        browse.Click += (_, _) =>
        {
            var dialog = new OpenFolderDialog { InitialDirectory = _source };
            if (dialog.ShowDialog(this) == true)
            {
                SetSource(dialog.FolderName);
            }
        };
        sourceRow.Children.Add(browse);
        DockPanel.SetDock(sourceRow, Dock.Top);
        root.Children.Add(sourceRow);

        var hint = new TextBlock { Text = GeneratorTexts.TakeOverHint, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var all = new Button { Content = new TextBlock { Text = GeneratorTexts.TakeOverAll }, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 6, 0) };
        all.Click += (_, _) => SetAll(true);
        var none = new Button { Content = new TextBlock { Text = GeneratorTexts.TakeOverNone }, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 18, 0) };
        none.Click += (_, _) => SetAll(false);
        var ok = new Button { Content = new TextBlock { Text = GeneratorTexts.TakeOverOk }, Padding = new Thickness(12, 2, 12, 2), IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        ok.Click += (_, _) => DialogResult = _source is not null && Chosen.Count > 0;
        var cancel = new Button { Content = new TextBlock { Text = CommonTexts.Cancel }, Padding = new Thickness(12, 2, 12, 2), IsCancel = true };
        buttons.Children.Add(all);
        buttons.Children.Add(none);
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        DockPanel.SetDock(hint, Dock.Bottom);
        root.Children.Add(hint);
        DockPanel.SetDock(_search, Dock.Top);
        root.Children.Add(_search);
        root.Children.Add(new ScrollViewer { Content = _keys, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        _search.TextChanged += (_, _) => Filter();
        Content = root;
    }

    private List<string> Chosen => _boxes.Where(b => b.Value.IsChecked == true).Select(b => b.Key).ToList();

    /// <summary>Quelle und gewählte Schlüssel; <see langword="null"/> bei Abbruch.</summary>
    public static (string Source, IReadOnlyList<string> Keys)? Ask(Window owner, GeneratorPackage package, IReadOnlyList<(string Name, string Path)> mods, ISet<string> existing)
    {
        var dialog = new GeneratorTakeOverDialog(package, mods, existing) { Owner = owner };
        return dialog.ShowDialog() == true && dialog._source is { } source ? (source, dialog.Chosen) : null;
    }

    private void SetSource(string? folder)
    {
        _source = folder is not null && Directory.Exists(folder) ? folder : null;
        _keys.Children.Clear();
        _boxes.Clear();
        foreach (var key in _source is null ? Array.Empty<string>() : GeneratorStateReader.Keys(_package, _source))
        {
            var text = _existing.Contains(key) ? $"{key}  ({GeneratorTexts.TakeOverReplaces})" : key;
            var box = new CheckBox { Content = new TextBlock { Text = text }, IsChecked = true, Margin = new Thickness(0, 1, 0, 1), Tag = key };
            _boxes[key] = box;
            _keys.Children.Add(box);
        }

        Filter();
    }

    private void Filter()
    {
        var filter = _search.Text.Trim();
        foreach (var (key, box) in _boxes)
        {
            box.Visibility = filter.Length == 0 || key.Contains(filter, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void SetAll(bool value)
    {
        foreach (var box in _boxes.Values.Where(b => b.Visibility == Visibility.Visible))
        {
            box.IsChecked = value;
        }
    }
}
