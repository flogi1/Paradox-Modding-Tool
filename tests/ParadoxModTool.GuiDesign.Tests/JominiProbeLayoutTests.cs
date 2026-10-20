using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.GuiDesign.Text;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Vergleicht die Jomini-Auslegung mit den im Spiel gemessenen Kästen (CK III, Probe-Fenster 1 bis 4).
/// Jeder Fall bleibt übersprungen, bis ihn eine spätere Aufgabe in <see cref="Implemented"/> einträgt.
/// </summary>
public class JominiProbeLayoutTests
{
    // Ck3Types: was die Probe aus Vanilla braucht (CK III gui/shared/windows.gui:597, gui/preload/defaults.gui:299). Dazu für die Textfälle Kopien von
    // type textbox (gui/preload/defaults.gui, ohne "using = tooltip_above"), type text_single (labels.gui) und den Vorlagen Font_Type_Standard und
    // Font_Size_Small (fonts.gui).
    internal const string Ck3Types =
        "types P { type expand = hbox { layoutpolicy_horizontal = growing layoutpolicy_vertical = growing } type flowcontainer = flowcontainer { ignoreinvisible = no } " +
        "type textbox = textbox { gfxtype = textboxgfx shaderfile = \"gfx/FX/pdxgui_default.shader\" font = \"StandardGameFont\" fontsize = 15 fontcolor = { 0.61 0.6 0.56 1.0 } text_selectable = no } " +
        "type text_single = textbox { using = Font_Type_Standard using = Font_Size_Small autoresize = yes elide = right default_format = \"#medium\" alwaystransparent = no fontsize_min = 12 } } " +
        "template Font_Size_Small { fontsize = 15 size = { 0 23 } } " +
        "template Font_Type_Standard { font = StandardGameFont }";

    /// <summary>Fälle, die die Engine schon wie das Spiel auslegt; jede Aufgabe trägt hier ihre Fälle ein.</summary>
    internal static readonly HashSet<string> Implemented = new(StringComparer.Ordinal)
    {
        // Aufgabe 2: Hauptachse von hbox/vbox.
        "1a", "1b", "1c", "1d", "1e", "1f", "1g", "1i", "1j", "1k", "1l", "2a", "2b", "2c",
        "3a", "3b", "3c", "3d", "3e", "3l", "3m", "9a", "9b", "9c", "9l",
        // Aufgabe 3: Querachse, unsichtbare Kinder, flowcontainer, set_parent_size_to_minimum.
        "1h", "3g", "3h", "3i", "3j", "3k", "3n", "3o", "4a", "4b", "4c", "4d", "10b",
        // Aufgabe 4: Anker, widget/container ohne size, -1 in size (6d ohne Sollwerte, siehe Minus_one_means_unset).
        "5a", "5b", "5c", "5d", "6b", "6c", "6e",
        // Aufgabe 5: background (9d, 9h und 9j zeigen ihre Größe über einen background; 9e, 9f, 9g und 9k sind Boxen in Boxen mit background).
        "8a", "8b", "8c", "8d", "9d", "9e", "9f", "9g", "9h", "9j", "9k",
        // Plan 2, Aufgabe 8: Text mit den CK-III-Schriften (nur mit Installation, siehe TextCases), resizeparent (10a).
        "7a", "7b", "7c", "7d", "7e", "7f", "7g", "7h", "7i", "10a",
    };

    /// <summary>Fälle, die die CK-III-Schriften brauchen; ohne Installation kehren sie zurück.</summary>
    private static readonly HashSet<string> TextCases = new(StringComparer.Ordinal) { "7a", "7b", "7c", "7d", "7e", "7f", "7g", "7h", "7i", "10a" };

    /// <summary>
    /// Buchstaben, die Text-Tinte sind, nie ein Kasten: die allgemeine Prüfung überspringt sie. 7g und 7h prüft <see cref="Probe_text_ink_matches"/> am
    /// Textbild.
    /// </summary>
    private static readonly HashSet<(string Case, string Letter)> InkLetters =
        new[] { "7a", "7b", "7c", "7d", "7e", "7f", "7g", "7h", "7i", "10a" }.Select(id => (id, "G")).ToHashSet();

    // Buchstaben, deren CK-III-Ablesung verfälscht ist: In 3g liegt G auf dem nach unten verschobenen G aus 3f (Fehlerfall,
    // nicht nachgebaut); dort zählen nur R und B.
    private static readonly HashSet<(string Case, string Letter)> Contaminated = new() { ("3g", "G") };

    // Fälle, die im Spiel Fehler melden (3f, 6a; Aufgabe 3 prüft deren Notizen).
    private static readonly HashSet<string> Excluded = new(StringComparer.Ordinal) { "3f", "6a" };

    // Palette der Probe (make_jomini_layout_probe.py), RGB 0..1.
    private static readonly (string Letter, double R, double G, double B)[] Palette =
    {
        ("R", 1, 0, 0), ("G", 0, 1, 0), ("B", 0, 0, 1), ("Y", 1, 1, 0), ("M", 1, 0, 1), ("C", 0, 1, 1),
    };

    private const double Tolerance = 3;

    private static readonly Lazy<Dictionary<string, Dictionary<string, double[]>>> Expected = new(() =>
        JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double[]>>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "jomini_probe_ck3_expected.json")))!);

    // Alle vier Probe-Fenster auf 1920x1080 ausgelegt; Schlüssel ist der Fallname case_<id>.
    private static readonly Lazy<Dictionary<string, LayoutBox>> CaseBoxes = new(LayOutWindows);

    // Fälle ohne Sollwerte (6d, 6f–6k: nur Textur- oder "-"-Ablesung) prüfen nichts; ihre Regeln halten Unit-Tests aus Aufgabe 4 fest.
    public static IEnumerable<object[]> Cases() =>
        Expected.Value.Where(c => !Excluded.Contains(c.Key) && c.Value.Count > 0).Select(c => new object[] { c.Key });

    [Theory, MemberData(nameof(Cases))]
    public void Probe_case_matches_the_game(string caseId)
    {
        if (!Implemented.Contains(caseId))
        {
            return;   // Fall gehört zu einer späteren Aufgabe
        }

        if (TextCases.Contains(caseId) && !VanillaGui.For("ck3").Exists)
        {
            return;   // Textfälle brauchen die CK-III-Schriften
        }

        var origin = CaseBoxes.Value["case_" + caseId];
        var found = new Dictionary<string, double[]>(StringComparer.Ordinal);
        foreach (var box in origin.Descendants())
        {
            // Jedes Element mit Palettenfarbe ist ein "Buchstabe"; ein background zählt mit seiner eigenen Farbe.
            var letter = LetterOf(box.Element);
            if (letter is not null && !found.ContainsKey(letter))
            {
                found[letter] = new[] { box.X - origin.X, box.Y - origin.Y, box.Width, box.Height };
            }
        }

        foreach (var (letter, want) in Expected.Value[caseId])
        {
            if (Contaminated.Contains((caseId, letter)) || InkLetters.Contains((caseId, letter)))
            {
                continue;
            }

            Assert.True(found.TryGetValue(letter, out var got), $"Fall {caseId}: Element {letter} fehlt");
            for (var i = 0; i < 4; i++)
            {
                Assert.True(
                    Math.Abs(want[i] - got![i]) <= Tolerance,
                    $"Fall {caseId}, {letter}: erwartet [{string.Join(", ", want)}], gemessen [{string.Join(", ", got!.Select(v => Math.Round(v, 1)))}]");
            }
        }
    }

    [Fact]
    public void Probe_text_ink_matches()
    {
        if (!VanillaGui.For("ck3").Exists)
        {
            return;
        }

        // 7g: drei Zeilen, die breiteste so breit wie die gemessene Tinte (±3; die Tinte ist ungekorrigiert).
        var wrapped = TextVisual("7g");
        var widest = wrapped.Lines.Max(l => l.Width);
        Assert.Equal(3, wrapped.Lines.Count);
        Assert.True(Math.Abs(widest - Expected.Value["7g"]["G"][2]) <= Tolerance, $"7g: breiteste Zeile {widest}");

        // 7h: gekürzt auf "Probe tex ..." in einem Kasten von 80.
        var elided = TextVisual("7h");
        Assert.Equal("Probe tex ...", string.Concat(Assert.Single(elided.Lines).Runs.Select(r => r.Text)));
        Assert.True(Math.Abs(elided.Width - Expected.Value["7h"]["G"][2]) <= Tolerance, $"7h: Breite {elided.Width}");
    }

    private static JominiTextVisual TextVisual(string caseId) =>
        CaseBoxes.Value["case_" + caseId].Descendants().Select(b => b.Visual).OfType<JominiTextVisual>().Single();

    [Fact]
    public void All_probe_cases_are_implemented()
    {
        // Jeder geprüfte Fall muss eingetragen sein, sonst kehrte Probe_case_matches_the_game für ihn stillschweigend zurück.
        var missing = Cases().Select(c => (string)c[0]).Where(id => !Implemented.Contains(id)).ToList();
        Assert.True(missing.Count == 0, "Nicht umgesetzt: " + string.Join(", ", missing));
    }

    [Fact]
    public void Expected_data_covers_the_probe_cases()
    {
        var ids = Expected.Value.Keys;
        Assert.Contains("1a", ids);
        Assert.Contains("3o", ids);
        Assert.Contains("9l", ids);
        Assert.Contains("10b", ids);
        Assert.DoesNotContain("9i", ids);
        Assert.Equal(new double[] { 50, 6, 125, 20 }, Expected.Value["1a"]["G"]);
    }

    [Fact]
    public void Probe_file_holds_a_widget_for_every_expected_case()
    {
        // Wird nur ausgelegt, wenn sich ein Fall an dieser Prüfung beteiligt; hier genügt das Auflösen der Fenster.
        var boxes = CaseBoxes.Value;
        foreach (var id in Expected.Value.Keys)
        {
            Assert.True(boxes.ContainsKey("case_" + id), $"Fall {id} fehlt in pmt_layout_probe_ck3.gui");
        }
    }

    private static Dictionary<string, LayoutBox> LayOutWindows()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "pmt_layout_probe_ck3.gui"));
        var (resolver, doc) = GuiResolverTests.Setup(text, Ck3Types);
        var engine = new GuiLayoutEngine(GuiResolverTests.Rules, TextureSize, Ck3TextService(text));
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

    /// <summary>Textdienst mit den Schriften der CK-III-Installation und einem Index aus Probe-Datei und <see cref="Ck3Types"/>; null ohne Installation.</summary>
    private static JominiTextService? Ck3TextService(string probe)
    {
        var install = VanillaGui.For("ck3");
        if (!install.Exists)
        {
            return null;
        }

        var index = GuiIndexTests.Build((GuiIndexTests.F("in_game/gui/library.gui"), Ck3Types), (GuiIndexTests.F("in_game/gui/pmt_layout_probe.gui"), probe));
        var textures = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(install.InstallDirectory, Array.Empty<string>(), null, Array.Empty<string>()));
        return JominiTextService.Build(new GuiWorkspaceOptions(install.InstallDirectory, null, Array.Empty<string>(), GuiResolverTests.Rules, null), index, textures);
    }

    // Texturgrößen der Probe: white.dds 10x10, icon_skills.dds 360x60.
    private static (int Width, int Height)? TextureSize(string path)
    {
        if (path.EndsWith("white.dds", StringComparison.OrdinalIgnoreCase))
        {
            return (10, 10);
        }

        return path.EndsWith("icon_skills.dds", StringComparison.OrdinalIgnoreCase) ? (360, 60) : null;
    }

    internal static string? LetterOf(ResolvedElement element)
    {
        if (element.Property("color")?.Value is not { } value)
        {
            return null;
        }

        var numbers = Regex.Matches(value, @"-?\d+(\.\d+)?").Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
        if (numbers.Count < 3)
        {
            return null;
        }

        foreach (var (letter, r, g, b) in Palette)
        {
            if (Math.Abs(numbers[0] - r) < 0.02 && Math.Abs(numbers[1] - g) < 0.02 && Math.Abs(numbers[2] - b) < 0.02)
            {
                return letter;
            }
        }

        return null;
    }
}
