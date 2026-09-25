using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiElementPathTests
{
    private const string Text =
        "types M {\n\ttype my_button = button {\n\t\tblock \"text\" { }\n\t}\n}\n" +
        "window = {\n\tname = \"main\"\n\tsize = { 10 10 }\n\thbox = {\n\t\tmy_button = {\n\t\t\tblockoverride \"text\" {\n\t\t\t\ttext_single = { text = \"x\" }\n\t\t\t}\n\t\t}\n\t}\n\ttooltipwidget = { widget = { } }\n}";

    [Fact]
    public void ElementAtFindsTheInnermostWidgetOrDefinition()
    {
        var (resolver, doc) = GuiResolverTests.Setup(Text);
        int At(string marker) => doc.Text.IndexOf(marker, StringComparison.Ordinal);

        Assert.Equal("window", GuiElementPath.ElementAt(doc, At("10 10"), resolver)!.Name);
        var definition = GuiElementPath.ElementAt(doc, At("block \"text\""), resolver)!;
        Assert.Equal(GuiNodeKind.TypeDefinition, definition.Kind);
        Assert.Equal("my_button", definition.Name);
        Assert.Equal("text_single", GuiElementPath.ElementAt(doc, At("text = \"x\""), resolver)!.Name);
        Assert.Equal("tooltipwidget", GuiElementPath.ElementAt(doc, At("tooltipwidget"), resolver)!.Name);
        Assert.Null(GuiElementPath.ElementAt(doc, At("types M"), resolver));
        Assert.Equal("window", GuiElementPath.ElementAt(doc, doc.Text.Length + 5, resolver)!.Name);
    }

    [Fact]
    public void CrumbsRunFromTheTopToTheElementAtTheCursor()
    {
        var (resolver, doc) = GuiResolverTests.Setup(Text);

        var crumbs = GuiElementPath.CrumbsAt(doc, doc.Text.IndexOf("text = \"x\"", StringComparison.Ordinal), resolver);

        Assert.Equal(new[] { "window \"main\"", "hbox", "my_button", "blockoverride \"text\"", "text_single" }, crumbs.Select(c => c.Label));
        Assert.Equal("text_single", crumbs[^1].Node.Name);
        Assert.Equal(
            new[] { "types M", "type my_button = button", "block \"text\"" },
            GuiElementPath.CrumbsAt(doc, doc.Text.IndexOf("block", StringComparison.Ordinal) + 1, resolver).Select(c => c.Label));
        Assert.Empty(GuiElementPath.CrumbsAt(GuiParser.Parse(string.Empty), 0, resolver));
    }

    [Fact]
    public void ElementAtLineUsesTheFirstNonBlankCharacter()
    {
        var (resolver, doc) = GuiResolverTests.Setup(Text);
        int LineOf(string marker) => Text[..Text.IndexOf(marker, StringComparison.Ordinal)].Count(c => c == '\n') + 1;

        Assert.Equal("text_single", GuiElementPath.ElementAtLine(doc, LineOf("text_single"), resolver)!.Name);
        Assert.Equal("window", GuiElementPath.ElementAtLine(doc, LineOf("size = { 10 10 }"), resolver)!.Name);
        Assert.Null(GuiElementPath.ElementAtLine(doc, 1, resolver));
        Assert.Equal("window", GuiElementPath.ElementAtLine(doc, 999, resolver)!.Name);
        Assert.Null(GuiElementPath.ElementAtLine(doc, 0, resolver));
    }
}
