using System.Windows.Controls;
using System.Windows.Media;
using ParadoxModTool.Settings;
using ParadoxModTool.SettingsUi.Pages;
using Xunit;

namespace ParadoxModTool.SettingsUi.Tests;

public sealed class LayoutAndThemesTests
{
    private static Button Swatch(SettingsPage page, string iconId) => UiFind.All<Button>(page.View).Single(button => Equals(button.Tag, iconId) && button.Content is Border);

    private static Button ResetOf(SettingsPage page, string iconId) =>
        UiFind.All<Button>(page.View).Single(button => UiFind.Label(button) == SettingsTexts.Reset && Equals(button.Tag, iconId));

    private static Color SwatchColor(Button swatch) => ((SolidColorBrush)((Border)swatch.Content).Background).Color;

    [Fact]
    public void PickedColorIsStoredForTheCurrentTheme() => Sta.Run(() =>
    {
        var host = new FakeHost { CurrentThemeId = "custom-1", NextPickedColor = Color.FromRgb(0x12, 0x34, 0x56) };
        using var page = new LayoutPage(host);

        UiFind.Click(Swatch(page, IconIds.Bug));

        Assert.Equal("#123456", host.Settings.GetIconColor("custom-1", IconIds.Bug));
        Assert.Equal(Color.FromRgb(0x12, 0x34, 0x56), SwatchColor(Swatch(page, IconIds.Bug)));
        Assert.True(ResetOf(page, IconIds.Bug).IsEnabled);
    });

    [Fact]
    public void CancelledPickChangesNothing() => Sta.Run(() =>
    {
        var host = new FakeHost { NextPickedColor = null };
        using var page = new LayoutPage(host);

        UiFind.Click(Swatch(page, IconIds.Note));

        Assert.Null(host.Settings.GetIconColor(host.CurrentThemeId, IconIds.Note));
        Assert.False(ResetOf(page, IconIds.Note).IsEnabled);
    });

    [Fact]
    public void ResetRemovesTheCustomColor() => Sta.Run(() =>
    {
        var host = new FakeHost();
        host.Settings.SetIconColor(host.CurrentThemeId, IconIds.Mod, "#FF0000");
        using var page = new LayoutPage(host);

        UiFind.Click(ResetOf(page, IconIds.Mod));

        Assert.Null(host.Settings.GetIconColor(host.CurrentThemeId, IconIds.Mod));
        Assert.Equal(Colors.Gray, SwatchColor(Swatch(page, IconIds.Mod)));
    });

    [Fact]
    public void HeadingNamesTheThemeAndFollowsThemeChanges() => Sta.Run(() =>
    {
        var host = new FakeHost { CurrentThemeName = "Hell" };
        using var page = new LayoutPage(host);
        Assert.Contains(SettingsTexts.IconColorsHeading("Hell"), UiFind.Texts(page));

        host.CurrentThemeName = "Mein_Theme";
        host.RaiseThemeChanged();

        Assert.Contains(SettingsTexts.IconColorsHeading("Mein_Theme"), UiFind.Texts(page));
    });

    [Fact]
    public void LayoutColorizeCheckboxWritesModel() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new LayoutPage(host);

        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.ColorizeWholeTab));

        Assert.True(host.Settings.ColorizeWholeTab);
    });

    [Fact]
    public void ThemesPageSelectsAndOpensEditor() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new ThemesPage(host);
        Assert.True(UiFind.Radio(page, "Wie Windows").IsChecked);

        UiFind.Choose(UiFind.Radio(page, "Dunkel"));
        UiFind.Click(UiFind.Button(page, SettingsTexts.ThemeEditor));

        Assert.Equal(new[] { "theme:builtin-dark", "themeEditor" }, host.Calls);
        Assert.True(UiFind.Radio(page, "Dunkel").IsChecked);
    });

    [Fact]
    public void ThemesPageRebuildsListOnThemeChange() => Sta.Run(() =>
    {
        var host = new FakeHost();
        host.Themes.Add(new ThemeChoice("custom-1", "Alt_Name"));
        using var page = new ThemesPage(host);

        host.Themes[3] = new ThemeChoice("custom-1", "Neuer Name");
        host.RaiseThemeChanged();

        var labels = UiFind.All<RadioButton>(page.View).Select(UiFind.Label).ToList();
        Assert.Contains("Neuer Name", labels);
        Assert.DoesNotContain("Alt_Name", labels);
    });

    [Fact]
    public void DisposedThemesPageUnsubscribesFromTheHost() => Sta.Run(() =>
    {
        var host = new FakeHost();
        var page = new ThemesPage(host);
        Assert.Equal(1, host.ThemeChangedSubscribers);

        page.Dispose();

        Assert.Equal(0, host.ThemeChangedSubscribers);
    });

    [Theory]
    [MemberData(nameof(Categories))]
    public void EveryCategoryBuildsAPageWithContent(SettingsCategory category) => Sta.Run(() =>
    {
        using var page = SettingsPages.Create(category, new FakeHost());

        Assert.NotNull(page.View);
        Assert.NotEmpty(UiFind.All<System.Windows.FrameworkElement>(page.View).Skip(2));
    });

    public static IEnumerable<object[]> Categories() => SettingsCategories.All.Select(category => new object[] { category });
}
