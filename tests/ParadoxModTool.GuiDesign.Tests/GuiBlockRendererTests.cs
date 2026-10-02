using ParadoxModTool.GuiDesign.Blocks;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiBlockRendererTests
{
    private const string TabsText =
        "# block: T\n# param: name | N | w\n# repeat: tabs | Tabs | 3\n# param: tabs.id | I | tab_$index$\n" +
        "# repeat: tabs.subtabs | S | 0\n# param: tabs.subtabs.id | I | $tabs.id$_sub_$index$\n" +
        "$repeat tabs$\nb $name$ $tabs.id$\n$repeat tabs.subtabs$\n  s $tabs.subtabs.id$ $index$\n$end tabs.subtabs$\n$end tabs$\n" +
        "$repeat tabs$\nc $tabs.id$\n$end tabs$\n";

    private static readonly GuiBlock Tabs = Parse(TabsText);

    private static GuiBlock Parse(string text)
    {
        var block = GuiBlockParser.Parse(text, GuiBlockLayer.Game, null);
        Assert.Empty(block.Errors);
        return block;
    }

    [Fact]
    public void Defaults_ResolveIndexAndReferences()
    {
        Assert.Equal(
            "b w tab_1\nb w tab_2\nb w tab_3\nc tab_1\nc tab_2\nc tab_3\n",
            GuiBlockRenderer.Render(Tabs, GuiBlockValues.Defaults(Tabs)));
    }

    [Fact]
    public void CountZero_RendersNothing()
    {
        var values = GuiBlockValues.Defaults(Tabs);
        values.Resize(Tabs, "tabs", 0);

        Assert.Equal(string.Empty, GuiBlockRenderer.Render(Tabs, values));
    }

    [Fact]
    public void SubRepeat_IsPerEntry()
    {
        var values = GuiBlockValues.Defaults(Tabs);
        values.Repeats["tabs"][0].Resize(Tabs, "tabs.subtabs", 2);

        Assert.Equal(
            "b w tab_1\n  s tab_1_sub_1 1\n  s tab_1_sub_2 2\nb w tab_2\nb w tab_3\nc tab_1\nc tab_2\nc tab_3\n",
            GuiBlockRenderer.Render(Tabs, values));
    }

    [Fact]
    public void InlineMarkers_KeepTheLine()
    {
        var block = Parse("# block: A\n# repeat: r | R | 2\na $repeat r$x$end r$ b\n");

        Assert.Equal("a xx b\n", GuiBlockRenderer.Render(block, GuiBlockValues.Defaults(block)));
    }

    [Fact]
    public void DoubleDollar_RendersOneDollar()
    {
        var block = Parse("# block: A\na$$b\n");

        Assert.Equal("a$b\n", GuiBlockRenderer.Render(block, GuiBlockValues.Defaults(block)));
    }

    [Fact]
    public void Resize_KeepsExistingEntries()
    {
        var values = GuiBlockValues.Defaults(Tabs);
        values.Repeats["tabs"][0].Values["tabs.id"] = "x";
        values.Resize(Tabs, "tabs", 5);

        var tabs = values.Repeats["tabs"];
        Assert.Equal(5, tabs.Count);
        Assert.Equal("x", tabs[0].Values["tabs.id"]);
        Assert.Equal("tab_5", tabs[4].Values["tabs.id"]);
        Assert.Equal(5, tabs[4].Index);
        Assert.Same(values, tabs[4].Parent);
    }

    [Fact]
    public void Resize_ClampsToMaxCount()
    {
        var values = GuiBlockValues.Defaults(Tabs);
        values.Resize(Tabs, "tabs", 100000);
        Assert.Equal(GuiBlockValues.MaxCount, values.Repeats["tabs"].Count);
        Assert.Equal(50, GuiBlockValues.MaxCount);

        values.Resize(Tabs, "tabs", -3);
        Assert.Empty(values.Repeats["tabs"]);
    }

    [Fact]
    public void Resize_UsesCurrentOuterValues()
    {
        var values = GuiBlockValues.Defaults(Tabs);
        var first = values.Repeats["tabs"][0];
        first.Values["tabs.id"] = "home";
        first.Resize(Tabs, "tabs.subtabs", 1);

        Assert.Equal("home_sub_1", first.Repeats["tabs.subtabs"][0].Values["tabs.subtabs.id"]);
    }

    [Fact]
    public void Lookup_WalksUpToTheRoot()
    {
        var values = GuiBlockValues.Defaults(Tabs);
        var first = values.Repeats["tabs"][0];

        Assert.Equal("w", first.Lookup("name"));
        Assert.Equal("tab_1", first.Lookup("tabs.id"));
        Assert.Null(values.Lookup("tabs.id"));
    }

    [Fact]
    public void Render_InvalidBlock_Throws()
    {
        var block = GuiBlockParser.Parse("x = $q$", GuiBlockLayer.Game, null);

        Assert.Throws<ArgumentException>(() => GuiBlockRenderer.Render(block, GuiBlockValues.Defaults(block)));
    }
}
