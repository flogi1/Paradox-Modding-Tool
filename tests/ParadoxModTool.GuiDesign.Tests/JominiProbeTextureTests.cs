using System.Text.Json;
using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Probefenster 6 (Lauf 5, Texturen) je Spiel: legt <c>TestData/probe6/pmt_layout_probe_6_&lt;spiel&gt;.gui</c> mit den eigenen Probe-Texturen in einer Mod im
/// Temp-Ordner aus und vergleicht mit den im Spiel gemessenen Werten (<c>TestData/probe6/expected_&lt;spiel&gt;.json</c> aus
/// <c>measure_texture_probe.py</c>). Keine Installation nötig. Messregeln: <c>layout-probe-notes.md</c>, Abschnitt "Lauf 5".
/// <list type="bullet">
/// <item>Textur-Fälle (13a–13r, 13y–14b): Die Engine-Stücke (<see cref="JominiSpriteGeometry.Pieces"/>) werden je GUI-Einheit abgetastet (Spiegelung, Strecken,
/// Kacheln) und auf das Texturpixel der Probe-Textur abgebildet. Geprüft werden die Farbgrenzen auf den Mittellinien (±3) und die Farbe jedes gemessenen
/// Farbteils an seiner Mitte (±1 Einheit). So unterscheiden auch Marken, die die Ecken nicht zeigen, die Randregeln (13b).</item>
/// <item>Farbfälle (13s–13x): die über den Hintergrund gemischte Farbe gegen das gemessene Mittel, ±12 je Kanal (sRGB).</item>
/// <item>Natürliche Größe (14c, 14d): Lage der Marken (±3) und Breite der sechs Icons (±1,5; trennt 100 / 96 / 102 bzw. 198 / 200 / 204).</item>
/// </list>
/// </summary>
public sealed class JominiProbeTextureTests : IDisposable
{
    private const double Tolerance = 3;
    private const double ColourTolerance = 12;
    private const double WidthTolerance = 1.5;

    /// <summary>Kleinere Farbteile (JPG-Ränder, Mischpixel) sagen nichts über die Geometrie.</summary>
    private const int MinComponentPixels = 4;

    /// <summary>Lage des Fensters in der Probe (<c>position = { 40 40 }</c>); der Ableser kann den Ursprung um ein Pixel anders finden (CK III, Victoria 3: 41).</summary>
    private const double WindowPosition = 40;

    private static readonly string[] SpriteCases =
    {
        "13a", "13b", "13c", "13d", "13e", "13f", "13g", "13h", "13i", "13j", "13k", "13l", "13m", "13n", "13o", "13p", "13q", "13r",
        "13y", "13z", "14a", "14b",
    };

    private static readonly string[] ColourCases = { "13s", "13t", "13u", "13v", "13w", "13x" };

    private static readonly (string Letter, int R, int G, int B)[] Palette =
    {
        ("R", 255, 0, 0), ("G", 0, 255, 0), ("B", 0, 0, 255), ("Y", 255, 255, 0), ("M", 255, 0, 255), ("C", 0, 255, 255), ("O", 255, 127, 0),
        ("W", 255, 255, 255),
    };

    /// <summary>Bekannte Abweichungen: gemeldet, nicht geprüft. Schlüssel (Spiel, Fall, Prüfung), Wert der Grund.</summary>
    private static readonly Dictionary<(string Game, string Case, string Check), string> KnownDeviations = new()
    {
        // 13t: CK III zeigt { 200 20 0 1 } als (55, 236, 0), vermutlich Byte-Überlauf; die Vorschau begrenzt auf 1 wie Victoria 3 und EU V (Gelb) und nennt
        // Farben über 1 im Hinweis (JominiTextureColor).
        [("ck3", "13t", "colour")] = "CK III Farbe über 1 (Überlauf), Vorschau begrenzt",
    };

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-probe6-" + Guid.NewGuid().ToString("N"));
    private readonly ITestOutputHelper _output;
    private readonly Dictionary<string, GuiImage> _images = new(StringComparer.OrdinalIgnoreCase);

    public JominiProbeTextureTests(ITestOutputHelper output) => _output = output;

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    [Theory]
    [InlineData("ck3")]
    [InlineData("eu5")]
    [InlineData("vic3")]
    public void Probe_textures_match_the_game(string game)
    {
        var probe = Path.Combine(AppContext.BaseDirectory, "TestData", "probe6");
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(probe, $"expected_{game}.json")));
        var bias = expected.RootElement.GetProperty("origin")[0].GetDouble() - WindowPosition;
        var cases = LayOut(game, probe);
        var failures = new List<string>();
        var known = new List<string>();

        void Check(string caseId, string check, bool ok, string line)
        {
            line = $"{game} {caseId} {check}: {line}";
            if (ok)
            {
                _output.WriteLine(line);
                if (KnownDeviations.ContainsKey((game, caseId, check)))
                {
                    failures.Add(line + ": trifft jetzt, aus KnownDeviations entfernen");
                }
            }
            else if (KnownDeviations.TryGetValue((game, caseId, check), out var reason))
            {
                known.Add($"{line} ({reason})");
            }
            else
            {
                failures.Add(line);
            }
        }

        var measuredCases = expected.RootElement.GetProperty("cases").EnumerateArray().ToDictionary(c => c.GetProperty("id").GetString()!);
        foreach (var caseId in SpriteCases)
        {
            var measured = measuredCases[caseId];
            var origin = cases["case_" + caseId];
            var raster = RasterOf(origin);
            var width = origin.Width;
            var height = origin.Height;

            // Farbgrenzen auf den Mittellinien (der Ableser nimmt die Pixelzeile h/2 bzw. -spalte w/2 ab seinem Ursprung).
            var lineH = Enumerable.Range(0, (int)width).Select(x => raster(x + 0.5, Math.Floor(height / 2) + bias + 0.5)).ToList();
            var lineV = Enumerable.Range(0, (int)height).Select(y => raster(Math.Floor(width / 2) + bias + 0.5, y + 0.5)).ToList();
            CompareEdges(caseId, "edges_h", measured.GetProperty("edges_h"), lineH, bias, Check);
            CompareEdges(caseId, "edges_v", measured.GetProperty("edges_v"), lineV, bias, Check);

            // Jedes gemessene Farbteil: an seiner Mitte (±1 Einheit) zeigt die Engine dieselbe Farbe.
            foreach (var colour in measured.GetProperty("components").EnumerateObject())
            {
                foreach (var part in colour.Value.EnumerateArray().Where(p => p.GetProperty("px").GetInt32() >= MinComponentPixels))
                {
                    var cx = part.GetProperty("x").GetDouble() + part.GetProperty("w").GetDouble() / 2 + bias;
                    var cy = part.GetProperty("y").GetDouble() + part.GetProperty("h").GetDouble() / 2 + bias;
                    if (cx < 0 || cy < 0 || cx >= width || cy >= height)
                    {
                        continue;   // außerhalb des Falls (Beschriftung, Nachbarfall im Rand von 12 Einheiten)
                    }

                    var hit = Neighbourhood(cx, cy).Select(p => raster(p.X, p.Y)).ToList();
                    Check(caseId, $"{colour.Name}@{cx:0.#},{cy:0.#}", hit.Contains(colour.Name),
                        $"Spiel {colour.Name}, PMT {string.Join("", hit.Distinct())}");
                }
            }
        }

        foreach (var caseId in ColourCases)
        {
            var origin = cases["case_" + caseId];
            var want = measuredCases[caseId].GetProperty("mean_rgb").EnumerateArray().Select(v => v.GetDouble()).ToArray();
            var got = CompositeColour(origin);
            var ok = Enumerable.Range(0, 3).All(i => Math.Abs(want[i] - got[i]) <= ColourTolerance);
            Check(caseId, "colour", ok, $"Spiel ({string.Join(", ", want)}), PMT ({string.Join(", ", got.Select(v => Math.Round(v)))})");
        }

        foreach (var (caseId, icon) in new[] { ("14c", "Y"), ("14d", "R") })
        {
            var origin = cases["case_" + caseId];
            var markers = origin.Descendants().Where(b => b.Sprite is { } s && s.Texture.EndsWith("white.dds", StringComparison.Ordinal) && b.Width == 4).ToList();
            Assert.Equal(2, markers.Count);
            var edges = measuredCases[caseId].GetProperty("edges_h").EnumerateArray()
                .ToDictionary(e => e.GetProperty("between").GetString()!, e => e.GetProperty("at").GetDouble() + bias);
            var start = edges["B|" + icon];
            var end = edges[icon + "|M"];
            var ourStart = markers[0].X + markers[0].Width - origin.X;
            var ourEnd = markers[1].X - origin.X;
            Check(caseId, "start", Math.Abs(start - ourStart) <= Tolerance, $"Spiel {start}, PMT {ourStart:0.##}");
            Check(caseId, "end", Math.Abs(end - ourEnd) <= Tolerance, $"Spiel {end}, PMT {ourEnd:0.##}");
            Check(caseId, "width", Math.Abs((end - start) - (ourEnd - ourStart)) <= WidthTolerance, $"Spiel {end - start}, PMT {ourEnd - ourStart:0.##}");
        }

        foreach (var line in known)
        {
            _output.WriteLine("bekannt: " + line);
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>Alle Fälle des Fensters (case_&lt;id&gt;), mit den Probe-Texturen in einer Mod im Temp-Ordner (EU V: im Kontextordner main_menu).</summary>
    private Dictionary<string, LayoutBox> LayOut(string game, string probe)
    {
        var contexts = game == "eu5" ? new[] { "in_game", "main_menu", "loading_screen" } : Array.Empty<string>();
        var mod = Path.Combine(_temp, game, "mod");
        var gfx = Path.Combine(game == "eu5" ? Path.Combine(mod, "main_menu") : mod, "gfx", "interface", "pmt_probe");
        Directory.CreateDirectory(gfx);
        foreach (var file in Directory.GetFiles(Path.Combine(probe, "gfx", "interface", "pmt_probe")))
        {
            File.Copy(file, Path.Combine(gfx, Path.GetFileName(file)));
        }

        var install = Path.Combine(_temp, game, "install");
        Directory.CreateDirectory(install);
        var textures = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(install, Array.Empty<string>(), mod, contexts));

        var (resolver, doc) = GuiResolverTests.Setup(File.ReadAllText(Path.Combine(probe, $"pmt_layout_probe_6_{game}.gui")), JominiProbeLayoutTests.Ck3Types);
        var engine = new GuiLayoutEngine(GuiResolverTests.Rules, textures.SizeOf, null, textures);
        var window = doc.Root.Children.Single(c => c.Kind == GuiNodeKind.KeyedBlock && c.Name == "window");
        var root = engine.Layout(resolver.Resolve(window), new LayoutOptions { ScreenWidth = 1920, ScreenHeight = 1080 });
        return root.Descendants()
            .Where(b => b.Element.Name is { } name && name.StartsWith("case_", StringComparison.Ordinal))
            .ToDictionary(b => b.Element.Name!, StringComparer.Ordinal);
    }

    /// <summary>
    /// Farbbuchstabe der Engine an einem Punkt des Falls (Einheiten ab seiner Ecke): das letzte Sprite des Falls (das Probe-Element über dem Hintergrund),
    /// gespiegelt wie im Zeichner (der ganze Kasten), dann das Stück, dessen Ziel den Punkt enthält, gestreckt oder gekachelt aufs Texturpixel.
    /// Außerhalb der Stücke "BACK".
    /// </summary>
    private Func<double, double, string> RasterOf(LayoutBox origin)
    {
        var box = origin.Descendants().Last(b => b.Sprite is { Placeholder: JominiSpritePlaceholder.None });
        var sprite = box.Sprite!;
        var target = new LayoutRect(box.X, box.Y, box.Width, box.Height);
        var pieces = JominiSpriteGeometry.Pieces(sprite, target);
        var image = Image(sprite.TextureFullPath!);
        return (ux, uy) =>
        {
            var x = origin.X + ux;
            var y = origin.Y + uy;
            if (sprite.MirrorHorizontal)
            {
                x = target.X + target.Width - (x - target.X);
            }

            if (sprite.MirrorVertical)
            {
                y = target.Y + target.Height - (y - target.Y);
            }

            foreach (var piece in pieces)
            {
                var t = piece.Target;
                if (x < t.X || y < t.Y || x >= t.X + t.Width || y >= t.Y + t.Height)
                {
                    continue;
                }

                var fx = piece.TileWidth > 0 ? ((x - t.X) % piece.TileWidth) / piece.TileWidth : (x - t.X) / t.Width;
                var fy = piece.TileHeight > 0 ? ((y - t.Y) % piece.TileHeight) / piece.TileHeight : (y - t.Y) / t.Height;
                var sx = Math.Clamp((int)Math.Floor(piece.Source.X + fx * piece.Source.Width), 0, image.Width - 1);
                var sy = Math.Clamp((int)Math.Floor(piece.Source.Y + fy * piece.Source.Height), 0, image.Height - 1);
                var i = (sy * image.Width + sx) * 4;
                return LetterOf(image.Bgra[i + 2] * sprite.Tint.R, image.Bgra[i + 1] * sprite.Tint.G, image.Bgra[i] * sprite.Tint.B);
            }

            return "BACK";
        };
    }

    /// <summary>
    /// Farbe in der Mitte des Falls: das Probe-Sprite (Texturpixel × Färbung) mit seiner Deckkraft über dem Hintergrund-Sprite des Falls (weiß × 0,3),
    /// gemischt in sRGB.
    /// </summary>
    private double[] CompositeColour(LayoutBox origin)
    {
        var sprites = origin.Descendants().Where(b => b.Sprite is { Placeholder: JominiSpritePlaceholder.None }).Select(b => b.Sprite!).ToList();
        var back = Texel(sprites[0]);
        var top = Texel(sprites[^1]);
        var alpha = sprites[^1].Opacity;
        return Enumerable.Range(0, 3).Select(i => top[i] * alpha + back[i] * (1 - alpha)).ToArray();
    }

    /// <summary>Mittleres Texturpixel des Ausschnitts × Färbung (die Probe-Texturen der Farbfälle sind einfarbig).</summary>
    private double[] Texel(JominiSpriteVisual sprite)
    {
        var image = Image(sprite.TextureFullPath!);
        var sx = (int)(sprite.Source.X + sprite.Source.Width / 2);
        var sy = (int)(sprite.Source.Y + sprite.Source.Height / 2);
        var i = (sy * image.Width + sx) * 4;
        return new[] { image.Bgra[i + 2] * sprite.Tint.R, image.Bgra[i + 1] * sprite.Tint.G, image.Bgra[i] * sprite.Tint.B };
    }

    private GuiImage Image(string path)
    {
        if (!_images.TryGetValue(path, out var image))
        {
            image = GuiImageDecoder.TryDecode(path) ?? throw new InvalidOperationException("Probe-Textur nicht lesbar: " + path);
            _images[path] = image;
        }

        return image;
    }

    private static string LetterOf(double r, double g, double b)
    {
        foreach (var (letter, pr, pg, pb) in Palette)
        {
            if (Math.Abs(r - pr) < 1 && Math.Abs(g - pg) < 1 && Math.Abs(b - pb) < 1)
            {
                return letter;
            }
        }

        return "?";
    }

    private static IEnumerable<(double X, double Y)> Neighbourhood(double x, double y)
    {
        foreach (var dy in new[] { 0.0, -1, 1 })
        {
            foreach (var dx in new[] { 0.0, -1, 1 })
            {
                yield return (x + dx, y + dy);
            }
        }
    }

    /// <summary>Farbgrenzen einer Zeile von Abtastwerten (je eine Einheit): (Paar "A|B", Lage der Grenze).</summary>
    private static List<(string Between, double At)> Edges(List<string> samples)
    {
        var edges = new List<(string, double)>();
        for (var i = 1; i < samples.Count; i++)
        {
            if (samples[i] != samples[i - 1])
            {
                edges.Add((samples[i - 1] + "|" + samples[i], i));
            }
        }

        return edges;
    }

    /// <summary>
    /// Vergleicht die Grenzen mit den gemessenen (gleiche Folge der Farbpaare, Lage ±3). Eine gemessene Grenze zwischen derselben Farbe ("O|O") heißt: eine
    /// schmale Marke ist im JPG verloren gegangen und teilt den Lauf. Dann zählt sie nicht, und ein höchstens 4 Einheiten schmaler Lauf der Engine an dieser
    /// Stelle (±3) zwischen zwei Läufen dieser Farbe geht in ihnen auf.
    /// </summary>
    private static void CompareEdges(string caseId, string name, JsonElement measured, List<string> line, double bias,
        Action<string, string, bool, string> check)
    {
        var all = measured.EnumerateArray()
            .Select(e => (Between: e.GetProperty("between").GetString()!, At: e.GetProperty("at").GetDouble() + bias))
            .ToList();
        var lost = all.Where(e => e.Between.Split('|') is [var a, var b] && a == b).ToList();
        var want = all.Except(lost).ToList();
        foreach (var (between, at) in lost)
        {
            var colour = between.Split('|')[0];
            for (var start = Math.Max(1, (int)(at - Tolerance - 4)); start < Math.Min(line.Count - 1, (int)(at + Tolerance)); start++)
            {
                var end = start;
                while (end < line.Count && line[end] == line[start])
                {
                    end++;
                }

                if (line[start] != colour && end - start <= 4 && line[start - 1] == colour && end < line.Count && line[end] == colour)
                {
                    for (var i = start; i < end; i++)
                    {
                        line[i] = colour;
                    }
                }
            }
        }

        var ours = Edges(line);
        var text = $"Spiel [{string.Join(" ", want.Select(e => $"{e.Between}@{e.At}"))}], PMT [{string.Join(" ", ours.Select(e => $"{e.Between}@{e.At}"))}]";
        var ok = want.Count == ours.Count
                 && want.Zip(ours).All(pair => pair.First.Between == pair.Second.Between && Math.Abs(pair.First.At - pair.Second.At) <= Tolerance);
        check(caseId, name, ok, text);
    }
}
