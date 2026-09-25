using ParadoxModTool.Session;
using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class IconColorSettingsTests
{
    private static readonly string[] Themes = { "builtin-light", "builtin-dark", "custom-1" };

    private static AppSettings Loaded(AppSessionState state)
    {
        var settings = new AppSettings();
        settings.Load(state, Themes);
        return settings;
    }

    [Fact]
    public void OldNoteColorIsCopiedIntoEveryKnownTheme()
    {
        var settings = Loaded(new AppSessionState { NoteIconColorHex = "#112233" });

        foreach (var theme in Themes)
        {
            Assert.Equal("#112233", settings.GetIconColor(theme, IconIds.Note));
        }

        Assert.Null(settings.GetIconColor("builtin-light", IconIds.Bookmark));
    }

    [Fact]
    public void ResetInOneThemeGivesTheDefaultAndKeepsTheOthers()
    {
        var settings = Loaded(new AppSessionState { NoteIconColorHex = "#112233" });

        settings.SetIconColor("builtin-dark", IconIds.Note, null);

        Assert.Null(settings.GetIconColor("builtin-dark", IconIds.Note));
        Assert.Equal("#112233", settings.GetIconColor("builtin-light", IconIds.Note));
    }

    [Fact]
    public void MigrationHappensOnlyOnceEvenWhenNothingIsLeft()
    {
        var first = Loaded(new AppSessionState { NoteIconColorHex = "#112233" });
        foreach (var theme in Themes)
        {
            first.SetIconColor(theme, IconIds.Note, null);
        }

        var saved = new AppSessionState { NoteIconColorHex = "#112233" };
        first.WriteTo(saved);
        var second = Loaded(saved);

        Assert.NotNull(saved.IconColorsByTheme);
        Assert.Null(second.GetIconColor("builtin-light", IconIds.Note));
    }

    [Fact]
    public void WithoutOldNoteColorNothingIsMigrated()
    {
        var settings = Loaded(new AppSessionState());

        Assert.Null(settings.GetIconColor("builtin-light", IconIds.Note));
    }

    [Fact]
    public void BrokenStoredValuesAreDropped()
    {
        var settings = Loaded(new AppSessionState
        {
            NoteIconColorHex = "kaputt",
            IconColorsByTheme = new Dictionary<string, Dictionary<string, string>>
            {
                ["builtin-light"] = new() { ["Bug"] = "#AABBCC", ["Bookmark"] = "rot", ["Unbekannt"] = "#000000" },
            },
        });

        Assert.Equal("#AABBCC", settings.GetIconColor("builtin-light", IconIds.Bug));
        Assert.Null(settings.GetIconColor("builtin-light", IconIds.Bookmark));
    }

    [Fact]
    public void BrokenOldNoteColorIsNotMigrated()
    {
        var settings = Loaded(new AppSessionState { NoteIconColorHex = "kaputt" });

        Assert.Null(settings.GetIconColor("builtin-light", IconIds.Note));
    }

    [Fact]
    public void SetRaisesChangedAndWriteToStoresAllThemes()
    {
        var settings = Loaded(new AppSessionState { IconColorsByTheme = new() });
        var names = new List<string>();
        settings.Changed += (_, name) => names.Add(name);

        settings.SetIconColor("custom-1", IconIds.Mod, "#80FF0000");
        settings.SetIconColor("custom-1", IconIds.Mod, "#80FF0000");
        var target = new AppSessionState();
        settings.WriteTo(target);

        Assert.Equal(new[] { nameof(AppSettings.IconColorsByTheme) }, names);
        Assert.Equal("#80FF0000", target.IconColorsByTheme!["custom-1"]["Mod"]);
    }

    [Theory]
    [InlineData("Unbekannt", "#000000")]
    [InlineData("Note", "rot")]
    [InlineData("Note", "#12345")]
    public void InvalidSetIsRejected(string icon, string hex)
    {
        Assert.Throws<ArgumentException>(() => new AppSettings().SetIconColor("builtin-light", icon, hex));
    }
}
