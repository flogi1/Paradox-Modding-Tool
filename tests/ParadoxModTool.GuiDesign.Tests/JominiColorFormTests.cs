using ParadoxModTool.GuiDesign.Designer;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class JominiColorFormTests
{
    [Theory]
    [InlineData("color", JominiColorKind.Texture)]
    [InlineData("tintcolor", JominiColorKind.Texture)]
    [InlineData("fontcolor", JominiColorKind.Text)]
    public void KindOf_known(string name, JominiColorKind kind) => Assert.Equal(kind, JominiColorForm.KindOf(name));

    [Fact]
    public void KindOf_other_is_null() => Assert.Null(JominiColorForm.KindOf("background"));

    [Fact]
    public void Read_ZeroToOne()
    {
        var value = JominiColorForm.Read("{ 0.5 0.25 1 0.8 }", JominiColorKind.Texture)!;
        Assert.Equal(new GuiColor(0.5, 0.25, 1, 0.8), value.Shown);
        Assert.False(value.ByteForm);
        Assert.False(value.AboveOne);
        Assert.Equal(4, value.Components);
        Assert.Equal("0.8", value.AlphaText);
    }

    // Controller ruling R1: the texture rule is the measured one (0-1, clamped); 0-255 numbers are never a byte form for textures.
    [Fact]
    public void Read_ByteForm_Texture()
    {
        var value = JominiColorForm.Read("{ 46 49 49 0.7 }", JominiColorKind.Texture)!;
        Assert.Equal(new GuiColor(1, 1, 1, 0.7), value.Shown);
        Assert.False(value.ByteForm);
        Assert.True(value.AboveOne);
    }

    [Fact]
    public void Read_Multiplier_Texture()
    {
        var value = JominiColorForm.Read("{ 2.0 2.0 2.0 1.0 }", JominiColorKind.Texture)!;
        Assert.False(value.ByteForm);
        Assert.True(value.AboveOne);
        Assert.Equal(new GuiColor(1, 1, 1, 1), value.Shown);
    }

    [Fact]
    public void Read_Text_UsesTextRule()
    {
        var value = JominiColorForm.Read("{ 2.0 2.0 2.0 1.0 }", JominiColorKind.Text)!;
        var expected = Text.JominiTextService.FontColor("{ 2.0 2.0 2.0 1.0 }", out var bytes)!.Value;
        Assert.Equal(bytes, value.ByteForm);
        Assert.Equal(Math.Min(1, expected.R), value.Shown.R, 9);
        Assert.Equal(Math.Min(1, expected.A), value.Shown.A, 9);
    }

    [Fact]
    public void Read_ByteForm_Text()
    {
        var value = JominiColorForm.Read("{ 46 49 49 255 }", JominiColorKind.Text)!;
        Assert.True(value.ByteForm);
        Assert.False(value.AboveOne);
        Assert.Equal(46 / 255.0, value.Shown.R, 9);
        Assert.Equal(1, value.Shown.A, 9);
        Assert.Equal("255", value.AlphaText);
    }

    [Theory]
    [InlineData("color_red")]
    [InlineData("\"color_red\"")]
    [InlineData("[GetColor]")]
    [InlineData("\"[GetColor]\"")]
    [InlineData("{ 1 2 }")]
    [InlineData("")]
    public void Read_Named_Null(string input)
    {
        Assert.Null(JominiColorForm.Read(input, JominiColorKind.Texture));
        Assert.Null(JominiColorForm.Read(input, JominiColorKind.Text));
    }

    [Fact]
    public void FormatLike_KeepsByteForm()
    {
        var original = JominiColorForm.Read("{ 46 49 49 0.7 }", JominiColorKind.Text);
        Assert.Equal("{ 200 20 0 0.7 }", JominiColorForm.FormatLike(200, 20, 0, original));
    }

    // Fast schwarz in 0–255: alle RGB ≤ 1. Ein Alpha über 1 hält die 0–255-Form (Regressionsschutz).
    [Fact]
    public void FormatLike_ByteForm_NearBlack_ByteAlpha_StaysByteForm()
    {
        var original = JominiColorForm.Read("{ 46 49 49 255 }", JominiColorKind.Text);
        var written = JominiColorForm.FormatLike(1, 0, 0, original);
        Assert.Equal("{ 1 0 0 255 }", written);
        var back = JominiColorForm.Read(written, JominiColorKind.Text)!;
        Assert.True(back.ByteForm);
        Assert.Equal(1 / 255.0, back.Shown.R, 9);
        Assert.Equal(1, back.Shown.A, 9);
    }

    // Fast schwarz in 0–255 ohne Alpha über 1: die Regel läse das Tupel als 0–1 (hellrot), also kommt die 0–1-Form.
    [Theory]
    [InlineData("{ 46 49 49 0.7 }")]
    [InlineData("{ 46 49 49 1 }")]
    [InlineData("{ 46 49 49 }")]
    public void FormatLike_ByteForm_NearBlack_FallsBackToZeroToOne(string originalText)
    {
        var original = JominiColorForm.Read(originalText, JominiColorKind.Text)!;
        var written = JominiColorForm.FormatLike(1, 0, 0, original);
        var back = JominiColorForm.Read(written, JominiColorKind.Text)!;
        Assert.False(back.ByteForm);
        Assert.Equal(original.Components, back.Components);
        Assert.Equal(1 / 255.0, back.Shown.R, 2);
        Assert.Equal(0, back.Shown.G, 9);
        Assert.Equal(original.Shown.A, back.Shown.A, 2);
    }

    [Fact]
    public void FormatLike_ByteForm_NearBlack_ZeroToOneText()
    {
        Assert.Equal("{ 0.004 0 0 0.003 }", JominiColorForm.FormatLike(1, 0, 0, JominiColorForm.Read("{ 46 49 49 0.7 }", JominiColorKind.Text)));
        Assert.Equal("{ 0.004 0 0 }", JominiColorForm.FormatLike(1, 0, 0, JominiColorForm.Read("{ 46 49 49 }", JominiColorKind.Text)));
    }

    [Fact]
    public void FormatLike_TextureByteValues_WritesZeroToOne()
    {
        var original = JominiColorForm.Read("{ 46 49 49 0.7 }", JominiColorKind.Texture);
        Assert.Equal("{ 0.784 0.078 0 0.7 }", JominiColorForm.FormatLike(200, 20, 0, original));
    }

    [Fact]
    public void FormatLike_KeepsAlpha()
    {
        var original = JominiColorForm.Read("{ 0.1 0.2 0.3 0.45 }", JominiColorKind.Texture);
        Assert.Equal("{ 1 0 0 0.45 }", JominiColorForm.FormatLike(255, 0, 0, original));
    }

    [Fact]
    public void FormatLike_ThreeTuple()
    {
        var original = JominiColorForm.Read("{ 1 0 0 }", JominiColorKind.Texture);
        Assert.Equal(3, original!.Components);
        Assert.Equal("{ 0 1 0 }", JominiColorForm.FormatLike(0, 255, 0, original));
    }

    [Fact]
    public void FormatLike_Named()
    {
        Assert.Equal("{ 1 0 0 1 }", JominiColorForm.FormatLike(255, 0, 0, null));
    }
}
