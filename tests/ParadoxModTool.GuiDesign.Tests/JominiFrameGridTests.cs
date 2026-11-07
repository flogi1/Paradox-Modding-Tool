using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public sealed class JominiFrameGridTests
{
    private static ResolvedProperty Prop(string name, string value, params string[] items) => new()
    {
        Name = name,
        Role = items.Length > 0 ? GuiRole.ValueBlock : GuiRole.Property,
        Value = value,
        Items = items,
        Origin = new GuiOrigin(GuiOriginKind.Here, "", null, 0, null!),
    };

    [Fact]
    public void ForFrameSize_CountsRowWise()
    {
        var layout = JominiFrameGrid.ForFrameSize(80, 80, 40, 40);
        Assert.Equal(2, layout.Columns);
        Assert.Equal(2, layout.Rows);
        Assert.Equal(4, layout.Count);
        Assert.Equal(new LayoutRect(0, 40, 40, 40), layout.SourceOf(3));
    }

    [Fact]
    public void ForFrameSize_LargerThanTexture_Clamps()
    {
        var layout = JominiFrameGrid.ForFrameSize(50, 20, 80, 20);
        Assert.Equal(1, layout.Count);
        Assert.Equal(50, layout.FrameWidth);
    }

    [Fact]
    public void ForFrameSize_FractionalWholePixels()
    {
        var layout = JominiFrameGrid.ForFrameSize(100, 20, 33.33, 20);
        Assert.Equal(33, layout.FrameWidth);
        Assert.Equal(3, layout.Columns);
    }

    [Fact]
    public void ForGrid_WholePixels()
    {
        var layout = JominiFrameGrid.ForGrid(255, 64, 5, 1);
        Assert.Equal(51, layout.FrameWidth);
        Assert.Equal(5, layout.Count);
    }

    [Fact]
    public void For_FrameSizeBinding_Null() =>
        Assert.Null(JominiFrameGrid.For(80, 80, Prop("framesize", "[X] [Y]", "[X]", "[Y]"), null));

    [Fact]
    public void For_FrameSizeWinsOverGrid()
    {
        var layout = JominiFrameGrid.For(80, 80, Prop("framesize", "40 40", "40", "40"), Prop("frame_grid", "8 8", "8", "8"));
        Assert.Equal(4, layout!.Value.Count);
    }

    [Fact]
    public void For_Neither_IsWhole()
    {
        var layout = JominiFrameGrid.For(80, 60, null, null)!.Value;
        Assert.Equal(1, layout.Count);
        Assert.Equal(new LayoutRect(0, 0, 80, 60), layout.SourceOf(1));
    }

    [Fact]
    public void FrameNumber_Variants()
    {
        Assert.Equal(1, JominiFrameGrid.FrameNumber(null));
        Assert.Equal(1, JominiFrameGrid.FrameNumber(Prop("frame", "\"0\"")));
        Assert.Equal(3, JominiFrameGrid.FrameNumber(Prop("frame", "\"3\"")));
        Assert.Equal(3, JominiFrameGrid.FrameNumber(Prop("frame", "3.9")));
        Assert.Null(JominiFrameGrid.FrameNumber(Prop("frame", "[GetFrame]")));
    }

    [Fact]
    public void FrameNumberOf_Text_Variants()
    {
        Assert.Equal(1, JominiFrameGrid.FrameNumberOf(""));
        Assert.Equal(1, JominiFrameGrid.FrameNumberOf("  "));
        Assert.Equal(1, JominiFrameGrid.FrameNumberOf("0"));
        Assert.Equal(9, JominiFrameGrid.FrameNumberOf(" 9 "));
        Assert.Null(JominiFrameGrid.FrameNumberOf("[GetFrame]"));
    }

    [Fact]
    public void ForItems_SamePrecedenceAsFor()
    {
        Assert.Equal(4, JominiFrameGrid.ForItems(40, 40, new[] { "20", "20" }, null)!.Value.Count);
        Assert.Equal(6, JominiFrameGrid.ForItems(60, 40, null, new[] { "3", "2" })!.Value.Count);
        // framesize gewinnt vor frame_grid; halb getippt oder Bindung: kein Raster.
        Assert.Equal(4, JominiFrameGrid.ForItems(40, 40, new[] { "20", "20" }, new[] { "8", "8" })!.Value.Count);
        Assert.Null(JominiFrameGrid.ForItems(40, 40, new[] { "20", "" }, null));
        Assert.Null(JominiFrameGrid.ForItems(40, 40, new[] { "[X]", "[Y]" }, null));
        Assert.Equal(1, JominiFrameGrid.ForItems(40, 40, null, null)!.Value.Count);
    }
}
