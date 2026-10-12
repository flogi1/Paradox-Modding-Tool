using ParadoxModTool.Session;
using Xunit;

namespace ParadoxModTool.Settings.Tests;

/// <summary>Interface-Rework 2: Fensterlayout, Kachelleiste und Reihenfolge der kleinen Werkzeugleiste in <see cref="AppSettings"/>.</summary>
public sealed class LayoutSettingsTests
{
    private static AppSettings Loaded(AppSessionState state, bool migrateTileTools = true)
    {
        var settings = new AppSettings(migrateTileTools);
        settings.Load(state, Array.Empty<string>());
        return settings;
    }

    [Fact]
    public void EmptySessionLoadsTheDefaultToolbarOrderWithSeparators()
    {
        var s = Loaded(new AppSessionState());

        Assert.Equal(ToolbarCatalog.DefaultOrder, s.ToolbarOrder);
        var state = new AppSessionState();
        s.WriteTo(state);
        Assert.Equal(ToolbarCatalog.DefaultOrder, state.ToolbarOrder);
    }

    [Fact]
    public void MigrationIsOffUntilTheTileBarExists()
    {
        var s = Loaded(new AppSessionState { HiddenToolbarItems = new List<string>(), WindowLayout = null }, migrateTileTools: false);

        Assert.Empty(s.HiddenToolbarItems);
    }

    [Fact]
    public void MigrationHappensOnceAndKeepsLaterChoices()
    {
        var first = Loaded(new AppSessionState { HiddenToolbarItems = new List<string>(), WindowLayout = null });
        Assert.False(first.IsToolbarItemVisible(ToolbarItemIds.ScopeFinder));
        first.SetToolbarItemVisible(ToolbarItemIds.ScopeFinder, true);
        var saved = new AppSessionState();
        first.WriteTo(saved);

        var second = Loaded(saved);

        Assert.Equal("Tiles", saved.WindowLayout);
        Assert.True(second.IsToolbarItemVisible(ToolbarItemIds.ScopeFinder));
        Assert.False(second.IsToolbarItemVisible(ToolbarItemIds.Templates));
    }

    [Fact]
    public void NullAndInvalidEntriesInStoredListsAreIgnored()
    {
        var s = Loaded(new AppSessionState
        {
            ToolbarOrder = new List<string> { null!, "sep:0", "sep:x", "sep:2", ToolbarItemIds.Undo },
            TileOrder = new List<string> { null!, TileIds.Gui },
            HiddenTiles = new List<string> { null!, TileIds.Gui },
            IconOnlyTiles = new List<string> { null! },
        });

        Assert.Equal(new[] { "sep:2", ToolbarItemIds.Undo }, s.ToolbarOrder.Take(2));
        Assert.DoesNotContain("sep:0", s.ToolbarOrder);
        Assert.DoesNotContain("sep:x", s.ToolbarOrder);
        Assert.Equal(TileIds.Gui, s.TileOrder[0]);
        Assert.Equal(new[] { TileIds.Gui }, s.HiddenTiles);
        Assert.Empty(s.IconOnlyTiles);
    }

    [Fact]
    public void DefaultsWithoutSession()
    {
        var s = new AppSettings();

        Assert.Equal("Tiles", s.WindowLayout);
        Assert.Equal("Top", s.ToolbarPosition);
        Assert.True(s.ToolbarLabelsVisible);
        Assert.Equal(ToolbarCatalog.DefaultOrder, s.ToolbarOrder);
        Assert.True(s.TileBarVisible);
        Assert.Equal("Large", s.TileSize);
        Assert.True(s.TileLabelsVisible);
        Assert.Equal(TileCatalog.DefaultOrder, s.TileOrder);
        Assert.Equal(
            new[] { TileIds.GeneratorPackages, TileIds.PackageEditor, TileIds.Desync },
            s.HiddenTiles.OrderBy(id => TileCatalog.DefaultOrder.ToList().IndexOf(id)));
        Assert.Empty(s.IconOnlyTiles);
        Assert.True(s.WorkspaceCategoryButtonVisible && s.WorkspaceSortButtonVisible && s.WorkspaceMarkButtonsVisible);
    }

    [Fact]
    public void StringSettingsNormalizeUnknownValues()
    {
        var s = new AppSettings { WindowLayout = "classic", ToolbarPosition = "foo", TileSize = "MEDIUM" };

        Assert.Equal("Classic", s.WindowLayout);
        Assert.Equal("Top", s.ToolbarPosition);
        Assert.Equal("Medium", s.TileSize);
    }

    [Fact]
    public void OldSessionWithoutLayoutHidesTheTileToolsInTheSmallToolbar()
    {
        var s = Loaded(new AppSessionState { HiddenToolbarItems = new List<string> { ToolbarItemIds.Undo }, WindowLayout = null });

        Assert.False(s.IsToolbarItemVisible(ToolbarItemIds.Undo));
        Assert.All(ToolbarCatalog.DefaultHiddenInTilesLayout, id => Assert.False(s.IsToolbarItemVisible(id)));
        Assert.True(s.IsToolbarItemVisible(ToolbarItemIds.Search));
    }

    [Fact]
    public void SessionWithLayoutKeepsTheStoredHiddenListAsIs()
    {
        var s = Loaded(new AppSessionState { HiddenToolbarItems = new List<string>(), WindowLayout = "Tiles" });
        Assert.Empty(s.HiddenToolbarItems);

        var classic = Loaded(new AppSessionState { HiddenToolbarItems = null, WindowLayout = "Classic" });
        Assert.Empty(classic.HiddenToolbarItems);
    }

    [Fact]
    public void ToolbarOrderIsCompletedAndCleanedOnLoad()
    {
        var s = Loaded(new AppSessionState { ToolbarOrder = new List<string> { "sep:1", "redo", "bogus", "redo", "sep:1", "new" } });

        Assert.Equal("sep:1", s.ToolbarOrder[0]);
        Assert.Equal(new[] { "redo", "new" }, s.ToolbarOrder.Skip(1).Take(2));
        Assert.Equal(ToolbarCatalog.All.Count, s.ToolbarOrder.Count(id => !ToolbarCatalog.IsSeparator(id)));
        Assert.Equal(1, s.ToolbarOrder.Count(id => id == "sep:1"));
        Assert.DoesNotContain("bogus", s.ToolbarOrder);
    }

    [Fact]
    public void TileOrderAndSetsRoundTripAndIgnoreUnknownIds()
    {
        var s = Loaded(new AppSessionState
        {
            TileOrder = new List<string> { TileIds.Gui, "nope" },
            HiddenTiles = new List<string> { TileIds.Gui, "nope" },
            IconOnlyTiles = new List<string> { TileIds.Templates },
        });

        Assert.Equal(TileIds.Gui, s.TileOrder[0]);
        Assert.Equal(TileCatalog.DefaultOrder.Count, s.TileOrder.Count);
        Assert.Equal(new[] { TileIds.Gui }, s.HiddenTiles);
        Assert.True(s.IsTileIconOnly(TileIds.Templates));

        var state = new AppSessionState();
        s.WriteTo(state);
        Assert.Equal(s.TileOrder, state.TileOrder);
        Assert.Equal(new[] { TileIds.Gui }, state.HiddenTiles);
        Assert.Equal(new[] { TileIds.Templates }, state.IconOnlyTiles);
        Assert.Equal("Tiles", state.WindowLayout);
    }

    [Fact]
    public void ChangedFiresOncePerRealChange()
    {
        var s = new AppSettings();
        var names = new List<string>();
        s.Changed += (_, n) => names.Add(n);

        s.SetTileVisible(TileIds.Gui, false);
        s.SetTileVisible(TileIds.Gui, false);
        s.SetTileIconOnly(TileIds.Gui, true);
        s.ToolbarOrder = ToolbarCatalog.DefaultOrder;
        s.ToolbarPosition = "Left";

        Assert.Equal(new[] { nameof(AppSettings.HiddenTiles), nameof(AppSettings.IconOnlyTiles), nameof(AppSettings.ToolbarPosition) }, names);
    }

    [Fact]
    public void ClassicLayoutStillShowsTheToolsHiddenByTheMigration()
    {
        var s = Loaded(new AppSessionState { HiddenToolbarItems = new List<string> { ToolbarItemIds.Undo }, WindowLayout = null });

        Assert.False(s.IsToolbarItemVisible(ToolbarItemIds.BugTracker));
        Assert.True(s.IsToolbarItemVisibleInClassic(ToolbarItemIds.BugTracker));
        Assert.False(s.IsToolbarItemVisibleInClassic(ToolbarItemIds.Undo));
        Assert.Equal(ToolbarCatalog.DefaultHiddenInTilesLayout.OrderBy(id => id, StringComparer.Ordinal), s.ToolbarItemsHiddenByMigration.OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public void MigrationRecordsOnlyWhatItHidAndAnExplicitChoiceWinsInBothLayouts()
    {
        var s = Loaded(new AppSessionState { HiddenToolbarItems = new List<string> { ToolbarItemIds.Templates }, WindowLayout = null });
        Assert.DoesNotContain(ToolbarItemIds.Templates, s.ToolbarItemsHiddenByMigration);
        Assert.False(s.IsToolbarItemVisibleInClassic(ToolbarItemIds.Templates));

        s.SetToolbarItemVisible(ToolbarItemIds.BugTracker, true);
        s.SetToolbarItemVisible(ToolbarItemIds.BugTracker, false);
        Assert.False(s.IsToolbarItemVisibleInClassic(ToolbarItemIds.BugTracker));
        Assert.True(s.IsToolbarItemVisibleInClassic(ToolbarItemIds.ScopeFinder));

        var saved = new AppSessionState();
        s.WriteTo(saved);
        var second = Loaded(saved);
        Assert.Equal(new[] { ToolbarItemIds.GuiDataHelper, ToolbarItemIds.ScopeFinder }, saved.ToolbarItemsHiddenByMigration);
        Assert.True(second.IsToolbarItemVisibleInClassic(ToolbarItemIds.ScopeFinder));
        Assert.False(second.IsToolbarItemVisibleInClassic(ToolbarItemIds.BugTracker));
    }
}
