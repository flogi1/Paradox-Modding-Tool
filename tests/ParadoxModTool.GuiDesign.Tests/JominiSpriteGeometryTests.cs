using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Text;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class JominiSpriteGeometryTests
{
    private static JominiSpriteVisual Sprite(
        LayoutRect? source = null,
        JominiSpriteKind kind = JominiSpriteKind.Stretched,
        JominiBorder border = default,
        double density = 1,
        JominiFit fit = JominiFit.Fill,
        int width = 40,
        int height = 40,
        JominiSpritePlaceholder placeholder = JominiSpritePlaceholder.None) =>
        new("gfx/x.dds", "C:\\x.dds", width, height, source ?? new LayoutRect(0, 0, width, height), density, kind, border, fit,
            false, false, JominiColor.White, 1, placeholder, null);

    [Fact]
    public void Stretched_maps_the_frame_onto_the_box()
    {
        var piece = Assert.Single(JominiSpriteGeometry.Pieces(Sprite(source: new(40, 0, 40, 40)), new(10, 20, 200, 100)));
        Assert.Equal(new LayoutRect(40, 0, 40, 40), piece.Source);
        Assert.Equal(new LayoutRect(10, 20, 200, 100), piece.Target);
        Assert.Equal(0, piece.TileWidth);
    }

    [Fact]
    public void Cornered_stretched_border_is_gui_units_and_density_times_texture_pixels()
    {
        // Lauf 5, Fall 13b (alle drei Spiele): Rand 8 bei Dichte 2 ist 8 GUI-Einheiten breit und zeigt 16 Texturpixel unverkleinert; die Mitte streckt
        // nur die Pixel 16–24. Die Annahme vor der Messung (8 Pixel, im Kasten 8 / 2 = 4) widerlegen die W-Marken der Probe.
        var pieces = JominiSpriteGeometry.Pieces(Sprite(kind: JominiSpriteKind.CorneredStretched, border: new(8, 8, 8, 8), density: 2), new(10, 20, 200, 100));
        Assert.Equal(9, pieces.Count);
        Assert.Equal((new LayoutRect(0, 0, 16, 16), new LayoutRect(10, 20, 8, 8)), (pieces[0].Source, pieces[0].Target));   // oben links
        Assert.Equal((new LayoutRect(16, 16, 8, 8), new LayoutRect(18, 28, 184, 84)), (pieces[4].Source, pieces[4].Target)); // Mitte
        Assert.All(pieces, p => Assert.Equal(0, p.TileWidth));
    }

    [Fact]
    public void Cornered_tiled_tiles_edges_and_centre()
    {
        var pieces = JominiSpriteGeometry.Pieces(Sprite(kind: JominiSpriteKind.CorneredTiled, border: new(8, 8, 8, 8), density: 2), new(0, 0, 200, 100));
        Assert.Equal(9, pieces.Count);

        // Ecken ohne Kachel
        foreach (var i in new[] { 0, 2, 6, 8 })
        {
            Assert.Equal(0, pieces[i].TileWidth);
            Assert.Equal(0, pieces[i].TileHeight);
        }

        // Mitte: Quelle 40 − 2 × 16 = 8 Texturpixel, Kachel 8 / 2 = 4 (Kachel ÷ Dichte gemessen an 13d; die Mitte von 13c ist einfarbig).
        Assert.Equal((4.0, 4.0), (pieces[4].TileWidth, pieces[4].TileHeight));

        // Oben/unten: Kachel 4 breit, Randhöhe 16 Pixel / 2 = 8 hoch; links/rechts umgekehrt
        Assert.Equal((4.0, 8.0), (pieces[1].TileWidth, pieces[1].TileHeight));
        Assert.Equal((4.0, 8.0), (pieces[7].TileWidth, pieces[7].TileHeight));
        Assert.Equal((8.0, 4.0), (pieces[3].TileWidth, pieces[3].TileHeight));
        Assert.Equal((8.0, 4.0), (pieces[5].TileWidth, pieces[5].TileHeight));
    }

    [Fact]
    public void Cornered_tiled_without_border_tiles_the_whole_texture()
    {
        var piece = Assert.Single(JominiSpriteGeometry.Pieces(Sprite(kind: JominiSpriteKind.Tiled, density: 2), new(5, 5, 100, 60)));
        Assert.Equal(new LayoutRect(0, 0, 40, 40), piece.Source);
        Assert.Equal(new LayoutRect(5, 5, 100, 60), piece.Target);
        Assert.Equal((20.0, 20.0), (piece.TileWidth, piece.TileHeight));
    }

    [Fact]
    public void Borders_larger_than_the_box_shrink_as_a_pair()
    {
        var pieces = JominiSpriteGeometry.Pieces(Sprite(kind: JominiSpriteKind.CorneredStretched, border: new(8, 8, 8, 8)), new(0, 0, 12, 12));
        Assert.Equal(4, pieces.Count); // keine Kante, keine Mitte
        Assert.All(pieces, p => Assert.Equal((6.0, 6.0), (p.Target.Width, p.Target.Height)));
    }

    [Fact]
    public void Single_side_border_keeps_its_own_width()
    {
        var pieces = JominiSpriteGeometry.Pieces(Sprite(kind: JominiSpriteKind.CorneredStretched, border: new(16, 8, 8, 8)), new(0, 0, 200, 100));
        Assert.Equal(new LayoutRect(0, 0, 16, 8), pieces[0].Target);
        Assert.Equal(new LayoutRect(0, 0, 16, 8), pieces[0].Source);
        Assert.Equal(new LayoutRect(192, 0, 8, 8), pieces[2].Target);
    }

    [Theory]
    [InlineData(0, 0, 2250, 0)]
    [InlineData(0, 0, 0, -500)]
    [InlineData(-5, -5, -5, -5)]
    [InlineData(double.NaN, double.PositiveInfinity, 3, 4)]
    public void Extreme_borders_never_give_negative_rectangles(double l, double t, double r, double b)
    {
        var pieces = JominiSpriteGeometry.Pieces(Sprite(kind: JominiSpriteKind.CorneredStretched, border: new(l, t, r, b)), new(0, 0, 200, 100));
        Assert.NotEmpty(pieces);
        foreach (var p in pieces)
        {
            foreach (var rect in new[] { p.Source, p.Target })
            {
                Assert.True(double.IsFinite(rect.X) && double.IsFinite(rect.Y) && double.IsFinite(rect.Width) && double.IsFinite(rect.Height));
                Assert.True(rect.Width >= 0 && rect.Height >= 0);
            }
        }

        // Je Zeile (gleiches Ziel-Y) und je Spalte ergeben die Ziele den ganzen Kasten
        foreach (var row in pieces.GroupBy(p => p.Target.Y))
        {
            Assert.Equal(200, row.Sum(p => p.Target.Width), 6);
        }

        foreach (var column in pieces.GroupBy(p => p.Target.X))
        {
            Assert.Equal(100, column.Sum(p => p.Target.Height), 6);
        }
    }

    [Fact]
    public void Borders_wider_than_the_texture_shrink_in_the_source_too()
    {
        // Rand 30+30 bei 40 Texturpixeln: die Quelle schrumpft auf 20+20, die Mitte hat dann keine Quelle und entfällt
        var pieces = JominiSpriteGeometry.Pieces(Sprite(kind: JominiSpriteKind.CorneredStretched, border: new(30, 30, 30, 30)), new(0, 0, 200, 100));
        Assert.Equal(4, pieces.Count);
        Assert.All(pieces, p => Assert.Equal((20.0, 20.0), (p.Source.Width, p.Source.Height)));
        Assert.All(pieces, p => Assert.Equal((30.0, 30.0), (p.Target.Width, p.Target.Height)));
    }

    [Fact]
    public void Negative_borders_count_as_zero_with_a_note()
    {
        var notes = new List<string>();
        var pieces = JominiSpriteGeometry.Pieces(Sprite(kind: JominiSpriteKind.CorneredStretched, border: new(-5, 8, 8, 8)), new(0, 0, 200, 100), notes);
        Assert.Equal(6, pieces.Count); // linke Spalte entfällt
        Assert.Single(notes);
    }

    [Fact]
    public void Tiny_tiles_are_stretched_instead()
    {
        var notes = new List<string>();
        var piece = Assert.Single(JominiSpriteGeometry.Pieces(Sprite(kind: JominiSpriteKind.Tiled, density: 100), new(0, 0, 2000, 2000), notes));
        Assert.Equal((0.0, 0.0), (piece.TileWidth, piece.TileHeight));
        Assert.Equal(new LayoutRect(0, 0, 2000, 2000), piece.Target);
        Assert.Equal(new[] { GuiTexts.LayoutTextureApproximated("tiling") }, notes);
    }

    [Fact]
    public void A_tile_is_at_least_one_gui_unit()
    {
        var piece = Assert.Single(JominiSpriteGeometry.Pieces(Sprite(kind: JominiSpriteKind.Tiled, density: 100), new(0, 0, 50, 50)));
        Assert.Equal((1.0, 1.0), (piece.TileWidth, piece.TileHeight));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void Density_below_one_counts_as_one(double density)
    {
        var pieces = JominiSpriteGeometry.Pieces(Sprite(kind: JominiSpriteKind.CorneredStretched, border: new(8, 8, 8, 8), density: density), new(0, 0, 200, 100));
        Assert.Equal(new LayoutRect(0, 0, 8, 8), pieces[0].Target);
    }

    [Theory]
    [InlineData(JominiFit.CenterCrop, 20, 0, 40, 40)]
    [InlineData(JominiFit.Start, 0, 0, 40, 40)]
    [InlineData(JominiFit.End, 40, 0, 40, 40)]
    [InlineData(JominiFit.Fill, 0, 0, 80, 40)]
    public void Fit_chooses_the_visible_part(JominiFit fit, double x, double y, double w, double h)
    {
        var piece = Assert.Single(JominiSpriteGeometry.Pieces(Sprite(fit: fit, width: 80, height: 40), new(0, 0, 100, 100)));
        Assert.Equal(new LayoutRect(x, y, w, h), piece.Source);
        Assert.Equal(new LayoutRect(0, 0, 100, 100), piece.Target);
    }

    [Fact]
    public void Fit_center_fits_inside_and_centres()
    {
        var piece = Assert.Single(JominiSpriteGeometry.Pieces(Sprite(fit: JominiFit.Center, width: 80, height: 40), new(0, 0, 100, 100)));
        Assert.Equal(new LayoutRect(0, 0, 80, 40), piece.Source);
        Assert.Equal(new LayoutRect(0, 25, 100, 50), piece.Target);
    }

    [Fact]
    public void Fit_crop_respects_a_frame_offset()
    {
        var piece = Assert.Single(JominiSpriteGeometry.Pieces(Sprite(source: new(80, 0, 80, 40), fit: JominiFit.End, width: 160, height: 40), new(0, 0, 100, 100)));
        Assert.Equal(new LayoutRect(120, 0, 40, 40), piece.Source);
    }

    [Theory]
    [InlineData(JominiSpritePlaceholder.Missing)]
    [InlineData(JominiSpritePlaceholder.Dynamic)]
    [InlineData(JominiSpritePlaceholder.UnsupportedFormat)]
    [InlineData(JominiSpritePlaceholder.NotDrawn)]
    public void Placeholder_sprites_give_no_pieces(JominiSpritePlaceholder placeholder)
    {
        Assert.Empty(JominiSpriteGeometry.Pieces(Sprite(placeholder: placeholder), new(0, 0, 100, 100)));
    }

    [Fact]
    public void Empty_box_or_source_gives_no_pieces()
    {
        Assert.Empty(JominiSpriteGeometry.Pieces(Sprite(), new(0, 0, 0, 100)));
        Assert.Empty(JominiSpriteGeometry.Pieces(Sprite(source: new(0, 0, 0, 40)), new(0, 0, 100, 100)));
    }

    [Fact]
    public void Borders_and_tiles_follow_render_scale()
    {
        // scale 0,5 am Kasten (Lauf 6): Ränder und Kachelgrößen werden mitskaliert (nicht gemessen, angenommen wie der Kasten), die Quelle bleibt.
        var cornered = JominiSpriteGeometry.Pieces(Sprite(kind: JominiSpriteKind.CorneredStretched, border: new(8, 8, 8, 8)), new(10, 20, 100, 50), null, 0.5);
        Assert.Equal((new LayoutRect(0, 0, 8, 8), new LayoutRect(10, 20, 4, 4)), (cornered[0].Source, cornered[0].Target));
        Assert.Equal(new LayoutRect(14, 24, 92, 42), cornered[4].Target);

        var tiled = Assert.Single(JominiSpriteGeometry.Pieces(Sprite(kind: JominiSpriteKind.Tiled, density: 2), new(0, 0, 100, 50), null, 0.5));
        Assert.Equal((10.0, 10.0), (tiled.TileWidth, tiled.TileHeight));

        // Faktor 1 (und ungültige Faktoren) wie ohne Angabe.
        Assert.Equal(JominiSpriteGeometry.Pieces(Sprite(kind: JominiSpriteKind.CorneredTiled, border: new(8, 8, 8, 8), density: 2), new(0, 0, 200, 100)),
            JominiSpriteGeometry.Pieces(Sprite(kind: JominiSpriteKind.CorneredTiled, border: new(8, 8, 8, 8), density: 2), new(0, 0, 200, 100), null, 1));
        Assert.Equal(JominiSpriteGeometry.Pieces(Sprite(kind: JominiSpriteKind.Tiled), new(0, 0, 200, 100)),
            JominiSpriteGeometry.Pieces(Sprite(kind: JominiSpriteKind.Tiled), new(0, 0, 200, 100), null, double.NaN));
    }
}
