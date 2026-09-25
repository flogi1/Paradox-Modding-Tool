using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiParserTests
{
    [Fact]
    public void ParsesTypesContainerAndTypeDefinitionWithBase()
    {
        var doc = GuiParser.Parse("types Buttons {\n\ttype my_button = button_standard {\n\t\tsize = { 10 20 }\n\t}\n}\n");

        var types = Assert.Single(doc.Root.Children);
        Assert.Equal(GuiNodeKind.TypesContainer, types.Kind);
        Assert.Equal("Buttons", types.Name);
        var type = Assert.Single(types.Children);
        Assert.Equal(GuiNodeKind.TypeDefinition, type.Kind);
        Assert.Equal("my_button", type.Name);
        Assert.Equal("button_standard", type.BaseName);
        var size = Assert.Single(type.Children);
        Assert.Equal(GuiNodeKind.KeyedBlock, size.Kind);
        Assert.Equal("size", size.Name);
        Assert.Equal(new[] { "10", "20" }, size.Children.Select(c => c.Value));
        Assert.Empty(doc.Diagnostics);
        SpanChecks.AssertNested(doc, "types");
        SpanChecks.AssertGapsAreWhitespace(doc, "types");
    }

    [Fact]
    public void TypesKeywordIgnoresCase()
    {
        // 5 Vanilla-Dateien schreiben "Types X {" (z.B. in_game/gui/bureaucracies_lateralview.gui); das Spiel liest sie als types-Container.
        var doc = GuiParser.Parse("Types BureaucracyTypes {\n\ttype a = icon { }\n}");

        var types = Assert.Single(doc.Root.Children);
        Assert.Equal(GuiNodeKind.TypesContainer, types.Kind);
        Assert.Equal("BureaucracyTypes", types.Name);
        Assert.Equal(GuiNodeKind.TypeDefinition, Assert.Single(types.Children).Kind);
    }

    [Fact]
    public void ParsesTemplateBlockBlockOverrideUsingAndConstant()
    {
        var doc = GuiParser.Parse(
            "@width = 300\n" +
            "template my_template {\n\tusing = other_template\n\tblock \"content\" {\n\t\ttext = \"x\"\n\t}\n}\n" +
            "widget = {\n\tblockoverride \"content\" {\n\t\ttext = \"y\"\n\t}\n}\n");

        var constant = doc.Root.Children[0];
        Assert.Equal(GuiNodeKind.ConstantDefinition, constant.Kind);
        Assert.Equal("width", constant.Name);
        Assert.Equal("300", constant.Value);

        var template = doc.Root.Children[1];
        Assert.Equal(GuiNodeKind.TemplateDefinition, template.Kind);
        Assert.Equal("my_template", template.Name);
        Assert.Equal(GuiNodeKind.Using, template.Children[0].Kind);
        Assert.Equal("using", template.Children[0].Name);
        Assert.Equal("other_template", template.Children[0].Value);
        var block = template.Children[1];
        Assert.Equal(GuiNodeKind.Block, block.Kind);
        Assert.Equal("content", block.Name);
        Assert.True(block.IsQuoted);

        var widget = doc.Root.Children[2];
        Assert.Equal(GuiNodeKind.KeyedBlock, widget.Kind);
        var blockOverride = Assert.Single(widget.Children);
        Assert.Equal(GuiNodeKind.BlockOverride, blockOverride.Kind);
        Assert.Equal("content", blockOverride.Name);
        Assert.Equal("y", Assert.Single(blockOverride.Children).Value);
        SpanChecks.AssertNested(doc, "template");
        SpanChecks.AssertGapsAreWhitespace(doc, "template");
    }

    [Fact]
    public void PropertySpansCoverKeyOperatorAndValue()
    {
        const string text = "icon = {\n\ttexture = \"gfx/a.dds\" # Kommentar\n\talpha < 0.5\n}";
        var doc = GuiParser.Parse(text);
        var icon = doc.Root.Children[0];

        var texture = icon.Children[0];
        Assert.Equal(GuiNodeKind.Property, texture.Kind);
        Assert.Equal("texture = \"gfx/a.dds\"", doc.TextOf(texture));
        Assert.Equal("\"gfx/a.dds\"", doc.TextOf(texture.ValueSpan!.Value));
        Assert.Equal("gfx/a.dds", texture.Value);
        Assert.True(texture.IsQuoted);
        Assert.Equal(GuiNodeKind.Comment, icon.Children[1].Kind);
        Assert.Equal("# Kommentar", icon.Children[1].Name);
        Assert.Equal("<", icon.Children[2].Operator);
        Assert.Equal(text, doc.TextOf(icon));
    }

    [Fact]
    public void InlineMathStaysOneListItem()
    {
        var doc = GuiParser.Parse("size = { @[w - m * 2] 20 }");

        Assert.Equal(new[] { "@[w - m * 2]", "20" }, doc.Root.Children[0].Children.Select(c => c.Value));
        Assert.All(doc.Root.Children[0].Children, c => Assert.Equal(GuiNodeKind.ListItem, c.Kind));
    }

    [Fact]
    public void AtSignInsideStringIsNotAConstant()
    {
        var doc = GuiParser.Parse("text = \"@gold! [Amount]\"");

        Assert.Equal(GuiNodeKind.Property, doc.Root.Children[0].Kind);
    }

    [Fact]
    public void AnonymousBlockIsKeyedBlockWithEmptyName()
    {
        var doc = GuiParser.Parse("list = { { a = 1 } { a = 2 } }");

        var list = doc.Root.Children[0];
        Assert.Equal(2, list.Children.Count);
        Assert.All(list.Children, c => Assert.Equal(string.Empty, c.Name));
        Assert.All(list.Children, c => Assert.Equal(GuiNodeKind.KeyedBlock, c.Kind));
    }

    [Fact]
    public void ExtraClosingBraceIsReportedAndSkipped()
    {
        var doc = GuiParser.Parse("a = { b = 1 }\n}\nc = { d = 2 }");

        Assert.Equal(new[] { "a", "c" }, doc.Root.Children.Select(c => c.Name));
        var diagnostic = Assert.Single(doc.Diagnostics);
        Assert.Equal(GuiDiagnosticKind.UnexpectedCloseBrace, diagnostic.Kind);
        Assert.Equal(2, diagnostic.Line);
        SpanChecks.AssertGapsAreWhitespace(doc, "stray");
    }

    [Fact]
    public void MissingClosingBraceEndsNodeAtEndOfText()
    {
        const string text = "a = {\n\tb = {\n\t\tc = 1\n";
        var doc = GuiParser.Parse(text);

        var a = doc.Root.Children[0];
        Assert.Equal(-1, a.CloseBrace);
        Assert.Equal(text.Length, a.Span.End);
        Assert.Equal(2, doc.Diagnostics.Count(d => d.Kind == GuiDiagnosticKind.MissingCloseBrace));
        SpanChecks.AssertNested(doc, "missing");
    }

    [Fact]
    public void MissingValueBecomesEmptyPropertyWithDiagnostic()
    {
        var doc = GuiParser.Parse("a = {\n\tb =\n}");

        var b = doc.Root.Children[0].Children[0];
        Assert.Equal(GuiNodeKind.Property, b.Kind);
        Assert.Equal(string.Empty, b.Value);
        Assert.Null(b.ValueSpan);
        Assert.Contains(doc.Diagnostics, d => d.Kind == GuiDiagnosticKind.MissingValue);
        Assert.True(doc.Root.Children[0].CloseBrace >= 0);
    }

    [Fact]
    public void FindDeepestAtReturnsInnermostNode()
    {
        const string text = "window = {\n\thbox = {\n\t\tbutton = { size = { 1 2 } }\n\t}\n}";
        var doc = GuiParser.Parse(text);

        var node = doc.FindDeepestAt(text.IndexOf("1 2", StringComparison.Ordinal));

        Assert.Equal(GuiNodeKind.ListItem, node!.Kind);
        Assert.Equal("size", node.Parent!.Name);
        Assert.Equal(new[] { "size", "button", "hbox", "window" }, node.Ancestors().Where(a => a.Kind != GuiNodeKind.Document).Select(a => a.Name));
        Assert.Null(doc.FindDeepestAt(text.Length + 5));
    }

    [Fact]
    public void RootOfAnyNodeIsTheDocumentRoot()
    {
        var doc = GuiParser.Parse("a = { b = { c = 1 } }");

        Assert.Same(doc.Root, doc.Root.Descendants().Last().Root);
        Assert.Same(doc.Root, doc.Root.Root);
    }
}
