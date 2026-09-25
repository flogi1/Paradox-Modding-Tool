using ParadoxModTool.Scopes;
using Xunit;

namespace ParadoxModTool.Templates.Tests;

public class RuleEvaluatorTests
{
    private const string Ara = "iv2_ga_select_historic_ARA_1_1";

    private static CountingRule Rule(string id, RuleAction action, params string[] patterns) =>
        new() { Id = id, Name = id, Action = action, Patterns = patterns.ToList() };

    private static RuleEvaluator Evaluator(string text, params CountingRule[] rules) =>
        new(rules.Select(r => new EffectiveRule(r, RuleLayer.Template)).ToList(), text, ScopeLexer.Tokenize(text));

    private static int[] RunStarts(string content)
    {
        var starts = new List<int>();
        for (var i = 0; i < content.Length; i++)
        {
            if (char.IsAsciiDigit(content[i]) && (i == 0 || !char.IsAsciiDigit(content[i - 1])))
            {
                starts.Add(i);
            }
        }

        return starts.ToArray();
    }

    private static RuleVerdict[] Verdicts(RuleEvaluator evaluator, string content, int tokenStart = 0) =>
        evaluator.Evaluate(tokenStart, content, RunStarts(content)).Select(v => v.Verdict).ToArray();

    [Fact]
    public void Only_marked_counts_decides_every_number_of_the_name()
    {
        var evaluator = Evaluator("x", Rule("r", RuleAction.OnlyMarkedCounts, "iv#_ga_*_ARA_[#]_#"));

        Assert.Equal(new[] { RuleVerdict.NotCounts, RuleVerdict.Counts, RuleVerdict.NotCounts }, Verdicts(evaluator, Ara));
    }

    [Fact]
    public void Ignore_makes_no_number_count()
    {
        var evaluator = Evaluator("x", Rule("r", RuleAction.Ignore, "*button*"));

        Assert.Equal(new[] { RuleVerdict.NotCounts, RuleVerdict.NotCounts }, Verdicts(evaluator, "card_button_04_2"));
    }

    [Fact]
    public void All_count_makes_every_number_count()
    {
        var evaluator = Evaluator("x", Rule("r", RuleAction.AllCount, "*"));

        Assert.Equal(new[] { RuleVerdict.Counts, RuleVerdict.Counts, RuleVerdict.Counts }, Verdicts(evaluator, Ara));
    }

    [Fact]
    public void Marked_does_not_count_leaves_the_other_numbers_open()
    {
        var evaluator = Evaluator("x", Rule("r", RuleAction.MarkedDoesNotCount, "iv_[#]*"));

        Assert.Equal(new[] { RuleVerdict.NotCounts, RuleVerdict.Open, RuleVerdict.Open }, Verdicts(evaluator, "iv_2_tooltip_x_1_1"));
    }

    [Fact]
    public void The_next_rule_decides_what_a_rule_left_open()
    {
        var evaluator = Evaluator(
            "x",
            Rule("first", RuleAction.MarkedDoesNotCount, "iv_[#]*"),
            Rule("second", RuleAction.AllCount, "*"));

        var result = evaluator.Evaluate(0, "iv_2_tooltip_x_1_1", RunStarts("iv_2_tooltip_x_1_1"));

        Assert.Equal(new[] { "first", "second", "second" }, result.Select(v => v.Rule!.Rule.Id).ToArray());
        Assert.Equal(new[] { RuleVerdict.NotCounts, RuleVerdict.Counts, RuleVerdict.Counts }, result.Select(v => v.Verdict).ToArray());
    }

    [Fact]
    public void The_first_rule_that_decides_a_number_wins()
    {
        var evaluator = Evaluator("x", Rule("a", RuleAction.AllCount, "*"), Rule("b", RuleAction.Ignore, "*"));

        Assert.All(Verdicts(evaluator, Ara), v => Assert.Equal(RuleVerdict.Counts, v));
    }

    [Fact]
    public void Several_patterns_mean_or()
    {
        var evaluator = Evaluator("x", Rule("r", RuleAction.MarkedDoesNotCount, "iv[#]*", "iv_[#]*"));

        Assert.Equal(RuleVerdict.NotCounts, Verdicts(evaluator, "iv2_a")[0]);
        Assert.Equal(RuleVerdict.NotCounts, Verdicts(evaluator, "iv_2_a")[0]);
        Assert.Equal(RuleVerdict.Open, Verdicts(evaluator, "ck2_a")[0]);
    }

    [Fact]
    public void A_name_no_pattern_matches_stays_open()
    {
        var evaluator = Evaluator("x", Rule("r", RuleAction.Ignore, "*button*"));

        Assert.Equal(new[] { RuleVerdict.Open }, Verdicts(evaluator, "idea_1"));
    }

    [Fact]
    public void Invalid_rules_decide_nothing_and_do_not_throw()
    {
        var evaluator = Evaluator(
            "x",
            Rule("no-pattern", RuleAction.Ignore),
            Rule("empty", RuleAction.Ignore, "", "   "),
            Rule("no-marker", RuleAction.OnlyMarkedCounts, "idea_#"),
            new CountingRule { Id = "no-key", Name = "k", Where = RuleWhere.InsideKey, WhereKey = "", Patterns = { "*" }, Action = RuleAction.Ignore });

        Assert.Equal(new[] { RuleVerdict.Open }, Verdicts(evaluator, "idea_1"));
        Assert.Empty(evaluator.Hits);
    }

    [Fact]
    public void Inside_key_reaches_the_whole_entry_at_any_depth()
    {
        const string text = "a = { position = { 105 37 } other = { 105 } deep = { position = { x = { 5 } } } }";
        var evaluator = Evaluator(
            text,
            new CountingRule { Id = "p", Name = "p", Where = RuleWhere.InsideKey, WhereKey = "position", Patterns = { "*" }, Action = RuleAction.Ignore });

        Assert.Equal(new[] { RuleVerdict.NotCounts }, Verdicts(evaluator, "105", text.IndexOf("105", StringComparison.Ordinal)));
        Assert.Equal(new[] { RuleVerdict.Open }, Verdicts(evaluator, "105", text.LastIndexOf("105", StringComparison.Ordinal)));
        Assert.Equal(new[] { RuleVerdict.NotCounts }, Verdicts(evaluator, "5", text.LastIndexOf("5", StringComparison.Ordinal)));
    }

    [Fact]
    public void Inside_key_accepts_a_glob_and_ignores_case()
    {
        const string text = "Card_Button = { a = 5 } card = { b = 6 }";
        var evaluator = Evaluator(
            text,
            new CountingRule { Id = "g", Name = "g", Where = RuleWhere.InsideKey, WhereKey = "*_button", Patterns = { "#" }, Action = RuleAction.Ignore });

        Assert.Equal(RuleVerdict.NotCounts, Verdicts(evaluator, "5", text.IndexOf('5'))[0]);
        Assert.Equal(RuleVerdict.Open, Verdicts(evaluator, "6", text.IndexOf('6'))[0]);
    }

    [Fact]
    public void Hits_count_the_numbers_a_rule_decided()
    {
        var evaluator = Evaluator("x", Rule("a", RuleAction.MarkedDoesNotCount, "iv[#]*"), Rule("b", RuleAction.AllCount, "*"));

        Verdicts(evaluator, Ara);
        Verdicts(evaluator, "iv3_x_9");

        Assert.Equal(2, evaluator.Hits["a"]);
        Assert.Equal(3, evaluator.Hits["b"]);
    }

    [Fact]
    public void Case_sensitive_rules_respect_case()
    {
        var rule = Rule("r", RuleAction.Ignore, "BUTTON_#");
        rule.CaseSensitive = true;
        var evaluator = Evaluator("x", rule);

        Assert.Equal(RuleVerdict.Open, Verdicts(evaluator, "button_1")[0]);
        Assert.Equal(RuleVerdict.NotCounts, Verdicts(evaluator, "BUTTON_1")[0]);
    }
}
