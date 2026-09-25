using ParadoxModTool.Templates;
using Xunit;

namespace ParadoxModTool.Templates.Tests;

public class IgnoreAndSpanTests
{
    private const string Gui =
        "card_header_button_04 = {\n" +
        "\ttexture = \"gfx/interface/progressbar_frame_iv2_3.dds\"\n" +
        "\tposition = { 105 37 }\n" +
        "\tsize = { 25 25 }\n" +
        "\tname = card_7\n" +
        "}";

    [Fact]
    public void Textures_buttons_positions_and_sizes_are_ignored_by_default()
    {
        var model = TemplateAnalyzer.Analyze(Gui);

        var enabled = Assert.Single(model.Points, p => p.Enabled);
        Assert.Equal("card_7", enabled.Label);
        Assert.Contains(model.Points, p => p.IgnoredReason == "Regel „Button“ (mitgeliefert)");
        Assert.Contains(model.Points, p => p.IgnoredReason == "Regel „Texturdatei“ (mitgeliefert)");
        Assert.Contains(model.Points, p => p.IgnoredReason == "Regel „position-Eintrag“ (mitgeliefert)");
        Assert.Contains(model.Points, p => p.IgnoredReason == "Regel „size-Eintrag“ (mitgeliefert)");

        var text = TemplateEngine.Render(model, 2);
        Assert.Contains("card_header_button_04 = {", text);
        Assert.Contains("progressbar_frame_iv2_3.dds", text);
        Assert.Contains("position = { 105 37 }", text);
        Assert.Contains("name = card_8", text);
        Assert.DoesNotContain("button_05", text);
    }

    [Theory]
    [InlineData(ShippedRules.TextureFileId, ShippedRules.TextureEntryId, "progressbar_frame_iv2_4.dds")]
    [InlineData(ShippedRules.ButtonId, ShippedRules.ButtonId, "card_header_button_05 = {")]
    public void Each_shipped_rule_can_be_switched_off_on_its_own(string firstOff, string secondOff, string expected)
    {
        var options = new AnalyzeOptions(Rules: ShippedRules.Effective(new[] { firstOff, secondOff }));

        var text = TemplateEngine.Render(TemplateAnalyzer.Analyze(Gui, options), 2);

        Assert.Contains(expected, text);
    }

    [Fact]
    public void Nothing_is_ignored_when_there_are_no_rules()
    {
        var model = TemplateAnalyzer.Analyze(Gui, new AnalyzeOptions(Rules: Array.Empty<EffectiveRule>()));

        Assert.All(model.Points, p => Assert.Equal(string.Empty, p.IgnoredReason));
    }

    [Fact]
    public void Multi_line_position_blocks_are_ignored_too()
    {
        var model = TemplateAnalyzer.Analyze("x_1 = {\n\tposition = {\n\t\t\"a_5\"\n\t}\n}");

        Assert.Contains("a_5", TemplateEngine.Render(model, 2));
        Assert.DoesNotContain("a_6", TemplateEngine.Render(model, 2));
    }

    [Fact]
    public void The_same_digits_inside_and_outside_an_ignored_place_are_separate_points()
    {
        var model = TemplateAnalyzer.Analyze("idea_1 = { texture = \"gfx/idea_1.dds\" }");

        var text = TemplateEngine.Render(model, 2);

        Assert.Contains("idea_2 = { texture = \"gfx/idea_1.dds\" }", text);
    }

    [Fact]
    public void Ignored_points_can_still_be_switched_on_by_the_user()
    {
        var first = TemplateAnalyzer.Analyze(Gui);
        var texture = first.Points.Single(p => p.IgnoredReason == "Regel „Texturdatei“ (mitgeliefert)" && p.Label.Contains("[3]"));

        var model = TemplateAnalyzer.Analyze(Gui, null, new[] { new PointOverride(texture.Key, Enabled: true) });

        Assert.Contains("progressbar_frame_iv2_4.dds", TemplateEngine.Render(model, 2));
    }

    [Fact]
    public void Examples_that_really_change_win_over_the_ignore_rule()
    {
        var snippet = "a = { texture = \"gfx/x_1.dds\" }\na = { texture = \"gfx/x_2.dds\" }";

        var model = TemplateAnalyzer.Analyze(snippet);

        Assert.Contains("gfx/x_3.dds", TemplateEngine.Render(model, 3));
    }

    [Fact]
    public void Rendered_spans_mark_every_inserted_number()
    {
        var model = TemplateAnalyzer.Analyze("idea_1 = { name = idea_1 cost = 10 }");
        var spans = new List<RenderedSpan>();

        var text = TemplateEngine.Render(model, 3, spans);

        Assert.Equal(6, spans.Count);
        Assert.All(spans, s => Assert.Equal(s.Point.Format(0, 0).Length, s.Length));
        Assert.Equal("2", text.Substring(spans[2].Start, spans[2].Length));
        Assert.Equal("3", text.Substring(spans[5].Start, spans[5].Length));
        Assert.All(spans, s => Assert.Equal("idea_", text.Substring(s.Start - 5, 5)));
    }
}
