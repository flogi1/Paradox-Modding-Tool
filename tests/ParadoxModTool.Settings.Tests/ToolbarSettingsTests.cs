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
    public void CatalogHasTwentyUniqueItemsInGroupOrder()
    {
        var ids = ToolbarCatalog.All.Select(i => i.Id).ToList();

        Assert.Equal(22, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
        var groups = ToolbarCatalog.All.Select(i => (int)i.Group).ToList();
        Assert.Equal(groups.OrderBy(g => g), groups);
        Assert.Equal(ToolbarItemIds.Undo, ids[0]);
        Assert.Equal(ToolbarItemIds.LaunchGame, ids[^1]);
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
        var settings = Loaded(new AppSessionState { ScriptHintButtonsVisible = true, HiddenToolbarItems = null });

        Assert.Empty(settings.HiddenToolbarItems);
    }

    [Fact]
    public void StoredListWinsOverTheOldFlagAndDropsUnknownIds()
    {
        var settings = Loaded(new AppSessionState
        {
            ScriptHintButtonsVisible = false,
            HiddenToolbarItems = new List<string> { ToolbarItemIds.Bug, "gibtsnicht" },
        });

        Assert.Equal(new[] { ToolbarItemIds.Bug }, settings.HiddenToolbarItems);
        Assert.True(settings.IsToolbarItemVisible(ToolbarItemIds.AutoComplete));
    }

    [Fact]
    public void MigrationHappensOnlyOnce()
    {
        var first = Loaded(new AppSessionState { ScriptHintButtonsVisible = false, HiddenToolbarItems = null });
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
        var settings = Loaded(new AppSessionState { HiddenToolbarItems = new List<string>() });
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
