using System.ComponentModel;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Threading;
using ParadoxModTool.Patches;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ListView = System.Windows.Controls.ListView;
using TextBox = System.Windows.Controls.TextBox;
using Orientation = System.Windows.Controls.Orientation;
using SelectionMode = System.Windows.Controls.SelectionMode;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace ParadoxModTool.App;

/// <summary>Eine Tabellenzeile für die Anzeige: eigene Zellen bearbeitbar, abgeleitete Werte und Fehler aus der letzten Auswertung.</summary>
public sealed class PatchTableRow : INotifyPropertyChanged
{
    private readonly PatchRecipe _recipe;
    private readonly Action _edited;

    public PatchTableRow(PatchRecipe recipe, PatchRow row, Action edited)
    {
        _recipe = recipe;
        Row = row;
        _edited = edited;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public PatchRow Row { get; }

    public ResolvedRow? Resolved { get; private set; }

    public string Key
    {
        get => Row.Key;
        set
        {
            if (Row.Key != value)
            {
                Row.Key = value.Trim();
                _edited();
            }
        }
    }

    /// <summary>Eigene Spalte: der Zelleninhalt (leer = Standardwert); abgeleitete Spalte: der zuletzt ermittelte Wert.</summary>
    public string this[string column]
    {
        get
        {
            var definition = _recipe.Columns.FirstOrDefault(c => c.Name == column);
            if (definition?.Lookup is not null)
            {
                return Resolved?.Values.GetValueOrDefault(column) ?? string.Empty;
            }

            return Row.Values.GetValueOrDefault(column) ?? string.Empty;
        }
        set
        {
            if ((Row.Values.GetValueOrDefault(column) ?? string.Empty) != value)
            {
                Row.Values[column] = value;
                _edited();
            }
        }
    }

    public string Hint => Resolved is null ? string.Empty : string.Join("; ", Resolved.Errors);

    public bool HasErrors => Resolved?.HasErrors ?? false;

    /// <summary>Für den Filter: die aufgelöste Zeile oder, solange keine da ist, Schlüssel und eigene Werte.</summary>
    public ResolvedRow ForFilter => Resolved ?? new ResolvedRow { Key = Row.Key, Values = Row.Values };

    public void SetResolved(ResolvedRow? resolved)
    {
        Resolved = resolved;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hint)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasErrors)));
    }
}

/// <summary>Reiter "Tabelle": Zeilen mit eigenen und abgeleiteten Spalten, Filter, Fehlerzeilen rot, Auswertung im Hintergrund.</summary>
public partial class PatchRecipesWindow
{
    private readonly List<PatchTableRow> _tableRows = new();
    private readonly DispatcherTimer _resolveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly TextBox _tableFilter = new() { Width = 240, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly CheckBox _onlyErrors = new() { Content = new TextBlock { Text = PatchRecipeTexts.OnlyErrors }, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly ListView _tableList = new() { SelectionMode = SelectionMode.Extended };
    private Button? _importSpecialsButton;
    private readonly TextBlock _tableStatus = new() { Margin = new Thickness(2, 6, 2, 0) };
    private ICollectionView? _tableView;
    private int _resolveVersion;

    /// <summary>Die letzte Auswertung der Tabelle (für die Regel-Vorschau "Vorschau für").</summary>
    internal ResolvedTable? LastTable { get; private set; }

    partial void InitTable()
    {
        var bar = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        bar.Children.Add(new TextBlock { Text = "🔍", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        bar.Children.Add(_tableFilter);
        bar.Children.Add(_onlyErrors);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        _importSpecialsButton = TableButton(PatchRecipeTexts.ImportSpecials, ImportSpecials_Click);
        _importSpecialsButton.ToolTip = PatchRecipeTexts.ImportSpecialsTip;
        _importSpecialsButton.Visibility = Visibility.Collapsed;
        buttons.Children.Add(_importSpecialsButton);
        _adoptModButton = TableButton(PatchRecipeTexts.AdoptModButton, AdoptModBuildings_Click);
        _adoptModButton.ToolTip = PatchRecipeTexts.AdoptModButtonTip;
        _adoptModButton.Visibility = Visibility.Collapsed;
        buttons.Children.Add(_adoptModButton);
        buttons.Children.Add(TableButton(PatchRecipeTexts.AddRow, AddRow_Click));
        buttons.Children.Add(TableButton(PatchRecipeTexts.RemoveRow, RemoveRow_Click));
        buttons.Children.Add(TableButton(PatchRecipeTexts.Columns, Columns_Click));
        DockPanel.SetDock(buttons, Dock.Right);
        bar.Children.Insert(0, buttons);
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(_tableStatus, Dock.Bottom);
        TableRoot.Children.Add(bar);
        TableRoot.Children.Add(_tableStatus);
        TableRoot.Children.Add(_tableList);

        _tableFilter.TextChanged += (_, _) => _tableView?.Refresh();
        _onlyErrors.Click += (_, _) => _tableView?.Refresh();
        _resolveTimer.Tick += async (_, _) =>
        {
            _resolveTimer.Stop();
            await ResolveTableAsync();
        };
    }

    private Button TableButton(string text, RoutedEventHandler click)
    {
        var button = new Button { Content = new TextBlock { Text = text }, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(4, 0, 0, 0) };
        button.Click += click;
        return button;
    }

    partial void TableRecipeSelected() => RebuildTable();

    /// <summary>Baut Spalten und Zeilen neu auf (anderes Rezept oder geänderte Spalten) und wertet die Tabelle aus.</summary>
    private void RebuildTable()
    {
        _tableRows.Clear();
        var view = new GridView();
        if (Recipe is { } recipe)
        {
            _tableRows.AddRange(recipe.Rows.Select(r => new PatchTableRow(recipe, r, TableEdited)));
            view.Columns.Add(new GridViewColumn { Header = "key", Width = 220, CellTemplate = CellTemplate("Key", editable: true, placeholder: null, errorColor: true) });
            foreach (var column in recipe.Columns)
            {
                var derived = column.Lookup is not null;
                view.Columns.Add(new GridViewColumn
                {
                    Header = column.Name,
                    Width = 170,
                    CellTemplate = CellTemplate("[" + column.Name + "]", editable: !derived, placeholder: derived ? null : column.Default, errorColor: false),
                });
            }

            view.Columns.Add(new GridViewColumn { Header = PatchRecipeTexts.Hint, Width = 260, CellTemplate = HintTemplate() });
        }

        _tableList.View = view;
        if (_importSpecialsButton is not null)
        {
            _importSpecialsButton.Visibility = Recipe?.Preset is SpecialBuildingsPreset.Id or SpecialResourcesPreset.Id ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_adoptModButton is not null)
        {
            _adoptModButton.Visibility = Recipe?.Preset == SpecialBuildingsPreset.Id ? Visibility.Visible : Visibility.Collapsed;
        }

        _tableView = CollectionViewSource.GetDefaultView(_tableRows.ToList());
        _tableView.Filter = item => item is PatchTableRow row && TableFilter.Matches(row.ForFilter, _tableFilter.Text, _onlyErrors.IsChecked == true);
        _tableList.ItemsSource = _tableView;
        LastTable = null;
        UpdateTableStatus();
        _resolveTimer.Stop();
        _ = ResolveTableAsync();
    }

    private void TableEdited()
    {
        MarkRecipeEdited();
        _resolveTimer.Stop();
        _resolveTimer.Start();
    }

    /// <summary>Wertet eine Kopie des Rezepts im Hintergrund aus; nur das Ergebnis des neuesten Laufs wird gezeigt.</summary>
    private async Task ResolveTableAsync()
    {
        if (Recipe is not { } recipe || ModRoot is not { } modRoot || _host.VanillaRoot() is not { } vanilla)
        {
            return;
        }

        var version = ++_resolveVersion;
        var copy = PatchRecipeStore.Clone(recipe);
        ResolvedTable table;
        try
        {
            table = await Task.Run(() => ColumnResolver.Resolve(copy, new PatchWorkspace(modRoot, vanilla)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus(ex.Message);
            return;
        }

        if (version != _resolveVersion || !ReferenceEquals(recipe, Recipe) || table.Rows.Count != _tableRows.Count)
        {
            return;
        }

        for (var i = 0; i < _tableRows.Count; i++)
        {
            _tableRows[i].SetResolved(table.Rows[i]);
        }

        LastTable = table;
        _tableView?.Refresh();
        UpdateTableStatus();
    }

    private void UpdateTableStatus()
    {
        var errors = _tableRows.Count(r => r.HasErrors);
        TableBadge.Visibility = errors > 0 ? Visibility.Visible : Visibility.Collapsed;
        TableBadgeText.Text = errors.ToString();
        var defaults = Recipe?.Columns.Where(c => c.Lookup is null && c.Default.Length > 0).Select(c => c.Name + " = " + c.Default).ToList() ?? new List<string>();
        _tableStatus.Text = PatchRecipeTexts.TableStatus(_tableRows.Count, errors, defaults.Count > 0 ? string.Join(" · ", defaults) : PatchRecipeTexts.NoDefaults);
    }

    /// <summary>"Specials aus Vanilla einlesen": gewählte Specials werden neue Zeilen, vorhandene bleiben unverändert.</summary>
    private void ImportSpecials_Click(object sender, RoutedEventArgs e) => ImportSpecials();

    private void ImportSpecials()
    {
        if (Recipe is not { } recipe || ModRoot is not { } modRoot || _host.VanillaRoot() is not { } vanilla)
        {
            SetStatus(ContextMissingText);
            return;
        }

        var dialog = new SpecialsImportDialog(recipe, modRoot, vanilla) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.SelectedKeys.Count == 0)
        {
            return;
        }

        var existing = recipe.Rows.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        recipe.Rows.AddRange(dialog.SelectedKeys.Where(existing.Add).Select(key => new PatchRow { Key = key }));
        MarkRecipeEdited();
        RebuildTable();
    }

    private void AddRow_Click(object sender, RoutedEventArgs e)
    {
        if (Recipe is not { } recipe)
        {
            return;
        }

        recipe.Rows.Add(new PatchRow { Key = string.Empty });
        MarkRecipeEdited();
        RebuildTable();
        _tableList.ScrollIntoView(_tableRows[^1]);
    }

    private void RemoveRow_Click(object sender, RoutedEventArgs e)
    {
        if (Recipe is not { } recipe || _tableList.SelectedItems.Count == 0)
        {
            return;
        }

        foreach (var row in _tableList.SelectedItems.OfType<PatchTableRow>().ToList())
        {
            recipe.Rows.Remove(row.Row);
        }

        MarkRecipeEdited();
        RebuildTable();
    }

    private void Columns_Click(object sender, RoutedEventArgs e)
    {
        if (Recipe is { } recipe && PatchColumnsDialog.Edit(this, recipe))
        {
            MarkRecipeEdited();
            RebuildTable();
        }
    }

    /// <summary>Zellvorlage als XAML: bearbeitbares Feld mit grauem Standardwert als Platzhalter, oder graue Anzeige für abgeleitete Werte.</summary>
    private static DataTemplate CellTemplate(string path, bool editable, string? placeholder, bool errorColor)
    {
        var errorTrigger = errorColor
            ? "<DataTrigger Binding=\"{Binding HasErrors}\" Value=\"True\"><Setter Property=\"Foreground\" Value=\"{DynamicResource Theme.Danger}\" /></DataTrigger>"
            : string.Empty;
        var inner = editable
            ? "<Grid>" +
              (placeholder is { Length: > 0 }
                  ? "<TextBlock Text=\"" + SecurityElement.Escape(placeholder) + "\" Margin=\"5,0\" VerticalAlignment=\"Center\" IsHitTestVisible=\"False\" Foreground=\"{DynamicResource Theme.TextMuted}\" FontStyle=\"Italic\">" +
                    "<TextBlock.Style><Style TargetType=\"TextBlock\"><Setter Property=\"Visibility\" Value=\"Collapsed\" /><Style.Triggers>" +
                    "<DataTrigger Binding=\"{Binding " + path + "}\" Value=\"\"><Setter Property=\"Visibility\" Value=\"Visible\" /></DataTrigger>" +
                    "</Style.Triggers></Style></TextBlock.Style></TextBlock>"
                  : string.Empty) +
              "<TextBox Text=\"{Binding " + path + ", Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\" Background=\"Transparent\" MinWidth=\"120\">" +
              "<TextBox.Style><Style TargetType=\"TextBox\" BasedOn=\"{StaticResource {x:Type TextBox}}\"><Style.Triggers>" + errorTrigger + "</Style.Triggers></Style></TextBox.Style></TextBox>" +
              "</Grid>"
            : "<TextBlock Text=\"{Binding " + path + "}\" FontStyle=\"Italic\" Foreground=\"{DynamicResource Theme.TextMuted}\" Margin=\"5,0\" />";
        return (DataTemplate)XamlReader.Parse(
            "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" + inner + "</DataTemplate>");
    }

    private static DataTemplate HintTemplate() => (DataTemplate)XamlReader.Parse(
        "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" +
        "<TextBlock Text=\"{Binding Hint}\" Foreground=\"{DynamicResource Theme.Danger}\" TextTrimming=\"CharacterEllipsis\" ToolTip=\"{Binding Hint}\" Margin=\"5,0\" /></DataTemplate>");
}
