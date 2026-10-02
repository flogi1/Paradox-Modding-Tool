using ParadoxModTool.GuiDesign.Blocks;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiBlockInsertionTests
{
    [Fact]
    public void ForCaret_IndentsFollowingNonEmptyLines()
    {
        Assert.Equal("a\n\t\t\tb\n\n\t\tc\n\t\t", GuiBlockInsertion.ForCaret("a\n\tb\n\nc\n", "\t\t", "\n"));
    }

    [Fact]
    public void ForCaret_UsesDocumentNewline()
    {
        var text = GuiBlockInsertion.ForCaret("a\nb\n", "  ", "\r\n");

        Assert.Equal("a\r\n  b\r\n  ", text);
        Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty));
    }

    [Fact]
    public void ForCaret_SingleLineIsUnchanged()
    {
        Assert.Equal("x", GuiBlockInsertion.ForCaret("x", "    ", "\n"));
    }
}
