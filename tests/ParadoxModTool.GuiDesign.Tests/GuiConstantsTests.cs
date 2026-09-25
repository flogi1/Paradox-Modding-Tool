using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiConstantsTests
{
    [Theory]
    [InlineData("@w", "w")]
    [InlineData("@[a + b * 2]", "a,b")]
    [InlineData("@[-panel_x]", "panel_x")]
    [InlineData("@[Entry.GetGoods.GetName(Entry.GetPrice)]", "")]
    [InlineData("10", "")]
    [InlineData("@", "")]
    public void NamesInAValue(string value, string expected)
    {
        Assert.Equal(expected, string.Join(",", GuiConstants.NamesIn(value)));
    }

    [Fact]
    public void RequiredConstantsAreTransitiveAndOutsideTheDefinition()
    {
        var document = GuiParser.Parse(
            "@a = 1\n@b = @a\n@c = @[b * 2]\n@unused = 5\ntypes T {\n\t@d = 3\n\ttype x = widget {\n\t\t@inner = 7\n\t\tsize = { @c @d }\n\t\talpha = @inner\n\t\ttext = \"@unused\"\n\t}\n}");
        var definition = document.Root.Descendants().First(n => n.Kind == GuiNodeKind.TypeDefinition);

        var required = GuiConstants.RequiredBy(definition);

        Assert.Equal(new[] { "a", "b", "c", "d" }, required.Select(c => c.Name));
    }

    [Fact]
    public void RenameKeepsStringsCommentsAndMemberAccess()
    {
        var renames = new Dictionary<string, string> { ["w"] = "w_x" };

        Assert.Equal(
            "size = { @w_x @wide } # @w\ntext = \"@w\"\nx = @[w_x + Data.w]",
            GuiConstants.Rename("size = { @w @wide } # @w\ntext = \"@w\"\nx = @[w + Data.w]", renames));
    }
}
