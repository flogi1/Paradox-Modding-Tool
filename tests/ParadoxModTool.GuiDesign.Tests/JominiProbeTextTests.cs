using System.Text.Json;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Probe-Fenster 5 (Lauf 4, Text) je Spiel: legt <c>pmt_layout_probe_5_&lt;spiel&gt;.gui</c> über den echten Vanilla-Index und den Textdienst der
/// Installation aus und vergleicht mit den im Spiel gemessenen Werten (<c>jomini_probe_text_&lt;spiel&gt;_expected.json</c>). Ohne Installation kehrt der
/// Test zurück. Messregeln und Vorbehalte: Spezifikationsordner <c>layout-probe-notes.md</c>, Abschnitt "Lauf 4".
/// </summary>
public sealed class JominiProbeTextTests
{
    private const double Tolerance = 3;

    /// <summary>Größe des Textfelds der Ausrichtungsfälle 11k–11m in Spielpixeln.</summary>
    private const double AlignBoxWidth = 200;
    private const double AlignBoxHeight = 60;

    /// <summary>
    /// Die Tinte (G) ist ungekorrigiert: der Ableser findet den Ursprung bei (41,41) statt (40,40), alle G-x/y sind 1 zu klein (Lauf 4, Vorbehalt 12).
    /// Marken (B, R) sind korrigiert.
    /// </summary>
    private const double InkOriginBias = 1;

    /// <summary>
    /// Tinte, die nichts über den Text aussagt: 11g ist von der R-Marke überdeckt (der Text läuft aus dem 0 breiten Feld hinaus), die Victoria-3-Tinte in 11i
    /// schließt das farbige Warensymbol ein (CK III und EU V nicht).
    /// </summary>
    private static readonly HashSet<(string Game, string Case)> InkSkipped = new()
    {
        ("ck3", "11g"), ("eu5", "11g"), ("vic3", "11g"), ("vic3", "11i"),
    };

    /// <summary>
    /// Bekannte Abweichungen über der Toleranz; sie werden gemeldet, aber nicht geprüft. Schlüssel (Spiel, Fall, Prüfung), Wert der Grund.
    /// </summary>
    private static readonly Dictionary<(string Game, string Case, string Check), string> KnownDeviations = new()
    {
        // Lauf 4, Regel 7: Fett ist in CK III 7 breiter als die Summe der Vorschübe von GitanLatin-Bold (105 statt 98); die Tinte ist sichtbar
        // kräftiger und breiter. Ungeklärt, EU V und Victoria 3 treffen genau.
        [("ck3", "11h", "R.x")] = "CK III fett +7 (Lauf 4, Regel 7, ungeklärt)",
        [("ck3", "11h", "G.w")] = "CK III fett +7 (Lauf 4, Regel 7, ungeklärt)",

        // Ablesefehler, kein Engine-Fehler: der Grünleser erfasst die Punkte von " ..." in Größe 10 nur teilweise (Tinte 73). Die pixelgenaue
        // Luma-Messung (probe-measure-text.txt) gibt 79, PMT 78; der gekürzte Text ist identisch ("Probe text tha ...").
        [("ck3", "11j", "G.w")] = "Grünleser verliert die kleinen Punkte; Luma-Messung 79",
    };

    /// <summary>Im Spiel gekürzter Text von 11j (Größe 10); die Schnittgrenze ist ungeklärt (Lauf 4, Regel 9), ein Zeichen Unterschied gilt.</summary>
    private static readonly Dictionary<string, string> ElidedInGame = new(StringComparer.Ordinal)
    {
        ["ck3"] = "Probe text tha ...",
        ["eu5"] = "Probe text that i ...",
        ["vic3"] = "Probe text that is ...",
    };

    private readonly ITestOutputHelper _output;

    public JominiProbeTextTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData("ck3")]
    [InlineData("eu5")]
    [InlineData("vic3")]
    public void Probe_text_matches_the_game(string game)
    {
        if (JominiTextWidthTests.GameOf(game) is not { } loaded)
        {
            _output.WriteLine($"{game}: Installation fehlt, übersprungen");
            return;
        }

        var expected = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double[]>>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", $"jomini_probe_text_{game}_expected.json")))!;
        var cases = LayOut(loaded, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", $"pmt_layout_probe_5_{game}.gui")));
        var failures = new List<string>();
        var known = new List<string>();

        void Check(string caseId, string check, double want, double got)
        {
            var line = $"{game} {caseId} {check}: Spiel {want}, PMT {Math.Round(got, 1)}";
            if (Math.Abs(want - got) <= Tolerance)
            {
                _output.WriteLine(line);
                if (KnownDeviations.ContainsKey((game, caseId, check)))
                {
                    // Eine bekannte Abweichung, die jetzt trifft, muss aus der Liste, sonst verdeckte sie eine spätere Verschlechterung.
                    failures.Add($"{line}: trifft jetzt, aus KnownDeviations entfernen");
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

        foreach (var (caseId, letters) in expected)
        {
            Assert.True(cases.TryGetValue("case_" + caseId, out var origin), $"{game}: Fall {caseId} fehlt in der Probe");
            var markers = Markers(origin!);

            // Marken (B, R): Kästen ±3, alle vier Werte.
            foreach (var (letter, want) in letters.Where(l => l.Key != "G"))
            {
                Assert.True(markers.TryGetValue(letter, out var got), $"{game} {caseId}: Marke {letter} fehlt");
                Check(caseId, letter + ".x", want[0], got![0]);
                Check(caseId, letter + ".y", want[1], got[1]);
                Check(caseId, letter + ".w", want[2], got[2]);
                Check(caseId, letter + ".h", want[3], got[3]);
            }

            if (!letters.TryGetValue("G", out var ink) || InkSkipped.Contains((game, caseId)))
            {
                continue;
            }

            var (textBox, visual) = TextOf(origin!);
            var inkX = ink[0] + InkOriginBias;
            var inkY = ink[1] + InkOriginBias;
            if (caseId is "11k" or "11l" or "11m")
            {
                // Ausrichtung: erwartete Tinte = Zeilenanfang im Kasten + Tintenversatz von 11b (gleiche Größe 15, gleicher Anfangsbuchstabe P).
                // Die Formel spiegelt JominiTextRenderer (Editor-Projekt, hier nicht erreichbar): waagrecht floor((W − Zeile)/2) bzw. W − Zeile,
                // senkrecht floor((H − Block)/2) bzw. H − Block.
                var reference = expected["11b"]["G"];
                var (left, top) = LineOrigin(visual, textBox.Width, textBox.Height);
                Check(caseId, "G.x", inkX - (reference[0] + InkOriginBias), textBox.X - origin!.X + left);
                Check(caseId, "G.y", inkY - (reference[1] + InkOriginBias), textBox.Y - origin.Y + top);
            }
            else
            {
                // Tinte gegen die Zeilenausdehnung: Anfang und breiteste Zeile. Die Tintenhöhe ist die Glyphenhöhe, nicht die Zeilenhöhe; die Höhe
                // prüft die B-Marke.
                Check(caseId, "G.x", inkX, textBox.X - origin!.X);
            }

            Check(caseId, "G.w", ink[2], visual.Width);
        }

        // Umbruch (Regel 4): 11e zwei Zeilen, 11f drei, in allen drei Spielen.
        Assert.Equal(2, TextOf(cases["case_11e"]).Visual.Lines.Count);
        Assert.Equal(3, TextOf(cases["case_11f"]).Visual.Lines.Count);

        // fontsize_min + Kürzen (Regel 9): gezeichnet in Größe 10 (nicht in einer Zwischengröße), gekürzt; die Schnittgrenze ist ungeklärt, ein Zeichen
        // Unterschied im behaltenen Anfang gilt.
        var elided = TextOf(cases["case_11j"]).Visual;
        var elidedText = string.Concat(Assert.Single(elided.Lines).Runs.Select(r => r.Text));
        _output.WriteLine($"{game} 11j: \"{elidedText}\" (Spiel \"{ElidedInGame[game]}\"), Größe {elided.Lines[0].Runs[0].FontSize}");
        Assert.True(elided.Elided, $"{game} 11j: nicht gekürzt");
        Assert.All(elided.Lines[0].Runs, run => Assert.Equal(10, run.FontSize));
        if (KeptDistance(Elided11jSource, elidedText, ElidedInGame[game]) > 1)
        {
            failures.Add($"{game} 11j: \"{elidedText}\" statt \"{ElidedInGame[game]}\"");
        }

        foreach (var line in known)
        {
            _output.WriteLine("bekannt: " + line);
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Kept_distance_counts_a_trimmed_space_as_kept()
    {
        // Victoria 3 im Spiel "…that is ..." (behielt 18 oder 19 Zeichen), PMT "…that is t ..." (20): ein Zeichen.
        Assert.Equal(1, KeptDistance(Elided11jSource, "Probe text that is t ...", "Probe text that is ..."));
        Assert.Equal(1, KeptDistance(Elided11jSource, "Probe text that is ...", "Probe text that i ..."));
        Assert.Equal(0, KeptDistance(Elided11jSource, "Probe text tha ...", "Probe text tha ..."));
        Assert.Equal(5, KeptDistance(Elided11jSource, "Probe text tha ...", "Probe tex ..."));
        Assert.Equal(int.MaxValue, KeptDistance(Elided11jSource, "Probe text tha", "Probe text tha ..."));
    }

    /// <summary>Text von 11j vor dem Kürzen.</summary>
    private const string Elided11jSource = "Probe text that is too long";

    /// <summary>
    /// Wie viele Zeichen von <paramref name="source"/> die beiden gekürzten Texte mindestens auseinander liegen. Ein gekürzter Text ist Anfang + " ...", der
    /// Anfang ohne Leerzeichen am Ende; folgte ihm im Original ein Leerzeichen, kann er ein Zeichen mehr behalten haben. Kein " ..." oder ein Anfang, der
    /// nicht zum Original passt: unendlich.
    /// </summary>
    private static int KeptDistance(string source, string ours, string game)
    {
        static (int Min, int Max)? Kept(string source, string elided)
        {
            if (!elided.EndsWith(" ...", StringComparison.Ordinal) || !source.StartsWith(elided[..^4], StringComparison.Ordinal))
            {
                return null;
            }

            var length = elided.Length - 4;
            return (length, length < source.Length && source[length] == ' ' ? length + 1 : length);
        }

        if (Kept(source, ours) is not { } a || Kept(source, game) is not { } b)
        {
            return int.MaxValue;
        }

        return a.Max < b.Min ? b.Min - a.Max : b.Max < a.Min ? a.Min - b.Max : 0;
    }

    /// <summary>Zeilenanfang (links, oben) der ersten Zeile im Kasten, wie JominiTextRenderer ihn setzt.</summary>
    private static (double Left, double Top) LineOrigin(JominiTextVisual visual, double boxWidth, double boxHeight)
    {
        var block = visual.Lines.Sum(l => l.Height);
        var line = visual.Lines[0].Width;
        var left = visual.AlignH switch
        {
            JominiTextAlignH.Center => Math.Floor((boxWidth - line) / 2),
            JominiTextAlignH.Right => Math.Floor(boxWidth - line),
            _ => 0,
        };
        var top = visual.AlignV switch
        {
            JominiTextAlignV.Center => Math.Floor((boxHeight - block) / 2),
            JominiTextAlignV.Bottom => Math.Floor(boxHeight - block),
            _ => 0,
        };
        Assert.Equal(AlignBoxWidth, boxWidth);
        Assert.Equal(AlignBoxHeight, boxHeight);
        return (left, top);
    }

    /// <summary>Das eine Textfeld des Falls und sein gesetzter Text.</summary>
    private static (LayoutBox Box, JominiTextVisual Visual) TextOf(LayoutBox origin)
    {
        var box = Assert.Single(origin.Descendants(), b => b.Visual is JominiTextVisual);
        return (box, (JominiTextVisual)box.Visual!);
    }

    /// <summary>Marken des Falls nach Palettenfarbe (wie <see cref="JominiProbeLayoutTests"/>), relativ zum Fall.</summary>
    private static Dictionary<string, double[]> Markers(LayoutBox origin)
    {
        var found = new Dictionary<string, double[]>(StringComparer.Ordinal);
        foreach (var box in origin.Descendants())
        {
            if (JominiProbeLayoutTests.LetterOf(box.Element) is { } letter && !found.ContainsKey(letter))
            {
                found[letter] = new[] { box.X - origin.X, box.Y - origin.Y, box.Width, box.Height };
            }
        }

        return found;
    }

    /// <summary>Legt das Probe-Fenster auf 1920×1080 aus; Schlüssel ist der Fallname case_&lt;id&gt;.</summary>
    private static Dictionary<string, LayoutBox> LayOut(JominiTextWidthTests.Game game, string probe)
    {
        var doc = GuiParser.Parse(probe);
        var resolver = new GuiResolver(game.Index, game.Rules, doc, game.OverlayPath);
        var engine = new GuiLayoutEngine(game.Rules, game.Textures.SizeOf, game.Text);
        var result = new Dictionary<string, LayoutBox>(StringComparer.Ordinal);
        foreach (var window in doc.Root.Children.Where(c => c.Kind == GuiNodeKind.KeyedBlock && c.Name == "window"))
        {
            var root = engine.Layout(resolver.Resolve(window), new LayoutOptions { ScreenWidth = 1920, ScreenHeight = 1080 });
            foreach (var box in root.Descendants())
            {
                if (box.Element.Name is { } name && name.StartsWith("case_", StringComparison.Ordinal))
                {
                    result[name] = box;
                }
            }
        }

        return result;
    }
}
