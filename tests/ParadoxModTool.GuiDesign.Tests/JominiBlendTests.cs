using ParadoxModTool.GuiDesign.Layout;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Die Mischformeln je Pixel (<see cref="JominiBlend"/>), Kanäle 0–1. Liest <see cref="JominiEffectRules"/>, daher in deren Sammlung.</summary>
[Collection(nameof(JominiEffectRules))]
public sealed class JominiBlendTests
{
    private static readonly (double R, double G, double B, double A) Base = (0.25, 0.5, 0.75, 1);
    private static readonly (double R, double G, double B, double A) Grey = (0.5, 0.5, 0.5, 1);

    [Theory]
    [InlineData(JominiBlendMode.Normal, 0.5, 0.5, 0.5)]
    [InlineData(JominiBlendMode.Multiply, 0.125, 0.25, 0.375)]
    [InlineData(JominiBlendMode.Overlay, 0.25, 0.5, 0.75)]
    [InlineData(JominiBlendMode.Add, 0.75, 1, 1)]
    [InlineData(JominiBlendMode.ColorDodge, 0.5, 1, 1)]
    [InlineData(JominiBlendMode.Lighten, 0.5, 0.5, 0.75)]
    [InlineData(JominiBlendMode.Darken, 0.25, 0.5, 0.5)]
    public void Apply_ColourModes(JominiBlendMode mode, double r, double g, double b)
    {
        AssertColour((r, g, b, 1), JominiBlend.Apply(mode, Base, Grey, 1));
    }

    [Fact]
    public void Apply_NormalAtHalfOpacity()
    {
        AssertColour((0.375, 0.5, 0.625, 1), JominiBlend.Apply(JominiBlendMode.Normal, Base, Grey, 0.5));
    }

    [Fact]
    public void Apply_ColourModesKeepBaseAlpha()
    {
        AssertColour((0.375, 0.5, 0.625, 0.4), JominiBlend.Apply(JominiBlendMode.Normal, (0.25, 0.5, 0.75, 0.4), (0.5, 0.5, 0.5, 0.5), 1));
    }

    [Fact]
    public void Apply_AlphaMultiply()
    {
        AssertColour((0.25, 0.5, 0.75, 0.25), JominiBlend.Apply(JominiBlendMode.AlphaMultiply, Base, (0.5, 0.5, 0.5, 0.25), 1));
        AssertColour((0.25, 0.5, 0.75, 0.625), JominiBlend.Apply(JominiBlendMode.AlphaMultiply, Base, (0.5, 0.5, 0.5, 0.25), 0.5));
    }

    [Fact]
    public void Apply_Mask_ReadsRuleChannel()
    {
        AssertColour((0.25, 0.5, 0.75, 0.25), JominiBlend.Apply(JominiBlendMode.Mask, Base, (0.5, 0.5, 0.5, 0.25), 1));
        try
        {
            JominiEffectRules.MaskChannel = JominiAlphaChannel.Luminance;
            // Luminanz von (1, 0, 0) mit den Graustufen-Gewichten: 0,299.
            AssertColour((0.25, 0.5, 0.75, 0.299), JominiBlend.Apply(JominiBlendMode.Mask, Base, (1, 0, 0, 1), 1));
        }
        finally
        {
            JominiEffectRules.MaskChannel = JominiAlphaChannel.Alpha;
        }
    }

    [Fact]
    public void Grayscale_UsesRuleWeights()
    {
        var bgra = new byte[] { 0, 0, 255, 200 };
        JominiBlend.Grayscale(bgra);
        Assert.Equal(new byte[] { 76, 76, 76, 200 }, bgra);
    }

    private static void AssertColour((double R, double G, double B, double A) expected, (double R, double G, double B, double A) actual)
    {
        Assert.Equal(expected.R, actual.R, 1e-9);
        Assert.Equal(expected.G, actual.G, 1e-9);
        Assert.Equal(expected.B, actual.B, 1e-9);
        Assert.Equal(expected.A, actual.A, 1e-9);
    }
}
