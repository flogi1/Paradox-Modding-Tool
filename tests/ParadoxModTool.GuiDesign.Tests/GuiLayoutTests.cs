using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiLayoutTests
{
    private static readonly GuiLayout Flat = new(Array.Empty<string>(), FirstWins: true);

    [Fact]
    public void Profile_without_gui_layout_fields_is_eu5()
    {
        var profile = new GameProfile { Id = "x", Gui = new GuiProfileSettings { StructuredBlocks = { "state" } } };

        var layout = GuiRules.FromProfile(profile).Layout;

        Assert.Equal(GuiLayout.Default.Contexts, layout.Contexts);
        Assert.False(layout.FirstWins);
        Assert.False(layout.IsFlat);
    }

    [Fact]
    public void Ck3_profile_layout_is_flat_and_first_wins()
    {
        var rules = GuiRules.FromProfile(GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", "ck3.json")));

        Assert.True(rules.Layout.IsFlat);
        Assert.True(rules.Layout.FirstWins);
        Assert.Contains("attachto", rules.StructuredBlocks);
        Assert.Contains("glow_generation_rules", rules.StructuredBlocks);
        Assert.Contains("soundparam", rules.StructuredBlocks);
        Assert.Contains("tooltipwidget", rules.WidgetHolders);
        Assert.Equal(15, rules.WidgetHolders.Count);
    }

    [Fact]
    public void Flat_context_is_empty_and_folder_is_mod_gui()
    {
        Assert.Equal("", GuiContexts.Of("gui/x.gui", Flat));
        Assert.Equal("", GuiContexts.DefaultOf(Flat));
        Assert.Equal(@"C:\m\gui", GuiContexts.GuiFolder(@"C:\m", ""));
    }

    [Fact]
    public void Eu5_layout_keeps_contexts()
    {
        Assert.Equal("in_game", GuiContexts.Of("in_game/gui/x.gui", GuiLayout.Default));
        Assert.Null(GuiContexts.Of("gui/x.gui", GuiLayout.Default));
        Assert.Equal("in_game", GuiContexts.DefaultOf(GuiLayout.Default));
    }
}
