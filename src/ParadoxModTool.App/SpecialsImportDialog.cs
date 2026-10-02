using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using ParadoxModTool.Localization;
using ParadoxModTool.Patches;
using Binding = System.Windows.Data.Binding;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>Eine Zeile des Einlesen-Dialogs; <see cref="IsChecked"/> meldet Änderungen, weil "Alle sichtbaren wählen" sie im Code setzt.</summary>
public sealed class SpecialImportItem : INotifyPropertyChanged
{
    private bool _isChecked;

    public event PropertyChangedEventHandler? PropertyChanged;

    public required string Key { get; init; }

    public required string Baronies { get; init; }

    /// <summary>Erklärung bei "kein Ort" (Hover), sonst null.</summary>
    public string? BaroniesTip { get; init; }

    public required bool SpecialInMod { get; init; }

    public string InModText => SpecialInMod ? PatchRecipeTexts.ImportYes : string.Empty;

    public required bool IsRow { get; init; }

    public string IsRowText => IsRow ? PatchRecipeTexts.ImportYes : string.Empty;

    /// <summary>Ohne Ort in der History vergibt ein Script den Slot; solche Specials bleiben Special und sind nicht wählbar.</summary>
    public required bool HasPlace { get; init; }

    /// <summary>Gewählt werden darf nur, was zur Vorlage passt: Vorlage "normale Gebäude" braucht einen Ort, Vorlage "Ressourcen" nicht (sie bearbeitet die Gebäude nur).</summary>
    public required bool Eligible { get; init; }

    public bool CanCheck => !IsRow && Eligible;

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked != value)
            {
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }
    }
}

/// <summary>
/// "Specials aus Vanilla einlesen" (Spec 2026-09-30-patch-recipes, Etappe 4): alle Vanilla-Specials, die eine Kette beginnen, mit gefundenen
/// Baronien (oder "kein Ort", dann nicht wählbar), ob die Mod sie noch als Special definiert und ob sie schon Zeile sind. Nichts ist vorgewählt.
/// </summary>
public sealed class SpecialsImportDialog : Window
{
    private readonly PatchRecipe _recipe;
    private readonly string _modRoot;
    private readonly string _vanillaRoot;
    private readonly Controls.ListView _list = new() { SelectionMode = Controls.SelectionMode.Extended };
    private readonly Controls.TextBox _filter = new() { VerticalContentAlignment = VerticalAlignment.Center };
    private readonly Controls.CheckBox _onlyInMod = new() { Content = new Controls.TextBlock { Text = PatchRecipeTexts.ImportOnlyInMod }, IsChecked = true, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.CheckBox _hideRows = new() { Content = new Controls.TextBlock { Text = PatchRecipeTexts.ImportHideRows }, IsChecked = true, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.TextBlock _status = new() { VerticalAlignment = VerticalAlignment.Center, Text = PatchRecipeTexts.ImportLoading };
    private readonly Controls.Button _accept = new() { Content = new Controls.TextBlock { Text = PatchRecipeTexts.ImportAccept }, MinWidth = 90, IsDefault = true, IsEnabled = false, Margin = new Thickness(0, 0, 8, 0) };
    private List<SpecialImportItem> _items = new();
    private ICollectionView? _view;

    public SpecialsImportDialog(PatchRecipe recipe, string modRoot, string vanillaRoot)
    {
        _recipe = recipe;
        _modRoot = modRoot;
        _vanillaRoot = vanillaRoot;

        // Die Vorlage "Ressourcen" schreibt auch in Vanilla-Gebäude ohne Mod-Definition (Kopie wird angelegt): dort nicht auf "in Mod" einschränken.
        _onlyInMod.IsChecked = recipe.Preset != SpecialResourcesPreset.Id;
        Title = PatchRecipeTexts.ImportSpecials;
        Width = 900;
        Height = 680;
        MinWidth = 560;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var view = new Controls.GridView();
        view.Columns.Add(new Controls.GridViewColumn { Width = 34, CellTemplate = CellTemplate("<CheckBox IsChecked=\"{Binding IsChecked, Mode=TwoWay}\" IsEnabled=\"{Binding CanCheck}\" ToolTip=\"{Binding BaroniesTip}\" ToolTipService.ShowOnDisabled=\"True\" />") });
        view.Columns.Add(new Controls.GridViewColumn { Header = PatchRecipeTexts.ImportColKey, Width = 280, DisplayMemberBinding = new Binding(nameof(SpecialImportItem.Key)) });
        view.Columns.Add(new Controls.GridViewColumn
        {
            Header = PatchRecipeTexts.ImportColBaronies,
            Width = 300,
            CellTemplate = CellTemplate("<TextBlock Text=\"{Binding Baronies}\" ToolTip=\"{Binding BaroniesTip}\" TextTrimming=\"CharacterEllipsis\" />"),
        });
        view.Columns.Add(new Controls.GridViewColumn { Header = PatchRecipeTexts.ImportColInMod, Width = 130, DisplayMemberBinding = new Binding(nameof(SpecialImportItem.InModText)) });
        view.Columns.Add(new Controls.GridViewColumn { Header = PatchRecipeTexts.ImportColIsRow, Width = 100, DisplayMemberBinding = new Binding(nameof(SpecialImportItem.IsRowText)) });
        _list.View = view;

        _filter.TextChanged += (_, _) => RefreshView();
        _onlyInMod.Click += (_, _) => RefreshView();
        _hideRows.Click += (_, _) => RefreshView();

        var top = new Controls.DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var loupe = new Controls.TextBlock { Text = "🔍", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
        var toggles = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        toggles.Children.Add(_onlyInMod);
        toggles.Children.Add(_hideRows);
        Controls.DockPanel.SetDock(loupe, Controls.Dock.Left);
        Controls.DockPanel.SetDock(toggles, Controls.Dock.Right);
        top.Children.Add(loupe);
        top.Children.Add(toggles);
        top.Children.Add(_filter);

        var selectVisible = SmallButton(PatchRecipeTexts.ImportSelectVisible, () => SetVisible(true));
        var selectNone = SmallButton(PatchRecipeTexts.ImportSelectNone, () => _items.ForEach(i => i.IsChecked = false));
        var cancel = new Controls.Button { Content = new Controls.TextBlock { Text = CommonTexts.Cancel }, MinWidth = 90, IsCancel = true };
        _accept.Click += (_, _) => DialogResult = true;
        var bottom = new Controls.DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var left = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        left.Children.Add(selectVisible);
        left.Children.Add(selectNone);
        left.Children.Add(_status);
        var right = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        right.Children.Add(_accept);
        right.Children.Add(cancel);
        Controls.DockPanel.SetDock(right, Controls.Dock.Right);
        bottom.Children.Add(right);
        bottom.Children.Add(left);

        var root = new Controls.DockPanel { Margin = new Thickness(10) };
        Controls.DockPanel.SetDock(top, Controls.Dock.Top);
        Controls.DockPanel.SetDock(bottom, Controls.Dock.Bottom);
        root.Children.Add(top);
        root.Children.Add(bottom);
        root.Children.Add(_list);
        Content = root;
        Loaded += async (_, _) => await LoadAsync();
    }

    /// <summary>Die gewählten Schlüssel in Listenreihenfolge.</summary>
    public IReadOnlyList<string> SelectedKeys => _items.Where(i => i.IsChecked && i.CanCheck && Visible(i)).Select(i => i.Key).ToList();

    private static DataTemplate CellTemplate(string content) => (DataTemplate)XamlReader.Parse(
        "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" + content + "</DataTemplate>");

    private static Controls.Button SmallButton(string text, Action click)
    {
        var button = new Controls.Button { Content = new Controls.TextBlock { Text = text }, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>Liest die Specials und löst die Spalten des Rezepts für sie auf (im Hintergrund).</summary>
    private async Task LoadAsync()
    {
        var resources = _recipe.Preset == SpecialResourcesPreset.Id;
        // Die Ortsspalten (Anzeige "kein Ort") stammen aus der Vorlage "normale Gebäude": das Rezept "Ressourcen" hat keine eigenen.
        var copy = resources ? SpecialBuildingsPreset.Create(_recipe.Name, Array.Empty<string>()) : PatchRecipeStore.Clone(_recipe);
        var existing = _recipe.Rows.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        try
        {
            _items = await Task.Run(() =>
            {
                var workspace = new PatchWorkspace(_modRoot, _vanillaRoot);
                var specials = SpecialBuildingsPreset.VanillaSpecials(workspace);
                copy.Rows = specials.Select(s => new PatchRow { Key = s.Key }).ToList();
                var table = ColumnResolver.Resolve(copy, workspace);
                var baronyColumn = copy.Columns.FirstOrDefault(c => c.TitleKey && c.Lookup is not null)?.Name ?? "barony";
                return specials.Select((special, index) =>
                {
                    var baronies = table.Rows[index].Values.TryGetValue(baronyColumn, out var value) ? value : string.Empty;
                    return new SpecialImportItem
                    {
                        Key = special.Key,
                        Baronies = baronies.Length > 0 ? baronies : PatchRecipeTexts.ImportNoPlace,
                        BaroniesTip = baronies.Length > 0 || resources ? null : PatchRecipeTexts.ImportNoPlaceTip,
                        HasPlace = baronies.Length > 0,
                        Eligible = baronies.Length > 0 || resources,
                        SpecialInMod = special.SpecialInMod,
                        IsRow = existing.Contains(special.Key),
                    };
                }).ToList();
            });
        }
        catch (Exception ex)
        {
            // Läuft aus einem async-void-Handler (Loaded): kein Fehler darf bis zum Dispatcher durchschlagen und PMT beenden.
            _status.Text = ex.Message;
            return;
        }

        foreach (var item in _items)
        {
            item.PropertyChanged += (_, _) => UpdateStatus();
        }

        _view = CollectionViewSource.GetDefaultView(_items);
        _view.Filter = Visible;
        _list.ItemsSource = _view;
        _accept.IsEnabled = true;
        UpdateStatus();
    }

    private bool Visible(object item)
    {
        if (item is not SpecialImportItem special)
        {
            return false;
        }

        var filter = _filter.Text.Trim();
        return (filter.Length == 0 || special.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) || special.Baronies.Contains(filter, StringComparison.OrdinalIgnoreCase))
            && (_onlyInMod.IsChecked != true || special.SpecialInMod)
            && (_hideRows.IsChecked != true || !special.IsRow);
    }

    private void SetVisible(bool value)
    {
        foreach (var item in _items.Where(i => i.CanCheck && Visible(i)))
        {
            item.IsChecked = value;
        }
    }

    /// <summary>Filter neu anwenden; die Zählung folgt, weil nur sichtbare angehakte Zeilen übernommen werden.</summary>
    private void RefreshView()
    {
        _view?.Refresh();
        if (_view is not null)
        {
            UpdateStatus();
        }
    }

    private void UpdateStatus() => _status.Text = PatchRecipeTexts.ImportCount(SelectedKeys.Count, _items.Count);
}
