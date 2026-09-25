using Xunit;

namespace ParadoxModTool.Templates.Tests;

public class RuleAnalyzerTests
{
    private static EffectiveRule Custom(string name, RuleAction action, params string[] patterns) =>
        new(new CountingRule { Id = "id-" + name, Name = name, Action = action, Patterns = patterns.ToList() }, RuleLayer.Template);

    private static AnalyzeOptions With(params EffectiveRule[] rules) => new(Rules: rules);

    [Fact]
    public void A_rule_can_pick_the_second_number_as_the_counter()
    {
        var model = TemplateAnalyzer.Analyze(
            "iv2_ga_select_historic_ARA_1_1 = { }",
            With(Custom("Stufe", RuleAction.OnlyMarkedCounts, "iv#_ga_*_ARA_#_[#]")));

        var text = TemplateEngine.Render(model, 3);

        Assert.Contains("iv2_ga_select_historic_ARA_1_2 = { }", text);
        Assert.Contains("iv2_ga_select_historic_ARA_1_3 = { }", text);
        Assert.DoesNotContain("ARA_2_", text);
    }

    [Fact]
    public void A_number_a_rule_switches_off_no_longer_takes_part_in_first_counts()
    {
        var without = TemplateAnalyzer.Analyze("iv_2_a_1 = { }", With());
        var with = TemplateAnalyzer.Analyze("iv_2_a_1 = { }");

        Assert.Equal("iv_[2]_a_1", Assert.Single(without.Points, p => p.Enabled).Label);
        Assert.Equal("iv_2_a_[1]", Assert.Single(with.Points, p => p.Enabled).Label);
    }

    [Fact]
    public void Rules_work_inside_quoted_strings()
    {
        var model = TemplateAnalyzer.Analyze(
            "x = { name = \"Idee 5 von 10\" }",
            With(Custom("Zweite", RuleAction.OnlyMarkedCounts, "Idee # von [#]")));

        var text = TemplateEngine.Render(model, 2);

        Assert.Equal(PointKind.Text, Assert.Single(model.Points, p => p.Enabled).Kind);
        Assert.Contains("\"Idee 5 von 11\"", text);
        Assert.DoesNotContain("Idee 6", text);
    }

    [Fact]
    public void All_count_switches_on_a_plain_number_and_names_the_rule()
    {
        var model = TemplateAnalyzer.Analyze("a = { cost = 10 }", With(Custom("Kosten", RuleAction.AllCount, "#")));

        var point = Assert.Single(model.Points);

        Assert.True(point.Enabled);
        Assert.Equal("Regel „Kosten“ (Vorlage)", point.CountedByRule);
        Assert.Contains("cost = 11", TemplateEngine.Render(model, 2));
    }

    [Fact]
    public void All_count_switches_on_a_name_digit()
    {
        var model = TemplateAnalyzer.Analyze("ck3_special = { }", With(Custom("Alle", RuleAction.AllCount, "ck#_*")));

        Assert.Contains("ck4_special", TemplateEngine.Render(model, 2));
    }

    [Fact]
    public void An_ignoring_rule_turns_the_number_off_and_gives_the_reason()
    {
        var model = TemplateAnalyzer.Analyze("x_special_3 = { }", With(Custom("S", RuleAction.Ignore, "*_special_#")));

        var point = Assert.Single(model.Points);

        Assert.False(point.Enabled);
        Assert.Equal("Regel „S“ (Vorlage)", point.IgnoredReason);
        Assert.DoesNotContain("x_special_4", TemplateEngine.Render(model, 2));
    }

    [Fact]
    public void Checkbox_choices_per_shape_beat_rules()
    {
        var options = new AnalyzeOptions(
            ShapeNumbers: new Dictionary<string, IReadOnlyCollection<int>> { ["idea_#_#"] = new[] { 1 } },
            Rules: new[] { Custom("Alles aus", RuleAction.Ignore, "*") });

        var model = TemplateAnalyzer.Analyze("idea_1_2 = { }", options);

        Assert.Contains("idea_1_3 = { }", TemplateEngine.Render(model, 2));
    }

    [Fact]
    public void Learned_examples_beat_an_ignoring_rule()
    {
        var model = TemplateAnalyzer.Analyze("idea_1 = { }\nidea_2 = { }", With(Custom("Aus", RuleAction.Ignore, "idea_#")));

        Assert.Contains("idea_3 = { }", TemplateEngine.Render(model, 3));
    }

    [Fact]
    public void Rule_hits_are_reported_on_the_model()
    {
        var model = TemplateAnalyzer.Analyze("iv2_ga_select_historic_ARA_1_1 = { }");

        Assert.True(model.RuleHits[ShippedRules.NamePrefixId] >= 1);
    }

    [Fact]
    public void Without_any_rules_nothing_is_ignored()
    {
        var model = TemplateAnalyzer.Analyze("card_button_1 = { position = { 3 } }", With());

        Assert.All(model.Points, p => Assert.Equal(string.Empty, p.IgnoredReason));
        Assert.Empty(model.RuleHits);
    }

    [Fact]
    public void By_default_the_shipped_rules_apply()
    {
        var model = TemplateAnalyzer.Analyze("card_button_1 = { }");

        Assert.Equal("Regel „Button“ (mitgeliefert)", Assert.Single(model.Points).IgnoredReason);
    }
}
