using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class ToolAvailabilityTests
{
    private static ToolFacts Full(string game = "EU V") => new(
        GameName: game,
        HasSaveExplorer: true,
        PerformanceBlockedReason: null,
        HasFlagTable: true,
        HasScriptCheckRules: true,
        HasGfxAuditRules: true,
        HasDesyncRules: true,
        HasGuiProfile: true,
        GuiToolsEnabled: true,
        GuiClassic: false,
        GeneratorPackageCount: 3,
        HasRecipePresets: true,
        HasScriptDocsData: true);

    [Fact]
    public void Desync_OnlyWithRules()
    {
        Assert.True(ToolAvailability.For(TileIds.Desync, Full()).Available);

        var state = ToolAvailability.For(TileIds.Desync, Full("HOI IV") with { HasDesyncRules = false });

        Assert.False(state.Available);
        Assert.Equal(MenuTexts.NotSupported("HOI IV"), state.Reason);
        Assert.Null(state.Hint);
    }

    [Fact]
    public void Gui_BlockedWithoutGuiProfile()
    {
        var facts = Full("Stellaris") with { HasGuiProfile = false };

        foreach (var id in new[] { TileIds.Gui, TileIds.GuiDataHelper })
        {
            var state = ToolAvailability.For(id, facts);
            Assert.False(state.Available);
            Assert.Contains("Stellaris", state.Reason);
        }
    }

    [Fact]
    public void Performance_UsesBlockedReason()
    {
        var state = ToolAvailability.For(TileIds.Performance, Full() with { PerformanceBlockedReason = "kein Profiler" });

        Assert.False(state.Available);
        Assert.Equal("kein Profiler", state.Reason);
    }

    [Fact]
    public void SaveExplorer_Blocked()
    {
        var state = ToolAvailability.For(TileIds.SaveExplorer, Full("Victoria 3") with { HasSaveExplorer = false });

        Assert.False(state.Available);
        Assert.Equal(MenuTexts.NotSupported("Victoria 3"), state.Reason);
    }

    [Fact]
    public void GfxAuditGatedWithoutRules()
    {
        Assert.True(ToolAvailability.For(TileIds.GfxAudit, Full()).Available);

        var state = ToolAvailability.For(TileIds.GfxAudit, Full("Game") with { HasGfxAuditRules = false });

        Assert.False(state.Available);
        Assert.Equal(MenuTexts.NotSupported("Game"), state.Reason);
    }

    [Fact]
    public void ScriptCheck_GatedWithoutRules()
    {
        var state = ToolAvailability.For(TileIds.ScriptCheck, Full("Game") with { HasScriptCheckRules = false });

        Assert.False(state.Available);
        Assert.Equal(MenuTexts.NotSupported("Game"), state.Reason);
    }

    [Fact]
    public void ScriptCheck_HintWithoutDocs()
    {
        Assert.Null(ToolAvailability.For(TileIds.ScriptCheck, Full()).Hint);

        var state = ToolAvailability.For(TileIds.ScriptCheck, Full() with { HasScriptDocsData = false });

        Assert.True(state.Available);
        Assert.Equal(MenuTexts.HintNoScriptDocs, state.Hint);
    }

    [Fact]
    public void FlagTracker_AvailableWithTable()
    {
        Assert.True(ToolAvailability.For(TileIds.FlagTracker, Full()).Available);
        Assert.False(ToolAvailability.For(TileIds.FlagTracker, Full() with { HasFlagTable = false }).Available);
    }

    [Fact]
    public void Packages_HintWhenNone_StillAvailable()
    {
        foreach (var id in new[] { TileIds.GeneratorPackages, TileIds.PackageEditor })
        {
            var state = ToolAvailability.For(id, Full("CK III") with { GeneratorPackageCount = 0 });
            Assert.True(state.Available);
            Assert.Null(state.Reason);
            Assert.Equal(MenuTexts.HintNoPackages, state.Hint);
        }
    }

    [Fact]
    public void Recipes_HintWithoutPresets()
    {
        var state = ToolAvailability.For(TileIds.PatchRecipes, Full() with { HasRecipePresets = false });

        Assert.True(state.Available);
        Assert.Equal(MenuTexts.HintNoRecipePresets, state.Hint);
    }

    [Fact]
    public void ScriptDocs_HintWithoutData()
    {
        foreach (var id in new[] { TileIds.ScriptDocs, TileIds.ScopeFinder })
        {
            var state = ToolAvailability.For(id, Full() with { HasScriptDocsData = false });
            Assert.True(state.Available);
            Assert.Equal(MenuTexts.HintNoScriptDocs, state.Hint);
        }
    }

    [Fact]
    public void FullySupported_NoReasonNoHint()
    {
        foreach (var item in TileCatalog.All)
        {
            var state = ToolAvailability.For(item.Id, Full());
            Assert.True(state.Available, item.Id);
            Assert.Null(state.Reason);
            Assert.Null(state.Hint);
        }
    }

    [Fact]
    public void UnknownId_Available()
    {
        var state = ToolAvailability.For("doesNotExist", Full() with { HasGuiProfile = false, HasDesyncRules = false });

        Assert.Equal(new ToolState(true, null, null), state);
    }
}
