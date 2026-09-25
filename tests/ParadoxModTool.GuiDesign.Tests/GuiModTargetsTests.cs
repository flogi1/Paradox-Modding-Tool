using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiModTargetsTests
{
    [Fact]
    public void ContextsFollowTheFirstFolder()
    {
        Assert.Equal("in_game", GuiContexts.Of("in_game/gui/x.gui"));
        Assert.Equal("main_menu", GuiContexts.Of(@"main_menu\gui\y.gui"));
        Assert.Null(GuiContexts.Of("gui/x.gui"));
        Assert.Equal(@"C:\mods\a\loading_screen\gui", GuiContexts.GuiFolder(@"C:\mods\a", "loading_screen"));
        Assert.True(GuiContexts.IsVanillaPath(@"C:\eu5", @"C:\eu5\jomini\in_game\gui\x.gui"));
        Assert.False(GuiContexts.IsVanillaPath(@"C:\eu5", @"C:\eu5-mods\game\x.gui"));
    }

    [Fact]
    public void DefaultNamesComeFromTheModFolder()
    {
        var targets = GuiModTargets.For(@"C:\mods\Better UI 2\");

        Assert.Equal("better_ui_2_overrides.gui", targets.OverrideFileName);
        Assert.Equal("better_ui_2_types.gui", targets.TypesFileName);
        Assert.Equal("BetterUi2Overrides", targets.OverrideContainer);
        Assert.Equal("BetterUi2Types", targets.TypesContainer);
        Assert.Equal(@"C:\mods\Better UI 2\in_game\gui\better_ui_2_overrides.gui", targets.OverridePath("in_game"));
        Assert.Equal("Mod1stOverrides", GuiModTargets.For(@"C:\mods\1st").OverrideContainer);
        Assert.Equal("mine.gui", GuiModTargets.For(@"C:\mods\x", "mine.gui").OverrideFileName);
    }

    [Theory]
    [InlineData("a.gui", true)]
    [InlineData(".gui", false)]
    [InlineData("a.txt", false)]
    [InlineData("sub/a.gui", false)]
    [InlineData(" a.gui", false)]
    public void FileNamesMustBePlainGuiFiles(string name, bool valid)
    {
        Assert.Equal(valid, GuiModTargets.IsValidFileName(name));
    }
}
