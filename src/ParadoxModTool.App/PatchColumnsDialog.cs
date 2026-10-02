using System.Windows;
using ParadoxModTool.Localization;
using ParadoxModTool.Patches;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// Dialog "Spalten" der Patch-Rezepte: eigene und abgeleitete Spalten anlegen, umbenennen, sortieren, entfernen.
/// Bearbeitet eine Kopie; erst OK übernimmt sie ins Rezept (Umbenennen zieht die Zellwerte aller Zeilen mit).
/// </summary>
public sealed class PatchColumnsDialog : Window
{
    private static readonly string[] Types = { PatchRecipeTexts.TypeText, PatchRecipeTexts.TypeNumber, PatchRecipeTexts.TypeList };
    private static readonly string[] Sources = { PatchRecipeTexts.SourceVanilla, PatchRecipeTexts.SourceMod, PatchRecipeTexts.SourceModOverVanilla };
    private static readonly string[] Takes = { PatchRecipeTexts.TakeTop, PatchRecipeTexts.TakeInner };

    private readonly List<Entry> _entries;
    private readonly Controls.ListBox _list = new() { Width = 200, DisplayMemberPath = nameof(Entry.Display) };
    private readonly Controls.TextBox _name = new();
    private readonly Controls.TextBlock _nameProblem = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Controls.ComboBox _type = new() { ItemsSource = Types };
    private readonly Controls.TextBox _default = new();
    private readonly Controls.CheckBox _titleKey = new() { Content = new Controls.TextBlock { Text = PatchRecipeTexts.ColTitleKey } };
    private readonly Controls.CheckBox _derived = new() { Content = new Controls.TextBlock { Text = PatchRecipeTexts.ColDerived } };
    private readonly Controls.TextBox _files = new() { ToolTip = PatchRecipeTexts.FilesTip };
    private readonly Controls.ComboBox _source = new() { ItemsSource = Sources };
    private readonly Controls.TextBox _match = new() { ToolTip = PatchRecipeTexts.LookupMatchTip };
    private readonly Controls.ComboBox _take = new() { ItemsSource = Takes };
    private readonly Controls.CheckBox _optional = new() { Content = new Controls.TextBlock { Text = PatchRecipeTexts.LookupOptional } };
    private readonly Controls.CheckBox _all = new() { Content = new Controls.TextBlock { Text = PatchRecipeTexts.LookupAll }, ToolTip = PatchRecipeTexts.LookupAllTip };
    private readonly Controls.StackPanel _lookupPanel = new();
    private readonly Controls.StackPanel _form = new() { Margin = new Thickness(12, 0, 0, 0) };
    private readonly Controls.Button _ok = new() { Content = new Controls.TextBlock { Text = CommonTexts.Ok }, Width = 100, IsDefault = true, Margin = new Thickness(0, 0, 6, 0) };
    private bool _showing;

    private PatchColumnsDialog(PatchRecipe recipe)
    {
        _entries = PatchRecipeStore.Clone(recipe).Columns.Select(c => new Entry(c, c.Name)).ToList();
        Title = PatchRecipeTexts.ColumnsTitle;
        Width = 720;
        Height = 520;
        MinWidth = 560;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        _nameProblem.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.Danger");

        var left = new Controls.DockPanel();
        var listButtons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        listButtons.Children.Add(SmallButton(PatchRecipeTexts.ColAdd, (_, _) => Add()));
        listButtons.Children.Add(SmallButton(PatchRecipeTexts.ColRemove, (_, _) => Remove()));
        listButtons.Children.Add(SmallButton("▲", (_, _) => Move(-1)));
        listButtons.Children.Add(SmallButton("▼", (_, _) => Move(1)));
        Controls.DockPanel.SetDock(listButtons, Controls.Dock.Bottom);
        left.Children.Add(listButtons);
        left.Children.Add(_list);

        _lookupPanel.Children.Add(Field(PatchRecipeTexts.LookupFiles, _files));
        _lookupPanel.Children.Add(Field(PatchRecipeTexts.LookupSource, _source));
        _lookupPanel.Children.Add(Field(PatchRecipeTexts.LookupMatch, _match));
        _lookupPanel.Children.Add(Field(PatchRecipeTexts.LookupTake, _take));
        _lookupPanel.Children.Add(_optional);
        _lookupPanel.Children.Add(_all);
        _form.Children.Add(Field(PatchRecipeTexts.ColName, _name));
        _form.Children.Add(_nameProblem);
        _form.Children.Add(Field(PatchRecipeTexts.ColType, _type));
        _form.Children.Add(Field(PatchRecipeTexts.ColDefault, _default));
        _form.Children.Add(_titleKey);
        _form.Children.Add(new Controls.Separator { Margin = new Thickness(0, 8, 0, 8) });
        _form.Children.Add(_derived);
        _form.Children.Add(_lookupPanel);

        var body = new Controls.DockPanel { Margin = new Thickness(10) };
        var bottom = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        bottom.Children.Add(_ok);
        bottom.Children.Add(new Controls.Button { Content = new Controls.TextBlock { Text = CommonTexts.Cancel }, Width = 100, IsCancel = true });
        Controls.DockPanel.SetDock(bottom, Controls.Dock.Bottom);
        Controls.DockPanel.SetDock(left, Controls.Dock.Left);
        body.Children.Add(bottom);
        body.Children.Add(left);
        body.Children.Add(new Controls.ScrollViewer { Content = _form, VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto });
        Content = body;

        _list.ItemsSource = _entries;
        _list.SelectionChanged += (_, _) => Show(_list.SelectedItem as Entry);
        _name.TextChanged += (_, _) => Store(e => e.Column.Name = _name.Text.Trim());
        _type.SelectionChanged += (_, _) => Store(e => e.Column.Type = (ColumnType)Math.Max(0, _type.SelectedIndex));
        _default.TextChanged += (_, _) => Store(e => e.Column.Default = _default.Text);
        _titleKey.Click += (_, _) => Store(e => e.Column.TitleKey = _titleKey.IsChecked == true);
        _derived.Click += (_, _) => Store(e => e.Column.Lookup = _derived.IsChecked == true ? e.Column.Lookup ?? new DerivedLookup() : null);
        _files.TextChanged += (_, _) => Store(e => { if (e.Column.Lookup is { } l) { l.Files = _files.Text.Trim(); } });
        _source.SelectionChanged += (_, _) => Store(e => { if (e.Column.Lookup is { } l) { l.Source = (LookupSource)Math.Max(0, _source.SelectedIndex); } });
        _match.TextChanged += (_, _) => Store(e => { if (e.Column.Lookup is { } l) { l.Match = _match.Text; } });
        _take.SelectionChanged += (_, _) => Store(e => { if (e.Column.Lookup is { } l) { l.Take = (LookupTake)Math.Max(0, _take.SelectedIndex); } });
        _optional.Click += (_, _) => Store(e => { if (e.Column.Lookup is { } l) { l.Optional = _optional.IsChecked == true; } });
        _all.Click += (_, _) => Store(e => { if (e.Column.Lookup is { } l) { l.All = _all.IsChecked == true; } });
        _ok.Click += (_, _) => DialogResult = true;

        _list.SelectedIndex = _entries.Count > 0 ? 0 : -1;
        Show(_list.SelectedItem as Entry);
        Validate();
    }

    /// <summary>Zeigt den Dialog; bei OK stehen die Spalten im Rezept und die Zellwerte umbenannter Spalten sind mitgezogen.</summary>
    public static bool Edit(Window owner, PatchRecipe recipe)
    {
        var dialog = new PatchColumnsDialog(recipe) { Owner = owner };
        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        foreach (var entry in dialog._entries.Where(e => e.OriginalName is not null && e.OriginalName != e.Column.Name))
        {
            foreach (var row in recipe.Rows)
            {
                if (row.Values.Remove(entry.OriginalName!, out var value))
                {
                    row.Values[entry.Column.Name] = value;
                }
            }
        }

        recipe.Columns = dialog._entries.Select(e => e.Column).ToList();
        return true;
    }

    private void Add()
    {
        var name = "spalte";
        for (var i = 2; _entries.Any(e => e.Column.Name == name); i++)
        {
            name = "spalte" + i;
        }

        var entry = new Entry(new PatchColumn { Name = name }, null);
        _entries.Add(entry);
        RefreshList(entry);
    }

    private void Remove()
    {
        if (_list.SelectedItem is Entry entry)
        {
            _entries.Remove(entry);
            RefreshList(_entries.FirstOrDefault());
        }
    }

    private void Move(int delta)
    {
        if (_list.SelectedItem is not Entry entry)
        {
            return;
        }

        var index = _entries.IndexOf(entry);
        var target = Math.Clamp(index + delta, 0, _entries.Count - 1);
        _entries.RemoveAt(index);
        _entries.Insert(target, entry);
        RefreshList(entry);
    }

    private void RefreshList(Entry? select)
    {
        _list.ItemsSource = null;
        _list.ItemsSource = _entries;
        _list.SelectedItem = select;
        Show(select);
        Validate();
    }

    private void Show(Entry? entry)
    {
        _showing = true;
        _form.IsEnabled = entry is not null;
        var column = entry?.Column;
        _name.Text = column?.Name ?? string.Empty;
        _type.SelectedIndex = (int)(column?.Type ?? ColumnType.Text);
        _default.Text = column?.Default ?? string.Empty;
        _titleKey.IsChecked = column?.TitleKey == true;
        _derived.IsChecked = column?.Lookup is not null;
        _files.Text = column?.Lookup?.Files ?? string.Empty;
        _source.SelectedIndex = (int)(column?.Lookup?.Source ?? LookupSource.Vanilla);
        _match.Text = column?.Lookup?.Match ?? string.Empty;
        _take.SelectedIndex = (int)(column?.Lookup?.Take ?? LookupTake.TopLevelKey);
        _optional.IsChecked = column?.Lookup?.Optional == true;
        _all.IsChecked = column?.Lookup?.All == true;
        _lookupPanel.Visibility = column?.Lookup is not null ? Visibility.Visible : Visibility.Collapsed;
        _showing = false;
        Validate();
    }

    private void Store(Action<Entry> change)
    {
        if (_showing || _list.SelectedItem is not Entry entry)
        {
            return;
        }

        change(entry);
        _lookupPanel.Visibility = entry.Column.Lookup is not null ? Visibility.Visible : Visibility.Collapsed;
        _list.Items.Refresh();
        Validate();
    }

    /// <summary>OK nur, wenn jeder Name gültig und eindeutig ist; das Problem des gewählten Namens steht unter dem Feld.</summary>
    private void Validate()
    {
        string? ProblemOf(Entry entry)
        {
            var others = new PatchRecipe { Columns = _entries.Where(e => e != entry).Select(e => e.Column).ToList() };
            return RecipeEditing.ColumnNameProblem(others, entry.Column.Name, null);
        }

        _nameProblem.Text = _list.SelectedItem is Entry selected ? ProblemOf(selected) ?? string.Empty : string.Empty;
        _ok.IsEnabled = _entries.All(e => ProblemOf(e) is null);
    }

    private static Controls.Button SmallButton(string text, RoutedEventHandler click)
    {
        var button = new Controls.Button { Content = new Controls.TextBlock { Text = text }, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 0, 4, 0) };
        button.Click += click;
        return button;
    }

    private static Controls.DockPanel Field(string label, UIElement input)
    {
        var panel = new Controls.DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var text = new Controls.TextBlock { Text = label, Width = 130, VerticalAlignment = VerticalAlignment.Center };
        Controls.DockPanel.SetDock(text, Controls.Dock.Left);
        panel.Children.Add(text);
        panel.Children.Add(input);
        return panel;
    }

    /// <summary>Eine Spalte im Dialog mit ihrem Namen beim Öffnen (null = neu), damit Umbenennungen die Zellwerte mitnehmen.</summary>
    private sealed class Entry
    {
        public Entry(PatchColumn column, string? originalName)
        {
            Column = column;
            OriginalName = originalName;
        }

        public PatchColumn Column { get; }

        public string? OriginalName { get; }

        public string Display => Column.Lookup is null ? Column.Name : Column.Name + " (" + PatchRecipeTexts.ColDerivedShort + ")";
    }
}
