using System.Collections.Concurrent;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.GuiDesign.Text;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Textbreiten der Spieltabelle (Spezifikation, ±2) über die echten Vanilla-Typen, Schriften und Formate jeder Installation. Ohne Installation kehren die
/// Tests sofort zurück.
/// </summary>
public sealed class JominiTextWidthTests
{
    private const double Tolerance = 2;

    // Ein Spiel wird einmal je Testlauf indiziert (mehrere Sekunden); die Tests eines Spiels teilen sich den Stand.
    private static readonly ConcurrentDictionary<string, Lazy<Game?>> Games = new();

    private readonly ITestOutputHelper _output;

    public JominiTextWidthTests(ITestOutputHelper output) => _output = output;

    private static readonly (string Text, int Size)[] Samples =
    {
        ("MMMMMMMMMM", 15), ("iiiiiiiiii", 15), ("Probe text 123", 15), ("Probe text 123", 18), ("Probe text 123 with more words", 15),
    };

    [Fact]
    public void Ck3_text_widths_match_the_game() =>
        AssertWidths("ck3", "font = \"StandardGameFont\"", new double[] { 140, 41, 95, 113, 211 }, textSingle: 95);

    [Fact]
    public void Vic3_text_widths_match_the_game() =>
        // Victoria 3 kennt StandardGameFont nicht: der Vanilla-Typ textbox setzt EBGaramond über regular_font; text_single bleibt bei dessen fontsize 17.
        AssertWidths("vic3", string.Empty, new double[] { 140, 41, 85, 104, 188 }, textSingle: 99);

    [Fact]
    public void Eu5_text_widths_match_the_game() =>
        AssertWidths("eu5", "font = \"StandardGameFont\"", new double[] { 130, 41, 95, 113, 215 }, textSingle: 95);

    [Fact]
    public void Vic3_unknown_font_shows_no_text()
    {
        // Lauf 4: font = "StandardGameFont" ist in Victoria 3 nicht definiert; das Spiel zeigt nichts, das autoresize-Feld ist 0 breit.
        if (GameOf("vic3") is not { } game)
        {
            return;
        }

        var text = TextBox(game, "textbox = { raw_text = \"Probe text 123\" font = \"StandardGameFont\" fontsize = 15 autoresize = yes }");
        Assert.Equal(0, text.Width);
        Assert.Contains(GuiTexts.LayoutTextFontUnknown("StandardGameFont"), text.Notes);
    }

    [Fact]
    public void Warm_up_opens_the_textbox_type_fonts()
    {
        // Victoria 3 hat kein StandardGameFont; vorgeladen wird EBGaramond aus regular_font der Vanilla-Typen.
        if (GameOf("vic3") is { } vic3)
        {
            Assert.Contains("EBGaramond", vic3.Text.WarmFonts);
            vic3.Text.Warm("l_english");
        }

        if (GameOf("ck3") is { } ck3)
        {
            Assert.Contains("StandardGameFont", ck3.Text.WarmFonts);
        }
    }

    private void AssertWidths(string id, string font, double[] expected, double textSingle)
    {
        if (GameOf(id) is not { } game)
        {
            _output.WriteLine($"{id}: Installation fehlt, übersprungen");
            return;
        }

        var failures = new List<string>();
        for (var i = 0; i < Samples.Length; i++)
        {
            var (sample, size) = Samples[i];
            var box = TextBox(game, $"textbox = {{ raw_text = \"{sample}\" {font} fontsize = {size} autoresize = yes }}");
            _output.WriteLine($"{id} \"{sample}\" {size}: {box.Width} (Spiel {expected[i]}) {string.Join("; ", box.Notes)}");
            if (Math.Abs(box.Width - expected[i]) > Tolerance)
            {
                failures.Add($"\"{sample}\" {size}: {box.Width} statt {expected[i]}");
            }
        }

        var single = TextBox(game, "text_single = { raw_text = \"Probe text 123\" }");
        _output.WriteLine($"{id} text_single: {single.Width} (Spiel {textSingle}) {string.Join("; ", single.Notes)}");
        if (Math.Abs(single.Width - textSingle) > Tolerance)
        {
            failures.Add($"text_single: {single.Width} statt {textSingle}");
        }

        Assert.True(failures.Count == 0, id + ": " + string.Join(", ", failures));
    }

    /// <summary>Legt das Textfeld wie die Probe in eine hbox eines 600×40-widget und gibt seinen Kasten zurück.</summary>
    private static LayoutBox TextBox(Game game, string textbox)
    {
        var doc = GuiParser.Parse($"widget = {{ size = {{ 600 40 }} hbox = {{ {textbox} expand = {{}} }} }}");
        var resolver = new GuiResolver(game.Index, game.Rules, doc, game.OverlayPath);
        var engine = new GuiLayoutEngine(game.Rules, game.Textures.SizeOf, game.Text);
        var root = engine.Layout(resolver.Resolve(doc.Root.Children[0]));
        return root.Children[0].Children[0];
    }

    internal static Game? GameOf(string id) => Games.GetOrAdd(id, key => new Lazy<Game?>(() => Load(key))).Value;

    private static Game? Load(string id)
    {
        var install = VanillaGui.For(id);
        if (!install.Exists)
        {
            return null;
        }

        // Aufbau wie GuiWorkspace: Profilregeln, Index aus clausewitz/jomini/game, Texturen und Schriften in den Kontextordnern des Profils.
        var rules = GuiRules.FromProfile(GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", id + ".json")));
        var index = GuiIndex.Build(GuiSourceSet.Collect(GuiSourceSet.RootsFor(install.InstallDirectory, Array.Empty<string>(), null)), layout: rules.Layout);
        var textures = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(install.InstallDirectory, Array.Empty<string>(), null, rules.Layout.Contexts));
        var text = JominiTextService.Build(new GuiWorkspaceOptions(install.InstallDirectory, null, Array.Empty<string>(), rules, null), index, textures);
        var context = rules.Layout.Contexts.Count == 0 ? string.Empty : rules.Layout.Contexts[0];
        var overlay = Path.Combine(install.GameDirectory, context, "gui", "pmt_text_width_test.gui");
        return new Game(rules, index, textures, text, overlay);
    }

    internal sealed record Game(GuiRules Rules, GuiIndex Index, GuiTextureLocator Textures, JominiTextService Text, string OverlayPath);
}
