using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class JominiTextureColorTests
{
    private static ResolvedProperty Make(GuiRole role, string value, bool quoted, params string[] items)
    {
        var node = GuiParser.Parse("x = 1").Root.Children[0];
        return new ResolvedProperty
        {
            Name = "color",
            Role = role,
            Value = value,
            Items = items,
            IsQuoted = quoted,
            Origin = new GuiOrigin(GuiOriginKind.Here, string.Empty, null, 1, node),
        };
    }

    private static ResolvedProperty Block(params string[] items) => Make(GuiRole.ValueBlock, string.Join(' ', items), false, items);

    // Lauf 5 (Probefenster 6), Victoria 3 und EU V: jede Komponente wird auf 0–1 begrenzt. 13s: { 2 2 2 1 } auf Grau 128 bleibt 128 (kein Aufhellen),
    // 13t: { 200 20 0 1 } auf Weiß ist Gelb (keine 0–255-Schreibweise). Die Annahme vor der Messung (Vielfache hellen auf, über 2,5 heißt 0–255) ist
    // damit widerlegt. CK III rechnet RGB-Komponenten über 1 anders (13t: (55, 236, 0), vermutlich Byte-Überlauf); dafür steht der Hinweis "color > 1,
    // CK III". Alpha über 1 ist in keinem Spiel gemessen: eigener Hinweis "alpha > 1".
    [Theory]
    [InlineData("2.0 2.0 2.0 1.0", 1, 1, 1, 1, true, false)]
    [InlineData("1.77 1.77 1.80 1", 1, 1, 1, 1, true, false)]
    [InlineData("200 20 0 0.8", 1, 1, 0, 0.8, true, false)]
    [InlineData("46 49 49 0.7", 1, 1, 1, 0.7, true, false)]
    [InlineData("0.8 0.5 0.5 2.0", 0.8, 0.5, 0.5, 1, false, true)]
    [InlineData("1 0 0", 1, 0, 0, 1, false, false)]
    [InlineData("1 0 0 0.5", 1, 0, 0, 0.5, false, false)]
    [InlineData("-0.5 0.25 0 1", 0, 0.25, 0, 1, false, false)]
    [InlineData("255 255 255 255", 1, 1, 1, 1, true, true)]
    public void Real_colour_tuples_follow_the_texture_rule(string items, double r, double g, double b, double a, bool rgbAboveOne, bool alphaAboveOne)
    {
        var notes = new List<string>();
        var color = JominiTextureColor.Parse(Block(items.Split(' ')), notes);
        Assert.NotNull(color);
        Assert.Equal(r, color!.Value.R, 6);
        Assert.Equal(g, color.Value.G, 6);
        Assert.Equal(b, color.Value.B, 6);
        Assert.Equal(a, color.Value.A, 6);
        var expected = new List<string>();
        if (rgbAboveOne)
        {
            expected.Add(GuiTexts.LayoutTextureApproximated(JominiTextureColor.AboveOneNote));
        }

        if (alphaAboveOne)
        {
            expected.Add(GuiTexts.LayoutTextureApproximated(JominiTextureColor.AlphaAboveOneNote));
        }

        Assert.Equal(expected, notes);
    }

    [Fact]
    public void Missing_property_gives_no_colour_and_no_note()
    {
        var notes = new List<string>();
        Assert.Null(JominiTextureColor.Parse(null, notes));
        Assert.Empty(notes);
    }

    [Theory, InlineData("\"color_red\""), InlineData("\"[GetColor]\""), InlineData("color_red"), InlineData("[GetColor]")]
    public void Named_or_bound_colour_gives_a_note_and_no_colour(string value)
    {
        var notes = new List<string>();
        Assert.Null(JominiTextureColor.Parse(Make(GuiRole.Property, value, value.StartsWith('"')), notes));
        Assert.Equal(new[] { GuiTexts.LayoutTextureApproximated("color") }, notes);
    }

    [Fact]
    public void Block_with_a_non_number_gives_a_note_and_no_colour()
    {
        var notes = new List<string>();
        Assert.Null(JominiTextureColor.Parse(Block("1", "[GetR]", "0"), notes));
        Assert.Single(notes);
    }
}
