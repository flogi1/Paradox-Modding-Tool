using Xunit;

namespace ParadoxModTool.Templates.Tests;

public class RulePatternTests
{
    private static RuleMatch? Match(string pattern, string text, bool caseSensitive = false) =>
        RulePattern.Compile(pattern, caseSensitive).Match(text);

    [Fact]
    public void Star_matches_any_text_including_nothing()
    {
        Assert.NotNull(Match("iv*", "iv"));
        Assert.NotNull(Match("iv*", "iv2_ga_select"));
        Assert.NotNull(Match("*button*", "card_header_button_04"));
        Assert.Null(Match("iv*", "xiv2"));
    }

    [Fact]
    public void The_pattern_must_match_the_whole_text()
    {
        Assert.Null(Match("button", "card_button_1"));
        Assert.NotNull(Match("button", "button"));
        Assert.Null(Match("card", "card_1"));
    }

    [Fact]
    public void Hash_matches_a_whole_digit_run_and_reports_it()
    {
        var match = Match("iv#_x", "iv23_x");

        Assert.NotNull(match);
        Assert.Equal(new[] { (2, 4) }, match!.Value.Runs);
        Assert.Equal(-1, match.Value.MarkedStart);
        Assert.Null(Match("iv#_x", "iv_x"));
    }

    [Fact]
    public void Hash_never_matches_only_part_of_a_digit_run()
    {
        // "iv#*" darf "iv2" aus "iv23x" nicht als eigene Zahl greifen; der Block ist "23".
        var match = Match("iv#*", "iv23x");

        Assert.Equal(new[] { (2, 4) }, match!.Value.Runs);
        Assert.Null(Match("iv#3", "iv23"));
    }

    [Fact]
    public void The_marked_run_is_reported()
    {
        const string text = "iv2_ga_select_historic_ARA_1_1";

        var match = Match("iv#_ga_*_ARA_[#]_#", text);

        Assert.NotNull(match);
        Assert.Equal(3, match!.Value.Runs.Count);
        Assert.Equal(text.IndexOf("ARA_1", StringComparison.Ordinal) + 4, match.Value.MarkedStart);
        Assert.True(RulePattern.Compile("iv#_ga_*_ARA_[#]_#", false).HasMarker);
        Assert.False(RulePattern.Compile("iv#*", false).HasMarker);
    }

    [Fact]
    public void Only_the_first_marker_marks_and_a_second_one_is_a_plain_number()
    {
        var match = Match("[#]_[#]", "3_4");

        Assert.Equal(0, match!.Value.MarkedStart);
        Assert.Equal(2, match.Value.Runs.Count);
    }

    [Fact]
    public void Case_is_ignored_unless_requested()
    {
        Assert.NotNull(Match("IV#*", "iv2_x"));
        Assert.Null(Match("IV#*", "iv2_x", caseSensitive: true));
        Assert.NotNull(Match("iv#*", "iv2_x", caseSensitive: true));
    }

    [Fact]
    public void Other_characters_are_literal()
    {
        Assert.NotNull(Match("a.b(1)", "a.b(1)"));
        Assert.Null(Match("a.b(1)", "aXb(1)"));
        Assert.NotNull(Match("gfx/*.dds", "gfx/interface/x_3.dds"));
    }

    [Fact]
    public void A_bracket_without_hash_is_literal()
    {
        Assert.NotNull(Match("a[b]", "a[b]"));
        Assert.Null(Match("a[b]", "ab"));
    }

    [Fact]
    public void Plain_numbers_match_hash()
    {
        var match = Match("#", "105");

        Assert.Equal(new[] { (0, 3) }, match!.Value.Runs);
        Assert.NotNull(Match("[#]", "7"));
        Assert.Null(Match("#", "x7"));
    }

    [Fact]
    public void A_glob_matches_keys_ignoring_case()
    {
        var glob = RulePattern.CompileGlob("*_BUTTON");

        Assert.True(glob.IsMatch("card_header_button"));
        Assert.False(glob.IsMatch("button_card"));
        Assert.True(RulePattern.CompileGlob("position").IsMatch("Position"));
        Assert.False(RulePattern.CompileGlob("a#").IsMatch("a1"));
    }

    [Fact]
    public void A_pathological_pattern_times_out_instead_of_hanging()
    {
        var pattern = RulePattern.Compile("*a*a*a*a*a*a*a*a*a*a*a*a*b", false);
        var text = new string('a', 60_000);

        var started = DateTime.UtcNow;
        var result = pattern.Match(text);

        Assert.Null(result);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
    }
}
