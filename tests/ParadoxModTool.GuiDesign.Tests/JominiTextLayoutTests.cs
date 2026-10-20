using System.Windows.Media;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Text;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Textsatz: Bausteine zu gemessenen, umbrochenen und gekürzten Zeilen. Die synthetischen Tests benutzen Arial aus dem Windows-Schriftordner (zur Laufzeit
/// kopiert) und rechnen die erwarteten Werte mit <see cref="JominiFontFace.Advance"/> nach; die Spieltests lesen die echten Installationen.
/// </summary>
public sealed class JominiTextLayoutTests : IDisposable
{
    private const double Epsilon = 1e-6;

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-jominitext-" + Guid.NewGuid().ToString("N"));
    private readonly ITestOutputHelper _output;
    private readonly string _root;
    private readonly JominiFontLibrary _fonts;

    public JominiTextLayoutTests(ITestOutputHelper output)
    {
        _output = output;
        _root = JominiTestFonts.Root(_temp, "r1",
            JominiTestFonts.FontFiles("Arial-Regular", ("\"l_english\"", "\"fonts/a/arial.ttf\" \"fonts/a/segoeui.ttf\"")) +
            JominiTestFonts.FontFiles("Arial-Bold", ("\"l_english\"", "\"fonts/a/arialbd.ttf\"")) +
            JominiTestFonts.Font("Arial", ("regular", "Arial-Regular"), ("bold", "Arial-Bold")),
            "arial.ttf", "arialbd.ttf", "segoeui.ttf");
        _fonts = JominiFontLibrary.Load(new[] { _root });
    }

    public void Dispose() => JominiTestFonts.TryDelete(_temp);

    private JominiFontFace Regular => _fonts.Face("Arial", JominiFontStyle.Regular, "l_english")!;

    private JominiFontFace Bold => _fonts.Face("Arial", JominiFontStyle.Bold, "l_english")!;

    private static JominiTextStyle Style(string? font = "Arial", double size = 15) =>
        new(font, JominiFontStyle.Regular, size, JominiColor.White, false, false, false);

    private static IReadOnlyList<JominiTextToken> Tokens(string text) => JominiTextMarkup.Parse(text, _ => null);

    // Standard ohne align: links und senkrecht mittig (gemessen in allen drei Spielen).
    private static JominiTextRequest Request(
        string text,
        JominiTextStyle? style = null,
        bool multiline = false,
        double? maxWidth = null,
        double? boxWidth = null,
        bool elide = false,
        double? sizeMin = null,
        string? defaultFormat = null) =>
        new(Tokens(text), style ?? Style(), defaultFormat, multiline, maxWidth, boxWidth, elide, sizeMin, "l_english",
            JominiTextAlignH.Left, JominiTextAlignV.Center);

    private JominiTextVisual Layout(JominiTextRequest request, List<string>? notes = null, JominiTextFormats? formats = null,
        Func<string, string?>? locate = null) =>
        JominiTextLayout.Layout(request, _fonts, formats ?? JominiTextFormats.Empty, locate ?? (_ => null), notes ?? new List<string>());

    private static double Width(JominiFontFace face, string text, double size) =>
        text.EnumerateRunes().Sum(rune => face.Advance(rune.Value, size, out _));

    private static string LineText(JominiTextLine line) => string.Concat(line.Runs.Select(r => r.Text));

    private static double H1(JominiFontFace face, double size) =>
        Math.Ceiling(face.Ascent(size) - Epsilon) + Math.Floor(face.Descent(size) + Epsilon);

    private static double Pitch(JominiFontFace face, double size) => Math.Floor(face.Ascent(size) + face.Descent(size) + Epsilon);

    [Fact]
    public void Width_is_sum_of_rounded_advances()
    {
        var notes = new List<string>();
        var visual = Layout(Request("Probe text 123"), notes);

        var expected = Width(Regular, "Probe text 123", 15);
        var line = Assert.Single(visual.Lines);
        var run = Assert.Single(line.Runs);
        Assert.Equal(JominiTextRunKind.Text, run.Kind);
        Assert.Equal("Probe text 123", run.Text);
        Assert.Equal(JominiTestFonts.Copy(_root, "arial.ttf"), run.FontFile);
        Assert.Equal(15, run.FontSize);
        Assert.Equal("Probe text 123".Select(c => Regular.Advance(c, 15, out _)), run.Advances);
        Assert.Equal(expected, run.Width);
        Assert.Equal(expected, line.Width);
        Assert.Equal(expected, visual.Width);

        // Gemessene Zeilenhöhe: ceil(s·asc) + floor(s·desc); Grundlinie ceil(s·asc) unter der Zeilenoberkante.
        Assert.Equal(H1(Regular, 15), visual.Height);
        Assert.Equal(H1(Regular, 15), line.Height);
        Assert.Equal(Math.Ceiling(Regular.Ascent(15) - Epsilon), line.Baseline);
        Assert.Equal(JominiTextAlignH.Left, visual.AlignH);
        Assert.Equal(JominiTextAlignV.Center, visual.AlignV);
        Assert.False(visual.Elided);
        Assert.False(visual.HasPlaceholder);
        Assert.Empty(visual.MissingFonts);
        Assert.Empty(visual.MissingIcons);
        // Die Höhen sind gemessen: keine Annäherungsnotiz.
        Assert.True(JominiTextMetrics.HeightsMeasured);
        Assert.Empty(notes);
    }

    [Fact]
    public void Bold_run_uses_the_bold_file()
    {
        var visual = Layout(Request("Ab #bold Bold#! cd"));

        var runs = Assert.Single(visual.Lines).Runs;
        Assert.Equal(new[] { "Ab ", "Bold", " cd" }, runs.Select(r => r.Text));
        Assert.Equal(JominiTestFonts.Copy(_root, "arialbd.ttf"), runs[1].FontFile);
        Assert.False(runs[1].SimulateBold);
        Assert.Equal(Width(Bold, "Bold", 15), runs[1].Width);
        Assert.Equal(JominiTestFonts.Copy(_root, "arial.ttf"), runs[2].FontFile);
        Assert.Equal(Width(Regular, "Ab ", 15) + Width(Bold, "Bold", 15) + Width(Regular, " cd", 15), visual.Width);
    }

    [Fact]
    public void Glyph_from_a_fallback_file_starts_a_new_run_with_one_advance_per_code_point()
    {
        var arial = new GlyphTypeface(new Uri(JominiTestFonts.Copy(_root, "arial.ttf")));
        var segoe = new GlyphTypeface(new Uri(JominiTestFonts.Copy(_root, "segoeui.ttf")));
        var codePoint = segoe.CharacterToGlyphMap.Keys.Where(cp => cp > 0x7F && cp < 0xD800 && !arial.CharacterToGlyphMap.ContainsKey(cp)).Min();
        var symbol = char.ConvertFromUtf32(codePoint);

        var visual = Layout(Request("A" + symbol + "B"));

        var runs = Assert.Single(visual.Lines).Runs;
        Assert.Equal(new[] { "A", symbol, "B" }, runs.Select(r => r.Text));
        Assert.Equal(JominiTestFonts.Copy(_root, "segoeui.ttf"), runs[1].FontFile);
        Assert.Equal(new[] { Regular.Advance(codePoint, 15, out _) }, runs[1].Advances);

        // Ein Ersatzpaar (U+1F600, in keiner Datei: .notdef der ersten) ist ein Zeichen: ein Vorschub.
        var emoji = Layout(Request("A\U0001F600"));
        var run = Assert.Single(Assert.Single(emoji.Lines).Runs);
        Assert.Equal(new[] { Regular.Advance('A', 15, out _), Regular.Advance(0x1F600, 15, out _) }, run.Advances);
        Assert.Equal(run.Advances.Sum(), emoji.Width);
    }

    [Fact]
    public void Default_format_applies_over_the_base_style_and_formats_stack()
    {
        var formats = JominiTextFormats.From(GuiIndexTests.Build((GuiIndexTests.F("gui/textformatting.gui"),
            "textformatting = {\n\tformat = { name = medium format = \"color:{0.5,0.5,0.5}\" }\n\tformat = { name = big format = \"size:20\" }\n}")));
        var red = Style() with { Color = new JominiColor(1, 0, 0, 1) };

        var visual = Layout(Request("a #big b#! c", red, defaultFormat: "#medium"), formats: formats);

        var runs = Assert.Single(visual.Lines).Runs;
        Assert.Equal(new[] { "a ", "b", " c" }, runs.Select(r => r.Text));
        Assert.All(runs, r => Assert.Equal(new JominiColor(0.5, 0.5, 0.5, 1), r.Color));
        Assert.Equal(new[] { 15.0, 20.0, 15.0 }, runs.Select(r => r.FontSize));
        // Die Höhe folgt der größten Schrift im Text.
        Assert.Equal(H1(Regular, 20), visual.Height);
    }

    [Fact]
    public void Elide_keeps_the_longest_prefix_that_fits_with_suffix()
    {
        const string text = "Probe text that is too long";
        var box = Width(Regular, "Probe tex ...", 15);

        var visual = Layout(Request(text, boxWidth: box, elide: true));

        var line = Assert.Single(visual.Lines);
        Assert.Equal("Probe tex ...", LineText(line));
        Assert.Equal(box, visual.Width);
        Assert.True(visual.Elided);

        // Leerzeichen vor dem Kürzel fallen weg ("Probe" + " ...").
        var trimmed = Layout(Request(text, boxWidth: Width(Regular, "Probe ...", 15), elide: true));
        Assert.Equal("Probe ...", LineText(Assert.Single(trimmed.Lines)));

        // Ohne elide oder wenn der Text passt, bleibt er ganz.
        Assert.Equal(text, LineText(Assert.Single(Layout(Request(text, boxWidth: box)).Lines)));
        var fits = Layout(Request(text, boxWidth: Width(Regular, text, 15), elide: true));
        Assert.Equal(text, LineText(Assert.Single(fits.Lines)));
        Assert.False(fits.Elided);

        // Passt nicht einmal das Kürzel, bleibt nur es.
        var nothing = Layout(Request(text, boxWidth: 1, elide: true));
        Assert.Equal(" ...", LineText(Assert.Single(nothing.Lines)));
        Assert.True(nothing.Elided);
    }

    [Fact]
    public void Elide_suffix_takes_the_style_of_the_last_kept_char()
    {
        const string text = "ab #bold cdefghijklmnop#!";
        var box = Width(Regular, "ab ", 15) + Width(Bold, "cd ...", 15);

        var visual = Layout(Request(text, boxWidth: box, elide: true));

        var runs = Assert.Single(visual.Lines).Runs;
        Assert.Equal(new[] { "ab ", "cd ..." }, runs.Select(r => r.Text));
        Assert.Equal(JominiTestFonts.Copy(_root, "arialbd.ttf"), runs[1].FontFile);
        Assert.Equal(box, visual.Width);
    }

    [Fact]
    public void Fontsize_min_shrinks_before_eliding()
    {
        const string text = "Probe text 123";
        var notes = new List<string>();

        // Passt bei Größe 10: direkt auf das Minimum, nicht gekürzt.
        var visual = Layout(Request(text, boxWidth: Width(Regular, text, 10), elide: true, sizeMin: 10), notes);

        var run = Assert.Single(Assert.Single(visual.Lines).Runs);
        Assert.Equal(text, run.Text);
        Assert.Equal(10, run.FontSize);
        Assert.Equal(Width(Regular, text, 10), visual.Width);
        Assert.Equal(H1(Regular, 10), visual.Height);
        Assert.False(visual.Elided);
        Assert.Contains(GuiTexts.LayoutTextApproximated("fontsize_min"), notes);

        // Auch beim Minimum zu breit: auf Größe 10 gesetzt und dann gekürzt.
        var box = Width(Regular, "Probe te ...", 10);
        var elided = Layout(Request(text, boxWidth: box, elide: true, sizeMin: 10));
        Assert.Equal("Probe te ...", LineText(Assert.Single(elided.Lines)));
        Assert.All(elided.Lines[0].Runs, r => Assert.Equal(10, r.FontSize));
        Assert.True(elided.Elided);

        // Passt der Text bei der eigentlichen Größe, bleibt sie, ohne Notiz.
        var unchangedNotes = new List<string>();
        var unchanged = Layout(Request(text, boxWidth: 500, elide: true, sizeMin: 10), unchangedNotes);
        Assert.Equal(15, unchanged.Lines[0].Runs[0].FontSize);
        Assert.Empty(unchangedNotes);
    }

    [Theory]
    [InlineData(-5.0)]
    [InlineData(0.0)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void Unusable_fontsize_min_is_ignored(double sizeMin)
    {
        const string text = "Probe text 123";
        var notes = new List<string>();

        // Schlechte Mod-Daten (fontsize_min = -5): nicht verkleinern, sondern bei Größe 15 kürzen; keine negativen Breiten.
        var visual = Layout(Request(text, boxWidth: Width(Regular, "Probe te ...", 15), elide: true, sizeMin: sizeMin), notes);

        Assert.Equal("Probe te ...", LineText(Assert.Single(visual.Lines)));
        Assert.All(visual.Lines[0].Runs, r => Assert.Equal(15, r.FontSize));
        Assert.All(visual.Lines[0].Runs.SelectMany(r => r.Advances), advance => Assert.True(advance >= 0));
        Assert.DoesNotContain(GuiTexts.LayoutTextApproximated("fontsize_min"), notes);
    }

    [Theory]
    [InlineData("size:-10")]
    [InlineData("size:NaN")]
    public void Unusable_format_size_keeps_the_text_size(string spec)
    {
        var formats = JominiTextFormats.From(GuiIndexTests.Build((GuiIndexTests.F("gui/textformatting.gui"),
            $"textformatting = {{\n\tformat = {{ name = bad format = \"{spec}\" }}\n}}")));

        var visual = Layout(Request("A #bad [Root.GetName] B#! C"), formats: formats);

        var runs = Assert.Single(visual.Lines).Runs;
        Assert.All(runs, r => Assert.Equal(15, r.FontSize));
        Assert.All(runs, r => Assert.True(r.Width > 0));
        Assert.Equal(Width(Regular, "A ‹GetName› B C", 15), visual.Width);
    }

    [Fact]
    public void Multiline_wraps_at_spaces_within_max_width()
    {
        const string text = "aaa bbb ccc ddd";
        var max = Width(Regular, "aaa bbb", 15);

        var visual = Layout(Request(text, multiline: true, maxWidth: max + 2));

        Assert.Equal(new[] { "aaa bbb", "ccc ddd" }, visual.Lines.Select(LineText));
        // Die Breite ist die längste Zeile, nicht max_width; das Leerzeichen am Umbruch zählt zu keiner Zeile.
        Assert.Equal(max, visual.Width);
        Assert.Equal(max, visual.Lines[0].Width);
        // Höhe: H1 + (n − 1) × Zeilenabstand; jede Zeile außer der letzten trägt den Abstand.
        Assert.Equal(H1(Regular, 15) + Pitch(Regular, 15), visual.Height);
        Assert.Equal(Pitch(Regular, 15), visual.Lines[0].Height);
        Assert.Equal(H1(Regular, 15), visual.Lines[1].Height);
        Assert.Equal(visual.Height, visual.Lines.Sum(l => l.Height));

        // Harte Umbrüche brechen nur mit multiline, sonst zählen sie als Leerzeichen.
        var multilineNotes = new List<string>();
        Assert.Equal(new[] { "ab", "cd" }, Layout(Request("ab\ncd", multiline: true), multilineNotes).Lines.Select(LineText));
        Assert.Empty(multilineNotes);
        var singleNotes = new List<string>();
        Assert.Equal(new[] { "ab cd e" }, Layout(Request("ab\ncd\ne"), singleNotes).Lines.Select(LineText));
        // Die Annahme ist nie still: eine Notiz je Aufruf.
        Assert.Single(singleNotes, GuiTexts.LayoutTextLineBreakAsSpace);
        // Ohne multiline kein Umbruch an max_width.
        Assert.Single(Layout(Request(text, maxWidth: max)).Lines);
    }

    [Fact]
    public void Word_wider_than_max_width_breaks_between_chars()
    {
        // Ziffern sind in Arial gleich breit.
        var max = Width(Regular, "1234", 15);

        var visual = Layout(Request("xy 1234567890", multiline: true, maxWidth: max));

        Assert.Equal(new[] { "xy", "1234", "5678", "90" }, visual.Lines.Select(LineText));
    }

    [Fact]
    public void Wrap_width_smaller_than_a_glyph_still_terminates()
    {
        var text = string.Concat(Enumerable.Repeat("abcdefghij", 4));

        var visual = Layout(Request(text, multiline: true, maxWidth: 5));

        Assert.Equal(40, visual.Lines.Count);
        Assert.All(visual.Lines, line => Assert.Single(line.Runs[0].Advances));
        Assert.Equal(text, string.Concat(visual.Lines.Select(LineText)));
    }

    [Fact]
    public void Placeholder_and_icon_runs_have_widths()
    {
        var formats = JominiTextFormats.From(GuiIndexTests.Build((GuiIndexTests.F("gui/texticons.gui"),
            "texticon = {\n\ticon = coin\n\ticonsize = { texture = \"gfx/coin.dds\" size = { 25 20 } offset = { 0 3 } fontsize = 15 }\n}\n" +
            "texticon = {\n\ticon = faith\n\ticonsize = { texture = \"gfx/faith.dds\" size = { 25 25 } offset = { 0 2 } fontsize = 16 }\n}\n" +
            "texticon = {\n\ticon = half\n\ticonsize = { texture = \"gfx/half.dds\" size = { 30 28 } fontsize = 30 }\n}\n" +
            "texticon = {\n\ticon = plain\n\ticonsize = { texture = \"gfx/plain.dds\" size = { 17 13 } }\n}")));
        var located = Path.Combine(_temp, "coin.dds");
        var notes = new List<string>();

        var visual = Layout(Request("A [Root.GetName] @coin! @faith! @half! @plain! @nothing! @[x.y]! [concept_x]"), notes, formats,
            texture => texture == "gfx/coin.dds" ? located : null);

        var runs = Assert.Single(visual.Lines).Runs;
        var placeholder = runs.First(r => r.Kind == JominiTextRunKind.Placeholder);
        Assert.Equal("‹GetName›", placeholder.Text);
        Assert.Equal(Width(Regular, "‹GetName›", 15), placeholder.Width);
        // Konzepte zeigen ihren Namen ohne Klammern.
        Assert.Equal("x", runs.Last().Text);
        Assert.Equal(JominiTextRunKind.Placeholder, runs.Last().Kind);
        Assert.True(visual.HasPlaceholder);

        var icons = runs.Where(r => r.Kind == JominiTextRunKind.Icon).ToList();
        Assert.Equal(6, icons.Count);
        Assert.Equal((25.0, 20.0, 3.0, located), (icons[0].Width, icons[0].IconHeight, icons[0].IconOffsetY, icons[0].IconTexture));
        Assert.Equal(new[] { 25.0 }, icons[0].Advances);
        // Skaliert mit Textgröße / iconsize.fontsize und aufgerundet: 25 × 15/16 = 23,4 → 24; genaue Vielfache bleiben (30 × 15/30 = 15).
        Assert.Equal((24.0, 24.0), (icons[1].Width, icons[1].IconHeight));
        Assert.Equal((15.0, 14.0), (icons[2].Width, icons[2].IconHeight));
        Assert.Null(icons[1].IconTexture);
        // Ohne fontsize im iconsize-Block unskaliert.
        Assert.Equal((17.0, 13.0), (icons[3].Width, icons[3].IconHeight));
        // Fehlende und dynamische Icons: Quadrat der Schriftgröße mit Notiz.
        Assert.Equal((15.0, 15.0), (icons[4].Width, icons[4].IconHeight));
        Assert.Null(icons[4].IconTexture);
        Assert.Equal((15.0, 15.0), (icons[5].Width, icons[5].IconHeight));
        Assert.Equal(new[] { "nothing", JominiTextLayout.DynamicIconName }, visual.MissingIcons);
        Assert.Contains(GuiTexts.LayoutTextIconMissing("nothing"), notes);
        Assert.Contains(GuiTexts.LayoutTextIconMissing(JominiTextLayout.DynamicIconName), notes);
        // Definierte Icons mit nicht gefundener Textur: Notiz je Textur, die gefundene Textur ohne Notiz.
        Assert.Contains(GuiTexts.LayoutTextureMissing("gfx/faith.dds"), notes);
        Assert.Contains(GuiTexts.LayoutTextureMissing("gfx/plain.dds"), notes);
        Assert.DoesNotContain(GuiTexts.LayoutTextureMissing("gfx/coin.dds"), notes);

        Assert.Equal(runs.Sum(r => r.Width), visual.Width);
    }

    [Fact]
    public void Icon_texture_not_found_is_noted_once_per_texture()
    {
        var formats = JominiTextFormats.From(GuiIndexTests.Build((GuiIndexTests.F("gui/texticons.gui"),
            "texticon = {\n\ticon = gone\n\ticonsize = { texture = \"gfx/gone.dds\" size = { 20 20 } }\n}")));
        var notes = new List<string>();

        var visual = Layout(Request("@gone! @gone!"), notes, formats);

        var icons = visual.Lines[0].Runs.Where(r => r.Kind == JominiTextRunKind.Icon).ToList();
        Assert.Equal(2, icons.Count);
        // Die Größe bleibt, gezeichnet wird das schraffierte Quadrat.
        Assert.All(icons, icon => Assert.Equal((20.0, (string?)null), (icon.Width, icon.IconTexture)));
        Assert.Empty(visual.MissingIcons);
        Assert.Single(notes, GuiTexts.LayoutTextureMissing("gfx/gone.dds"));
    }

    [Fact]
    public void Icon_block_without_usable_size_is_a_missing_icon()
    {
        // Größe 0 (z. B. eine nicht auflösbare Konstante) würde das Icon still verschwinden lassen.
        var formats = JominiTextFormats.From(GuiIndexTests.Build((GuiIndexTests.F("gui/texticons.gui"),
            "texticon = {\n\ticon = zero\n\ticonsize = { texture = \"gfx/zero.dds\" size = { 0 0 } fontsize = 15 }\n}\n" +
            "texticon = {\n\ticon = unresolved\n\ticonsize = { texture = \"gfx/u.dds\" size = { @nope @nope } }\n}")));
        var notes = new List<string>();

        var visual = Layout(Request("@zero!@unresolved!"), notes, formats, _ => @"C:\x.dds");

        var icons = visual.Lines[0].Runs;
        Assert.All(icons, icon => Assert.Equal((15.0, 15.0, (string?)null), (icon.Width, icon.IconHeight, icon.IconTexture)));
        Assert.Equal(new[] { "zero", "unresolved" }, visual.MissingIcons);
        Assert.Contains(GuiTexts.LayoutTextIconMissing("zero"), notes);
        Assert.Contains(GuiTexts.LayoutTextIconMissing("unresolved"), notes);
    }

    [Fact]
    public void Icon_block_without_size_takes_the_texture_size()
    {
        // Victoria 3 texticons.gui: 311 iconsize-Blöcke mit texture, offset und fontsize, aber ohne size.
        var formats = JominiTextFormats.From(GuiIndexTests.Build((GuiIndexTests.F("gui/texticons.gui"),
            "texticon = {\n\ticon = balance\n\ticonsize = { offset = { 0 13 } texture = \"gfx/balance.dds\" fontsize = 45 }\n}\n" +
            "texticon = {\n\ticon = unscaled\n\ticonsize = { texture = \"gfx/unscaled.dds\" }\n}\n" +
            "texticon = {\n\ticon = gone\n\ticonsize = { texture = \"gfx/gone.dds\" fontsize = 45 }\n}\n" +
            "texticon = {\n\ticon = bare\n\ticonsize = { fontsize = 45 }\n}")));
        var notes = new List<string>();
        var sizes = new Dictionary<string, (int Width, int Height)?>
        {
            ["gfx/balance.dds"] = (90, 60),
            ["gfx/unscaled.dds"] = (17, 13),
        };

        var visual = JominiTextLayout.Layout(Request("@balance!@unscaled!@balance!@gone!@bare!"), _fonts, formats,
            texture => sizes.ContainsKey(texture) ? @"C:\" + texture : null, notes, texture => sizes.GetValueOrDefault(texture));

        var icons = visual.Lines[0].Runs;
        // Texturgröße mit Textgröße / iconsize.fontsize skaliert und aufgerundet: 90 × 15/45 = 30, 60 × 15/45 = 20; ohne fontsize unskaliert.
        Assert.Equal((30.0, 20.0, 13.0, @"C:\gfx/balance.dds"), (icons[0].Width, icons[0].IconHeight, icons[0].IconOffsetY, icons[0].IconTexture));
        Assert.Equal((17.0, 13.0), (icons[1].Width, icons[1].IconHeight));
        // Nicht gemessen: eine Notiz je Aufruf, keine "fehlt"-Notiz.
        Assert.Single(notes, GuiTexts.LayoutTextApproximated("texticon"));
        Assert.DoesNotContain(GuiTexts.LayoutTextIconMissing("balance"), notes);
        // Wirklich fehlend (Textur nicht gefunden bzw. keine Textur): Quadrat der Schriftgröße mit Notiz.
        Assert.Equal((15.0, 15.0, (string?)null), (icons[3].Width, icons[3].IconHeight, icons[3].IconTexture));
        Assert.Equal((15.0, 15.0), (icons[4].Width, icons[4].IconHeight));
        Assert.Equal(new[] { "gone", "bare" }, visual.MissingIcons);
        Assert.Contains(GuiTexts.LayoutTextureMissing("gfx/gone.dds"), notes);
    }

    [Fact]
    public void Negative_box_width_with_empty_text_does_not_throw()
    {
        var visual = Layout(Request("", boxWidth: -5, elide: true, sizeMin: 10));

        Assert.Empty(Assert.Single(visual.Lines).Runs);
        Assert.False(visual.Elided);
        Assert.Equal(0, visual.Width);

        // Mit Text und negativer Breite bleibt nur das Kürzel.
        var elided = Layout(Request("Probe", boxWidth: -5, elide: true));
        Assert.Equal(" ...", LineText(Assert.Single(elided.Lines)));
        Assert.True(elided.Elided);
    }

    [Fact]
    public void Missing_font_uses_segoe_ui_with_note()
    {
        var notes = new List<string>();

        var visual = Layout(Request("Ab #bold cd#!", Style("NoSuchFont")), notes);

        var runs = Assert.Single(visual.Lines).Runs;
        var substitute = JominiFontLibrary.Substitute;
        Assert.All(runs, r => Assert.Equal(substitute.Files[0], r.FontFile));
        Assert.Equal(Width(substitute, "Ab cd", 15), visual.Width);
        // Fett ohne Fettschnitt der Ersatzschrift wird simuliert.
        Assert.False(runs[0].SimulateBold);
        Assert.True(runs[1].SimulateBold);
        Assert.Equal(new[] { "NoSuchFont" }, visual.MissingFonts);
        Assert.Single(notes, GuiTexts.LayoutTextFontMissing("NoSuchFont"));

        // Ohne Schriftname: ebenfalls Ersatzschrift mit Notiz, kein Absturz.
        var nullNotes = new List<string>();
        var none = Layout(Request("Ab", Style(null)), nullNotes);
        Assert.Equal(substitute.Files[0], none.Lines[0].Runs[0].FontFile);
        Assert.Equal(new[] { JominiTextLayout.NoFontName }, none.MissingFonts);
        Assert.Single(nullNotes, GuiTexts.LayoutTextFontMissing(JominiTextLayout.NoFontName));
    }

    [Fact]
    public void Empty_text_has_one_empty_line_of_line_height()
    {
        var visual = Layout(Request(""));

        var line = Assert.Single(visual.Lines);
        Assert.Empty(line.Runs);
        Assert.Equal(0, visual.Width);
        Assert.Equal(H1(Regular, 15), visual.Height);
    }

    // ---- Spieltests (echte Installationen; ohne sie kehren die Tests sofort zurück) ----

    private JominiFontFace? GameFace(string game, string font, params string[] contexts)
    {
        var install = VanillaGui.For(game);
        if (!install.Exists)
        {
            _output.WriteLine($"{game}: Installation fehlt, übersprungen");
            return null;
        }

        var roots = GuiTextureLocator.ContextRootsFor(install.InstallDirectory, Array.Empty<string>(), null, contexts);
        return JominiFontLibrary.Load(roots).Face(font, JominiFontStyle.Regular, "l_english");
    }

    [Fact]
    public void Line_metrics_match_the_measured_games()
    {
        var measured = new (string Game, string Font, string[] Contexts, double[] H1, double Pitch15)[]
        {
            ("ck3", "StandardGameFont", Array.Empty<string>(), new[] { 16.0, 21, 25, 33 }, 21),
            ("eu5", "StandardGameFont", new[] { "in_game", "main_menu", "loading_screen" }, new[] { 16.0, 21, 25, 33 }, 20),
            ("vic3", "EBGaramond", Array.Empty<string>(), new[] { 16.0, 20, 24, 32 }, 19),
        };
        var sizes = new[] { 12.0, 15, 18, 24 };

        foreach (var (game, font, contexts, h1, pitch15) in measured)
        {
            var face = GameFace(game, font, contexts);
            if (face is null)
            {
                continue;
            }

            var actual = sizes.Select(s => JominiTextMetrics.LineHeight(face, s)).ToArray();
            var pitch = JominiTextMetrics.LinePitch(face, 15);
            _output.WriteLine($"{game}: H1 {string.Join("/", actual)} (gemessen {string.Join("/", h1)}), Abstand bei 15 {pitch} (gemessen {pitch15})");
            Assert.Equal(h1, actual);
            Assert.Equal(pitch15, pitch);
        }
    }

    private JominiFontLibrary? Ck3Fonts()
    {
        var install = VanillaGui.For("ck3");
        if (!install.Exists)
        {
            _output.WriteLine("ck3: Installation fehlt, übersprungen");
            return null;
        }

        return JominiFontLibrary.Load(GuiTextureLocator.ContextRootsFor(install.InstallDirectory, Array.Empty<string>(), null, Array.Empty<string>()));
    }

    [Fact]
    public void Ck3_probe_7h_elides_to_probe_tex()
    {
        var fonts = Ck3Fonts();
        if (fonts is null)
        {
            return;
        }

        var request = Request("Probe text that is too long", Style("StandardGameFont"), boxWidth: 80, elide: true);
        var visual = JominiTextLayout.Layout(request, fonts, JominiTextFormats.Empty, _ => null, new List<string>());

        _output.WriteLine($"7h: \"{LineText(visual.Lines[0])}\" Breite {visual.Width}");
        Assert.Equal("Probe tex ...", LineText(Assert.Single(visual.Lines)));
        Assert.Equal(80, visual.Width);
        Assert.True(visual.Elided);
    }

    [Fact]
    public void Ck3_probe_7g_wraps_to_three_lines()
    {
        var fonts = Ck3Fonts();
        if (fonts is null)
        {
            return;
        }

        var request = Request("Probe text that is long enough to wrap at least twice in the box", Style("StandardGameFont"), multiline: true, maxWidth: 150);
        var visual = JominiTextLayout.Layout(request, fonts, JominiTextFormats.Empty, _ => null, new List<string>());

        _output.WriteLine($"7g: {string.Join(" / ", visual.Lines.Select(LineText))}, Breite {visual.Width}, Höhe {visual.Height}");
        Assert.Equal(new[] { "Probe text that is long", "enough to wrap at", "least twice in the box" }, visual.Lines.Select(LineText));
        // Gemessen 147 (Lauf 4, 11f); Höhe 3 Zeilen = 21 + 2 × 21.
        Assert.Equal(147, visual.Width);
        Assert.Equal(63, visual.Height);
    }

    [Fact]
    public void Eu5_power_icon_scales_from_constants()
    {
        const string relative = "main_menu/gui/shared/font_icons.gui";
        var install = VanillaGui.For("eu5");
        var file = Path.Combine(install.GameDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
        var face = GameFace("eu5", "StandardGameFont", "in_game", "main_menu", "loading_screen");
        if (face is null || !File.Exists(file))
        {
            return;
        }

        var formats = JominiTextFormats.From(GuiIndexTests.Build((GuiIndexTests.F("gui/shared/font_icons.gui"), File.ReadAllText(file))));
        var roots = GuiTextureLocator.ContextRootsFor(install.InstallDirectory, Array.Empty<string>(), null, new[] { "in_game", "main_menu", "loading_screen" });
        var fonts = JominiFontLibrary.Load(roots);
        var notes = new List<string>();

        var visual = JominiTextLayout.Layout(Request("@power!", Style("StandardGameFont")), fonts, formats, _ => null, notes);

        // Lauf 4 (11i): size @[font_icon_default_size * 0.85] = 17, fontsize @font_icon_default_fontsize = 16 → 17 × 15/16 aufgerundet = 16.
        var icon = Assert.Single(Assert.Single(visual.Lines).Runs);
        _output.WriteLine($"eu5 power: {icon.Width}×{icon.IconHeight}");
        Assert.Equal(JominiTextRunKind.Icon, icon.Kind);
        Assert.Equal(16, icon.Width);
        Assert.Empty(visual.MissingIcons);
    }

    [Fact]
    public void Vic3_texticons_without_size_take_the_texture_size()
    {
        var install = VanillaGui.For("vic3");
        var file = Path.Combine(install.GameDirectory, "gui", "texticons.gui");
        if (!File.Exists(file))
        {
            _output.WriteLine("vic3: Installation fehlt, übersprungen");
            return;
        }

        // Vanilla: iconsize mit texture, offset und fontsize = 45, aber ohne size (balance.dds; spacebar.png mit fontsize 80).
        var formats = JominiTextFormats.From(GuiIndexTests.Build((GuiIndexTests.F("gui/texticons.gui"), File.ReadAllText(file))));
        var textures = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(install.InstallDirectory, Array.Empty<string>(), null, Array.Empty<string>()));
        var notes = new List<string>();
        var pngSize = Classic.ClassicTextureLocator.ReadSizeOfFile(textures.Locate("gfx/interface/tutorial/spacebar.png")!);

        var visual = JominiTextLayout.Layout(Request("@market_balance!@spacebar!"), _fonts, formats, textures.Locate, notes,
            path => textures.SizeOf(path) ?? (textures.Locate(path) is { } full ? Classic.ClassicTextureLocator.ReadSizeOfFile(full) : null));

        var icons = Assert.Single(visual.Lines).Runs;
        var dds = textures.SizeOf("gfx/interface/icons/trade_icons/balance.dds")!.Value;
        _output.WriteLine($"vic3 market_balance: {dds} → {icons[0].Width}×{icons[0].IconHeight}; spacebar: {pngSize} → {icons[1].Width}×{icons[1].IconHeight}");
        Assert.Empty(visual.MissingIcons);
        Assert.Equal((JominiTextMetrics.Ceiling(dds.Width * 15 / 45.0), JominiTextMetrics.Ceiling(dds.Height * 15 / 45.0)), (icons[0].Width, icons[0].IconHeight));
        Assert.Equal(13, icons[0].IconOffsetY);
        Assert.Equal(JominiTextMetrics.Ceiling(pngSize!.Value.Width * 15 / 80.0), icons[1].Width);
        Assert.Contains(GuiTexts.LayoutTextApproximated("texticon"), notes);
    }
}
