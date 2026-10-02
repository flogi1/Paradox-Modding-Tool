using Xunit;

namespace ParadoxModTool.SearchReplace.Tests;

/// <summary>Nach dem Ersetzen: OR-, NOR- und AND-Blöcke mit höchstens einem Eintrag finden, aber nur die, die durch das Ersetzen neu entstanden sind.</summary>
public sealed class SingleEntryBlockFinderTests
{
    [Theory]
    [InlineData("OR = { a = 1 }", 1)]
    [InlineData("OR = { }", 0)]
    [InlineData("OR = {\n\ta = 1\n\tb = 2\n}", 2)]
    [InlineData("OR = { a = 1 # b = 2\n}", 1)]
    [InlineData("OR = { a = \"x = y\" }", 1)]
    [InlineData("OR = { trait = { x = 1 y = 2 } }", 1)]
    [InlineData("OR = { has_a has_b }", 2)]
    [InlineData("OR = { a = 1 b < 5 }", 2)]
    [InlineData("OR = { a >= 5 }", 1)]
    [InlineData("OR = { { x = 1 } { y = 2 } }", 2)]
    public void Counts_the_entries_at_the_top_level_of_the_block(string text, int entries)
    {
        var found = SingleEntryBlockFinder.Blocks(text);

        Assert.Equal(entries, found.Single().Entries);
    }

    [Theory]
    [InlineData("OR")]
    [InlineData("NOR")]
    [InlineData("AND")]
    public void Covers_or_nor_and_and(string keyword)
    {
        var found = SingleEntryBlockFinder.Blocks($"{keyword} = {{ a = 1 }}");

        Assert.Equal(keyword, found.Single().Keyword);
    }

    [Theory]
    [InlineData("NOT = { a = 1 }")]
    [InlineData("or = { a = 1 }")]
    [InlineData("MY_OR = { a = 1 }")]
    [InlineData("OR = yes")]
    [InlineData("# OR = { a = 1 }")]
    [InlineData("x = \"OR = { a = 1 }\"")]
    public void Ignores_other_keywords_comments_and_strings(string text) => Assert.Empty(SingleEntryBlockFinder.Blocks(text));

    [Fact]
    public void Reports_line_and_offset_of_the_keyword_and_finds_nested_blocks()
    {
        const string text = "trigger = {\n\tOR = {\n\t\ta = 1\n\t\tAND = { b = 1 }\n\t\tc = 2\n\t}\n}\n";

        var found = SingleEntryBlockFinder.Blocks(text);

        var and = found.Single(b => b.Keyword == "AND");
        Assert.Equal("AND", and.Keyword);
        Assert.Equal(4, and.Line);
        Assert.Equal(text.IndexOf("AND", StringComparison.Ordinal), and.Offset);
    }

    [Fact]
    public void Only_blocks_that_are_new_after_the_replacement_are_reported()
    {
        const string oldText = "OR = { a = 1 }\nOR = { x = 1 y = 2 }\nOR = { z = 1 w = 2 }\n";
        const string newText = "OR = { a = 1 }\nOR = { x = 1 }\nOR = { z = 1 w = 2 }\n";

        var added = SingleEntryBlockFinder.NewIn(oldText, newText);

        var block = added.Single();
        Assert.Equal(2, block.Line);
        Assert.Equal(1, block.Entries);
    }

    [Fact]
    public void An_emptied_block_counts_as_new()
    {
        var added = SingleEntryBlockFinder.NewIn("OR = { a = 1 b = 2 }", "OR = { }");

        Assert.Equal(0, added.Single().Entries);
    }

    [Fact]
    public void Several_identical_blocks_are_matched_one_to_one()
    {
        const string oldText = "OR = { a = 1 }\nOR = { a = 1 b = 2 }\n";
        const string newText = "OR = { a = 1 }\nOR = { a = 1 }\n";

        var added = SingleEntryBlockFinder.NewIn(oldText, newText);

        Assert.Equal(2, added.Single().Line);
    }

    [Fact]
    public void Whitespace_differences_do_not_make_a_block_new()
    {
        Assert.Empty(SingleEntryBlockFinder.NewIn("OR = { a = 1 }", "OR = {\n\ta = 1\n}"));
    }

    [Fact]
    public void StillPresent_drops_blocks_that_were_fixed_and_follows_the_ones_that_moved()
    {
        const string before = "OR = { a = 1 }\nOR = { b = 1 }\nAND = { c = 1 }\n";
        var stored = SingleEntryBlockFinder.Blocks(before);

        // Der erste Block wurde ergänzt (zwei Einträge), der dritte ist nach unten gerutscht.
        const string after = "# neu\nOR = { a = 1 a2 = 2 }\nOR = { b = 1 }\n\nAND = { c = 1 }\n";
        var still = SingleEntryBlockFinder.StillPresent(stored, after);

        Assert.Equal(new[] { "OR", "AND" }, still.Select(b => b.Keyword));
        Assert.Equal(new[] { 3, 5 }, still.Select(b => b.Line));
        Assert.Equal(after.IndexOf("AND", StringComparison.Ordinal), still[1].Offset);
    }

    [Fact]
    public void StillPresent_matches_identical_blocks_one_to_one_and_drops_all_when_the_text_is_empty()
    {
        var stored = SingleEntryBlockFinder.Blocks("OR = { a = 1 }\nOR = { a = 1 }\n");

        Assert.Single(SingleEntryBlockFinder.StillPresent(stored, "OR = { a = 1 }\n"));
        Assert.Empty(SingleEntryBlockFinder.StillPresent(stored, string.Empty));
    }

    [Fact]
    public void Unchanged_text_and_unclosed_blocks_give_nothing()
    {
        Assert.Empty(SingleEntryBlockFinder.NewIn("OR = { a = 1 }", "OR = { a = 1 }"));
        Assert.Empty(SingleEntryBlockFinder.Blocks("OR = { a = 1"));
    }
}
