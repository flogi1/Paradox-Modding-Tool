using ParadoxModTool.SearchReplace.ReplaceLists;
using Xunit;

namespace ParadoxModTool.SearchReplace.Tests;

/// <summary>Ersetzungslisten: nur ganze Wörter, Text nach # (Kommentar) bleibt unberührt, damit ein zweiter Lauf nichts ändert.</summary>
public sealed class RuleMatcherTests
{
    [Theory]
    [InlineData("castle_01", true)]
    [InlineData("pmt_probe.2", true)]
    [InlineData("a:b-c", true)]
    [InlineData("", false)]
    [InlineData("two words", false)]
    [InlineData(".castle", false)]
    [InlineData("castle.", false)]
    public void Search_word_validity(string search, bool valid) => Assert.Equal(valid, RuleMatcher.IsValidSearchWord(search));

    [Theory]
    [InlineData("castle_010 x_castle_01 castle_01", "castle_010 x_castle_01 NEW", 1)]
    [InlineData("#castle_01", "#castle_01", 0)]
    [InlineData("buildings={castle_01}", "buildings={NEW}", 1)]
    [InlineData("castle_01\r\ncastle_01", "NEW\r\nNEW", 2)]
    [InlineData("a = castle_01 # castle_01", "a = NEW # castle_01", 1)]
    [InlineData("desc = \"a # b\" castle_01", "desc = \"a # b\" NEW", 1)]
    [InlineData("# say \"hi\" castle_01", "# say \"hi\" castle_01", 0)]
    [InlineData("x = \"open castle_01\ncastle_01", "x = \"open NEW\nNEW", 2)]
    public void Replaces_whole_words_outside_comments(string text, string expected, int count)
    {
        Assert.Equal(expected, RuleMatcher.Replace(text, "castle_01", "NEW", out var n));
        Assert.Equal(count, n);
    }

    [Fact]
    public void Real_history_comment_stays_and_building_is_commented_out()
    {
        const string text = "\t1054.1.1 = { #Area conquered by tribals and turned into nomadic pastures\n\t\tbuildings = {\n\t\t\tpastures_01\n\t\t}\n";

        var result = RuleMatcher.Replace(text, "pastures_01", "#pastures_01", out var n);

        Assert.Equal(text.Replace("\t\t\tpastures_01", "\t\t\t#pastures_01"), result);
        Assert.Equal(1, n);
        Assert.Equal(result, RuleMatcher.Replace(result, "pastures_01", "#pastures_01", out _));
    }

    [Fact]
    public void Validation_marks_empty_non_word_and_duplicate_rules()
    {
        var rules = new List<ReplaceRule>
        {
            new() { Search = "" },
            new() { Search = "a b" },
            new() { Search = "x" },
            new() { Search = "x" },
            new() { Search = "x", Enabled = false },
            new() { Search = "y" },
        };

        Assert.Equal(
            new[] { RuleProblem.EmptySearch, RuleProblem.NotAWord, RuleProblem.Duplicate, RuleProblem.Duplicate, RuleProblem.None, RuleProblem.None },
            RuleMatcher.Validate(rules));
    }
}
