using Xunit;

namespace ParadoxModTool.SearchReplace.Tests;

/// <summary>Mehrere markierte Treffer als ein Text: je Treffer ein Block, durch eine Leerzeile getrennt; die Vorschau kappt lange Auswahlen.</summary>
public sealed class ResultCopyTextTests
{
    private static readonly string Blank = Environment.NewLine + Environment.NewLine;

    [Fact]
    public void One_hit_is_copied_as_it_is()
    {
        Assert.Equal("a = 1", ResultCopyText.Join(new[] { "a = 1" }));
    }

    [Fact]
    public void Several_hits_are_separated_by_a_blank_line_in_the_given_order()
    {
        Assert.Equal("a = 1" + Blank + "b = {\n\tc = 2\n}" + Blank + "d", ResultCopyText.Join(new[] { "a = 1", "b = {\n\tc = 2\n}", "d" }));
    }

    [Fact]
    public void No_hits_give_an_empty_text()
    {
        Assert.Equal(string.Empty, ResultCopyText.Join(Array.Empty<string>()));
    }

    [Fact]
    public void Preview_shows_everything_up_to_the_limit()
    {
        var preview = ResultCopyText.Preview(new[] { "a", "b" }, 2, out var hidden);

        Assert.Equal("a" + Blank + "b", preview);
        Assert.Equal(0, hidden);
    }

    [Fact]
    public void Preview_cuts_after_the_limit_and_counts_the_rest()
    {
        var preview = ResultCopyText.Preview(new[] { "a", "b", "c", "d" }, 2, out var hidden);

        Assert.Equal("a" + Blank + "b", preview);
        Assert.Equal(2, hidden);
    }
}
