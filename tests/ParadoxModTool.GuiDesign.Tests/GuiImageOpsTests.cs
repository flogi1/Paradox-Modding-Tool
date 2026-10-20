using ParadoxModTool.GuiDesign.Imaging;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Pixelarbeit an BGRA-Puffern: Färben durch Multiplizieren.</summary>
public sealed class GuiImageOpsTests
{
    [Fact]
    public void Multiply_scales_and_clamps_colour_and_keeps_alpha()
    {
        var pixel = new byte[] { 100, 100, 100, 77 };
        GuiImageOps.MultiplyInPlace(pixel, new GuiTint(0.5, 2, 3));
        Assert.Equal(new byte[] { 255, 200, 50, 77 }, pixel);   // B×3, G×2, R×0,5
    }

    [Fact]
    public void White_tint_leaves_pixels_alone_and_bad_factors_count_as_zero()
    {
        var pixel = new byte[] { 10, 20, 30, 40, 10, 20, 30, 40 };
        GuiImageOps.MultiplyInPlace(pixel, GuiTint.White);
        Assert.Equal(new byte[] { 10, 20, 30, 40, 10, 20, 30, 40 }, pixel);
        Assert.True(GuiTint.White.IsWhite);
        Assert.False(new GuiTint(1, 1, 0.9).IsWhite);

        GuiImageOps.MultiplyInPlace(pixel, new GuiTint(double.NaN, -2, double.PositiveInfinity));
        Assert.Equal(new byte[] { 255, 0, 0, 40, 255, 0, 0, 40 }, pixel);
    }
}
