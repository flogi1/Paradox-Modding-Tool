using ParadoxModTool.Editor;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.Settings;
using ParadoxModTool.SettingsUi.Pages;
using Xunit;

namespace ParadoxModTool.SettingsUi.Tests;

public sealed class WorkspacePagesTests
{
    [Fact]
    public void GeneralPageShowsPathsAndChoosesNewOnes() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new GeneralPage(host);
        Assert.Equal(2, UiFind.Texts(page).Count(text => text == SettingsTexts.NotSet));

        foreach (var button in UiFind.All<System.Windows.Controls.Button>(page.View).Where(b => UiFind.Label(b) == SettingsTexts.Choose).ToList())
        {
            UiFind.Click(button);
        }

        Assert.Equal(new[] { "game", "playsets" }, host.Calls);
        Assert.Contains(@"C:\Spiel\game.exe", UiFind.Texts(page));
        Assert.Contains(@"C:\Docs\playsets.json", UiFind.Texts(page));
    });

    [Fact]
    public void GeneralPageButtonsCallTheHost() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new GeneralPage(host);

        UiFind.Click(UiFind.Button(page, SettingsTexts.StorageLocations));
        UiFind.Click(UiFind.Button(page, SettingsTexts.RelearnLocProfiles));
        UiFind.Click(UiFind.Button(page, SettingsTexts.RelearnScopeRules));

        Assert.Equal(new[] { "storage", "relearnLoc", "relearnScope" }, host.Calls);
    });

    [Fact]
    public void GeneralPageHidesLaunchItems() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new GeneralPage(host);

        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.ToolbarItemLabel(ToolbarItemIds.Playset)));

        Assert.False(host.Settings.IsToolbarItemVisible(ToolbarItemIds.Playset));
        Assert.Equal(3, UiFind.All<System.Windows.Controls.CheckBox>(page.View).Count());
    });

    [Fact]
    public void ToolbarPageListsAllButLaunchItems() => Sta.Run(() =>
    {
        using var page = new ToolbarPage(new FakeHost());
        var labels = UiFind.All<System.Windows.Controls.CheckBox>(page.View).Select(UiFind.Label).ToList();

        Assert.Equal(ToolbarCatalog.All.Where(item => item.Group != ToolbarGroup.Launch).Select(item => SettingsTexts.ToolbarItemLabel(item.Id)), labels);
    });

    [Fact]
    public void ToolbarPageFollowsExternalVisibilityChanges() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new ToolbarPage(host);
        var box = UiFind.CheckBox(page, SettingsTexts.ToolbarItemLabel(ToolbarItemIds.Format));
        Assert.True(box.IsChecked);

        host.Settings.SetToolbarItemVisible(ToolbarItemIds.Format, false);

        Assert.False(box.IsChecked);
    });

    [Fact]
    public void WorkspacePageWritesModelAndEditorSettings() => Sta.Run(() =>
    {
        var host = new FakeHost();
        ScopeSettings.SetWarningsEnabled(true);
        BracketSettings.SetHighlightEnabled(true);
        using var page = new WorkspacePage(host);

        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.SplitView));
        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.ExplorerVisible));
        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.ScopeWarnings));
        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.BracketHighlight));

        Assert.True(host.Settings.SplitViewEnabled);
        Assert.False(host.Settings.ExplorerVisible);
        Assert.False(ScopeSettings.WarningsEnabled);
        Assert.False(BracketSettings.HighlightEnabled);
        ScopeSettings.SetWarningsEnabled(true);
        BracketSettings.SetHighlightEnabled(true);
    });

    [Fact]
    public void WorkspacePageFollowsExternalEditorSettingChanges() => Sta.Run(() =>
    {
        ScriptHintSettings.SetAutoCompleteEnabled(true);
        using var page = new WorkspacePage(new FakeHost());
        var box = UiFind.CheckBox(page, SettingsTexts.AutoComplete);
        Assert.True(box.IsChecked);

        ScriptHintSettings.SetAutoCompleteEnabled(false);

        Assert.False(box.IsChecked);
        ScriptHintSettings.SetAutoCompleteEnabled(true);
    });

    [Fact]
    public void WorkspacePageTogglesThePathBarAndFollowsChanges() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new WorkspacePage(host);
        var box = UiFind.CheckBox(page, SettingsTexts.PathBar);
        Assert.True(box.IsChecked);

        UiFind.Toggle(box);
        Assert.False(host.Settings.PathBarVisible);

        host.Settings.PathBarVisible = true;
        Assert.True(box.IsChecked);
    });

    [Fact]
    public void WorkspaceGuiSwitchGoesThroughTheHost() => Sta.Run(() =>
    {
        GuiSettings.SetToolsEnabled(true);
        var host = new FakeHost();
        using var page = new WorkspacePage(host);

        UiFind.Toggle(UiFind.CheckBox(page, GuiTexts.MenuTools));

        Assert.Equal(new[] { "gui:False" }, host.Calls);
        Assert.False(GuiSettings.ToolsEnabled);
        GuiSettings.SetToolsEnabled(true);
    });

    [Fact]
    public void NotesPanelPageWritesBothPanelSettings() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new NotesPanelPage(host);

        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.NotesPanelVisible));
        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.NotesPanelKeepOpen));

        Assert.False(host.Settings.NotesPanelVisible);
        Assert.True(host.Settings.NotesPanelAlwaysVisible);
    });

    [Fact]
    public void DisposedWorkspacePageUnsubscribesFromStaticEvents() => Sta.Run(() =>
    {
        ScopeSettings.SetHoverEnabled(true);
        var page = new WorkspacePage(new FakeHost());
        var box = UiFind.CheckBox(page, SettingsTexts.ScopeHover);
        page.Dispose();

        ScopeSettings.SetHoverEnabled(false);

        Assert.True(box.IsChecked);
        ScopeSettings.SetHoverEnabled(true);
    });
}
