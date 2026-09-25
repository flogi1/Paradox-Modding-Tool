using ParadoxModTool.GuiDesign.Designer;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiPropertyRowsTests
{
    [Theory]
    [InlineData("name", GuiPropertyGroup.Data)]
    [InlineData("datacontext", GuiPropertyGroup.Data)]
    [InlineData("size", GuiPropertyGroup.Layout)]
    [InlineData("margin_left", GuiPropertyGroup.Layout)]
    [InlineData("layoutpolicy_horizontal", GuiPropertyGroup.Layout)]
    [InlineData("texture", GuiPropertyGroup.Appearance)]
    [InlineData("spriteborder", GuiPropertyGroup.Appearance)]
    [InlineData("text", GuiPropertyGroup.Text)]
    [InlineData("fontsize", GuiPropertyGroup.Text)]
    [InlineData("onclick", GuiPropertyGroup.Behavior)]
    [InlineData("visible", GuiPropertyGroup.Behavior)]
    [InlineData("tooltip", GuiPropertyGroup.Behavior)]
    [InlineData("foo", GuiPropertyGroup.Other)]
    public void GroupsFollowTheName(string name, GuiPropertyGroup expected)
    {
        Assert.Equal(expected, GuiPropertyRows.GroupOf(name));
    }

    [Fact]
    public void RowsKeepTheirResolvedProperty()
    {
        var (resolver, doc) = GuiResolverTests.Setup("widget = {\n\tsize = { 20 30 }\n\talpha = 0.5\n}");
        var element = resolver.Resolve(GuiResolverTests.Top(doc, "widget"));

        var rows = GuiPropertyRows.Build(element);

        Assert.Same(element.Property("size"), rows.Single(r => r.Name == "size").Source);
        Assert.Same(element.Property("alpha"), rows.Single(r => r.Name == "alpha").Source);
    }

    [Fact]
    public void RowsCarryOriginAndFollowTheGroupOrder()
    {
        var (resolver, doc) = GuiResolverTests.Setup(
            "my_button = {\n\tonclick = \"[Do]\"\n\tsize = { 20 30 }\n\tname = \"b\"\n}",
            "types T {\n\ttype my_button = button {\n\t\tsize = { 10 10 }\n\t\ttexture = \"gfx/b.dds\"\n\t}\n}");

        var rows = GuiPropertyRows.Build(resolver.Resolve(GuiResolverTests.Top(doc, "my_button")));

        Assert.Equal(new[] { "name", "size", "texture", "onclick" }, rows.Select(r => r.Name));
        var size = rows[1];
        Assert.Equal("{ 20 30 }", size.Value);
        Assert.True(size.IsHere);
        Assert.Equal("hier gesetzt", size.OriginText);
        Assert.Equal(1, size.OverriddenCount);
        Assert.Equal("\"gfx/b.dds\"", rows[2].Value);
        Assert.Equal("geerbt von my_button (library.gui:4)", rows[2].OriginText);
        Assert.False(rows[2].IsHere);
    }
}
