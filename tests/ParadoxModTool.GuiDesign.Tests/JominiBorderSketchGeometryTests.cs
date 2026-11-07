using ParadoxModTool.GuiDesign.Layout;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public sealed class JominiBorderSketchGeometryTests
{
    [Fact]
    public void Fit_DensityTwo_LinesAtSlice()
    {
        var sketch = JominiBorderSketchGeometry.Fit(new LayoutRect(0, 0, 40, 40), 2, new JominiBorder(8, 8, 8, 8), 220, 140);
        Assert.Equal(3.5, sketch.Scale, 6);
        Assert.Equal(140, sketch.ImageWidth, 6);
        Assert.Equal(140, sketch.ImageHeight, 6);
        Assert.Equal(40, sketch.OffsetX, 6);
        Assert.Equal(0, sketch.OffsetY, 6);
        Assert.Equal(96, sketch.Left, 6);
        Assert.Equal(124, sketch.Right, 6);
        Assert.Equal(56, sketch.Top, 6);
        Assert.Equal(84, sketch.Bottom, 6);
    }

    [Fact]
    public void Fit_BorderLargerThanRegion_Clamped()
    {
        var sketch = JominiBorderSketchGeometry.Fit(new LayoutRect(0, 0, 40, 40), 1, new JominiBorder(100, -5, 100, 100), 220, 140);
        Assert.Equal(40 + 40 * 3.5, sketch.Left, 6);
        Assert.Equal(40, sketch.Right, 6);
        Assert.Equal(0, sketch.Top, 6);
        Assert.Equal(0, sketch.Bottom, 6);
    }
}
