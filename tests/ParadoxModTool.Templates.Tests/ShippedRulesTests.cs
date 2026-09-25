using ParadoxModTool.Scopes;
using Xunit;

namespace ParadoxModTool.Templates.Tests;

public class ShippedRulesTests
{
    private static RunVerdict[] Eval(string content, string text = "x", int tokenStart = 0)
    {
        var evaluator = new RuleEvaluator(ShippedRules.Effective(), text, ScopeLexer.Tokenize(text));
        var starts = new List<int>();
        for (var i = 0; i < content.Length; i++)
        {
            if (char.IsAsciiDigit(content[i]) && (i == 0 || !char.IsAsciiDigit(content[i - 1])))
            {
                starts.Add(i);
            }
        }

        return evaluator.Evaluate(tokenStart, content, starts);
    }

    [Fact]
    public void There_are_six_rules_with_fixed_unique_ids_in_a_fixed_order()
    {
        var ids = ShippedRules.All.Select(r => r.Id).ToArray();

        Assert.Equal(
            new[]
            {
                ShippedRules.TextureFileId, ShippedRules.TextureEntryId, ShippedRules.ButtonId,
                ShippedRules.PositionId, ShippedRules.SizeId, ShippedRules.NamePrefixId,
            },
            ids);
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.All(ShippedRules.All, r => Assert.NotEmpty(r.Name));
    }

    [Fact]
    public void All_returns_fresh_copies()
    {
        ShippedRules.All[0].Name = "geändert";

        Assert.NotEqual("geändert", ShippedRules.All[0].Name);
    }

    [Fact]
    public void Effective_skips_disabled_ids_and_uses_the_shipped_layer()
    {
        var effective = ShippedRules.Effective(new[] { ShippedRules.ButtonId });

        Assert.DoesNotContain(effective, e => e.Rule.Id == ShippedRules.ButtonId);
        Assert.Equal(5, effective.Count);
        Assert.All(effective, e => Assert.Equal(RuleLayer.Shipped, e.Layer));
    }

    [Theory]
    [InlineData("gfx/interface/progressbar_frame_iv2_3.dds")]
    [InlineData("GFX/X_4.PNG")]
    [InlineData("a_1.tga")]
    public void Texture_files_are_ignored(string content)
    {
        Assert.All(Eval(content), v => Assert.Equal(RuleVerdict.NotCounts, v.Verdict));
        Assert.Equal("Regel „Texturdatei“ (mitgeliefert)", Eval(content)[0].Rule!.Reason);
    }

    [Fact]
    public void The_value_of_texture_is_ignored_even_without_an_image_extension()
    {
        const string text = "texture = \"abc_4\"";

        var verdicts = Eval("abc_4", text, text.IndexOf("abc_4", StringComparison.Ordinal));

        Assert.Equal(RuleVerdict.NotCounts, verdicts[0].Verdict);
        Assert.Equal("Regel „texture-Eintrag“ (mitgeliefert)", verdicts[0].Rule!.Reason);
    }

    [Fact]
    public void Names_with_button_are_ignored()
    {
        Assert.All(Eval("card_header_button_04"), v => Assert.Equal(RuleVerdict.NotCounts, v.Verdict));
        Assert.All(Eval("Button_2_x_3"), v => Assert.Equal(RuleVerdict.NotCounts, v.Verdict));
    }

    [Fact]
    public void Everything_inside_position_and_size_is_ignored()
    {
        const string text = "position = { 105 37 } size = { 25 25 } other = { 7 }";

        Assert.Equal(RuleVerdict.NotCounts, Eval("105", text, text.IndexOf("105", StringComparison.Ordinal))[0].Verdict);
        Assert.Equal(RuleVerdict.NotCounts, Eval("25", text, text.IndexOf("25", StringComparison.Ordinal))[0].Verdict);
        Assert.Equal(RuleVerdict.Open, Eval("7", text, text.LastIndexOf('7'))[0].Verdict);
    }

    [Theory]
    [InlineData("iv2_ga_select_historic_ARA_1_1")]
    [InlineData("iv_2_tooltip_template_idea_adm_1_1")]
    [InlineData("IV3_a")]
    public void Only_the_prefix_number_is_switched_off(string content)
    {
        var verdicts = Eval(content);

        Assert.Equal(RuleVerdict.NotCounts, verdicts[0].Verdict);
        Assert.All(verdicts.Skip(1), v => Assert.Equal(RuleVerdict.Open, v.Verdict));
        Assert.Equal("Regel „Namenspräfix“ (mitgeliefert)", verdicts[0].Rule!.Reason);
    }

    [Theory]
    [InlineData("xiv2_a")]
    [InlineData("ck3_a")]
    [InlineData("a_iv2")]
    public void A_prefix_only_counts_at_the_start_of_the_name(string content)
    {
        Assert.All(Eval(content), v => Assert.Equal(RuleVerdict.Open, v.Verdict));
    }
}
