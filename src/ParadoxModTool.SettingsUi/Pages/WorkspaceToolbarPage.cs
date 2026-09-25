using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Categories;
using ParadoxModTool.Editor;
using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi.Pages;

/// <summary>
/// Kategorie 4: Kategorien verwalten, dieselben Häkchen wie "Filter ▾" (Herkunft und Kategorien) und "Symbole ▾", und ob die beiden
/// Knöpfe in der Werkzeugleiste jedes Arbeitsbereichs stehen.
/// </summary>
public sealed class WorkspaceToolbarPage : SettingsPage
{
    private readonly StackPanel _filters = new();

    public WorkspaceToolbarPage(ISettingsHost host)
        : base(host)
    {
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

        var settings = Host.Settings;
        AddHeading(SettingsTexts.ButtonsHeading);
        AddCheckBox(SettingsTexts.WorkspaceFilterButton, SettingsTexts.WorkspaceFilterButtonTip, () => settings.WorkspaceFilterButtonVisible, value => settings.WorkspaceFilterButtonVisible = value);
        AddCheckBox(SettingsTexts.WorkspaceIconButton, SettingsTexts.WorkspaceIconButtonTip, () => settings.WorkspaceIconButtonVisible, value => settings.WorkspaceIconButtonVisible = value);

        RefreshOn(handler => FilterBarSettings.Changed += handler, handler => FilterBarSettings.Changed -= handler);
        RefreshOn(handler => TabIconSettings.Changed += handler, handler => TabIconSettings.Changed -= handler);
        RefreshOnSettings();
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
            AddFilter(FilterEntryKeys.ForCategory(category.Id), category.Name);
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
