using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class TileCatalogTests
{
    [Fact]
    public void ScriptCheckTile_HasStableIdAndIsOffByDefault()
    {
        Assert.Equal("scriptCheck", TileIds.ScriptCheck);
        Assert.False(TileCatalog.All.Single(i => i.Id == TileIds.ScriptCheck).DefaultVisible);
    }

    [Fact]
    public void GfxAuditTileDefaultOff()
    {
        Assert.Equal("gfxAudit", TileIds.GfxAudit);
        Assert.False(TileCatalog.All.Single(i => i.Id == TileIds.GfxAudit).DefaultVisible);
    }

    [Fact]
    public void CatalogHasTwentyTwoUniqueIdsInSpecOrder()
    {
        var ids = TileCatalog.All.Select(t => t.Id).ToList();

        Assert.Equal(22, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(TileIds.Project, ids[0]);
        Assert.Equal(TileIds.Mods, ids[1]);
        Assert.Equal(TileIds.Playsets, ids[5]);
        Assert.Equal(TileIds.Templates, ids[14]);
        Assert.Equal(TileIds.Performance, ids[15]);
        Assert.Equal(TileIds.FlagTracker, ids[16]);
        Assert.Equal(TileIds.Desync, ids[^1]);
        Assert.Equal(ids, TileCatalog.DefaultOrder);
    }

    [Fact]
    public void TileCatalog_ContainsMods_DefaultVisible()
    {
        var mods = TileCatalog.Find(TileIds.Mods)!;
        Assert.Equal("mods", mods.Id);
        Assert.True(mods.DefaultVisible);
        Assert.False(mods.HasMenu);
        Assert.Equal(TileBadge.None, mods.Badge);
        Assert.False(mods.RequiresGuiTools);
    }

    [Fact]
    public void SixTilesAreHiddenByDefault() =>
        Assert.Equal(
            new[] { TileIds.FlagTracker, TileIds.ScriptCheck, TileIds.GfxAudit, TileIds.GeneratorPackages, TileIds.PackageEditor, TileIds.Desync },
            TileCatalog.All.Where(t => !t.DefaultVisible).Select(t => t.Id));

    [Fact]
    public void MenusBadgesAndGuiFlagsFollowTheSpec()
    {
        Assert.Equal(new[] { TileIds.Gui }, TileCatalog.All.Where(t => t.HasMenu).Select(t => t.Id));
        Assert.Equal(TileBadge.ErrorLog, TileCatalog.Find(TileIds.ErrorLog)!.Badge);
        Assert.Equal(TileBadge.BugTracker, TileCatalog.Find(TileIds.BugTracker)!.Badge);
        Assert.Equal(new[] { TileIds.Gui, TileIds.GuiDataHelper }, TileCatalog.All.Where(t => t.RequiresGuiTools).Select(t => t.Id));
        Assert.True(TileCatalog.IsKnown("cuTool"));
        Assert.False(TileCatalog.IsKnown("cu"));
        Assert.Null(TileCatalog.Find("cu"));
    }
}
