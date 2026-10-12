using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.Settings;
using ParadoxModTool.SettingsUi.Pages;
using Xunit;

namespace ParadoxModTool.SettingsUi.Tests;

public sealed class TilesPageTests
{
    [Fact]
    public void TilesPageListsTheCatalogOrderWithVisibilityAndIconOnly() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new TilesPage(host);
        var box = UiFind.All<ReorderListBox>(page.View).Single();
        Assert.Equal(TileCatalog.DefaultOrder, box.CurrentIds);

        var checks = UiFind.All<CheckBox>(box.Row(TileIds.Desync)).ToList();
        Assert.False(checks[0].IsChecked);          // Sichtbar: Standard aus
        UiFind.Toggle(checks[0]);
        Assert.True(host.Settings.IsTileVisible(TileIds.Desync));
        UiFind.Toggle(UiFind.All<CheckBox>(box.Row(TileIds.Compatch)).Last());
        Assert.True(host.Settings.IsTileIconOnly(TileIds.Compatch));
    });

    [Fact]
    public void TilesPageArrowsAndResetWriteTheOrder() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new TilesPage(host);
        var box = UiFind.All<ReorderListBox>(page.View).Single();
        UiFind.Click(box.DownButton(TileIds.Project));
        Assert.Equal(TileIds.CuTool, host.Settings.TileOrder[0]);
        Assert.Equal(TileIds.CuTool, box.CurrentIds[0]);
        host.Settings.SetTileIconOnly(TileIds.Gui, true);

        UiFind.Click(UiFind.Button(page, SettingsTexts.TileResetOrder));
        Assert.Equal(TileCatalog.DefaultOrder, host.Settings.TileOrder);
        Assert.Empty(host.Settings.IconOnlyTiles);
    });

    [Fact]
    public void TilesPageSizeRadiosAndSwitchesWriteTheModel() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new TilesPage(host);
        UiFind.Choose(UiFind.Radio(page, SettingsTexts.TileSizeCompact));
        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.TileLabelsVisible));
        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.TileBarVisible));
        Assert.Equal("Compact", host.Settings.TileSize);
        Assert.False(host.Settings.TileLabelsVisible);
        Assert.False(host.Settings.TileBarVisible);
    });

    [Fact]
    public void TilesPageMutesGuiTilesWhenGuiToolsAreOff() => Sta.Run(() =>
    {
        var host = new FakeHost();
        GuiSettings.SetToolsEnabled(false);
        try
        {
            using var page = new TilesPage(host);
            var box = UiFind.All<ReorderListBox>(page.View).Single();
            var label = UiFind.All<TextBlock>(box.Row(TileIds.Gui)).Single(t => t.Text == MenuTexts.TileLabel(TileIds.Gui));
            Assert.Equal(SettingsTexts.TileNeedsGuiTools, label.ToolTip);
            Assert.Equal(TileCatalog.DefaultOrder, box.CurrentIds);
            Assert.True(UiFind.All<CheckBox>(box.Row(TileIds.Gui)).First().IsEnabled);
        }
        finally
        {
            GuiSettings.SetToolsEnabled(true);
        }
    });

    [Fact]
    public void TilesPageShowsTheClassicHintOnlyInClassicLayout() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new TilesPage(host);
        var hint = UiFind.All<TextBlock>(page.View).Single(t => t.Text == SettingsTexts.TilesClassicHint);
        Assert.Equal(Visibility.Collapsed, hint.Visibility);
        host.Settings.WindowLayout = "Classic";
        Assert.Equal(Visibility.Visible, hint.Visibility);
    });

    [Fact]
    public void EveryTileRowHasAnIcon() => Sta.Run(() =>
    {
        using var page = new TilesPage(new FakeHost());
        var paths = UiFind.All<System.Windows.Shapes.Path>(page.View).ToList();
        Assert.Equal(TileCatalog.All.Count, paths.Count);
    });
}
