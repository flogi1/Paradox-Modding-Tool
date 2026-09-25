using ParadoxModTool.Session;
using ParadoxModTool.Settings;
using ParadoxModTool.SettingsUi.Pages;
using Xunit;

namespace ParadoxModTool.SettingsUi.Tests;

public sealed class SettingsWindowTests
{
    [Fact]
    public void WindowOpensSavedCategory() => Sta.Run(() =>
    {
        var host = new FakeHost();
        host.Settings.SettingsWindowCategory = "Hotkeys";

        var window = new SettingsWindow(host);

        Assert.Equal(SettingsCategory.Hotkeys, window.CurrentCategory);
        Assert.IsType<HotkeysPage>(window.CurrentPage);
        window.Close();
    });

    [Fact]
    public void WindowWithoutSavedCategoryOpensGeneral() => Sta.Run(() =>
    {
        var window = new SettingsWindow(new FakeHost());

        Assert.Equal(SettingsCategory.General, window.CurrentCategory);
        Assert.IsType<GeneralPage>(window.CurrentPage);
        window.Close();
    });

    [Fact]
    public void SelectingDisposesOldPageAndStoresCategory() => Sta.Run(() =>
    {
        var host = new FakeHost();
        var window = new SettingsWindow(host);
        var first = window.CurrentPage!;

        window.Select(SettingsCategory.Themes);

        Assert.True(first.IsDisposed);
        Assert.IsType<ThemesPage>(window.CurrentPage);
        Assert.Equal("Themes", host.Settings.SettingsWindowCategory);
        window.Close();
    });

    [Fact]
    public void ListShowsAllTwelveCategories() => Sta.Run(() =>
    {
        var window = new SettingsWindow(new FakeHost());

        var labels = UiFind.All<System.Windows.Controls.ListBoxItem>(window).Select(item => ((System.Windows.Controls.TextBlock)item.Content).Text).ToList();

        Assert.Equal(SettingsCategories.All.Select(SettingsTexts.CategoryLabel), labels);
        window.Close();
    });

    [Fact]
    public void SaveBoundsWritesCurrentBoundsIntoTheModel() => Sta.Run(() =>
    {
        var host = new FakeHost();
        var window = new SettingsWindow(host) { Left = 50, Top = 60, Width = 800, Height = 600 };

        window.SaveBounds();

        var bounds = host.Settings.SettingsWindowBounds!;
        Assert.Equal((50d, 60d, 800d, 600d, false), (bounds.Left, bounds.Top, bounds.Width, bounds.Height, bounds.IsMaximized));
        window.Close();
    });

    [Fact]
    public void UsableSavedBoundsAreApplied() => Sta.Run(() =>
    {
        var host = new FakeHost();
        host.Settings.SettingsWindowBounds = new WindowBoundsState { Left = 40, Top = 30, Width = 820, Height = 610 };

        var window = new SettingsWindow(host);

        Assert.Equal((40d, 30d, 820d, 610d), (window.Left, window.Top, window.Width, window.Height));
        window.Close();
    });

    [Fact]
    public void ClosingDisposesThePage() => Sta.Run(() =>
    {
        var window = new SettingsWindow(new FakeHost());
        var page = window.CurrentPage!;

        window.Close();

        Assert.True(page.IsDisposed);
    });
}
