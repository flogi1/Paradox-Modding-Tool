using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiScaffoldTests
{
    [Theory]
    [InlineData("My Window", "my_window")]
    [InlineData("trade-screen.gui", "trade_screen")]
    [InlineData("  ", "new_window")]
    public void NameIsAWidgetName(string fileName, string expected)
    {
        Assert.Equal(expected, GuiScaffold.NameFor(fileName));
    }

    [Fact]
    public void WindowUsesTheVanillaPartsThatExist()
    {
        var all = GuiScaffold.Text(GuiScaffoldKind.Window, "my_window", _ => true, _ => true);

        Assert.Equal(
            "window = {\n" +
            "\tname = \"my_window\"\n" +
            "\tparentanchor = center\n" +
            "\twidgetanchor = center\n" +
            "\tsize = { 600 400 }\n" +
            "\tmovable = yes\n" +
            "\tusing = bg_window_default_alt\n" +
            "\n" +
            "\tvbox = {\n" +
            "\t\tusing = window_margin_alt\n" +
            "\n" +
            "\t\twindow_header_alt = {\n" +
            "\t\t\tblockoverride \"header_text\" {\n" +
            "\t\t\t\ttext = \"MY_WINDOW\"\n" +
            "\t\t\t}\n" +
            "\t\t}\n" +
            "\n" +
            "\t\twidget = {\n" +
            "\t\t\tlayoutpolicy_horizontal = expanding\n" +
            "\t\t\tlayoutpolicy_vertical = expanding\n" +
            "\t\t}\n" +
            "\t}\n" +
            "\n" +
            "\tbutton_close_alt = { }\n" +
            "}\n",
            all);
        Assert.Empty(GuiParser.Parse(all).Diagnostics);

        var bare = GuiScaffold.Text(GuiScaffoldKind.Window, "my_window", _ => false, _ => false);
        Assert.DoesNotContain("window_header_alt", bare);
        Assert.DoesNotContain("button_close_alt", bare);
        Assert.DoesNotContain("using", bare);
        Assert.Empty(GuiParser.Parse(bare).Diagnostics);
        Assert.Equal(string.Empty, GuiScaffold.Text(GuiScaffoldKind.Empty, "x", _ => true, _ => true));
    }

    [Fact]
    public void VanillaHasTheWindowParts()
    {
        if (!VanillaGui.Exists)
        {
            return;
        }

        var index = GuiRulesVanillaTests.Vanilla;

        Assert.NotNull(index.Find(GuiScaffold.HeaderType, GuiDefinitionKind.Type));
        Assert.NotNull(index.Find(GuiScaffold.CloseType, GuiDefinitionKind.Type));
        Assert.NotNull(index.Find(GuiScaffold.BackgroundTemplate, GuiDefinitionKind.Template));
        Assert.NotNull(index.Find(GuiScaffold.MarginTemplate, GuiDefinitionKind.Template));
    }
}
