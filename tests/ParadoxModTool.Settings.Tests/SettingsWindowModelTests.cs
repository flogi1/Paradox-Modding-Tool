using ParadoxModTool.Session;
using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class SettingsWindowModelTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unbekannt")]
    [InlineData("3")]
    [InlineData("hotkeys")]
    public void UnknownCategoryFallsBackToGeneral(string? stored)
    {
        Assert.Equal(SettingsCategory.General, SettingsCategories.Parse(stored));

        var settings = new AppSettings();
        settings.Load(new AppSessionState { SettingsWindowCategory = stored }, Array.Empty<string>());
        Assert.Equal("General", settings.SettingsWindowCategory);
    }

    [Fact]
    public void KnownCategoryIsKept()
    {
        Assert.Equal(SettingsCategory.Hotkeys, SettingsCategories.Parse("Hotkeys"));
        Assert.Equal(16, SettingsCategories.All.Count);
        Assert.Equal(SettingsCategory.Workspace, SettingsCategories.All[4]);
        Assert.Equal(SettingsCategory.SaveExplorer, SettingsCategories.All[SettingsCategories.All.ToList().IndexOf(SettingsCategory.ErrorLog) + 1]);
        Assert.Equal(SettingsCategory.BugTracker, SettingsCategories.All[^5]);
        Assert.Contains(SettingsCategory.ErrorLog, SettingsCategories.All);
        Assert.Equal(SettingsCategory.General, SettingsCategories.All[0]);
        Assert.Equal(SettingsCategory.Update, SettingsCategories.All[^1]);
    }

    [Theory]
    [InlineData("Toolbar")]
    [InlineData("WorkspaceToolbar")]
    public void OldToolbarCategoriesMapToToolbars(string stored) =>
        Assert.Equal(SettingsCategory.Toolbars, SettingsCategories.Parse(stored));

    [Fact]
    public void LayoutTilesAndToolbarsFollowGeneral()
    {
        Assert.Equal(SettingsCategory.Layout, SettingsCategories.All[1]);
        Assert.Equal(SettingsCategory.Tiles, SettingsCategories.All[2]);
        Assert.Equal(SettingsCategory.Toolbars, SettingsCategories.All[3]);
        Assert.Equal("Kachelleiste", SettingsTexts.CategoryLabel(SettingsCategory.Tiles));
        Assert.Equal("Werkzeugleisten", SettingsTexts.CategoryLabel(SettingsCategory.Toolbars));
    }

    [Theory]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData(" EN ", "en")]
    [InlineData("de", "de")]
    public void LanguageIsNormalized(string? stored, string expected)
    {
        var settings = new AppSettings();
        settings.Load(new AppSessionState { UiLanguage = stored! }, Array.Empty<string>());
        Assert.Equal(expected, settings.UiLanguage);

        settings.UiLanguage = stored!;
        Assert.Equal(expected, settings.UiLanguage);
    }

    [Fact]
    public void ANewSessionStartsInEnglish()
    {
        Assert.Equal("en", new AppSessionState().UiLanguage);
        Assert.Equal("en", new AppSettings().UiLanguage);
    }

    [Fact]
    public void ChangingLanguageAndCategoryRaisesChanged()
    {
        var settings = new AppSettings();
        var names = new List<string>();
        settings.Changed += (_, name) => names.Add(name);

        settings.UiLanguage = "de";
        settings.SettingsWindowCategory = "Themes";
        settings.SettingsWindowCategory = "Themes";

        Assert.Equal(new[] { nameof(AppSettings.UiLanguage), nameof(AppSettings.SettingsWindowCategory) }, names);
    }

    [Fact]
    public void BoundsAreCopiedInAndOut()
    {
        var settings = new AppSettings();
        var bounds = new WindowBoundsState { Left = 1, Top = 2, Width = 600, Height = 400 };

        settings.SettingsWindowBounds = bounds;
        bounds.Width = 9999;
        var read = settings.SettingsWindowBounds!;
        read.Height = 1;

        Assert.Equal(600, settings.SettingsWindowBounds!.Width);
        Assert.Equal(400, settings.SettingsWindowBounds!.Height);
    }

    [Fact]
    public void SettingEqualBoundsRaisesNoChange()
    {
        var settings = new AppSettings();
        settings.SettingsWindowBounds = new WindowBoundsState { Left = 1, Top = 2, Width = 600, Height = 400 };
        var changes = 0;
        settings.Changed += (_, _) => changes++;

        settings.SettingsWindowBounds = new WindowBoundsState { Left = 1, Top = 2, Width = 600, Height = 400 };

        Assert.Equal(0, changes);
    }
}
