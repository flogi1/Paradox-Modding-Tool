using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using ParadoxModTool.Localization;
using ParadoxModTool.Patches;
using Binding = System.Windows.Data.Binding;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>Eine Zeile des Zuordnungsfensters; der Vanilla-Schlüssel ist editierbar, jede Änderung prüft alle Zeilen neu (Ziel doppelt).</summary>
public sealed class ModBuildingItem : INotifyPropertyChanged
{
    private bool _isChecked;
    private string _newKey = string.Empty;
    private RenameStatus _status;
    private string? _detail;

    public event PropertyChangedEventHandler? PropertyChanged;

    public required string OldKey { get; init; }

    public required string File { get; init; }

    /// <summary>Wird vom Fenster gesetzt: prüft nach jeder Änderung des Vanilla-Schlüssels alle Zeilen neu.</summary>
    public Action? Changed { get; set; }

    public string NewKey
    {
        get => _newKey;
        set
        {
            if (_newKey != value)
            {
                _newKey = value;
                Raise(nameof(NewKey));
                Changed?.Invoke();
            }
        }
    }

    public RenameStatus Status => _status;

    public string? Detail => _detail;

    public bool CanCheck => _status == RenameStatus.Ok;

    public string StatusText => _status switch
    {
        RenameStatus.Ok => PatchRecipeTexts.AdoptModStatusOk,
        RenameStatus.NoProposal => PatchRecipeTexts.AdoptModStatusNoProposal,
        RenameStatus.Ambiguous => PatchRecipeTexts.AdoptModStatusAmbiguous(_detail ?? string.Empty),
        RenameStatus.TargetDefined => PatchRecipeTexts.AdoptModStatusTargetDefined(_detail ?? string.Empty),
        RenameStatus.TargetInVanilla => PatchRecipeTexts.AdoptModStatusTargetInVanilla(_detail ?? string.Empty),
        RenameStatus.TargetTwice => PatchRecipeTexts.AdoptModStatusTargetTwice,
        _ => PatchRecipeTexts.AdoptModStatusInvalid,
    };

    /// <summary>Ursprünglich vorgeschlagen (oder mehrdeutig); für "Nur mit Vorschlag".</summary>
    public bool HadProposal { get; init; }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked != value)
            {
                _isChecked = value;
                Raise(nameof(IsChecked));
            }
        }
    }

    internal void SetStatus(RenameStatus status, string? detail)
    {
        _status = status;
        _detail = detail;
        if (!CanCheck)
        {
            IsChecked = false;
        }

        Raise(nameof(Status));
        Raise(nameof(StatusText));
        Raise(nameof(Detail));
        Raise(nameof(CanCheck));
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// "Bestehende Mod-Gebäude übernehmen" (Spec 2026-09-30-patch-recipes, Etappe 5): Zuordnung Mod-Schlüssel → Vanilla-Schlüssel aus der Lokalisierung,
/// mit Prüfung je Zeile. Vorgewählt sind alle Zeilen mit Status ok. Ergebnis: <see cref="Selected"/> und <see cref="WasComments"/>.
/// </summary>
public sealed class ModBuildingsAdoptDialog : Window
{
    private readonly string _modRoot;
    private readonly string _vanillaRoot;
    private readonly Controls.ListView _list = new() { SelectionMode = Controls.SelectionMode.Extended };
    private readonly Controls.TextBox _filter = new() { VerticalContentAlignment = VerticalAlignment.Center };
    private readonly Controls.CheckBox _onlyProposals = new() { Content = new Controls.TextBlock { Text = PatchRecipeTexts.AdoptModOnlyProposals }, IsChecked = true, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.CheckBox _wasComments = new() { Content = new Controls.TextBlock { Text = PatchRecipeTexts.AdoptModWasComments }, IsChecked = true, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.TextBlock _status = new() { VerticalAlignment = VerticalAlignment.Center, Text = PatchRecipeTexts.AdoptModLoading, TextWrapping = TextWrapping.Wrap };
    private readonly Controls.Button _accept = new() { Content = new Controls.TextBlock { Text = PatchRecipeTexts.ComputePreview }, MinWidth = 120, IsDefault = true, IsEnabled = false, Margin = new Thickness(0, 0, 8, 0) };
    private List<ModBuildingItem> _items = new();
    private KeyRenameProposals? _proposals;
    private ICollectionView? _view;
    private bool _rechecking;

    public ModBuildingsAdoptDialog(string modRoot, string vanillaRoot)
    {
        _modRoot = modRoot;
        _vanillaRoot = vanillaRoot;
        Title = PatchRecipeTexts.AdoptModButton;
        Width = 1000;
        Height = 680;
        MinWidth = 600;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var view = new Controls.GridView();
        view.Columns.Add(new Controls.GridViewColumn { Width = 34, CellTemplate = CellTemplate("<CheckBox IsChecked=\"{Binding IsChecked, Mode=TwoWay}\" IsEnabled=\"{Binding CanCheck}\" />") });
        view.Columns.Add(new Controls.GridViewColumn { Header = PatchRecipeTexts.AdoptModColOld, Width = 300, DisplayMemberBinding = new Binding(nameof(ModBuildingItem.OldKey)) });
        view.Columns.Add(new Controls.GridViewColumn
        {
            Header = PatchRecipeTexts.AdoptModColNew,
            Width = 260,
            CellTemplate = CellTemplate("<TextBox Text=\"{Binding NewKey, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\" MinWidth=\"220\" />"),
        });
        view.Columns.Add(new Controls.GridViewColumn
        {
            Header = PatchRecipeTexts.AdoptModColStatus,
            Width = 200,
            CellTemplate = CellTemplate("<TextBlock Text=\"{Binding StatusText}\" ToolTip=\"{Binding StatusText}\" TextTrimming=\"CharacterEllipsis\" />"),
        });
        view.Columns.Add(new Controls.GridViewColumn { Header = PatchRecipeTexts.AdoptModColFile, Width = 180, DisplayMemberBinding = new Binding(nameof(ModBuildingItem.File)) });
        _list.View = view;

        _filter.TextChanged += (_, _) => RefreshView();
        _onlyProposals.Click += (_, _) => RefreshView();

        var top = new Controls.DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var loupe = new Controls.TextBlock { Text = "🔍", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
        var toggles = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        toggles.Children.Add(_onlyProposals);
        toggles.Children.Add(_wasComments);
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
        var left = new Controls.DockPanel();
        Controls.DockPanel.SetDock(selectVisible, Controls.Dock.Left);
        Controls.DockPanel.SetDock(selectNone, Controls.Dock.Left);
        left.Children.Add(selectVisible);
        left.Children.Add(selectNone);
        left.Children.Add(_status);
        var right = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
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

    /// <summary>Die gewählten Umbenennungen in Listenreihenfolge.</summary>
    public IReadOnlyList<KeyRename> Selected => _items.Where(i => i.IsChecked && i.CanCheck && Visible(i)).Select(i => new KeyRename(i.OldKey, i.NewKey.Trim())).ToList();

    public bool WasComments => _wasComments.IsChecked == true;

    private static DataTemplate CellTemplate(string content) => (DataTemplate)XamlReader.Parse(
        "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" + content + "</DataTemplate>");

    private static Controls.Button SmallButton(string text, Action click)
    {
        var button = new Controls.Button { Content = new Controls.TextBlock { Text = text }, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Top };
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>Liest Vorschläge im Hintergrund; vorgewählt ist jede Zeile mit Status ok.</summary>
    private async Task LoadAsync()
    {
        try
        {
            _proposals = await Task.Run(() => KeyRenameProposals.Build(new PatchWorkspace(_modRoot, _vanillaRoot), SpecialBuildingsPreset.BuildingsFolder));
        }
        catch (Exception ex)
        {
            // Läuft aus einem async-void-Handler (Loaded): kein Fehler darf bis zum Dispatcher durchschlagen und PMT beenden.
            _status.Text = ex.Message;
            return;
        }

        _items = _proposals.Proposals.Select(p =>
        {
            var item = new ModBuildingItem { OldKey = p.OldKey, File = p.File, HadProposal = p.Status != RenameStatus.NoProposal };
            item.NewKey = p.NewKey ?? string.Empty;
            item.SetStatus(p.Status, p.Detail);
            item.IsChecked = item.CanCheck;
            item.Changed = Recheck;
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ModBuildingItem.IsChecked))
                {
                    UpdateStatus();
                }
            };
            return item;
        }).ToList();

        _view = CollectionViewSource.GetDefaultView(_items);
        _view.Filter = Visible;
        _list.ItemsSource = _view;
        _accept.IsEnabled = _items.Count > 0;
        UpdateStatus();
    }

    /// <summary>Nach einer Änderung am Vanilla-Schlüssel: alle Zeilen mit eingetragenem Schlüssel neu prüfen (auch "Ziel doppelt" bei anderen).</summary>
    private void Recheck()
    {
        if (_rechecking || _proposals is null)
        {
            return;
        }

        _rechecking = true;
        var all = _items.Where(i => i.NewKey.Trim().Length > 0).Select(i => new KeyRename(i.OldKey, i.NewKey.Trim())).ToList();
        foreach (var item in _items)
        {
            if (item.NewKey.Trim().Length == 0 && item.Status is RenameStatus.NoProposal or RenameStatus.Ambiguous)
            {
                continue;
            }

            var status = _proposals.Check(item.OldKey, item.NewKey.Trim(), all, out var detail);
            if (status != item.Status || detail != item.Detail)
            {
                item.SetStatus(status, detail);
            }
        }

        _rechecking = false;
        UpdateStatus();
    }

    private bool Visible(object item)
    {
        if (item is not ModBuildingItem building)
        {
            return false;
        }

        var filter = _filter.Text.Trim();
        return (filter.Length == 0 || building.OldKey.Contains(filter, StringComparison.OrdinalIgnoreCase) || building.NewKey.Contains(filter, StringComparison.OrdinalIgnoreCase))
            && (_onlyProposals.IsChecked != true || building.HadProposal || building.NewKey.Trim().Length > 0);
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

    private void UpdateStatus()
    {
        if (_items.Count == 0)
        {
            _status.Text = PatchRecipeTexts.AdoptModNone;
            return;
        }

        if (!_items.Any(i => i.HadProposal || i.NewKey.Trim().Length > 0))
        {
            // Sonst wäre die Liste mit "Nur mit Vorschlag" leer und nur "0 gewählt von n" erklärte das.
            _status.Text = PatchRecipeTexts.AdoptModNoProposals(_items.Count);
            return;
        }

        var text = PatchRecipeTexts.ImportCount(Selected.Count, _items.Count);
        if (_proposals is { OrphanLocKeys: > 0 } proposals)
        {
            text += " · " + PatchTexts.RenameOrphans(proposals.OrphanLocKeys);
        }

        _status.Text = text;
    }
}
