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
        nameof(AppSessionState.CompatchResultRight),
        nameof(AppSessionState.CompatchMainAsBase),
        nameof(AppSessionState.CompatchFillMissing),
        nameof(AppSessionState.CompatchHideFilled),
        nameof(AppSessionState.CompatchHideReplaced),
        nameof(AppSessionState.CompatchHideDecided),
        nameof(AppSessionState.CompatchUncertainOnly),
        nameof(AppSessionState.CompatchKindsSideBySide),
        nameof(AppSessionState.ExplorerButtonsOnLeft),
        nameof(AppSessionState.ExplorerShowErrorMarks),
        nameof(AppSessionState.ExplorerSearchFoldersOnly),
        nameof(AppSessionState.ExplorerHideUgcReferences),
        nameof(AppSessionState.CompatchDefaultView),
        nameof(AppSessionState.UiLanguage),
        nameof(AppSessionState.SettingsWindowCategory),
        nameof(AppSessionState.SettingsWindowBounds),
        nameof(AppSessionState.ErrorLogStandalone),
        nameof(AppSessionState.ErrorLogWindowBounds),
        nameof(AppSessionState.SaveExplorerStandalone),
        nameof(AppSessionState.SaveExplorerWindowBounds),
        nameof(AppSessionState.MyModsView),
        nameof(AppSessionState.MyModsGrouping),
        nameof(AppSessionState.MyModsSort),
        nameof(AppSessionState.MyModsSortDescending),
        nameof(AppSessionState.MyModsOnlyPublished),
        nameof(AppSessionState.MyModsOnlyOpenBugs),
        nameof(AppSessionState.MyModsOnlyWarnings),
        nameof(AppSessionState.MyModsGameOrder),
        nameof(AppSessionState.MyModsHiddenGames),
        nameof(AppSessionState.MyModsAlertsCollapsed),
        nameof(AppSessionState.MyModsWindowBounds),
        nameof(AppSessionState.MyModsHiddenAlertKinds),
        nameof(AppSessionState.MyModsStartMaximized),
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
        CompatchResultRight = true,
        CompatchMainAsBase = true,
        CompatchFillMissing = true,
        CompatchHideFilled = true,
        CompatchHideReplaced = true,
        CompatchHideDecided = true,
        CompatchUncertainOnly = true,
        CompatchKindsSideBySide = true,
        ExplorerButtonsOnLeft = true,
        ExplorerShowErrorMarks = false,
        ExplorerSearchFoldersOnly = true,
        ExplorerHideUgcReferences = false,
        CompatchDefaultView = "C",
        UiLanguage = "en",
        SettingsWindowCategory = "Hotkeys",
        SettingsWindowBounds = new WindowBoundsState { Left = 10, Top = 20, Width = 700, Height = 500 },
        ErrorLogStandalone = true,
        ErrorLogWindowBounds = new WindowBoundsState { Left = 30, Top = 40, Width = 900, Height = 600, IsMaximized = true },
        SaveExplorerStandalone = true,
        SaveExplorerWindowBounds = new WindowBoundsState { Left = 50, Top = 60, Width = 1000, Height = 700 },
        MyModsView = "List",
        MyModsGrouping = "All",
        MyModsSort = "Subscribers",
        MyModsSortDescending = true,
        MyModsOnlyPublished = true,
        MyModsOnlyOpenBugs = true,
        MyModsOnlyWarnings = true,
        MyModsGameOrder = new List<string> { "ck3", "eu4" },
        MyModsHiddenGames = new List<string> { "eu5" },
        MyModsAlertsCollapsed = true,
        MyModsWindowBounds = new WindowBoundsState { Left = 1, Top = 2, Width = 3, Height = 4 },
        MyModsHiddenAlertKinds = new List<string> { "ReleaseStatus" },
        MyModsStartMaximized = false,
    };

    [Fact]
    public void MyModsStartsMaximizedByDefault()
    {
        Assert.True(new AppSessionState().MyModsStartMaximized);
        Assert.True(new AppSettings().MyModsStartMaximized);
    }

    [Fact]
    public void MyModsView_NormalizesUnknown()
    {
        var settings = new AppSettings { MyModsView = "Foo", MyModsGrouping = "Foo", MyModsSort = "Foo" };

        Assert.Equal("Tiles", settings.MyModsView);
        Assert.Equal("Games", settings.MyModsGrouping);
        Assert.Equal("Name", settings.MyModsSort);

        settings.MyModsView = "List";
        settings.MyModsSort = "LastRelease";
        Assert.Equal("List", settings.MyModsView);
        Assert.Equal("LastRelease", settings.MyModsSort);
    }

    [Fact]
    public void MyModsGameOrder_ChangedOnlyWhenContentDiffers()
    {
        var settings = new AppSettings();
        var names = new List<string>();
        settings.Changed += (_, name) => names.Add(name);

        settings.MyModsGameOrder = new[] { "eu4", "ck3" };
        settings.MyModsGameOrder = new List<string> { "eu4", "ck3" };

        Assert.Equal(new[] { nameof(AppSettings.MyModsGameOrder) }, names);
        Assert.Equal(new[] { "eu4", "ck3" }, settings.MyModsGameOrder);
    }

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
