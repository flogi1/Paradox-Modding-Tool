using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Categories;
using ParadoxModTool.Editor;
using ParadoxModTool.Settings;
using ParadoxModTool.Shell;

namespace ParadoxModTool.SettingsUi.Pages;

/// <summary>
/// Kategorie "Werkzeugleisten": die kleine Werkzeugleiste des Kachel-Layouts (Reihenfolge, Sichtbarkeit, Trennlinien) oder im klassischen
/// Layout die Gruppen-Häkchen, dazu die Knöpfe in der Werkzeugleiste jedes Arbeitsbereichs samt Kategorien, Filter- und Symbolliste.
/// </summary>
public sealed class ToolbarsPage : SettingsPage
{
    private readonly StackPanel _filters = new();

    public ToolbarsPage(ISettingsHost host)
        : base(host)
    {
        var tilesPanel = new StackPanel();
        var classicPanel = new StackPanel();
        Root.Children.Add(tilesPanel);
        Root.Children.Add(classicPanel);
        BuildTilesPanel(tilesPanel);
        BuildClassicPanel(classicPanel);

        var settings = Host.Settings;
        AddHeading(SettingsTexts.WorkspaceButtonsHeading);
        AddHint(SettingsTexts.WorkspaceButtonsHint);
        AddCheckBox(SettingsTexts.WorkspaceCategoryButton, null, () => settings.WorkspaceCategoryButtonVisible, value => settings.WorkspaceCategoryButtonVisible = value);
        AddCheckBox(SettingsTexts.WorkspaceFilterButton, SettingsTexts.WorkspaceFilterButtonTip, () => settings.WorkspaceFilterButtonVisible, value => settings.WorkspaceFilterButtonVisible = value);
        AddCheckBox(SettingsTexts.WorkspaceIconButton, SettingsTexts.WorkspaceIconButtonTip, () => settings.WorkspaceIconButtonVisible, value => settings.WorkspaceIconButtonVisible = value);
        AddCheckBox(SettingsTexts.WorkspaceSortButton, null, () => settings.WorkspaceSortButtonVisible, value => settings.WorkspaceSortButtonVisible = value);
        var marks = AddCheckBox(SettingsTexts.WorkspaceMarkButtons, null, () => settings.WorkspaceMarkButtonsVisible, value => settings.WorkspaceMarkButtonsVisible = value);

        // Notizen, Lesezeichen und Bug stehen nur im Kachel-Layout in der Werkzeugleiste des Arbeitsbereichs.
        OnRefresh(() =>
        {
            var tiles = settings.WindowLayout == "Tiles";
            tilesPanel.Visibility = tiles ? Visibility.Visible : Visibility.Collapsed;
            classicPanel.Visibility = tiles ? Visibility.Collapsed : Visibility.Visible;
            marks.Visibility = tiles ? Visibility.Visible : Visibility.Collapsed;
        });

        AddButton(SettingsTexts.ManageCategories, SettingsTexts.ManageCategoriesTip, () =>
        {
            Host.OpenCategories();
            BuildFilterList();
        });

        AddHeading(SettingsTexts.FilterHeading);
        AddHint(SettingsTexts.FilterHint);
        Root.Children.Add(_filters);
        BuildFilterList();
        OnRefresh(SyncFilterList);

        AddHeading(SettingsTexts.IconHeading);
        AddHint(SettingsTexts.IconHint);
        foreach (var kind in Enum.GetValues<TabIconKind>())
        {
            AddCheckBox(SettingsTexts.IconLabel(kind.ToString()), null, () => TabIconSettings.IsVisible(kind), visible => TabIconSettings.SetVisible(kind, visible));
        }

        RefreshOn(handler => FilterBarSettings.Changed += handler, handler => FilterBarSettings.Changed -= handler);
        RefreshOn(handler => TabIconSettings.Changed += handler, handler => TabIconSettings.Changed -= handler);
        RefreshOnSettings();
    }

    /// <summary>Kachel-Layout: Beschriftung, dann die Einträge mit Trennlinien als umsortierbare Liste.</summary>
    private void BuildTilesPanel(StackPanel panel)
    {
        var settings = Host.Settings;
        AddHeading(SettingsTexts.ToolbarItemsHeading, panel);
        AddCheckBox(SettingsTexts.ToolbarLabelsVisible, null, () => settings.ToolbarLabelsVisible, value => settings.ToolbarLabelsVisible = value, panel);
        AddHint(SettingsTexts.ToolbarItemsHint, panel);

        var box = new ReorderListBox(() => settings.ToolbarOrder, list => settings.ToolbarOrder = list, BuildRow);
        panel.Children.Add(box);
        OnRefresh(box.Rebuild);

        AddButton(SettingsTexts.ToolbarAddSeparator, null, AddSeparator, panel);
        AddButton(SettingsTexts.ToolbarResetOrder, null, () => settings.ToolbarOrder = ToolbarCatalog.DefaultOrder, panel);
    }

    /// <summary>Klassisches Layout: feste Gruppen, einzelne Einträge lassen sich ausblenden (ohne Dateien und Start).</summary>
    private void BuildClassicPanel(StackPanel panel)
    {
        AddHint(SettingsTexts.ToolbarClassicHint, panel);
        foreach (var group in Enum.GetValues<ToolbarGroup>().Where(group => group != ToolbarGroup.Files && group != ToolbarGroup.Launch))
        {
            AddHeading(SettingsTexts.ToolbarGroupLabel(group), panel);
            foreach (var item in ToolbarCatalog.All.Where(item => item.Group == group))
            {
                AddToolbarCheckBox(item.Id, panel);
            }
        }
    }

    private ReorderRow BuildRow(string id)
    {
        if (ToolbarCatalog.IsSeparator(id))
        {
            return new ReorderRow(
                id,
                SettingsTexts.ToolbarSeparator,
                IsSeparator: true,
                Remove: () => Host.Settings.ToolbarOrder = Host.Settings.ToolbarOrder.Where(entry => entry != id).ToList());
        }

        // Das Steuerelement übernimmt "Trailing" und hängt es um; deshalb in jedem Aufruf neu bauen.
        var visible = Host.Settings.IsToolbarItemVisible(id);
        var check = new CheckBox { IsChecked = visible, ToolTip = SettingsTexts.TileVisible, VerticalAlignment = VerticalAlignment.Center };
        check.Click += (_, _) => Host.Settings.SetToolbarItemVisible(id, check.IsChecked == true);
        return new ReorderRow(id, SettingsTexts.ToolbarItemLabel(id), Muted: !visible, Icon: ToolIcons.Create(id, 16, "Theme.Text"), Trailing: check);
    }

    /// <summary>Hängt eine neue Trennlinie ans Ende; ihre Nummer liegt über allen vorhandenen, damit sie eindeutig bleibt.</summary>
    private void AddSeparator()
    {
        var order = Host.Settings.ToolbarOrder;
        var next = order
            .Where(ToolbarCatalog.IsSeparator)
            .Select(id => int.Parse(id.Substring(ToolbarCatalog.SeparatorPrefix.Length)))
            .DefaultIfEmpty(0)
            .Max() + 1;
        Host.Settings.ToolbarOrder = order.Append(ToolbarCatalog.Separator(next)).ToList();
    }

    /// <summary>Baut die Filter-Häkchen neu (die Kategorienliste kann sich in "Kategorien verwalten" geändert haben).</summary>
    private void BuildFilterList()
    {
        _filters.Children.Clear();
        AddFilter(FilterEntryKeys.ForOrigin(FileOrigin.Vanilla), SettingsTexts.OriginVanilla);
        AddFilter(FilterEntryKeys.ForOrigin(FileOrigin.Mod), SettingsTexts.OriginMod);
        AddFilter(FilterEntryKeys.ForOrigin(FileOrigin.External), SettingsTexts.OriginExternal);
        foreach (var category in FileCategoryClassifier.Categories)
        {
            AddFilter(FilterEntryKeys.ForCategory(category.Id), CategoryDefaults.DisplayName(category));
        }
    }

    private void AddFilter(string key, string label)
    {
        var box = new CheckBox { Content = Label(label), Tag = key, Margin = new Thickness(0, 3, 0, 3), IsChecked = FilterBarSettings.IsShown(key) };
        box.Click += (_, _) => FilterBarSettings.SetShown(key, box.IsChecked == true);
        _filters.Children.Add(box);
    }

    private void SyncFilterList()
    {
        foreach (var box in _filters.Children.OfType<CheckBox>())
        {
            box.IsChecked = FilterBarSettings.IsShown((string)box.Tag);
        }
    }
}
