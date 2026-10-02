using ParadoxModTool.GuiDesign.Blocks;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiBlockDraftTests
{
    [Fact]
    public void FromCode_DedentsAndEscapes()
    {
        Assert.Equal(
            "window = {\n\tname = \"a$$b\"\n\n}\n",
            GuiBlockDraft.FromCode("\t\twindow = {\r\n\t\t\tname = \"a$b\"\r\n\r\n\t\t}\r\n"));
    }

    [Fact]
    public void KeysIn_FindsParametersRepeatsAndParents()
    {
        var keys = GuiBlockDraft.KeysIn(
            "$name$\n$repeat tabs$\n$tabs.id$ $index$\n$repeat tabs.sub$\n$tabs.sub.x$\n$end tabs.sub$\n$end tabs$\n$$ $name$");

        Assert.Equal(
            new[]
            {
                new GuiBlockDraftKey("name", false),
                new GuiBlockDraftKey("tabs", true),
                new GuiBlockDraftKey("tabs.id", false),
                new GuiBlockDraftKey("tabs.sub", true),
                new GuiBlockDraftKey("tabs.sub.x", false),
            },
            keys);
    }

    [Fact]
    public void KeysIn_AddsImpliedParentRepeat()
    {
        Assert.Equal(
            new[] { new GuiBlockDraftKey("rows", true), new GuiBlockDraftKey("rows.text", false) },
            GuiBlockDraft.KeysIn("$rows.text$"));
    }

    [Fact]
    public void KeysIn_IgnoresBrokenSpots()
    {
        Assert.Equal(new[] { new GuiBlockDraftKey("ok", false) }, GuiBlockDraft.KeysIn("$Bad$ $ok$ $"));
        Assert.Equal(new[] { new GuiBlockDraftKey("ok", false) }, GuiBlockDraft.KeysIn("$ok$ x = 5$"));
    }

    [Fact]
    public void ComposedDraft_RoundTrips()
    {
        var body = GuiBlockDraft.FromCode("\t\twindow = {\r\n\t\t\tname = \"x\"\r\n\t\t}\r\n").Replace("\"x\"", "\"$name$\"")
            + "$repeat tabs$\nt = $tabs.id$\n$end tabs$\n";
        var text = GuiBlockDraft.Compose(
            "Mein | Block",
            string.Empty,
            "Zeile1\nZeile2",
            new[]
            {
                new GuiBlockDraftParameter("name", "Name|x", "my_window", GuiBlockParameterKind.Text),
                new GuiBlockDraftParameter("tabs.id", string.Empty, "tab_$index$", GuiBlockParameterKind.Text),
            },
            new[] { new GuiBlockDraftRepeat("tabs", "Tabs", 2) },
            body);

        var block = GuiBlockParser.Parse(text, GuiBlockLayer.Game, null);

        Assert.Empty(block.Errors);
        Assert.Equal("Mein / Block", block.Name);
        Assert.Equal("Eigene", block.Group);
        Assert.Equal("Zeile1 Zeile2", block.Description);
        Assert.Equal("Name/x", block.Parameters[0].Label);
        Assert.Equal("tabs.id", block.FindRepeat("tabs")!.Parameters[0].Label);
        Assert.Equal(
            "window = {\n\tname = \"my_window\"\n}\nt = tab_1\nt = tab_2\n",
            GuiBlockRenderer.Render(block, GuiBlockValues.Defaults(block)));
    }

    [Fact]
    public void Compose_OrdersRepeatsBeforeTheirParameters()
    {
        var text = GuiBlockDraft.Compose(
            "B",
            "G",
            string.Empty,
            new[]
            {
                new GuiBlockDraftParameter("tabs.sub.x", "X", "x", GuiBlockParameterKind.Modifier),
                new GuiBlockDraftParameter("name", "N", "n", GuiBlockParameterKind.Text),
                new GuiBlockDraftParameter("tabs.id", "I", "i", GuiBlockParameterKind.Text),
            },
            new[] { new GuiBlockDraftRepeat("tabs.sub", "S", 1), new GuiBlockDraftRepeat("tabs", "T", 2) },
            "x\n");

        var header = text.Split('\n').TakeWhile(l => l.StartsWith('#')).ToArray();

        Assert.Equal(
            new[]
            {
                "# block: B",
                "# group: G",
                "# param: name | N | n",
                "# repeat: tabs | T | 2",
                "# param: tabs.id | I | i",
                "# repeat: tabs.sub | S | 1",
                "# param: tabs.sub.x | X | x | modifier",
            },
            header);
        Assert.Empty(GuiBlockParser.Parse(text, GuiBlockLayer.Game, null).Errors);
    }
}
