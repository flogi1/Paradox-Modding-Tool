using ParadoxModTool.SearchReplace.ReplaceLists;
using Xunit;

namespace ParadoxModTool.SearchReplace.Tests;

/// <summary>Ersetzungslisten: Regelart Text (beliebiger Suchtext, Leerraum tolerant), Platzhalter (* und $n) und sicheres Auskommentieren.</summary>
public sealed class RuleModesTests
{
    private const string Trigger = "building_requirement_castle_city_church = { LEVEL = 01 }";

    private static ReplaceRule Text(string search, string replace, bool safe = false) => new() { Mode = RuleMode.Text, Search = search, Replace = replace, SafeComment = safe };

    private static ReplaceRule Pattern(string search, string replace, bool safe = false) => new() { Mode = RuleMode.Pattern, Search = search, Replace = replace, SafeComment = safe };

    private static string Run(string text, ReplaceRule rule, out int count) => RuleMatcher.Replace(text, rule, out count);

    // ---- Text

    [Fact]
    public void Text_rule_finds_braces_and_several_words()
    {
        var result = Run("a = 1\n\t" + Trigger + "\n", Text(Trigger, "building_requirement_main_building = { LEVEL = 2 }"), out var count);

        Assert.Equal("a = 1\n\tbuilding_requirement_main_building = { LEVEL = 2 }\n", result);
        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData("building_requirement_castle_city_church={LEVEL=01}", 0)]
    [InlineData("building_requirement_castle_city_church  =\t{ LEVEL = 01 }", 1)]
    [InlineData("building_requirement_castle_city_church = {\n\t\tLEVEL = 01\n\t}", 1)]
    [InlineData("building_requirement_castle_city_church = {\r\n\tLEVEL = 01\r\n}", 1)]
    public void Text_rule_ignores_how_much_whitespace_separates_the_words(string text, int expected)
    {
        Run(text, Text(Trigger, "X"), out var count);

        Assert.Equal(expected, count);
    }

    [Fact]
    public void Text_rule_replaces_the_whole_multiline_match_and_keeps_surrounding_text()
    {
        var result = Run("a = {\n\tfoo = {\n\t\tLEVEL = 01\n\t}\n}\n", Text("foo = { LEVEL = 01 }", "bar = { LEVEL = 2 }"), out var count);

        Assert.Equal("a = {\n\tbar = { LEVEL = 2 }\n}\n", result);
        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData("# " + Trigger, 0)]
    [InlineData("x = 1 # " + Trigger, 0)]
    [InlineData("desc = \"#\" " + Trigger, 1)]
    public void Text_rule_skips_matches_inside_comments(string text, int expected)
    {
        Run(text, Text(Trigger, "X"), out var count);

        Assert.Equal(expected, count);
    }

    [Fact]
    public void Text_rule_matches_inside_words_because_it_has_no_word_boundaries()
    {
        Assert.Equal("xNEW_01", Run("xcastle_01", Text("castle", "NEW"), out var count));
        Assert.Equal(1, count);
    }

    [Fact]
    public void Text_rule_ignores_spaces_at_the_ends_of_the_search_text()
    {
        Assert.Equal("a=X", Run("a=b", Text("  =b  ", "=X"), out _));
    }

    [Fact]
    public void Text_rule_run_twice_changes_nothing_the_second_time()
    {
        var first = Run("x\n" + Trigger + "\n", Text(Trigger, "building_requirement_main_building = { LEVEL = 2 }"), out _);

        Run(first, Text(Trigger, "building_requirement_main_building = { LEVEL = 2 }"), out var count);

        Assert.Equal(0, count);
    }

    [Fact]
    public void Dollar_signs_stay_literal_in_text_rules()
    {
        Assert.Equal("a $1 b", Run("a x b", Text("x", "$1"), out _));
    }

    // ---- Platzhalter

    [Fact]
    public void Wildcard_matches_one_value_and_dollar_one_reuses_it()
    {
        var result = Run("special_building = hunting_grounds_01\nspecial_building = 0.5\n", Pattern("special_building = *", "#special_building = $1"), out var count);

        Assert.Equal("#special_building = hunting_grounds_01\n#special_building = 0.5\n", result);
        Assert.Equal(2, count);
    }

    [Fact]
    public void Several_wildcards_are_numbered_in_order_and_dollar_dollar_is_a_dollar()
    {
        Assert.Equal("b=a $", Run("x = a y = b", Pattern("x = * y = *", "$2=$1 $$"), out _));
    }

    [Fact]
    public void Wildcard_does_not_cross_spaces_or_braces()
    {
        Run("special_building = { a }", Pattern("special_building = *", "X"), out var count);

        Assert.Equal(0, count);
    }

    [Fact]
    public void Wildcard_rule_skips_comments_and_run_twice_changes_nothing()
    {
        var first = Run("# special_building = a\nspecial_building = b\n", Pattern("special_building = *", "#special_building = $1"), out var count);

        Assert.Equal("# special_building = a\n#special_building = b\n", first);
        Assert.Equal(1, count);
        Run(first, Pattern("special_building = *", "#special_building = $1"), out var again);
        Assert.Equal(0, again);
    }

    [Fact]
    public void Wildcard_values_may_hold_dots_colons_dashes_and_at_signs()
    {
        Assert.Equal("[a.b:c-d@e]", Run("k = a.b:c-d@e", Pattern("k = *", "[$1]"), out _));
    }

    // ---- Prüfung

    [Fact]
    public void Validation_covers_the_new_kinds()
    {
        var rules = new List<ReplaceRule>
        {
            Text("", "x"),
            Text("a\nb", "x"),
            Text("{ a = b }", "x"),
            Pattern("a = *", "$2"),
            Pattern("b = *", "$1"),
            Pattern("c = *", "$$1"),
            Text("same", "1"),
            Text("same", "2"),
            new() { Search = "same" },
        };

        Assert.Equal(
            new[]
            {
                RuleProblem.EmptySearch,
                RuleProblem.Multiline,
                RuleProblem.None,
                RuleProblem.BadCapture,
                RuleProblem.None,
                RuleProblem.None,
                RuleProblem.Duplicate,
                RuleProblem.Duplicate,
                RuleProblem.None,
            },
            RuleMatcher.Validate(rules));
    }

    // ---- Sicher auskommentieren

    [Fact]
    public void Safe_comment_puts_the_commented_word_on_its_own_line_and_keeps_the_closing_brace()
    {
        var result = Run("buildings = { a b }", new ReplaceRule { Search = "b", Replace = "#b", SafeComment = true }, out var count);

        Assert.Equal("buildings = { a\n#b\n}", result);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Safe_comment_keeps_the_indentation_of_the_line()
    {
        var result = Run("\t\tbuildings = { a b }\n", new ReplaceRule { Search = "b", Replace = "#b", SafeComment = true }, out _);

        Assert.Equal("\t\tbuildings = { a\n\t\t#b\n\t\t}\n", result);
    }

    [Fact]
    public void Safe_comment_does_not_split_when_nothing_follows_on_the_line()
    {
        var rule = new ReplaceRule { Search = "b", Replace = "#b", SafeComment = true };

        Assert.Equal("\t\t#b\n", Run("\t\tb\n", rule, out _));
        Assert.Equal("buildings = { a #b\n}", Run("buildings = { a b\n}", rule, out _));
        Assert.Equal("a #b # old\n", Run("a b # old\n", rule, out _));
    }

    [Fact]
    public void Safe_comment_splits_after_a_word_at_the_start_of_the_line()
    {
        var result = Run("\tb }\n", new ReplaceRule { Search = "b", Replace = "#b", SafeComment = true }, out _);

        Assert.Equal("\t#b\n\t}\n", result);
    }

    [Fact]
    public void Safe_comment_keeps_crlf_line_endings()
    {
        var result = Run("x = { a b }\r\n", new ReplaceRule { Search = "b", Replace = "#b", SafeComment = true }, out _);

        Assert.Equal("x = { a\r\n#b\r\n}\r\n", result);
    }

    [Fact]
    public void Safe_comment_handles_several_matches_on_one_line_without_blank_lines()
    {
        var result = Run("x = { a b c }\n", new ReplaceRule { Search = "b", Replace = "#b", SafeComment = true }, out _);
        var both = RuleMatcher.Replace(result, new ReplaceRule { Search = "c", Replace = "#c", SafeComment = true }, out _);
        var samePass = Run("x = { b c }\n", Pattern("b", "#b", safe: true), out _);

        Assert.Equal("x = { a\n#b\nc }\n", result);
        Assert.Equal("x = { a\n#b\n#c\n}\n", both);
        Assert.DoesNotContain("\n\n", samePass);
    }

    [Fact]
    public void Safe_comment_is_a_second_run_no_op()
    {
        var rule = new ReplaceRule { Search = "b", Replace = "#b", SafeComment = true };
        var first = Run("buildings = { a b }", rule, out _);

        Run(first, rule, out var count);

        Assert.Equal(0, count);
    }

    [Fact]
    public void Safe_comment_only_applies_to_replacements_that_start_with_a_hash()
    {
        Assert.Equal("buildings = { a B }", Run("buildings = { a b }", new ReplaceRule { Search = "b", Replace = "B", SafeComment = true }, out _));
    }

    [Fact]
    public void Without_the_option_commenting_out_works_as_before()
    {
        Assert.Equal("buildings = { a #b }", Run("buildings = { a b }", new ReplaceRule { Search = "b", Replace = "#b" }, out _));
    }

    [Fact]
    public void Safe_comment_works_with_pattern_rules()
    {
        var result = Run("x = { special_building = hg_01 }", Pattern("special_building = *", "#special_building = $1", safe: true), out _);

        Assert.Equal("x = {\n#special_building = hg_01\n}", result);
    }
}
