using System.Text.Json;
using ParadoxModTool.Session;
using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class AppSettingsTests
{
    /// <summary>Alle Felder von <see cref="AppSessionState"/>, die <see cref="AppSettings"/> liest und schreibt.</summary>
    private static readonly string[] MappedNames =
    {
        nameof(AppSessionState.SplitViewEnabled),
        nameof(AppSessionState.ResultsPanelOnTop),
        nameof(AppSessionState.ColorizeWholeTab),
        nameof(AppSessionState.MultiRowTabs),
        nameof(AppSessionState.NotesPanelVisible),
        nameof(AppSessionState.NotesPanelAlwaysVisible),
        nameof(AppSessionState.DocumentBarVisible),
        nameof(AppSessionState.CheckForUpdatesOnStartup),
        nameof(AppSessionState.CompatchCautiousWrite),
        nameof(AppSessionState.CompatchSkipLocked),
        nameof(AppSessionState.CompatchDefaultView),
        nameof(AppSessionState.UiLanguage),
        nameof(AppSessionState.SettingsWindowCategory),
        nameof(AppSessionState.SettingsWindowBounds),
    };

    private static AppSessionState NonDefaultState() => new()
    {
        SplitViewEnabled = true,
        ResultsPanelOnTop = true,
        ColorizeWholeTab = true,
        MultiRowTabs = true,
        NotesPanelVisible = false,
        NotesPanelAlwaysVisible = true,
        DocumentBarVisible = false,
        CheckForUpdatesOnStartup = false,
        CompatchCautiousWrite = true,
        CompatchSkipLocked = true,
        CompatchDefaultView = "C",
        UiLanguage = "en",
        SettingsWindowCategory = "Hotkeys",
        SettingsWindowBounds = new WindowBoundsState { Left = 10, Top = 20, Width = 700, Height = 500 },
    };

    private static string Json(AppSessionState state, string name) =>
        JsonSerializer.SerializeToNode(state)!.AsObject()[name]?.ToJsonString() ?? "null";

    [Fact]
    public void TestStateDiffersFromDefaultsInEveryMappedField()
    {
        var source = NonDefaultState();
        var defaults = new AppSessionState();

        foreach (var name in MappedNames)
        {
            Assert.NotEqual(Json(defaults, name), Json(source, name));
        }
    }

    [Fact]
    public void LoadThenWriteKeepsEveryMappedField()
    {
        var source = NonDefaultState();
        var settings = new AppSettings();
        settings.Load(source, Array.Empty<string>());

        var target = new AppSessionState();
        settings.WriteTo(target);

        foreach (var name in MappedNames)
        {
            Assert.Equal(Json(source, name), Json(target, name));
        }
    }

    [Fact]
    public void WriteToChangesOnlyItsOwnFields()
    {
        var target = new AppSessionState { GameExecutablePath = @"C:\Spiel\eu5.exe", ThemeId = "builtin-dark" };

        new AppSettings().WriteTo(target);

        Assert.Equal(@"C:\Spiel\eu5.exe", target.GameExecutablePath);
        Assert.Equal("builtin-dark", target.ThemeId);
    }

    [Fact]
    public void SettingAValueRaisesChangedOnceWithItsName()
    {
        var settings = new AppSettings();
        var names = new List<string>();
        settings.Changed += (_, name) => names.Add(name);

        settings.NotesPanelVisible = false;
        settings.NotesPanelVisible = false;
        settings.MultiRowTabs = true;

        Assert.Equal(new[] { nameof(AppSettings.NotesPanelVisible), nameof(AppSettings.MultiRowTabs) }, names);
    }

    [Fact]
    public void LoadRaisesOnlyReloaded()
    {
        var settings = new AppSettings();
        var changed = 0;
        var reloaded = 0;
        settings.Changed += (_, _) => changed++;
        settings.Reloaded += (_, _) => reloaded++;

        settings.Load(NonDefaultState(), Array.Empty<string>());

        Assert.Equal(0, changed);
        Assert.Equal(1, reloaded);
    }

    [Theory]
    [InlineData("B", "B")]
    [InlineData("c", "C")]
    [InlineData("X", "A")]
    [InlineData("", "A")]
    public void UnknownCompatchViewFallsBackToA(string stored, string expected)
    {
        var settings = new AppSettings();
        settings.Load(new AppSessionState { CompatchDefaultView = stored }, Array.Empty<string>());

        Assert.Equal(expected, settings.CompatchDefaultView);
    }

    [Fact]
    public void ExplorerIsVisibleByDefaultAndNotStored()
    {
        var settings = new AppSettings();
        Assert.True(settings.ExplorerVisible);

        settings.ExplorerVisible = false;
        settings.Load(new AppSessionState(), Array.Empty<string>());

        Assert.False(settings.ExplorerVisible);
    }
}
