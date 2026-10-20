using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.Editor.Tests;

/// <summary>"Überstand markieren": rote Rahmen für alle überstehenden Kästen nur auf Wunsch, sonst nur am gewählten Element (Florian 2026-10-17).</summary>
public sealed class GuiSchematicCanvasOverflowTests
{
    private static LayoutBox Box(bool outsideParent, bool outsideScreen = false)
    {
        var node = GuiParser.Parse("iconType = { name = \"a\" }").Root.Children[0];
        var element = new ResolvedElement
        {
            Key = "iconType",
            Role = GuiRole.EngineWidget,
            BaseWidget = "iconType",
            TypeChain = new[] { "iconType" },
            Origin = new GuiOrigin(GuiOriginKind.Here, string.Empty, null, 1, node),
        };
        return new LayoutBox { Element = element, OutsideParent = outsideParent, OutsideScreen = outsideScreen };
    }

    [Fact]
    public void Overflow_of_unselected_box_is_hidden_by_default()
    {
        var box = Box(outsideParent: true);

        Assert.False(GuiSchematicCanvas.OverflowShown(box, markAll: false, Array.Empty<LayoutBox>()));
    }

    [Fact]
    public void Overflow_of_selected_box_is_shown_by_default()
    {
        var box = Box(outsideParent: false, outsideScreen: true);

        Assert.True(GuiSchematicCanvas.OverflowShown(box, markAll: false, new[] { box }));
    }

    [Fact]
    public void Mark_all_shows_every_overflow()
    {
        var box = Box(outsideParent: true);

        Assert.True(GuiSchematicCanvas.OverflowShown(box, markAll: true, Array.Empty<LayoutBox>()));
    }

    [Fact]
    public void Box_inside_its_parent_is_never_marked()
    {
        var box = Box(outsideParent: false);

        Assert.False(GuiSchematicCanvas.OverflowShown(box, markAll: true, new[] { box }));
    }

    [Fact]
    public void Clipped_box_is_not_drawn_outside_its_clip()
    {
        Sta.Run(() =>
        {
            // Scrollbereich 0..50, Kind 30..90 ragt hinaus. Szene 90 breit, Fläche 204 → Zoom 2, Rand 12: Weltpunkt x → Pixel 12 + 2x.
            var clipped = Render(new LayoutRect(0, 0, 50, 50));
            var unclipped = Render(null);
            var background = unclipped[(1, 1)];

            // Ohne Beschnitt ist das Kind außerhalb des Scrollbereichs zu sehen (Weltpunkt 70|70), mit Beschnitt bleibt dort Hintergrund.
            Assert.NotEqual(background, unclipped[(152, 152)]);
            Assert.Equal(background, clipped[(152, 152)]);

            // Innerhalb des Beschnitts (Weltpunkt 45|45) wird das Kind wie ohne Beschnitt gezeichnet.
            Assert.Equal(unclipped[(102, 102)], clipped[(102, 102)]);
        });
    }

    [Fact]
    public void Overflow_and_selection_outlines_stay_inside_the_clip()
    {
        Sta.Run(() =>
        {
            // Rechte Kante des Kindes (Weltpunkt x 90 → Pixel 192) unterhalb des Scrollbereichs (Weltpunkt y 70 → Pixel 152): außerhalb des Beschnitts.
            var plain = Render(new LayoutRect(0, 0, 50, 50));
            var background = plain[(1, 1)];
            foreach (var (markAll, select) in new[] { (true, false), (false, true) })
            {
                var outlined = Render(new LayoutRect(0, 0, 50, 50), markAll, select);
                Assert.All(new[] { (191, 152), (192, 152), (193, 152) }, point => Assert.Equal(background, outlined[point]));

                // Linke Kante (Pixel 72) auf Höhe 40 liegt im Beschnitt: dort ist der Umriss zu sehen.
                Assert.NotEqual(plain[(72, 92)], outlined[(72, 92)]);
            }
        });
    }

    [Fact]
    public void Bounds_of_clipped_jomini_text_stay_inside_the_clip()
    {
        Sta.Run(() =>
        {
            // "Rahmen zeigen" an einem Jomini-Textkasten im Scrollbereich: die rechte Kante (Pixel 192) unterhalb des Scrollbereichs bleibt Hintergrund.
            var background = Render(new LayoutRect(0, 0, 50, 50))[(1, 1)];
            foreach (var uncertain in new[] { false, true })
            {
                var framed = Render(new LayoutRect(0, 0, 50, 50), jominiText: true, showBounds: true, uncertain: uncertain);
                Assert.All(new[] { (191, 152), (192, 152), (193, 152) }, point => Assert.Equal(background, framed[point]));
            }
        });
    }

    private static readonly (int X, int Y)[] Probes = { (1, 1), (152, 152), (102, 102), (191, 152), (192, 152), (193, 152), (72, 92) };

    /// <summary>
    /// Zeichnet Scrollbereich und Kind auf 204×204 Pixel und liefert die Farbe (BGRA) je Pixel; wahlweise mit "Überstand markieren"
    /// (<paramref name="markAll"/>) oder gewähltem Kind (<paramref name="selectChild"/>).
    /// </summary>
    private static Dictionary<(int X, int Y), int> Render(
        LayoutRect? clip,
        bool markAll = false,
        bool selectChild = false,
        bool jominiText = false,
        bool showBounds = false,
        bool uncertain = false)
    {
        var root = Box(outsideParent: false);
        root.Width = 50;
        root.Height = 50;
        var child = Box(outsideParent: true);
        child.X = 30;
        child.Y = 30;
        child.Width = 60;
        child.Height = 60;
        child.Clip = clip;
        if (uncertain)
        {
            child.Notes.Add("angenähert");
        }

        if (jominiText)
        {
            // Leerer gesetzter Text: der Zeichner zeichnet nur den Kasten, keine Glyphen.
            child.Visual = new JominiTextVisual(
                Array.Empty<JominiTextLine>(), 0, 0,
                JominiTextAlignH.Left, JominiTextAlignV.Center,
                false, false, Array.Empty<string>(), Array.Empty<string>());
        }

        root.Children.Add(child);

        var canvas = new GuiSchematicCanvas();
        canvas.SetScene(new GuiSchematicScene(root, _ => false), keepView: false);
        canvas.MarkAllOverflow = markAll;
        canvas.ShowBounds = showBounds;
        if (selectChild)
        {
            canvas.SetSelection(child.Element.Origin.Node);
        }

        canvas.Measure(new System.Windows.Size(204, 204));
        canvas.Arrange(new System.Windows.Rect(0, 0, 204, 204));
        canvas.UpdateLayout();
        var bitmap = new RenderTargetBitmap(204, 204, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(canvas);
        var pixels = new int[204 * 204];
        bitmap.CopyPixels(pixels, 204 * 4, 0);
        var result = new Dictionary<(int X, int Y), int>();
        foreach (var point in Probes)
        {
            result[point] = pixels[point.Item2 * 204 + point.Item1];
        }

        return result;
    }
}
