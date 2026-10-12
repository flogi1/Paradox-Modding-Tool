using ParadoxModTool.Session;
using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class ToolbarSettingsTests
{
    private static AppSettings Loaded(AppSessionState state)
    {
        var settings = new AppSettings();
        settings.Load(state, Array.Empty<string>());
        return settings;
    }

    [Fact]
    public void CatalogHasTwentyEightUniqueItemsInGroupOrder()
    {
        var ids = ToolbarCatalog.All.Select(i => i.Id).ToList();

        Assert.Equal(28, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
        var groups = ToolbarCatalog.All.Select(i => (int)i.Group).ToList();
        Assert.Equal(groups.OrderBy(g => g), groups);
        Assert.Equal(ToolbarItemIds.New, ids[0]);
        Assert.Equal(ToolbarItemIds.Move, ids[5]);
        Assert.Equal(ToolbarItemIds.Undo, ids[6]);
        Assert.Equal(ToolbarItemIds.LaunchGame, ids[^1]);
    }

    [Fact]
    public void DefaultOrderContainsEveryItemOnceAndFiveSeparators()
    {
        var order = ToolbarCatalog.DefaultOrder;

        Assert.Equal(ToolbarCatalog.All.Select(i => i.Id), order.Where(id => !ToolbarCatalog.IsSeparator(id)));
        Assert.Equal(new[] { "sep:1", "sep:2", "sep:3", "sep:4", "sep:5" }, order.Where(ToolbarCatalog.IsSeparator));
        Assert.Equal(ToolbarItemIds.Move, order[5]);
        Assert.Equal("sep:1", order[6]);
        Assert.Equal(ToolbarItemIds.Format, order[16]);
    }

    [Fact]
    public void TileToolsAreHiddenByDefaultInTheTilesLayout() =>
        Assert.Equal(
            new[] { ToolbarItemIds.ScopeFinder, ToolbarItemIds.GuiDataHelper, ToolbarItemIds.Templates, ToolbarItemIds.BugTracker },
            ToolbarCatalog.DefaultHiddenInTilesLayout);

    [Fact]
    public void SeparatorHelpers()
    {
        Assert.Equal("sep:7", ToolbarCatalog.Separator(7));
        Assert.True(ToolbarCatalog.IsSeparator("sep:7"));
        Assert.False(ToolbarCatalog.IsSeparator("separator"));
        Assert.False(ToolbarCatalog.IsSeparator("sep:"));
        Assert.False(ToolbarCatalog.IsSeparator("sep:0"));
        Assert.False(ToolbarCatalog.IsSeparator("sep:x"));
        Assert.False(ToolbarCatalog.IsSeparator("sep:-1"));
        Assert.False(ToolbarCatalog.IsKnown("sep:1"));
    }

    [Fact]
    public void FormatFollowsTemplatesAfterScopeFinder()
    {
        var tools = ToolbarCatalog.All.Where(i => i.Group == ToolbarGroup.Tools).Select(i => i.Id).ToList();

        Assert.Equal(
            new[] { ToolbarItemIds.Search, ToolbarItemIds.GoToLine, ToolbarItemIds.GoToBracket, ToolbarItemIds.ScopeFinder, ToolbarItemIds.GuiDataHelper, ToolbarItemIds.Templates, ToolbarItemIds.Format, ToolbarItemIds.Notes, ToolbarItemIds.Bookmark, ToolbarItemIds.Bug, ToolbarItemIds.BugTracker },
            tools);
    }

    [Fact]
    public void OldSessionWithHiddenHintButtonsHidesBothToggles()
    {
        var settings = Loaded(new AppSessionState { ScriptHintButtonsVisible = false, HiddenToolbarItems = null });

        Assert.False(settings.IsToolbarItemVisible(ToolbarItemIds.AutoComplete));
        Assert.False(settings.IsToolbarItemVisible(ToolbarItemIds.Hover));
        Assert.True(settings.IsToolbarItemVisible(ToolbarItemIds.Search));
    }

    [Fact]
    public void OldSessionWithVisibleHintButtonsHidesNothing()
    {
        var settings = Loaded(new AppSessionState { ScriptHintButtonsVisible = true, HiddenToolbarItems = null, WindowLayout = "Tiles" });

        Assert.Empty(settings.HiddenToolbarItems);
    }

    [Fact]
    public void StoredListWinsOverTheOldFlagAndDropsUnknownIds()
    {
        var settings = Loaded(new AppSessionState
        {
            ScriptHintButtonsVisible = false,
            HiddenToolbarItems = new List<string> { ToolbarItemIds.Bug, "gibtsnicht" },
            WindowLayout = "Tiles",
        });

        Assert.Equal(new[] { ToolbarItemIds.Bug }, settings.HiddenToolbarItems);
        Assert.True(settings.IsToolbarItemVisible(ToolbarItemIds.AutoComplete));
    }

    [Fact]
    public void MigrationHappensOnlyOnce()
    {
        var first = Loaded(new AppSessionState { ScriptHintButtonsVisible = false, HiddenToolbarItems = null, WindowLayout = "Tiles" });
        first.SetToolbarItemVisible(ToolbarItemIds.AutoComplete, true);
        first.SetToolbarItemVisible(ToolbarItemIds.Hover, true);
        var saved = new AppSessionState();
        first.WriteTo(saved);
        saved.ScriptHintButtonsVisible = false; // alter Schalter aus: darf die gespeicherte Liste nicht mehr überstimmen

        var second = Loaded(saved);

        Assert.NotNull(saved.HiddenToolbarItems);
        Assert.Empty(second.HiddenToolbarItems);
    }

    [Fact]
    public void WriteToStoresSortedListAndKeepsTheOldFlagMeaningful()
    {
        var settings = Loaded(new AppSessionState { HiddenToolbarItems = new List<string>(), WindowLayout = "Tiles" });
        settings.SetToolbarItemVisible(ToolbarItemIds.Hover, false);
        settings.SetToolbarItemVisible(ToolbarItemIds.Bug, false);
        var target = new AppSessionState();

        settings.WriteTo(target);

        Assert.Equal(new[] { ToolbarItemIds.Bug, ToolbarItemIds.Hover }, target.HiddenToolbarItems);
        Assert.False(target.ScriptHintButtonsVisible);
    }

    [Fact]
    public void ChangingVisibilityRaisesChangedOnlyOnRealChange()
    {
        var settings = new AppSettings();
        var names = new List<string>();
        settings.Changed += (_, name) => names.Add(name);

        settings.SetToolbarItemVisible(ToolbarItemIds.Format, false);
        settings.SetToolbarItemVisible(ToolbarItemIds.Format, false);
        settings.SetToolbarItemVisible(ToolbarItemIds.Format, true);

        Assert.Equal(new[] { nameof(AppSettings.HiddenToolbarItems), nameof(AppSettings.HiddenToolbarItems) }, names);
    }

    [Fact]
    public void UnknownIdIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new AppSettings().SetToolbarItemVisible("gibtsnicht", false));
    }
}
