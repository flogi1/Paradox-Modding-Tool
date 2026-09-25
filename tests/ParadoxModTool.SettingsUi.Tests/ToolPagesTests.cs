using ParadoxModTool.Categories;
using ParadoxModTool.Compatch;
using ParadoxModTool.Editor;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.Settings;
using ParadoxModTool.SettingsUi.Pages;
using Xunit;

namespace ParadoxModTool.SettingsUi.Tests;

public sealed class ToolPagesTests
{
    [Fact]
    public void WorkspaceToolbarPageTogglesFilterAndIconEntries() => Sta.Run(() =>
    {
        FilterBarSettings.LoadHidden(null);
        TabIconSettings.LoadHidden(null);
        using var page = new WorkspaceToolbarPage(new FakeHost());

        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.OriginVanilla));
        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.IconLabel("Lock")));

        Assert.False(FilterBarSettings.IsShown(FilterEntryKeys.ForOrigin(FileOrigin.Vanilla)));
        Assert.False(TabIconSettings.IsVisible(TabIconKind.Lock));
        FilterBarSettings.LoadHidden(null);
        TabIconSettings.LoadHidden(null);
    });

    [Fact]
    public void WorkspaceToolbarPageListsCategoriesAndRebuildsAfterManaging() => Sta.Run(() =>
    {
        FileCategoryClassifier.SetCategories(new[] { new CategoryDefinition { Id = "a", Name = "Erste_Kategorie" } });
        var host = new FakeHost();
        using var page = new WorkspaceToolbarPage(host);
        Assert.Single(UiFind.All<System.Windows.Controls.CheckBox>(page.View), box => UiFind.Label(box) == "Erste_Kategorie");

        FileCategoryClassifier.SetCategories(new[] { new CategoryDefinition { Id = "b", Name = "Zweite" } });
        UiFind.Click(UiFind.Button(page, SettingsTexts.ManageCategories));

        Assert.Equal(new[] { "categories" }, host.Calls);
        var labels = UiFind.All<System.Windows.Controls.CheckBox>(page.View).Select(UiFind.Label).ToList();
        Assert.Contains("Zweite", labels);
        Assert.DoesNotContain("Erste_Kategorie", labels);
        FileCategoryClassifier.SetCategories(null);
    });

    [Fact]
    public void WorkspaceToolbarPageFollowsDropdownChanges() => Sta.Run(() =>
    {
        TabIconSettings.LoadHidden(null);
        using var page = new WorkspaceToolbarPage(new FakeHost());
        var box = UiFind.CheckBox(page, SettingsTexts.IconLabel("Bug"));

        TabIconSettings.SetVisible(TabIconKind.Bug, false);

        Assert.False(box.IsChecked);
        TabIconSettings.LoadHidden(null);
    });

    [Fact]
    public void WorkspaceToolbarPageTogglesTheToolbarButtons() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new WorkspaceToolbarPage(host);

        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.WorkspaceFilterButton));

        Assert.False(host.Settings.WorkspaceFilterButtonVisible);
        Assert.True(host.Settings.WorkspaceIconButtonVisible);
    });

    [Fact]
    public void WorkspaceToolbarPageFollowsButtonChanges() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new WorkspaceToolbarPage(host);
        var box = UiFind.CheckBox(page, SettingsTexts.WorkspaceIconButton);
        Assert.True(box.IsChecked);

        host.Settings.WorkspaceIconButtonVisible = false;

        Assert.False(box.IsChecked);
    });

    [Fact]
    public void CompatchPageWritesSwitchesAndDefaultView() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new CompatchPage(host);
        Assert.True(UiFind.Radio(page, CompatchTexts.View(CompatchView.Actions)).IsChecked);

        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.CompatchCautious));
        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.CompatchSkipLocked));
        UiFind.Choose(UiFind.Radio(page, CompatchTexts.View(CompatchView.Cards)));

        Assert.True(host.Settings.CompatchCautiousWrite);
        Assert.True(host.Settings.CompatchSkipLocked);
        Assert.Equal("C", host.Settings.CompatchDefaultView);
    });

    [Fact]
    public void CompatchPageFollowsViewChosenInTheMergeWindow() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new CompatchPage(host);

        host.Settings.CompatchDefaultView = "B";

        Assert.True(UiFind.Radio(page, CompatchTexts.View(CompatchView.Document)).IsChecked);
        Assert.False(UiFind.Radio(page, CompatchTexts.View(CompatchView.Actions)).IsChecked);
    });

    [Fact]
    public void GuiToolsPageGreysOutSubSwitchesWhenMasterIsOff() => Sta.Run(() =>
    {
        GuiSettings.SetToolsEnabled(true);
        var host = new FakeHost();
        using var page = new GuiToolsPage(host);
        var designer = UiFind.CheckBox(page, GuiTexts.MenuDesigner);
        var rebuild = UiFind.Button(page, GuiTexts.MenuRebuild);
        Assert.True(designer.IsEnabled);

        UiFind.Toggle(UiFind.CheckBox(page, GuiTexts.MenuTools));

        Assert.Equal(new[] { "gui:False" }, host.Calls);
        Assert.False(designer.IsEnabled);
        Assert.False(rebuild.IsEnabled);
        GuiSettings.SetToolsEnabled(true);
        Assert.True(designer.IsEnabled);
    });

    [Fact]
    public void GuiToolsPageWritesSubSwitchesAndRebuilds() => Sta.Run(() =>
    {
        GuiSettings.SetToolsEnabled(true);
        GuiSettings.SetBreadcrumbEnabled(true);
        GuiSettings.SetShownInEnabled(true);
        var host = new FakeHost();
        using var page = new GuiToolsPage(host);

        UiFind.Toggle(UiFind.CheckBox(page, GuiTexts.MenuBreadcrumb));
        UiFind.Toggle(UiFind.CheckBox(page, GuiTexts.MenuShownIn));
        UiFind.Click(UiFind.Button(page, GuiTexts.MenuRebuild));

        Assert.False(GuiSettings.BreadcrumbEnabled);
        Assert.False(GuiSettings.ShownInEnabled);
        Assert.Equal(new[] { "rebuildGui" }, host.Calls);
        GuiSettings.SetBreadcrumbEnabled(true);
        GuiSettings.SetShownInEnabled(true);
    });

    [Fact]
    public void GuiToolsPageRegistersAndRestoresTheGameEditor() => Sta.Run(() =>
    {
        GuiSettings.SetToolsEnabled(false);
        var host = new FakeHost { GameDisplayName = "EU V", GameEditorStatus = "Eingetragener Editor: notepad++.exe" };
        using var page = new GuiToolsPage(host);
        var register = UiFind.Button(page, GuiTexts.GameEditorRegister("EU V"));
        var restore = UiFind.Button(page, GuiTexts.GameEditorRestore);

        // Unabhängig vom GUI-Hauptschalter (Plan 4 Ruling 6).
        Assert.True(register.IsEnabled);
        Assert.Contains("Eingetragener Editor: notepad++.exe", UiFind.Texts(page));

        UiFind.Click(register);
        UiFind.Click(restore);
        Assert.Equal(new[] { "registerEditor", "restoreEditor" }, host.Calls);
        Assert.Contains("PMT", UiFind.Texts(page));

        host.CanRegisterGameEditor = false;
        page.Refresh();
        Assert.False(register.IsEnabled);
        Assert.False(restore.IsEnabled);
        GuiSettings.SetToolsEnabled(true);
    });
}
