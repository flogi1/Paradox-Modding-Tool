using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ParadoxModTool.Generators;
using Binding = System.Windows.Data.Binding;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ListView = System.Windows.Controls.ListView;
using Orientation = System.Windows.Controls.Orientation;
using SelectionMode = System.Windows.Controls.SelectionMode;
using TextBox = System.Windows.Controls.TextBox;

namespace ParadoxModTool.App;

/// <summary>
/// Ergebnis von "Mod prüfen" (Spec Teil 4): eine Zeile je Schlüssel mit fehlenden, abweichenden und gleichen Dateien, Alt-Einträgen, ungenutzten
/// Blöcken und Fehlern. "Öffnen" bzw. "Ungenutzte entfernen" übergeben den gelesenen Stand an das Generator-Fenster (dort Vorschau, dann Erzeugen).
/// </summary>
public sealed class GeneratorCheckWindow : Window
{
    private readonly GeneratorPackage _package;
    private readonly IReadOnlyList<CheckRow> _rows;
    private readonly Action<GeneratorState> _open;
    private readonly ListView _list = new();
    private readonly TextBox _search = new() { Margin = new Thickness(0, 0, 0, 6), ToolTip = GeneratorTexts.SearchStatesTip };
    private readonly Button _openButton = new() { Content = new TextBlock { Text = GeneratorTexts.CheckOpen }, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 8, 0) };
    private readonly Button _removeButton = new() { Content = new TextBlock { Text = GeneratorTexts.CheckRemoveUnused }, Padding = new Thickness(10, 2, 10, 2) };

    public GeneratorCheckWindow(GeneratorPackage package, string modName, IReadOnlyList<CheckRow> rows, Action<GeneratorState> open)
    {
        _package = package;
        _rows = rows;
        _open = open;
        Title = GeneratorTexts.CheckTitle(modName);
        Width = 920;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var view = new GridView();
        view.Columns.Add(Column(GeneratorTexts.CheckColumnTag, nameof(Row.Key), 80));
        view.Columns.Add(Column(GeneratorTexts.CheckColumnMissing, nameof(Row.Missing), 70));
        view.Columns.Add(Column(GeneratorTexts.CheckColumnChanged, nameof(Row.Changed), 80));
        view.Columns.Add(Column(GeneratorTexts.CheckColumnEqual, nameof(Row.Equal), 70));
        view.Columns.Add(Column(GeneratorTexts.CheckColumnFormatOnly, nameof(Row.FormatOnly), 90));
        view.Columns.Add(Column(GeneratorTexts.CheckColumnLegacy, nameof(Row.Legacy), 90));
        view.Columns.Add(Column(GeneratorTexts.CheckColumnUnused, nameof(Row.Unused), 300));
        view.Columns.Add(Column(GeneratorTexts.CheckColumnError, nameof(Row.Error), 200));
        _list.View = view;
        _list.SelectionMode = SelectionMode.Single;
        _list.SelectionChanged += (_, _) => UpdateButtons();
        _list.MouseDoubleClick += (_, _) => Open(withoutUnused: false);
        _search.TextChanged += (_, _) => Fill();

        var root = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(_search, Dock.Top);
        root.Children.Add(_search);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        _openButton.Click += (_, _) => Open(withoutUnused: false);
        _removeButton.Click += (_, _) => Open(withoutUnused: true);
        buttons.Children.Add(_openButton);
        buttons.Children.Add(_removeButton);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        if (rows.Count == 0)
        {
            var none = new TextBlock { Text = GeneratorTexts.CheckNone, Margin = new Thickness(0, 4, 0, 0) };
            DockPanel.SetDock(none, Dock.Top);
            root.Children.Add(none);
        }

        root.Children.Add(_list);
        Content = root;
        Fill();
    }

    private static GridViewColumn Column(string header, string property, double width) =>
        new() { Header = header, DisplayMemberBinding = new Binding(property), Width = width };

    private void Fill()
    {
        var filter = _search.Text.Trim();
        _list.ItemsSource = _rows.Where(r => filter.Length == 0 || r.Key.Contains(filter, StringComparison.OrdinalIgnoreCase)).Select(r => new Row(r)).ToList();
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var row = (_list.SelectedItem as Row)?.Check;
        _openButton.IsEnabled = row?.State is not null;
        _removeButton.IsEnabled = row?.State is not null && row.Unreferenced.Any(b => b.Removable);
    }

    private void Open(bool withoutUnused)
    {
        if ((_list.SelectedItem as Row)?.Check is not { State: { } state } row)
        {
            return;
        }

        _open(withoutUnused ? GeneratorCheck.WithoutUnreferenced(_package, row) : GeneratorStateStore.Clone(state));
    }

    /// <summary>Anzeigezeile; ungenutzte Blöcke in der Mitte tragen den Hinweis, dass sie bleiben.</summary>
    private sealed class Row
    {
        public Row(CheckRow check)
        {
            Check = check;
        }

        public CheckRow Check { get; }

        public string Key => Check.Key;

        public int Missing => Check.Missing;

        public int Changed => Check.Changed;

        public int Equal => Check.Equal;

        public int FormatOnly => Check.FormatOnly;

        public int Legacy => Check.LegacyRemoved;

        public string Unused => string.Join(", ", Check.Unreferenced.Select(b => b.Removable ? b.BlockKey : GeneratorTexts.CheckMiddleKept(b.BlockKey)));

        public string Error => Check.Error ?? string.Empty;
    }
}
