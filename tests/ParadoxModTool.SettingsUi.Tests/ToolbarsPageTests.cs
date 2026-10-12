using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Settings;
using ParadoxModTool.SettingsUi.Pages;
using Xunit;

namespace ParadoxModTool.SettingsUi.Tests;

public sealed class ToolbarsPageTests
{
    [Fact]
    public void ToolbarsPageListsTheOrderWithSeparatorsAndTogglesVisibility() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new ToolbarsPage(host);
        var box = UiFind.All<ReorderListBox>(page.View).Single();
        Assert.Equal(ToolbarCatalog.DefaultOrder, box.CurrentIds);
        UiFind.Toggle(UiFind.All<CheckBox>(box.Row(ToolbarItemIds.Undo)).Single());
        Assert.False(host.Settings.IsToolbarItemVisible(ToolbarItemIds.Undo));
    });

    [Fact]
    public void SeparatorsCanBeRemovedAddedAndReset() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new ToolbarsPage(host);
        var box = UiFind.All<ReorderListBox>(page.View).Single();
        UiFind.Click(UiFind.All<Button>(box.Row("sep:3")).Single(b => UiFind.Label(b) == "✕"));
        Assert.DoesNotContain("sep:3", host.Settings.ToolbarOrder);

        UiFind.Click(UiFind.Button(page, SettingsTexts.ToolbarAddSeparator));
        UiFind.Click(UiFind.Button(page, SettingsTexts.ToolbarAddSeparator));
        Assert.Equal(new[] { "sep:6", "sep:7" }, host.Settings.ToolbarOrder.TakeLast(2));

        UiFind.Click(UiFind.Button(page, SettingsTexts.ToolbarResetOrder));
        Assert.Equal(ToolbarCatalog.DefaultOrder, host.Settings.ToolbarOrder);
    });

    [Fact]
    public void ClassicLayoutShowsGroupCheckBoxesInsteadOfTheList() => Sta.Run(() =>
    {
        var host = new FakeHost();
        host.Settings.WindowLayout = "Classic";
        using var page = new ToolbarsPage(host);
        var box = UiFind.All<ReorderListBox>(page.View).Single();
        Assert.Equal(Visibility.Collapsed, ((FrameworkElement)box.Parent).Visibility);

        // Die Listenzeilen des Kachel-Layouts haben beschriftungslose Häkchen; diese Beschriftungen stammen also nur aus dem klassischen Bereich.
        var labels = UiFind.All<CheckBox>(page.View).Select(UiFind.Label).ToList();
        Assert.Contains(SettingsTexts.ToolbarItemLabel(ToolbarItemIds.Undo), labels);
        Assert.DoesNotContain(SettingsTexts.ToolbarItemLabel(ToolbarItemIds.New), labels);
        Assert.DoesNotContain(SettingsTexts.ToolbarItemLabel(ToolbarItemIds.LaunchGame), labels);
        Assert.Equal(Visibility.Collapsed, UiFind.CheckBox(page, SettingsTexts.WorkspaceMarkButtons).Visibility);

        // Der klassische Bereich ist im Klassik-Layout sichtbar (der Eintrag liegt direkt darin) ...
        var classicPanel = (FrameworkElement)UiFind.CheckBox(page, SettingsTexts.ToolbarItemLabel(ToolbarItemIds.Undo)).Parent;
        Assert.Equal(Visibility.Visible, classicPanel.Visibility);

        // ... und beim Umschalten auf Kacheln kehrt sich alles um.
        host.Settings.WindowLayout = "Tiles";
        Assert.Equal(Visibility.Collapsed, classicPanel.Visibility);
        Assert.Equal(Visibility.Visible, ((FrameworkElement)box.Parent).Visibility);
        Assert.Equal(Visibility.Visible, UiFind.CheckBox(page, SettingsTexts.WorkspaceMarkButtons).Visibility);
    });

    [Fact]
    public void WorkspaceButtonCheckBoxesWriteTheModel() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new ToolbarsPage(host);
        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.WorkspaceCategoryButton));
        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.WorkspaceSortButton));
        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.WorkspaceMarkButtons));
        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.WorkspaceFilterButton));
        Assert.False(host.Settings.WorkspaceCategoryButtonVisible);
        Assert.False(host.Settings.WorkspaceSortButtonVisible);
        Assert.False(host.Settings.WorkspaceMarkButtonsVisible);
        Assert.False(host.Settings.WorkspaceFilterButtonVisible);
    });
}
