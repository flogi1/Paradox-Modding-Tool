using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class ClassicProfileTests
{
    private static GameProfile Load(string id) =>
        GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", id + ".json"));

    private static GuiLayout LayoutOf(string id) => GuiRules.FromProfile(Load(id)).Layout;

    [Fact]
    public void Hoi4_profile_is_classic_last_wins()
    {
        var layout = LayoutOf("hoi4");

        Assert.Equal(GuiDialect.Classic, layout.Dialect);
        Assert.True(layout.IsClassic);
        Assert.False(layout.FirstWins);
        Assert.False(layout.ModsBeforeVanilla);
        Assert.False(layout.SpriteFirstWins);
        Assert.True(layout.CaseSensitive);
        Assert.Empty(layout.Contexts);
        Assert.Equal(new[] { "", "integrated_dlc/*", "dlc/*" }, layout.VanillaRoots);
    }

    [Fact]
    public void Eu4_profile_is_classic_windows_first_wins()
    {
        var layout = LayoutOf("eu4");

        Assert.Equal(GuiDialect.Classic, layout.Dialect);
        Assert.True(layout.FirstWins);
        Assert.False(layout.ModsBeforeVanilla);
        Assert.False(layout.SpriteFirstWins);
        Assert.False(layout.CaseSensitive);
        Assert.Equal(new[] { "", "builtin_dlc/*", "dlc/*" }, layout.VanillaRoots);
    }

    [Fact]
    public void Classic_structured_blocks_ignore_case()
    {
        var rules = GuiRules.FromProfile(Load("hoi4"));

        Assert.Equal(GuiDialect.Classic, rules.Dialect);
        Assert.Contains("Position", rules.StructuredBlocks);
        Assert.Contains("BorderSize", rules.StructuredBlocks);
    }

    [Theory]
    [InlineData("eu5", false, true, 3)]
    [InlineData("ck3", true, true, 0)]
    public void Eu5_and_ck3_stay_jomini(string id, bool firstWins, bool modsBeforeVanilla, int contexts)
    {
        var rules = GuiRules.FromProfile(Load(id));
        var layout = rules.Layout;

        Assert.Equal(GuiDialect.Jomini, rules.Dialect);
        Assert.False(layout.IsClassic);
        Assert.Equal(firstWins, layout.FirstWins);
        Assert.Equal(modsBeforeVanilla, layout.ModsBeforeVanilla);
        Assert.Equal(contexts, layout.Contexts.Count);
        Assert.False(layout.SpriteFirstWins);
        Assert.False(layout.CaseSensitive);
        Assert.Null(layout.VanillaRoots);
        Assert.DoesNotContain("Modify_Texture", rules.StructuredBlocks);
    }

    [Fact]
    public void Stellaris_has_no_gui_section()
    {
        Assert.Null(Load("stellaris").Gui);
    }
}
