using ParadoxModTool.GuiDesign.Blocks;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiBlockParserTests
{
    private static GuiBlock Parse(string text) => GuiBlockParser.Parse(text, GuiBlockLayer.Game, null);

    [Fact]
    public void Parse_ReadsAllHeaderLines()
    {
        var block = Parse(
            "# block: Knopf\n# group: Bedienelemente\n# description: Ein Knopf\n# param: name | Name | my_button\n" +
            "# param: mod | Modifier | x | modifier\n# repeat: rows | Zeilen | 2\n# param: rows.text | Text | ROW_$index$\n" +
            "# requires: a, b\n# requires: c\n\nbutton = { name = \"$name$\" }\n");

        Assert.Empty(block.Errors);
        Assert.True(block.IsValid);
        Assert.Equal("Knopf", block.Name);
        Assert.Equal("Bedienelemente", block.Group);
        Assert.Equal("Ein Knopf", block.Description);
        Assert.Equal(new[] { "name", "mod" }, block.Parameters.Select(p => p.Key));
        Assert.Equal("Name", block.Parameters[0].Label);
        Assert.Equal("my_button", block.Parameters[0].Default);
        Assert.Equal(GuiBlockParameterKind.Text, block.Parameters[0].Kind);
        Assert.Equal(GuiBlockParameterKind.Modifier, block.Parameters[1].Kind);
        var rows = Assert.Single(block.Repeats);
        Assert.Equal("rows", rows.Key);
        Assert.Equal("Zeilen", rows.Label);
        Assert.Equal(2, rows.DefaultCount);
        Assert.Equal("rows.text", Assert.Single(rows.Parameters).Key);
        Assert.Equal(new[] { "a", "b", "c" }, block.Requires);
        Assert.Equal(GuiBlockLayer.Game, block.Layer);
    }

    [Fact]
    public void Parse_GroupDefaultsToOwn()
    {
        var block = Parse("# block: A\nx\n");

        Assert.Equal("Eigene", GuiTexts.BlocksDefaultGroup);
        Assert.Equal(GuiTexts.BlocksDefaultGroup, block.Group);
        Assert.Equal(string.Empty, block.Description);
    }

    [Fact]
    public void Parse_HeaderEndsAtFirstOtherLine_CommentsStayInBody()
    {
        var block = Parse("# block: A\n# note: x\n# param: p | P | v\n");

        Assert.Empty(block.Parameters);
        var text = Assert.IsType<GuiBlockText>(Assert.Single(block.Body)).Text;
        Assert.StartsWith("# note: x\n", text);
        Assert.Contains("# param: p | P | v", text);
    }

    [Fact]
    public void Parse_NestedRepeats()
    {
        var block = Parse(
            "# block: T\n# repeat: tabs | Tabs | 3\n# param: tabs.id | I | tab_$index$\n# repeat: tabs.subtabs | S | 0\n" +
            "# param: tabs.subtabs.id | I | $tabs.id$_sub_$index$\nx\n");

        Assert.Empty(block.Errors);
        var tabs = Assert.Single(block.Repeats);
        Assert.Equal("tabs.id", Assert.Single(tabs.Parameters).Key);
        var sub = Assert.Single(tabs.Repeats);
        Assert.Equal("tabs.subtabs", sub.Key);
        Assert.Equal(0, sub.DefaultCount);
        Assert.Equal("tabs.subtabs.id", Assert.Single(sub.Parameters).Key);
        Assert.Same(sub, block.FindRepeat("tabs.subtabs"));
        Assert.Null(block.FindRepeat("nope"));
    }

    [Fact]
    public void Parse_BomAndCrlf_AreAcceptedAndLinesStayRight()
    {
        var block = Parse("\uFEFF# block: A\r\n\r\nx = $nope$\r\n");

        Assert.Equal("A", block.Name);
        var error = Assert.Single(block.Errors);
        Assert.Equal(GuiBlockErrorKind.UnknownPlaceholder, error.Kind);
        Assert.Equal(3, error.Line);
    }

    [Fact]
    public void Parse_DoubleDollarIsText()
    {
        var block = Parse("# block: A\na$$b");

        Assert.Empty(block.Errors);
        Assert.Equal("a$b", Assert.IsType<GuiBlockText>(Assert.Single(block.Body)).Text);
    }

    [Fact]
    public void Parse_MarkerOnlyLinesAreDropped()
    {
        var block = Parse("# block: A\n# repeat: r | R | 1\nx\n  $repeat r$\ny\n\t$end r$\nz\n");

        Assert.Empty(block.Errors);
        Assert.Equal(3, block.Body.Count);
        Assert.Equal("x\n", Assert.IsType<GuiBlockText>(block.Body[0]).Text);
        var section = Assert.IsType<GuiBlockSection>(block.Body[1]);
        Assert.Equal("r", section.RepeatKey);
        Assert.Equal("y\n", Assert.IsType<GuiBlockText>(Assert.Single(section.Parts)).Text);
        Assert.Equal("z\n", Assert.IsType<GuiBlockText>(block.Body[2]).Text);
    }

    [Fact]
    public void Parse_NameFallsBackToFileName()
    {
        var block = GuiBlockParser.Parse("x", GuiBlockLayer.Mod, @"C:\x\Mein Block.guiblock");

        Assert.Equal("Mein Block", block.Name);
        Assert.Equal(@"C:\x\Mein Block.guiblock", block.SourcePath);
        Assert.False(block.IsBuiltIn);
        Assert.Contains(block.Errors, e => e.Kind == GuiBlockErrorKind.MissingBlock);
        Assert.False(string.IsNullOrEmpty(block.Errors[0].Message));
    }

    [Theory]
    [InlineData("# group: G\nx", GuiBlockErrorKind.MissingBlock, 1)]
    [InlineData("# block: A\n# block: B\nx", GuiBlockErrorKind.DuplicateBlock, 2)]
    [InlineData("# group: G\n# block: A\nx", GuiBlockErrorKind.BlockNotFirst, 2)]
    [InlineData("# block: A\n# param: p | P\nx", GuiBlockErrorKind.InvalidHeader, 2)]
    [InlineData("# block: A\n# param: p | P | v | colour\nx", GuiBlockErrorKind.InvalidHeader, 2)]
    [InlineData("# block: A\n# repeat: r | R | -1\nx", GuiBlockErrorKind.InvalidHeader, 2)]
    [InlineData("# block: A\n# param: Bad | P | v\nx", GuiBlockErrorKind.InvalidKey, 2)]
    [InlineData("# block: A\n# param: index | P | v\nx", GuiBlockErrorKind.InvalidKey, 2)]
    [InlineData("# block: A\n# param: r.p | P | v\nx", GuiBlockErrorKind.UndeclaredParent, 2)]
    [InlineData("# block: A\n# param: p | P | v\n# param: p | Q | w\nx", GuiBlockErrorKind.DuplicateKey, 3)]
    [InlineData("# block: A\nx = $q$", GuiBlockErrorKind.UnknownPlaceholder, 2)]
    [InlineData("# block: A\n# param: p | P | $q$\nx", GuiBlockErrorKind.UnknownPlaceholder, 2)]
    [InlineData("# block: A\n# repeat: r | R | 1\n# param: r.p | P | v\nx = $r.p$", GuiBlockErrorKind.OutsideRepeat, 4)]
    [InlineData("# block: A\nx = $index$", GuiBlockErrorKind.OutsideRepeat, 2)]
    [InlineData("# block: A\n# repeat: r | R | 1\n# repeat: r.s | S | 1\n$repeat r.s$\n$end r.s$", GuiBlockErrorKind.OutsideRepeat, 4)]
    [InlineData("# block: A\n# repeat: r | R | 1\n$repeat r$\nx", GuiBlockErrorKind.MissingEnd, 3)]
    [InlineData("# block: A\n# repeat: r | R | 1\n# repeat: s | S | 1\n$repeat r$\n$end s$", GuiBlockErrorKind.MismatchedEnd, 5)]
    [InlineData("# block: A\nx = 5$", GuiBlockErrorKind.LoneDollar, 2)]
    public void Parse_ReportsErrorWithLine(string text, GuiBlockErrorKind kind, int line)
    {
        var block = Parse(text);

        Assert.Contains(block.Errors, e => e.Kind == kind && e.Line == line);
        Assert.False(block.IsValid);
    }
}
