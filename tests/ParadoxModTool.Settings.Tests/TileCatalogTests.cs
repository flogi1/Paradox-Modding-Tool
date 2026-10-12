using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class TileCatalogTests
{
    [Fact]
    public void CatalogHasSixteenUniqueIdsInSpecOrder()
    {
        var ids = TileCatalog.All.Select(t => t.Id).ToList();

        Assert.Equal(16, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(TileIds.Project, ids[0]);
        Assert.Equal(TileIds.Templates, ids[12]);
        Assert.Equal(TileIds.Desync, ids[^1]);
        Assert.Equal(ids, TileCatalog.DefaultOrder);
    }

    [Fact]
    public void ThreeTilesAreHiddenByDefault() =>
        Assert.Equal(
            new[] { TileIds.GeneratorPackages, TileIds.PackageEditor, TileIds.Desync },
            TileCatalog.All.Where(t => !t.DefaultVisible).Select(t => t.Id));

    [Fact]
    public void MenusBadgesAndGuiFlagsFollowTheSpec()
    {
        Assert.Equal(new[] { TileIds.Project, TileIds.Gui }, TileCatalog.All.Where(t => t.HasMenu).Select(t => t.Id));
        Assert.Equal(TileBadge.ErrorLog, TileCatalog.Find(TileIds.ErrorLog)!.Badge);
        Assert.Equal(TileBadge.BugTracker, TileCatalog.Find(TileIds.BugTracker)!.Badge);
        Assert.Equal(new[] { TileIds.Gui, TileIds.GuiDataHelper }, TileCatalog.All.Where(t => t.RequiresGuiTools).Select(t => t.Id));
        Assert.True(TileCatalog.IsKnown("cuTool"));
        Assert.False(TileCatalog.IsKnown("cu"));
        Assert.Null(TileCatalog.Find("cu"));
    }
}
