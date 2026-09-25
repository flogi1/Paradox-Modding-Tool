using Xunit;

namespace ParadoxModTool.Templates.Tests;

public class RuleToolsTests
{
    [Theory]
    [InlineData("iv#_ga_select_historic_ARA_#_#", 1, "iv#_ga_select_historic_ARA_[#]_#")]
    [InlineData("iv#_ga_select_historic_ARA_#_#", 0, "iv[#]_ga_select_historic_ARA_#_#")]
    [InlineData("iv#_ga_select_historic_ARA_#_#", 2, "iv#_ga_select_historic_ARA_#_[#]")]
    [InlineData("#", 0, "[#]")]
    [InlineData("Idee # von #", 1, "Idee # von [#]")]
    public void A_pattern_is_built_from_the_shape_of_a_name(string shape, int run, string expected)
    {
        Assert.Equal(expected, RulePatternBuilder.FromShape(shape, run));
    }

    private static CountingRule Valid() => new()
    {
        Id = "x",
        Name = "Regel",
        Action = RuleAction.OnlyMarkedCounts,
        Patterns = { "a_[#]" },
    };

    [Fact]
    public void A_complete_rule_is_valid()
    {
        Assert.Empty(RuleValidator.Validate(Valid()));
    }

    [Fact]
    public void A_name_and_a_pattern_are_required()
    {
        var rule = Valid();
        rule.Name = "  ";
        rule.Patterns = new List<string> { "", "  " };

        var errors = RuleValidator.Validate(rule);

        Assert.Contains(errors, e => e.Contains("Namen"));
        Assert.Contains(errors, e => e.Contains("Muster"));
    }

    [Fact]
    public void A_marker_may_appear_once_per_pattern()
    {
        var rule = Valid();
        rule.Patterns = new List<string> { "a_[#]_[#]" };

        Assert.Contains(RuleValidator.Validate(rule), e => e.Contains("[#]") && e.Contains("mehrfach"));
    }

    [Theory]
    [InlineData(RuleAction.OnlyMarkedCounts, true)]
    [InlineData(RuleAction.MarkedDoesNotCount, true)]
    [InlineData(RuleAction.Ignore, false)]
    [InlineData(RuleAction.AllCount, false)]
    public void Only_two_actions_need_a_marker(RuleAction action, bool needsMarker)
    {
        var rule = Valid();
        rule.Action = action;
        rule.Patterns = new List<string> { "a_#" };

        Assert.Equal(needsMarker, RuleValidator.Validate(rule).Any(e => e.Contains("[#]")));
    }

    [Fact]
    public void Inside_key_needs_a_key()
    {
        var rule = Valid();
        rule.Where = RuleWhere.InsideKey;
        rule.WhereKey = " ";

        Assert.Contains(RuleValidator.Validate(rule), e => e.Contains("Schlüssel"));

        rule.WhereKey = "position";

        Assert.Empty(RuleValidator.Validate(rule));
    }

    [Fact]
    public void The_summary_texts_are_short_and_german()
    {
        var rule = new CountingRule { Where = RuleWhere.InsideKey, WhereKey = "position", Patterns = { "a#", "b#" }, Games = new List<string> { "eu5", "ck3" } };

        Assert.Equal("Ignorieren", RuleSummary.ActionText(RuleAction.Ignore));
        Assert.Equal("Nur markierte zählt", RuleSummary.ActionText(RuleAction.OnlyMarkedCounts));
        Assert.Equal("Alle zählen", RuleSummary.ActionText(RuleAction.AllCount));
        Assert.Equal("Markierte zählt nicht", RuleSummary.ActionText(RuleAction.MarkedDoesNotCount));
        Assert.Equal("innerhalb von position", RuleSummary.WhereText(rule));
        Assert.Equal("überall", RuleSummary.WhereText(new CountingRule()));
        Assert.Equal("a# · b#", RuleSummary.PatternsText(rule));
        Assert.EndsWith("…", RuleSummary.PatternsText(new CountingRule { Patterns = { new string('x', 100) } }, 20));
        Assert.Equal("eu5, ck3", RuleSummary.GamesText(rule, RuleLayer.AllGames));
        Assert.Equal("alle", RuleSummary.GamesText(new CountingRule(), RuleLayer.AllGames));
        Assert.Equal(string.Empty, RuleSummary.GamesText(rule, RuleLayer.Mod));
        Assert.All(Enum.GetValues<RuleAction>(), a => Assert.NotEmpty(RuleSummary.ActionHelp(a)));
    }

    [Fact]
    public void The_preview_lists_only_names_the_rule_decides_and_each_once()
    {
        const string snippet = "a = iv2_ga_select_historic_ARA_1_1\nb = iv2_ga_select_historic_ARA_1_1\nc = other_5";
        var draft = new CountingRule { Id = "d", Name = "d", Action = RuleAction.OnlyMarkedCounts, Patterns = { "iv#_ga_*_ARA_[#]_#" } };

        var lines = RulePreview.Evaluate(snippet, draft);

        var line = Assert.Single(lines);
        Assert.Equal("iv2_ga_select_historic_ARA_1_1", line.Text);
        Assert.Equal(
            new[] { RuleVerdict.NotCounts, RuleVerdict.Counts, RuleVerdict.NotCounts },
            line.Runs.Select(r => r.Verdict).ToArray());
        Assert.Equal(2, line.Runs[0].Start);
        Assert.Equal(1, line.Runs[0].Length);
    }

    [Fact]
    public void The_preview_works_for_strings_and_stops_at_the_limit()
    {
        var draft = new CountingRule { Id = "d", Name = "d", Action = RuleAction.AllCount, Patterns = { "*" } };
        var snippet = string.Join("\n", Enumerable.Range(1, 30).Select(i => $"n{i} = \"Idee {i}\""));

        var lines = RulePreview.Evaluate(snippet, draft, maxLines: 5);

        Assert.Equal(5, lines.Count);
        Assert.StartsWith("n1", lines[0].Text);
    }

    [Fact]
    public void The_preview_of_an_incomplete_rule_is_empty()
    {
        Assert.Empty(RulePreview.Evaluate("a_1 = { }", new CountingRule { Id = "d", Name = "d" }));
        Assert.Empty(RulePreview.Evaluate(string.Empty, new CountingRule { Id = "d", Name = "d", Patterns = { "*" } }));
    }
}
