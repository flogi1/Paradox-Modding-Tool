using System.Text.Json;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Probefenster 7 (Lauf 6, Zustand <c>_show</c> und <c>scale</c>) je Spiel gegen die im Spiel gemessenen Werte (<c>TestData/probe7/expected_&lt;spiel&gt;.json</c> aus
/// <c>measure_texture_probe.py --window 7</c>). Messregeln: <c>layout-probe-notes.md</c>, Abschnitt "Lauf 6".
/// <list type="bullet">
/// <item>Kästen (15a, 15b, 15d–15f): <c>TestData/probe7/pmt_layout_probe_7_&lt;spiel&gt;.gui</c> mit den Probe-Texturen aus <c>probe6/gfx</c> in einer Mod im Temp-Ordner,
/// ohne Installation. Je Farbe (R, G, B) der Engine-Kasten gegen den gemessenen Farbkasten: Mitte mit dem Ursprungsversatz des Ablesers (die Mitte hängt
/// nicht an den im JPG je Kante etwa 1 kürzeren Rändern), Breite und Höhe wie abgelesen, alles ±3. 15d und 15f: R sichtbar (Deckkraft 1, kein Hinweis
/// "alpha 0").</item>
/// <item>Text (15c, 15g): über den Vanilla-Index und den Textdienst der Installation (ohne Installation übersprungen). Mitte der R-Marke (Fach des Textfelds in
/// der hbox) und Breite der Tinte (gezeichnete Textbreite) ±3. Die frühere Annahme "Fach unverändert" läge in EU V und Victoria 3 um 5 bzw. 6 daneben.</item>
/// </list>
/// </summary>
public sealed class JominiProbeScaleTests : IDisposable
{
    private const double Tolerance = 3;

    /// <summary>Lage des Fensters in der Probe (<c>position = { 40 40 }</c>); der Ableser findet den Ursprung in den JPGs bei 41.</summary>
    private const double WindowPosition = 40;

    private static readonly string[] BoxCases = { "15a", "15b", "15d", "15e", "15f" };

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-probe7-" + Guid.NewGuid().ToString("N"));
    private readonly ITestOutputHelper _output;

    public JominiProbeScaleTests(ITestOutputHelper output) => _output = output;

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private static string ProbeFolder => Path.Combine(AppContext.BaseDirectory, "TestData", "probe7");

    private static JsonDocument Expected(string game) => JsonDocument.Parse(File.ReadAllText(Path.Combine(ProbeFolder, $"expected_{game}.json")));

    [Theory]
    [InlineData("ck3")]
    [InlineData("eu5")]
    [InlineData("vic3")]
    public void Probe_scale_and_show_boxes_match_the_game(string game)
    {
        using var expected = Expected(game);
        var bias = expected.RootElement.GetProperty("origin")[0].GetDouble() - WindowPosition;
        var measuredCases = expected.RootElement.GetProperty("cases").EnumerateArray().ToDictionary(c => c.GetProperty("id").GetString()!);
        var cases = LayOutWithProbeTextures(game);
        var failures = new List<string>();

        foreach (var caseId in BoxCases)
        {
            var origin = cases["case_" + caseId];
            var engine = ColouredBoxes(origin);
            foreach (var measured in measuredCases[caseId].GetProperty("boxes").EnumerateObject())
            {
                var box = measured.Value;
                var (mw, mh) = (box.GetProperty("w").GetDouble(), box.GetProperty("h").GetDouble());
                var (mx, my) = (box.GetProperty("x").GetDouble() + bias + mw / 2, box.GetProperty("y").GetDouble() + bias + mh / 2);
                if (!engine.TryGetValue(measured.Name, out var found))
                {
                    failures.Add($"{game} {caseId} {measured.Name}: kein Engine-Kasten");
                    continue;
                }

                var (ew, eh) = (found.Width, found.Height);
                var (ex, ey) = (found.X - origin.X + ew / 2, found.Y - origin.Y + eh / 2);
                var line = $"{game} {caseId} {measured.Name}: Engine Mitte {ex:0.#}/{ey:0.#} {ew:0.#}x{eh:0.#}, Spiel Mitte {mx:0.#}/{my:0.#} {mw:0.#}x{mh:0.#}";
                _output.WriteLine(line);
                if (Math.Abs(ex - mx) > Tolerance || Math.Abs(ey - my) > Tolerance || Math.Abs(ew - mw) > Tolerance || Math.Abs(eh - mh) > Tolerance)
                {
                    failures.Add(line);
                }
            }
        }

        // 15d, 15f: _show macht R sichtbar (im Spiel deckend rot).
        foreach (var caseId in new[] { "15d", "15f" })
        {
            var red = ColouredBoxes(cases["case_" + caseId])["R"];
            if (red.Sprite!.Opacity < 0.99 || red.Notes.Contains(GuiTexts.LayoutAlphaInvisible))
            {
                failures.Add($"{game} {caseId}: R unsichtbar (Deckkraft {red.Sprite.Opacity})");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Theory]
    [InlineData("ck3")]
    [InlineData("eu5")]
    [InlineData("vic3")]
    public void Probe_scaled_text_matches_the_game(string game)
    {
        if (JominiTextWidthTests.GameOf(game) is not { } loaded)
        {
            _output.WriteLine($"{game}: Installation fehlt, übersprungen");
            return;
        }

        using var expected = Expected(game);
        var bias = expected.RootElement.GetProperty("origin")[0].GetDouble() - WindowPosition;
        var measuredCases = expected.RootElement.GetProperty("cases").EnumerateArray().ToDictionary(c => c.GetProperty("id").GetString()!);
        var doc = GuiParser.Parse(File.ReadAllText(Path.Combine(ProbeFolder, $"pmt_layout_probe_7_{game}.gui")));
        var resolver = new GuiResolver(loaded.Index, loaded.Rules, doc, loaded.OverlayPath);
        var engine = new GuiLayoutEngine(loaded.Rules, loaded.Textures.SizeOf, loaded.Text);
        var window = doc.Root.Children.Single(c => c.Kind == GuiNodeKind.KeyedBlock && c.Name == "window");
        var cases = CasesOf(engine.Layout(resolver.Resolve(window), new LayoutOptions { ScreenWidth = 1920, ScreenHeight = 1080 }));
        var failures = new List<string>();

        foreach (var caseId in new[] { "15c", "15g" })
        {
            var origin = cases["case_" + caseId];
            var hbox = origin.Children.Single(c => c.Element.BaseWidget.EndsWith("hbox", StringComparison.Ordinal));
            var text = hbox.Children[1];
            var marker = hbox.Children[2];
            var measured = measuredCases[caseId];

            // Fach: Mitte der R-Marke hinter dem Textfeld.
            var markerX = marker.X - origin.X + marker.Width / 2;
            var measuredBox = measured.GetProperty("boxes").GetProperty("R");
            var measuredMarker = measuredBox.GetProperty("x").GetDouble() + bias + measuredBox.GetProperty("w").GetDouble() / 2;
            var line = $"{game} {caseId} R Mitte: Engine {markerX:0.#}, Spiel {measuredMarker:0.#}";
            _output.WriteLine(line);
            if (Math.Abs(markerX - measuredMarker) > Tolerance)
            {
                failures.Add(line);
            }

            // Tinte: gezeichnete Textbreite (Victoria 3 zeichnet hellgrau, dort die helle Tinte).
            var ink = measured.GetProperty("ink").ValueKind == JsonValueKind.Object ? measured.GetProperty("ink") : measured.GetProperty("ink_light");
            var drawnWidth = ((JominiTextVisual)text.Visual!).Width * text.RenderScale;
            line = $"{game} {caseId} Tinte: Engine {drawnWidth:0.#}, Spiel {ink.GetProperty("w").GetDouble():0.#}";
            _output.WriteLine(line);
            if (Math.Abs(drawnWidth - ink.GetProperty("w").GetDouble()) > Tolerance)
            {
                failures.Add(line);
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>Fenster 7 mit den Probe-Texturen (aus <c>probe6/gfx</c>) in einer Mod im Temp-Ordner (EU V: im Kontextordner main_menu).</summary>
    private Dictionary<string, LayoutBox> LayOutWithProbeTextures(string game)
    {
        var contexts = game == "eu5" ? new[] { "in_game", "main_menu", "loading_screen" } : Array.Empty<string>();
        var mod = Path.Combine(_temp, game, "mod");
        var gfx = Path.Combine(game == "eu5" ? Path.Combine(mod, "main_menu") : mod, "gfx", "interface", "pmt_probe");
        Directory.CreateDirectory(gfx);
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "TestData", "probe6", "gfx", "interface", "pmt_probe")))
        {
            File.Copy(file, Path.Combine(gfx, Path.GetFileName(file)));
        }

        var install = Path.Combine(_temp, game, "install");
        Directory.CreateDirectory(install);
        var textures = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(install, Array.Empty<string>(), mod, contexts));
        var (resolver, doc) = GuiResolverTests.Setup(File.ReadAllText(Path.Combine(ProbeFolder, $"pmt_layout_probe_7_{game}.gui")), JominiProbeLayoutTests.Ck3Types);
        var engine = new GuiLayoutEngine(GuiResolverTests.Rules, textures.SizeOf, null, textures);
        var window = doc.Root.Children.Single(c => c.Kind == GuiNodeKind.KeyedBlock && c.Name == "window");
        return CasesOf(engine.Layout(resolver.Resolve(window), new LayoutOptions { ScreenWidth = 1920, ScreenHeight = 1080 }));
    }

    private static Dictionary<string, LayoutBox> CasesOf(LayoutBox root) =>
        root.Descendants()
            .Where(b => b.Element.Name is { } name && name.StartsWith("case_", StringComparison.Ordinal))
            .ToDictionary(b => b.Element.Name!, StringComparer.Ordinal);

    /// <summary>Kästen der rein farbigen Probe-Icons im Fall nach Farbbuchstabe (R, G, B); der graue Hintergrund des Falls zählt nicht.</summary>
    private static Dictionary<string, LayoutBox> ColouredBoxes(LayoutBox origin)
    {
        var result = new Dictionary<string, LayoutBox>(StringComparer.Ordinal);
        foreach (var box in origin.Descendants().Where(b => b.Sprite is not null))
        {
            var tint = box.Sprite!.Tint;
            var letter = (tint.R, tint.G, tint.B) switch
            {
                (1, 0, 0) => "R",
                (0, 1, 0) => "G",
                (0, 0, 1) => "B",
                _ => null,
            };
            if (letter is not null)
            {
                result.TryAdd(letter, box);
            }
        }

        return result;
    }
}
