using System.Globalization;
using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.GuiDesign.Text;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Probefenster 8 (Lauf 7, Fälle 16a–16y) gegen die Messung im Spiel. Jeder Fall geht als GUI-Text durch Auflösen, <see cref="JominiSprites.Build"/>,
/// <see cref="JominiEffects.Build"/>, <see cref="JominiEffectRecipe.Create"/> und <see cref="JominiEffectComposer.Compose"/>; was der Zeichner anwendet
/// (Deckkraft und Spiegelung des Elements), wendet der Test danach an. Die Texturen sind die des Generators (<c>make_jomini_layout_probe.py</c>,
/// <c>probe_textures</c>), Dichte 1.
/// <para>
/// Sollwerte: <c>docs/superpowers/specs/2026-10-18-jomini-gui-look/probe-readout-run7-ck3.txt</c> (CK III 1.20.1.2). Victoria 3 liest sich in allen
/// Fällen gleich. EU V ebenso, nur das Leuchten-Profil ist um einen Bruchteil eines Pixels verschoben (Quadratkante dort teilweise gedeckt: 0,86 bei
/// x = 65): 0,52 0,39 0,30 0,22 0,13 0,05 statt 0,48 0,40 0,31 0,23 0,14 0,06; keine andere Regel.
/// </para>
/// Toleranz: ±3 je Kanal (8 Bit), Alpha ±0,02. Je 16×16-Teilzelle zählt das Mittel der inneren 8×8 Pixel, wie im Auswerter.
/// </summary>
[Collection(nameof(JominiEffectRules))]
public sealed class JominiEffectProbeTests : IDisposable
{
    private const string Probe = "gfx/interface/pmt_probe";

    private const string Types = "types pmt_fx_types { type pmt_fx_icon = icon { modify_texture = { name = a texture = \"" + Probe
        + "/fx_layer.dds\" blend_mode = normal } } }";

    private static readonly GuiRules Rules = new(new[] { "modify_texture", "glow", "state" }, new[] { "tooltipwidget" }, Array.Empty<string>());

    private static readonly (byte R, byte G, byte B)[] FxBaseColors = { (32, 96, 160), (96, 160, 224), (160, 224, 32), (224, 32, 96) };
    private static readonly (byte R, byte G, byte B)[] FxLayerColors = { (224, 32, 96), (160, 224, 32), (96, 160, 224), (32, 96, 160) };
    private static readonly byte[] FxAlphaV = { 255, 170, 85, 0 };
    private static readonly byte[] FxAlphaA = { 0, 85, 170, 255 };

    /// <summary>Die Probe-Texturen: Name → Breite, Höhe, Pixel (r, g, b, a).</summary>
    private static readonly Dictionary<string, (int W, int H, Func<int, int, (byte R, byte G, byte B, byte A)> At)> TextureDefs = new()
    {
        ["fx_base"] = (64, 64, (x, y) => With(FxBaseColors[x / 16], 255)),
        ["fx_layer"] = (64, 64, (x, y) => With(FxLayerColors[y / 16], 255)),
        ["fx_alpha"] = (64, 64, (x, y) => (FxAlphaV[y / 16], FxAlphaV[y / 16], FxAlphaV[y / 16], FxAlphaA[y / 16])),
        ["fx_square"] = (64, 64, (x, y) => (255, 255, 255, (byte)(x is >= 16 and < 48 && y is >= 16 and < 48 ? 255 : 0))),
        ["fx_arrow"] = (64, 64, (x, y) => x < 32 ? ((byte)255, (byte)0, (byte)0, (byte)255) : ((byte)0, (byte)0, (byte)255, (byte)255)),
        ["white"] = (8, 8, (x, y) => (255, 255, 255, 255)),
    };

    /// <summary>
    /// Gemessen, probe-readout-run7-ck3.txt: je Teilzelle zeilenweise (r1c1, r1c2, …) "r,g,b,alpha" (Farbe ohne Vormultiplikation, aus der Lesung auf
    /// Schwarz und Weiß gelöst).
    /// </summary>
    private static readonly Dictionary<string, string> Readout = new()
    {
        ["16a"] = "224,32,96,1.00 224,32,96,1.00 224,32,96,1.00 224,32,96,1.00 160,224,32,1.00 160,224,32,1.00 160,224,32,1.00 160,224,32,1.00 96,160,224,1.00 96,160,224,1.00 96,160,224,1.00 96,160,224,1.00 32,96,160,1.00 32,96,160,1.00 32,96,160,1.00 32,96,160,1.00",
        ["16b"] = "28,12,60,1.00 84,20,84,1.00 140,28,12,1.00 197,4,36,1.00 20,84,20,1.00 60,140,28,1.00 100,197,4,1.00 140,28,12,1.00 12,60,140,1.00 36,100,197,1.00 60,140,28,1.00 84,20,84,1.00 4,36,100,1.00 12,60,140,1.00 20,84,20,1.00 28,12,60,1.00",
        ["16c"] = "56,24,136,1.00 169,89,216,1.00 232,201,24,1.00 247,8,72,1.00 40,169,89,1.00 120,232,201,1.00 184,247,8,1.00 232,56,24,1.00 24,120,232,1.00 72,184,247,1.00 136,232,56,1.00 216,40,169,1.00 8,72,184,1.00 24,136,232,1.00 89,216,40,1.00 201,24,120,1.00",
        ["16d"] = "255,128,255,1.00 255,192,255,1.00 255,255,128,1.00 255,64,192,1.00 192,255,192,1.00 255,255,255,1.00 255,255,64,1.00 255,255,128,1.00 128,255,255,1.00 192,255,255,1.00 255,255,255,1.00 255,192,255,1.00 64,192,255,1.00 128,255,255,1.00 192,255,192,1.00 255,128,255,1.00",
        ["16e"] = "32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00 32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00 32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00 32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00",
        ["16f"] = "255,110,255,1.00 255,183,255,1.00 255,255,51,1.00 255,37,154,1.00 86,255,183,1.00 255,255,255,1.00 255,255,37,1.00 255,255,110,1.00 51,255,255,1.00 154,255,255,1.00 255,255,255,1.00 255,86,255,1.00 37,154,255,1.00 110,255,255,1.00 183,255,86,1.00 255,51,255,1.00",
        ["16g"] = "224,96,160,1.00 224,160,224,1.00 224,224,96,1.00 224,32,96,1.00 160,224,160,1.00 160,224,224,1.00 160,224,32,1.00 224,224,96,1.00 96,160,224,1.00 96,160,224,1.00 160,224,224,1.00 224,160,224,1.00 32,96,160,1.00 96,160,224,1.00 160,224,160,1.00 224,96,160,1.00",
        ["16h"] = "32,32,96,1.00 96,32,96,1.00 160,32,32,1.00 224,32,96,1.00 32,96,32,1.00 96,160,32,1.00 160,224,32,1.00 160,32,32,1.00 32,96,160,1.00 96,160,224,1.00 96,160,32,1.00 96,32,96,1.00 32,96,160,1.00 32,96,160,1.00 32,96,32,1.00 32,32,96,1.00",
        ["16i"] = "32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00 32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00 32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00 32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00",
        ["16j"] = "128,64,128,1.00 160,96,160,1.00 192,128,64,1.00 224,32,96,1.00 96,160,96,1.00 128,192,128,1.00 160,224,32,1.00 192,128,64,1.00 64,128,192,1.00 96,160,224,1.00 128,192,128,1.00 160,96,160,1.00 32,96,160,1.00 64,128,192,1.00 96,160,96,1.00 128,64,128,1.00",
        ["16k"] = "28,0,0,1.00 84,0,0,1.00 140,0,0,1.00 197,0,0,1.00 20,0,0,1.00 60,0,0,1.00 100,0,0,1.00 140,0,0,1.00 12,0,0,1.00 36,0,0,1.00 60,0,0,1.00 84,0,0,1.00 4,0,0,1.00 12,0,0,1.00 20,0,0,1.00 28,0,0,1.00",
        ["16l"] = "28,24,136,1.00 84,89,216,1.00 115,201,24,1.00 123,8,72,1.00 20,169,89,1.00 60,232,201,1.00 92,247,8,1.00 115,56,24,1.00 12,120,232,1.00 36,184,247,1.00 68,232,56,1.00 108,40,169,1.00 4,72,184,1.00 12,136,232,1.00 44,216,40,1.00 100,24,120,1.00",
        ["16m"] = "225,32,96,0.50 225,32,96,0.50 225,32,96,0.50 225,32,96,0.50 161,225,32,0.50 161,225,32,0.50 161,225,32,0.50 161,225,32,0.50 96,161,225,0.50 96,161,225,0.50 96,161,225,0.50 96,161,225,0.50 32,96,161,0.50 32,96,161,0.50 32,96,161,0.50 32,96,161,0.50",
        ["16n"] = "112,16,175,1.00 112,16,175,1.00 112,16,175,1.00 112,16,175,1.00 80,112,143,1.00 80,112,143,1.00 80,112,143,1.00 80,112,143,1.00 48,80,239,1.00 48,80,239,1.00 48,80,239,1.00 48,80,239,1.00 16,48,207,1.00 16,48,207,1.00 16,48,207,1.00 16,48,207,1.00",
        ["16o"] = "224,32,96,1.00 224,32,96,1.00 224,32,96,1.00 224,32,96,1.00 224,32,96,1.00 224,32,96,1.00 224,32,96,1.00 224,32,96,1.00 160,224,32,1.00 160,224,32,1.00 160,224,32,1.00 160,224,32,1.00 160,224,32,1.00 160,224,32,1.00 160,224,32,1.00 160,224,32,1.00 96,160,224,1.00 96,160,224,1.00 96,160,224,1.00 96,160,224,1.00 96,160,224,1.00 96,160,224,1.00 96,160,224,1.00 96,160,224,1.00 32,96,160,1.00 32,96,160,1.00 32,96,160,1.00 32,96,160,1.00 32,96,160,1.00 32,96,160,1.00 32,96,160,1.00 32,96,160,1.00",
        ["16p"] = "0,0,255,1.00 0,0,255,1.00 255,0,0,1.00 255,0,0,1.00 0,0,255,1.00 0,0,255,1.00 255,0,0,1.00 255,0,0,1.00 0,0,255,1.00 0,0,255,1.00 255,0,0,1.00 255,0,0,1.00 0,0,255,1.00 0,0,255,1.00 255,0,0,1.00 255,0,0,1.00",
        ["16q"] = "0,32,0,1.00 0,32,0,1.00 0,32,0,1.00 0,32,0,1.00 0,224,0,1.00 0,224,0,1.00 0,224,0,1.00 0,224,0,1.00 0,160,0,1.00 0,160,0,1.00 0,160,0,1.00 0,160,0,1.00 0,96,0,1.00 0,96,0,1.00 0,96,0,1.00 0,96,0,1.00",
        ["16u"] = "32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00 32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00 32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00 32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00",
        ["16v"] = "32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00 32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00 32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00 32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00",
        ["16w"] = "32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00 32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00 32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00 32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00",
        ["16x"] = "255,128,255,1.00 255,192,255,1.00 255,255,128,1.00 255,64,192,1.00 192,255,192,1.00 255,255,255,1.00 255,255,64,1.00 255,255,128,1.00 128,255,255,1.00 192,255,255,1.00 255,255,255,1.00 255,192,255,1.00 64,192,255,1.00 128,255,255,1.00 192,255,192,1.00 255,128,255,1.00",
        ["16y"] = "32,96,159,0.50 96,159,223,0.50 159,223,32,0.50 223,32,96,0.50 31,96,160,0.67 96,160,223,0.67 160,223,31,0.67 223,31,96,0.67 32,96,160,0.83 96,160,225,0.83 159,224,32,0.83 224,32,96,0.83 32,96,160,1.00 96,160,224,1.00 160,224,32,1.00 224,32,96,1.00",
    };

    /// <summary>Gemessen, probe-readout-run7-ck3.txt: Alpha des Leuchtens je Einheit ab der rechten Quadratkante (0..19), Mittellinie, 4 Zeilen gemittelt.</summary>
    private static readonly Dictionary<string, string> GlowReadout = new()
    {
        ["16r"] = "0.02 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00",
        ["16s"] = "0.48 0.40 0.31 0.23 0.14 0.06 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00",
        ["16t"] = "0.01 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00 0.00",
    };

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-effect-probe-" + Guid.NewGuid().ToString("N"));
    private readonly GuiTextureLocator _textures;
    private readonly Dictionary<string, GuiImage> _images = new(StringComparer.OrdinalIgnoreCase);

    private readonly ITestOutputHelper _output;

    public JominiEffectProbeTests(ITestOutputHelper output)
    {
        _output = output;
        var install = Path.Combine(_temp, "install");
        foreach (var (name, def) in TextureDefs)
        {
            // Für Größe und Format reicht der Kopf; die Pixel liefert Load.
            var path = TestDds.WriteBgra(Path.Combine(install, "game", Probe.Replace('/', Path.DirectorySeparatorChar), name + ".dds"), def.W, def.H);
            var bgra = new byte[def.W * def.H * 4];
            for (var y = 0; y < def.H; y++)
            {
                for (var x = 0; x < def.W; x++)
                {
                    var (r, g, b, a) = def.At(x, y);
                    var at = (y * def.W + x) * 4;
                    (bgra[at], bgra[at + 1], bgra[at + 2], bgra[at + 3]) = (b, g, r, a);
                }
            }

            _images[Path.GetFullPath(path)] = new GuiImage(def.W, def.H, bgra);
        }

        _textures = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(install, Array.Empty<string>(), null, Array.Empty<string>()));
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    public static TheoryData<string> GridCases() => new()
    {
        "16a", "16b", "16c", "16d", "16e", "16f", "16g", "16h", "16i", "16j", "16k", "16l", "16m", "16n", "16o", "16p", "16q", "16x", "16y",
    };

    [Theory]
    [MemberData(nameof(GridCases))]
    public void Probe_Case_MatchesGame(string id)
    {
        var (_, effects, notes, image) = Run(id);

        Assert.NotNull(effects);
        Assert.DoesNotContain(GuiTexts.LayoutEffectApproximated("blend_mode"), notes);
        AssertCells(id, image);
    }

    [Theory]
    [InlineData("16u")]
    [InlineData("16v")]
    [InlineData("16w")]
    public void Probe_GrayscaleName_HasNoEffect(string id)
    {
        // Gemessen 16u–16w: die Graustufen-Namen ändern ein Icon nicht. Mit GrayscaleApplies = false gibt es keine Effekte, das Bild ist das Grundbild.
        var (_, effects, notes, image) = Run(id);

        Assert.Null(effects);
        Assert.Contains(GuiTexts.LayoutEffectApproximated("effectname"), notes);
        AssertCells(id, image);
    }

    [Theory]
    [InlineData("16r", 0)]
    [InlineData("16s", 6)]
    [InlineData("16t", 0)]
    public void Probe_Glow_MatchesGame(string id, int padding)
    {
        var (_, effects, _, image) = Run(id, out var recipe);

        Assert.NotNull(effects!.Glow);
        Assert.Equal(padding, recipe!.PaddingPixels);
        var expected = GlowReadout[id].Split(' ').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        var colourChecked = false;
        for (var d = 0; d < expected.Length; d++)
        {
            // Rechte Quadratkante bei x = 48 im Icon; Mittellinie: Zeilen 30..33 (Zelle 48..51, Icon bei 18).
            var x = padding + 48 + d;
            var alpha = 0.0;
            var colour = (R: 0.0, G: 0.0, B: 0.0);
            if (x < image.Width)
            {
                for (var y = padding + 30; y < padding + 34; y++)
                {
                    var (r, g, b, a) = Pixel(image, x, y);
                    alpha += a / 255.0 / 4;
                    colour = (colour.R + r / 4.0, colour.G + g / 4.0, colour.B + b / 4.0);
                }
            }

            _output.WriteLine(FormattableString.Invariant($"{id} d={d}: gerechnet {alpha:0.000}, gemessen {expected[d]:0.00}"));
            Assert.True(Math.Abs(alpha - expected[d]) <= 0.02, $"{id} d={d}: Alpha {alpha:0.000}, gemessen {expected[d]:0.00}");
            if (!colourChecked && alpha > 0.02)
            {
                // Gemessen: Farbe an der Kante (255, 0, 0).
                AssertChannel($"{id} d={d}", 0, 255, colour.R);
                AssertChannel($"{id} d={d}", 1, 0, colour.G);
                AssertChannel($"{id} d={d}", 2, 0, colour.B);
                colourChecked = true;
            }
        }
    }

    private (JominiSpriteVisual Sprite, JominiEffectVisual? Effects, List<string> Notes, GuiImage Image) Run(string id) => Run(id, out _);

    /// <summary>Baut den Fall wie die Vorschau und gibt das Bild, wie es im Kasten erscheint (Deckkraft und Spiegelung des Elements angewandt).</summary>
    private (JominiSpriteVisual Sprite, JominiEffectVisual? Effects, List<string> Notes, GuiImage Image) Run(string id, out JominiEffectRecipe? recipe)
    {
        var (gui, width) = Case(id);
        var index = GuiIndexTests.Build((GuiIndexTests.F("in_game/gui/library.gui"), Types));
        var doc = GuiParser.Parse(gui);
        var element = new GuiResolver(index, Rules, doc, @"C:\mod\in_game\gui\edit.gui").Resolve(doc.Root.Children.First(c => c.Kind == GuiNodeKind.KeyedBlock));
        var notes = new List<string>();
        var sprite = JominiSprites.Build(element, _textures, notes);
        Assert.NotNull(sprite);
        Assert.Equal(JominiSpritePlaceholder.None, sprite!.Placeholder);
        var effects = JominiEffects.Build(element, sprite, _textures, notes);

        GuiImage image;
        if (effects is null)
        {
            recipe = null;
            image = new GuiImage(width, 64, new byte[width * 64 * 4]);
            JominiSpriteRaster.Draw(image, Load(sprite.TextureFullPath!)!, sprite, 1, 1, applyMirror: true, JominiSpriteRaster.TintOf(sprite));
            return (sprite, effects, notes, image);
        }

        recipe = JominiEffectRecipe.Create(sprite, effects, width, 64, 1, notes);
        Assert.Equal(1, recipe.PixelsPerUnit);
        image = JominiEffectComposer.Compose(recipe, Load);

        // Zeichner: Deckkraft des Elements (16m) und, wenn er das ganze Bild spiegelt, die Spiegelung des Elements (16p).
        var opacity = Math.Clamp(sprite.Opacity, 0, 1);
        var mirror = JominiEffectRules.ElementMirrorFlipsLayers && sprite.MirrorHorizontal;
        var shown = new byte[image.Bgra.Length];
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var from = (y * image.Width + (mirror ? image.Width - 1 - x : x)) * 4;
                var to = (y * image.Width + x) * 4;
                Array.Copy(image.Bgra, from, shown, to, 4);
                shown[to + 3] = (byte)Math.Round(image.Bgra[from + 3] * opacity, MidpointRounding.AwayFromZero);
            }
        }

        return (sprite, effects, notes, new GuiImage(image.Width, image.Height, shown));
    }

    private GuiImage? Load(string path) => _images.TryGetValue(Path.GetFullPath(path), out var image) ? image : null;

    /// <summary>Der Fall als GUI-Text wie im Generator (ohne die Hintergrund-Zellen) und seine Breite in Einheiten (Höhe immer 64).</summary>
    private static (string Gui, int Width) Case(string id)
    {
        static string Icon(string extra, string texture = "fx_base", int w = 64, string kind = "icon") =>
            $"{kind} = {{ texture = \"{Probe}/{texture}.dds\" size = {{ {w} 64 }} {extra} }}";
        static string Layer(string texture, string extra = "") => $"modify_texture = {{ texture = \"{Probe}/{texture}.dds\" {extra} }}";
        static string Glow(double radius, double alpha) =>
            Icon(FormattableString.Invariant($"glow = {{ glow_radius = {radius} color = {{ 1 0 0 1 }} alpha = {alpha} }}"), "fx_square");

        var modes = new[] { "normal", "multiply", "overlay", "add", "alphamultiply", "colordodge", "lighten", "darken", "mask" };
        if (id[2] is >= 'a' and <= 'i')
        {
            return (Icon(Layer("fx_layer", "blend_mode = " + modes[id[2] - 'a'])), 64);
        }

        return id switch
        {
            "16j" => (Icon(Layer("fx_layer", "blend_mode = normal alpha = 0.5")), 64),
            "16k" => (Icon(Layer("fx_layer", "blend_mode = multiply color = { 1 0 0 1 }")), 64),
            "16l" => (Icon("color = { 0.5 1 1 1 } " + Layer("fx_layer", "blend_mode = overlay")), 64),
            "16m" => (Icon("alpha = 0.5 " + Layer("fx_layer", "blend_mode = normal")), 64),
            "16n" => (Icon(Layer("fx_layer", "blend_mode = normal") + " " + Layer("white", "blend_mode = normal color = { 0 0 1 1 } alpha = 0.5")), 64),
            "16o" => (Icon(Layer("fx_layer", "blend_mode = normal spriteType = corneredstretched spriteborder = { 8 8 }"), w: 128), 128),
            "16p" => (Icon("mirror = horizontal " + Layer("fx_arrow", "blend_mode = normal")), 64),
            "16q" => (Icon(Layer("white", "name = a blend_mode = multiply color = { 0 1 0 1 }"), kind: "pmt_fx_icon"), 64),
            "16r" => (Glow(3, 1), 64),
            "16s" => (Glow(8, 1), 64),
            "16t" => (Glow(3, 0.3), 64),
            "16u" => (Icon("effectname = PdxGuiGrayscale"), 64),
            "16v" => (Icon("effectname = PdxGuiDesaturate"), 64),
            "16w" => (Icon("effectname = GreyedOut"), 64),
            "16x" => (Icon(Layer("fx_layer")), 64),
            "16y" => (Icon(Layer("fx_alpha", "blend_mode = alphamultiply alpha = 0.5")), 64),
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, null),
        };
    }

    private static void AssertCells(string id, GuiImage image)
    {
        var cells = Readout[id].Split(' ');
        var columns = image.Width / 16;
        Assert.Equal(columns * (image.Height / 16), cells.Length);
        for (var i = 0; i < cells.Length; i++)
        {
            var parts = cells[i].Split(',');
            var (row, column) = (i / columns, i % columns);
            double r = 0, g = 0, b = 0, a = 0;
            for (var y = row * 16 + 4; y < row * 16 + 12; y++)
            {
                for (var x = column * 16 + 4; x < column * 16 + 12; x++)
                {
                    var p = Pixel(image, x, y);
                    (r, g, b, a) = (r + p.R / 64.0, g + p.G / 64.0, b + p.B / 64.0, a + p.A / 255.0 / 64);
                }
            }

            var cell = $"{id} r{row + 1}c{column + 1}";
            var expectedAlpha = double.Parse(parts[3], CultureInfo.InvariantCulture);
            Assert.True(Math.Abs(a - expectedAlpha) <= 0.02, $"{cell}: Alpha {a:0.000}, gemessen {expectedAlpha:0.00}");
            AssertChannel(cell, 0, int.Parse(parts[0], CultureInfo.InvariantCulture), r);
            AssertChannel(cell, 1, int.Parse(parts[1], CultureInfo.InvariantCulture), g);
            AssertChannel(cell, 2, int.Parse(parts[2], CultureInfo.InvariantCulture), b);
        }
    }

    private static void AssertChannel(string where, int channel, int expected, double actual) =>
        Assert.True(Math.Abs(actual - expected) <= 3, $"{where} Kanal {channel}: {actual:0.0}, gemessen {expected}");

    private static (byte R, byte G, byte B, byte A) With((byte R, byte G, byte B) colour, byte alpha) => (colour.R, colour.G, colour.B, alpha);

    private static (int R, int G, int B, int A) Pixel(GuiImage image, int x, int y)
    {
        var at = (y * image.Width + x) * 4;
        return (image.Bgra[at + 2], image.Bgra[at + 1], image.Bgra[at], image.Bgra[at + 3]);
    }
}
