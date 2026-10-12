using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ParadoxModTool.Editor;
using ParadoxModTool.Localization;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ListView = System.Windows.Controls.ListView;
using ListViewItem = System.Windows.Controls.ListViewItem;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using Binding = System.Windows.Data.Binding;
using SelectionMode = System.Windows.Controls.SelectionMode;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ParadoxModTool.App;

/// <summary>
/// "Modifier suchen" (Spec 2026-10-05-generator-packages-lookups, Abschnitt 2): alle Modifier aus PMTs Modifier-Index (Spiel und Mods), Suche nach Schlüssel oder
/// übersetztem Namen. "Einfügen" reicht den Schlüssel an das Formular weiter; das Fenster bleibt offen.
/// </summary>
public sealed class ModifierSearchWindow : Window
{
    private readonly Func<string, bool> _tryInsert;
    private readonly ListView _list = new() { SelectionMode = SelectionMode.Single };
    private readonly TextBox _search;
    private readonly TextBlock _status = new() { Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
    private List<Row> _rows = new();

    /// <param name="tryInsert">Fügt den Schlüssel ins Feld ein; <see langword="false"/>, wenn er dort schon steht.</param>
    public ModifierSearchWindow(Window owner, Func<string, bool> tryInsert)
    {
        _tryInsert = tryInsert;
        Owner = owner;
        Title = GeneratorTexts.ModifierSearchTitle;
        Width = 760;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var view = new GridView();
        view.Columns.Add(new GridViewColumn { Header = GeneratorTexts.ModifierColumnName, DisplayMemberBinding = new Binding(nameof(Row.Name)), Width = 330 });
        view.Columns.Add(new GridViewColumn { Header = GeneratorTexts.ModifierColumnKey, DisplayMemberBinding = new Binding(nameof(Row.Key)), Width = 260 });
        view.Columns.Add(new GridViewColumn { Header = GeneratorTexts.ModifierColumnCategory, DisplayMemberBinding = new Binding(nameof(Row.Category)), Width = 110 });
        _list.View = view;
        var muted = (Brush)FindResource("Theme.TextMuted");
        // Auf dem Zeilenstil des Themes aufbauen, sonst fehlen im Dark-Mode die Theme-Farben der Auswahl.
        var style = new Style(typeof(ListViewItem), TryFindResource(typeof(ListViewItem)) as Style);
        var unnamed = new DataTrigger { Binding = new Binding(nameof(Row.HasName)), Value = false };
        unnamed.Setters.Add(new Setter(ForegroundProperty, muted));
        style.Triggers.Add(unnamed);
        _list.ItemContainerStyle = style;
        _list.MouseDoubleClick += (_, _) => Insert();
        _list.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Insert();
                e.Handled = true;
            }
        };

        var (searchPanel, search) = SearchBox.Create();
        _search = search;
        _search.TextChanged += (_, _) => Fill();
        _search.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Down && _list.Items.Count > 0)
            {
                _list.SelectedIndex = Math.Max(_list.SelectedIndex, 0);
                (_list.ItemContainerGenerator.ContainerFromIndex(_list.SelectedIndex) as ListViewItem)?.Focus();
                e.Handled = true;
            }
        };

        var root = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(searchPanel, Dock.Top);
        searchPanel.Margin = new Thickness(0, 0, 0, 6);
        root.Children.Add(searchPanel);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        var insert = new Button { Content = new TextBlock { Text = GeneratorTexts.ModifierInsert }, Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        insert.Click += (_, _) => Insert();
        var close = new Button { Content = new TextBlock { Text = CommonTexts.Close }, Padding = new Thickness(12, 2, 12, 2), IsCancel = true };
        close.Click += (_, _) => Close();
        buttons.Children.Add(insert);
        buttons.Children.Add(close);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        DockPanel.SetDock(_status, Dock.Bottom);
        root.Children.Add(_status);
        root.Children.Add(_list);
        Content = root;

        ModifierValueSettings.Changed += OnIndexChanged;
        Closed += (_, _) => ModifierValueSettings.Changed -= OnIndexChanged;
        Loaded += (_, _) => _search.Focus();
        Load();
    }

    /// <summary>Eine Zeile: angezeigter Name (ohne Übersetzung der Schlüssel), Schlüssel, Kategorie.</summary>
    public sealed record Row(string Name, string Key, string Category, bool HasName);

    private void OnIndexChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(Load);

    private void Load()
    {
        var index = ModifierValueSettings.Index;
        var names = ModifierValueSettings.Names;
        _rows = index.AllNames
            .Select(key => names.Find(key) is { Length: > 0 } name
                ? new Row(name, key, index.Traits.GetValueOrDefault(key)?.Category ?? string.Empty, true)
                : new Row(key, key, index.Traits.GetValueOrDefault(key)?.Category ?? string.Empty, false))
            .OrderBy(r => r.HasName ? 0 : 1)
            .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        Fill();
    }

    private void Fill()
    {
        var query = _search.Text.Trim();
        _list.ItemsSource = query.Length == 0
            ? _rows
            : _rows.Where(r => r.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) || r.Key.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        _status.Text = _rows.Count == 0 ? GeneratorTexts.ModifierNoneFound : string.Empty;
    }

    private void Insert()
    {
        if (_list.SelectedItem is not Row row)
        {
            return;
        }

        _status.Text = _tryInsert(row.Key) ? GeneratorTexts.ModifierInserted(row.Name) : GeneratorTexts.ModifierAlreadyThere(row.Key);
    }
}
