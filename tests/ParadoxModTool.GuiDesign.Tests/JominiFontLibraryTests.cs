using System.Windows.Media;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Text;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Schriften der Jomini-Spiele aus <c>fonts/*.font</c> und Glyphenbreiten. Die synthetischen Tests kopieren Windows-Schriften zur Laufzeit in einen
/// Temp-Ordner (keine Schriftdateien im Repository); der Test mit den gemessenen Breiten liest die echten Installationen.
/// </summary>
public sealed class JominiFontLibraryTests : IDisposable
{
    private static readonly string WindowsFonts = JominiTestFonts.WindowsFonts;

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-jominifont-" + Guid.NewGuid().ToString("N"));
    private readonly ITestOutputHelper _output;

    public JominiFontLibraryTests(ITestOutputHelper output) => _output = output;

    public void Dispose() => JominiTestFonts.TryDelete(_temp);

    private string Root(string name, string fontText, params string[] windowsFonts) => JominiTestFonts.Root(_temp, name, fontText, windowsFonts);

    private static string Copy(string root, string file) => JominiTestFonts.Copy(root, file);

    private static string FontFiles(string name, params (string Languages, string Files)[] groups) => JominiTestFonts.FontFiles(name, groups);

    private static string Font(string name, params (string Style, string FontFiles)[] styles) => JominiTestFonts.Font(name, styles);

    private static GlyphTypeface Glyphs(string path) => new(new Uri(path));

    private static double Rounded(double advance, double size) => Math.Round(advance * size, MidpointRounding.AwayFromZero);

    [Fact]
    public void Face_follows_font_style_fontfiles_and_language_group()
    {
        var root = Root("r1",
            "# Kommentar\n" +
            FontFiles("Test-Regular", ("\"l_english\" \"l_german\"", "\"/fonts/a/arial.ttf\"")) +
            FontFiles("Test-Bold", ("\"l_english\"", "\"fonts/a/arial.ttf\""), ("\"l_french\" \"l_german\"", "\"fonts/a/arialbd.ttf\"")) +
            FontFiles("Test-BoldItalic", ("\"l_english\" \"l_german\"", "\"fonts/a/segoeui.ttf\"")) +
            Font("StandardGameFont",
                ("regular", "Test-Regular"),
                ("semibold", "Test-Bold"),
                ("bold", "Test-Bold"),
                ("bold|italic", "Test-BoldItalic")),
            "arial.ttf", "arialbd.ttf", "segoeui.ttf");

        var library = JominiFontLibrary.Load(new[] { root });

        Assert.True(library.Defines("StandardGameFont"));
        Assert.False(library.Defines("Test-Bold"));

        var bold = library.Face("StandardGameFont", JominiFontStyle.Bold, "l_german");
        Assert.NotNull(bold);
        Assert.Equal(new[] { Copy(root, "arialbd.ttf") }, bold!.Files);
        Assert.False(bold.SimulateBold);
        Assert.False(bold.SimulateItalic);
        Assert.False(bold.IsSubstitute);

        // "semibold" wird übersprungen und verdrängt das echte "regular" nicht; der führende "/" zählt nicht.
        var regular = library.Face("StandardGameFont", JominiFontStyle.Regular, "l_german");
        Assert.Equal(new[] { Copy(root, "arial.ttf") }, regular!.Files);

        var boldItalic = library.Face("StandardGameFont", JominiFontStyle.Bold | JominiFontStyle.Italic, "l_german");
        Assert.Equal(new[] { Copy(root, "segoeui.ttf") }, boldItalic!.Files);
        Assert.False(boldItalic.SimulateBold);
        Assert.False(boldItalic.SimulateItalic);

        Assert.Null(library.Face("UnknownFont", JominiFontStyle.Regular, "l_english"));
    }

    [Fact]
    public void Missing_style_falls_back_to_regular_with_simulation()
    {
        var root = Root("r1",
            FontFiles("Test-Regular", ("\"l_english\"", "\"fonts/a/arial.ttf\"")) +
            Font("OnlyRegular", ("regular", "Test-Regular")) +
            Font("NoRegular", ("bold", "Test-Regular")),
            "arial.ttf");

        var library = JominiFontLibrary.Load(new[] { root });

        var bold = library.Face("OnlyRegular", JominiFontStyle.Bold, "l_english");
        Assert.Equal(new[] { Copy(root, "arial.ttf") }, bold!.Files);
        Assert.True(bold.SimulateBold);
        Assert.False(bold.SimulateItalic);

        var both = library.Face("OnlyRegular", JominiFontStyle.Bold | JominiFontStyle.Italic, "l_english");
        Assert.True(both!.SimulateBold);
        Assert.True(both.SimulateItalic);

        var regular = library.Face("OnlyRegular", JominiFontStyle.Regular, "l_english");
        Assert.False(regular!.SimulateBold);
        Assert.False(regular.SimulateItalic);

        // Ohne "regular" gibt es nichts, worauf zurückgefallen werden kann.
        Assert.Null(library.Face("NoRegular", JominiFontStyle.Italic, "l_english"));
    }

    [Fact]
    public void Earlier_root_wins_for_same_font_name()
    {
        var mod = Root("mod",
            Font("StandardGameFont", ("regular", "Mod-Regular")) +
            FontFiles("Mod-Regular", ("\"l_english\"", "\"fonts/a/segoeui.ttf\"")),
            "segoeui.ttf");
        var game = Root("game",
            Font("StandardGameFont", ("regular", "Game-Regular")) +
            FontFiles("Game-Regular", ("\"l_english\"", "\"fonts/a/arial.ttf\"")) +
            FontFiles("Mod-Regular", ("\"l_english\"", "\"fonts/a/arial.ttf\"")) +
            Font("GameOnly", ("regular", "Game-Regular")),
            "arial.ttf");

        var library = JominiFontLibrary.Load(new[] { mod, game });

        Assert.Equal(new[] { Copy(mod, "segoeui.ttf") }, library.Face("StandardGameFont", JominiFontStyle.Regular, "l_english")!.Files);
        // Fontfiles gelten über alle Wurzeln; Dateien suchen ebenfalls alle Wurzeln ab.
        Assert.Equal(new[] { Copy(game, "arial.ttf") }, library.Face("GameOnly", JominiFontStyle.Regular, "l_english")!.Files);
    }

    [Fact]
    public void Language_without_group_uses_english_group()
    {
        var root = Root("r1",
            FontFiles("WithEnglish", ("\"l_german\"", "\"fonts/a/segoeui.ttf\""), ("\"l_english\"", "\"fonts/a/arial.ttf\"")) +
            FontFiles("WithoutEnglish", ("\"l_german\"", "\"fonts/a/segoeui.ttf\""), ("\"l_french\"", "\"fonts/a/arial.ttf\"")) +
            Font("A", ("regular", "WithEnglish")) +
            Font("B", ("regular", "WithoutEnglish")),
            "arial.ttf", "segoeui.ttf");

        var library = JominiFontLibrary.Load(new[] { root });

        Assert.Equal(new[] { Copy(root, "arial.ttf") }, library.Face("A", JominiFontStyle.Regular, "l_russian")!.Files);
        Assert.Equal(new[] { Copy(root, "arial.ttf") }, library.Face("A", JominiFontStyle.Regular, "")!.Files);
        Assert.Equal(new[] { Copy(root, "segoeui.ttf") }, library.Face("A", JominiFontStyle.Regular, "german")!.Files);
        // Ohne englische Gruppe gilt die erste.
        Assert.Equal(new[] { Copy(root, "segoeui.ttf") }, library.Face("B", JominiFontStyle.Regular, "l_russian")!.Files);
    }

    [Fact]
    public void Glyph_missing_in_first_file_comes_from_next_file()
    {
        var root = Root("r1",
            FontFiles("Pair", ("\"l_english\"", "\"fonts/a/arial.ttf\" \"fonts/a/segoeui.ttf\"")) +
            Font("Pair", ("regular", "Pair")),
            "arial.ttf", "segoeui.ttf");
        var arial = Glyphs(Copy(root, "arial.ttf"));
        var segoe = Glyphs(Copy(root, "segoeui.ttf"));
        var codePoint = segoe.CharacterToGlyphMap.Keys.Where(cp => !arial.CharacterToGlyphMap.ContainsKey(cp)).Min();

        var face = JominiFontLibrary.Load(new[] { root }).Face("Pair", JominiFontStyle.Regular, "l_english")!;
        var advance = face.Advance(codePoint, 15, out var file);

        Assert.Equal(Copy(root, "segoeui.ttf"), file);
        Assert.Equal(Rounded(segoe.AdvanceWidths[segoe.CharacterToGlyphMap[codePoint]], 15), advance);

        face.Advance('A', 15, out var fileA);
        Assert.Equal(Copy(root, "arial.ttf"), fileA);
    }

    [Fact]
    public void Character_missing_everywhere_uses_notdef_advance()
    {
        var root = Root("r1",
            FontFiles("Arial", ("\"l_english\"", "\"fonts/a/arial.ttf\"")) +
            Font("Arial", ("regular", "Arial")),
            "arial.ttf");
        var arial = Glyphs(Copy(root, "arial.ttf"));
        Assert.False(arial.CharacterToGlyphMap.ContainsKey(0x1F600));

        var face = JominiFontLibrary.Load(new[] { root }).Face("Arial", JominiFontStyle.Regular, "l_english")!;
        var advance = face.Advance(0x1F600, 15, out var file);

        Assert.True(double.IsFinite(advance));
        Assert.Equal(Rounded(arial.AdvanceWidths[0], 15), advance);
        Assert.Equal(Copy(root, "arial.ttf"), file);
    }

    [Fact]
    public void Corrupt_font_file_is_skipped()
    {
        var root = Root("r1",
            FontFiles("Broken", ("\"l_english\"", "\"fonts/a/broken.ttf\" \"fonts/a/missing.ttf\" \"../r1/fonts/a/arial.ttf\" \"fonts/a/arial.ttf\"")) +
            FontFiles("OnlyBroken", ("\"l_english\"", "\"fonts/a/broken.ttf\"")) +
            Font("Broken", ("regular", "Broken")) +
            Font("OnlyBroken", ("regular", "OnlyBroken")),
            "arial.ttf");
        var bytes = new byte[100];
        new Random(4711).NextBytes(bytes);
        File.WriteAllBytes(Copy(root, "broken.ttf"), bytes);

        var library = JominiFontLibrary.Load(new[] { root });

        // Kaputte, fehlende und mit ".." herausführende Einträge fallen weg.
        Assert.Equal(new[] { Copy(root, "arial.ttf") }, library.Face("Broken", JominiFontStyle.Regular, "l_english")!.Files);
        Assert.Null(library.Face("OnlyBroken", JominiFontStyle.Regular, "l_english"));
    }

    [Fact]
    public void Fallback_files_open_only_when_a_glyph_is_missing()
    {
        // Victoria 3 hängt an jede Gruppe mehrere MB CJK-Schriften an: sie werden erst für ein fehlendes Zeichen geöffnet, nicht beim Anlegen.
        var root = Root("r1",
            FontFiles("Mixed", ("\"l_english\"", "\"fonts/a/arial.ttf\" \"fonts/a/segoeui.ttf\"")) +
            Font("Mixed", ("regular", "Mixed")),
            "arial.ttf", "segoeui.ttf");
        var face = JominiFontLibrary.Load(new[] { root }).Face("Mixed", JominiFontStyle.Regular, "l_english")!;

        // Arial hat "M": die Rückfalldatei bleibt zu. Wird sie danach entfernt, fehlt sie in Files (beim sofortigen Öffnen stünde sie dort).
        Assert.True(face.Advance('M', 15, out var file) > 0);
        Assert.Equal(Copy(root, "arial.ttf"), file);
        File.Delete(Copy(root, "segoeui.ttf"));
        Assert.Equal(new[] { Copy(root, "arial.ttf") }, face.Files);
    }

    [Fact]
    public void Advances_are_rounded_per_glyph()
    {
        var root = Root("r1",
            FontFiles("Arial", ("\"l_english\"", "\"fonts/a/arial.ttf\"")) +
            Font("Arial", ("regular", "Arial")),
            "arial.ttf");
        var arial = Glyphs(Copy(root, "arial.ttf"));
        var rawM = arial.AdvanceWidths[arial.CharacterToGlyphMap['M']];
        var rawI = arial.AdvanceWidths[arial.CharacterToGlyphMap['i']];

        var face = JominiFontLibrary.Load(new[] { root }).Face("Arial", JominiFontStyle.Regular, "l_english")!;

        Assert.Equal(Rounded(rawM, 15), face.Advance('M', 15, out _));
        var tenI = Enumerable.Range(0, 10).Sum(i => face.Advance('i', 15, out _));
        Assert.Equal(10 * Rounded(rawI, 15), tenI);
        // Gerundet wird je Glyphe, nicht die Summe.
        Assert.NotEqual(Rounded(10 * rawI, 15), tenI);

        Assert.Equal(arial.Baseline * 15, face.Ascent(15), 6);
        Assert.Equal((arial.Height - arial.Baseline) * 15, face.Descent(15), 6);
    }

    [Fact]
    public void Substitute_is_segoe_ui()
    {
        var face = JominiFontLibrary.Substitute;

        Assert.True(face.IsSubstitute);
        Assert.Equal(new[] { Path.Combine(WindowsFonts, "segoeui.ttf") }, face.Files, StringComparer.OrdinalIgnoreCase);
        Assert.True(face.Advance('M', 15, out _) > 0);
    }

    /// <summary>Breiten aus der Spezifikationstabelle (Global Constraints): Text, Größe, Breite.</summary>
    private static readonly (string Game, string Font, string FirstFile, string[] Contexts, (string Text, double Size, double Width)[] Rows)[] Measured =
    {
        ("ck3", "StandardGameFont", "GitanLatin-Regular.otf", Array.Empty<string>(), new[]
        {
            ("MMMMMMMMMM", 15.0, 140.0),
            ("iiiiiiiiii", 15.0, 41.0),
            ("Probe text 123", 15.0, 95.0),
            ("Probe text 123", 18.0, 113.0),
            ("Probe text 123 with more words", 15.0, 211.0),
        }),
        ("eu5", "StandardGameFont", "NotoSerif_SemiCondensed-Regular.ttf", new[] { "in_game", "main_menu", "loading_screen" }, new[]
        {
            ("MMMMMMMMMM", 15.0, 130.0),
            ("iiiiiiiiii", 15.0, 41.0),
            ("Probe text 123", 15.0, 95.0),
            ("Probe text 123", 18.0, 113.0),
            ("Probe text 123 with more words", 15.0, 215.0),
        }),
        ("vic3", "EBGaramond", "EBGaramond-Medium.ttf", Array.Empty<string>(), new[]
        {
            ("MMMMMMMMMM", 15.0, 140.0),
            ("iiiiiiiiii", 15.0, 41.0),
            ("Probe text 123", 15.0, 85.0),
            ("Probe text 123", 18.0, 104.0),
            ("Probe text 123 with more words", 15.0, 188.0),
            ("Probe text 123", 17.0, 99.0),
        }),
    };

    [Fact]
    public void Rounded_advances_match_the_measured_widths()
    {
        foreach (var (gameName, fontName, firstFile, contexts, rows) in Measured)
        {
            var game = VanillaGui.For(gameName);
            if (!game.Exists)
            {
                _output.WriteLine($"{gameName}: Installation fehlt, übersprungen");
                continue;
            }

            var roots = GuiTextureLocator.ContextRootsFor(game.InstallDirectory, Array.Empty<string>(), null, contexts);
            var face = JominiFontLibrary.Load(roots).Face(fontName, JominiFontStyle.Regular, "l_english");

            Assert.NotNull(face);
            // Erste Datei über ein lateinisches Zeichen, ohne die CJK-Rückfalldateien zu öffnen (Files öffnet alle).
            face!.Advance('M', 15, out var first);
            Assert.Equal(firstFile, Path.GetFileName(first), StringComparer.OrdinalIgnoreCase);
            foreach (var (text, size, expected) in rows)
            {
                var width = text.EnumerateRunes().Sum(rune => face.Advance(rune.Value, size, out _));
                _output.WriteLine($"{gameName} \"{text}\" {size}: {width} (Tabelle {expected})");
                Assert.InRange(width, expected - 2, expected + 2);
            }
        }
    }
}
