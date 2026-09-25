using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiElementTreeTests
{
    [Fact]
    public void BuildsDefinitionsWidgetsBlocksAndOfferedBlocks()
    {
        var (resolver, doc) = GuiResolverTests.Setup(
            "types M {\n\ttype my_button = button {\n\t\tblock \"text\" { }\n\t\tblock \"icon\" { }\n\t}\n}\n" +
            "template my_template { size = { 1 1 } }\n" +
            "window = {\n\tname = \"main\"\n\tsize = { 10 10 }\n\thbox = {\n\t\tmy_button = {\n\t\t\tblockoverride \"text\" { }\n\t\t}\n\t}\n\ttooltipwidget = { widget = { } }\n}");

        var items = GuiElementTree.Build(doc, resolver);

        Assert.Equal(new[] { "types M", "template my_template", "window \"main\"" }, items.Select(i => i.Label));
        var type = Assert.Single(items[0].Children);
        Assert.Equal("type my_button = button", type.Label);
        Assert.Equal("button", type.Detail);
        Assert.Equal(new[] { "block \"text\"", "block \"icon\"" }, type.Children.Select(c => c.Label));

        var window = items[2];
        Assert.Equal(GuiWidgetCategory.Window, window.Category);
        Assert.Equal(new[] { "hbox", "tooltipwidget" }, window.Children.Select(c => c.Label));
        Assert.Equal(GuiTreeItemKind.Holder, window.Children[1].Kind);

        var button = Assert.Single(window.Children[0].Children);
        Assert.Equal("my_button", button.Label);
        Assert.Equal("button", button.Detail);
        Assert.Equal(new[] { "blockoverride \"text\"", "block \"icon\"" }, button.Children.Select(c => c.Label));
        Assert.Equal(GuiTreeItemKind.OfferedBlock, button.Children[1].Kind);
        Assert.Equal("nicht überschrieben", button.Children[1].Detail);
        Assert.Same(button.Node, button.Children[1].Node);
    }

    [Fact]
    public void InstancesAndOfferedBlocksCarryWhatTheActionsNeed()
    {
        var (resolver, doc) = GuiResolverTests.Setup("window = { my_button = { } }", "types T { type my_button = button { block \"label\" { } } }");

        var window = Assert.Single(GuiElementTree.Build(doc, resolver));
        var instance = Assert.Single(window.Children);

        Assert.False(window.IsTypeInstance);
        Assert.True(instance.IsTypeInstance);
        var offered = Assert.Single(instance.Children);
        Assert.Equal("label", offered.BlockName);

        // "Block überschreiben" schreibt an die Instanz in dieser Datei, nicht an den block der Typdefinition.
        Assert.Same(instance.Node, offered.Node);
    }

    [Fact]
    public void MarksUnknownEngineWidgets()
    {
        var (resolver, doc) = GuiResolverTests.Setup("window = { mystery = { a = 1 } }");

        var mystery = Assert.Single(GuiElementTree.Build(doc, resolver, name => name != "mystery")[0].Children);

        Assert.True(mystery.Unknown);
        Assert.Equal("unbekannter Typ", mystery.Detail);
    }

    [Fact]
    public void BuildsWithAnEmptyIndex()
    {
        var doc = GuiParser.Parse("window = { hbox = { my_button = { } } }");
        var resolver = new GuiResolver(GuiIndex.Empty, GuiRules.Empty, doc, null);

        var items = GuiElementTree.Build(doc, resolver);

        Assert.Equal("my_button", items[0].Children[0].Children[0].Label);
    }
}
