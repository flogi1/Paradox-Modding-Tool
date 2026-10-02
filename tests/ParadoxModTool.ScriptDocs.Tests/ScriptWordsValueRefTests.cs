using Xunit;

namespace ParadoxModTool.ScriptDocs.Tests;

public class ScriptWordsValueRefTests
{
    [Theory]
    [InlineData("\tcounty_fertility_growth_mult = small_gain", 33, "county_fertility_growth_mult", "small_gain")]
    [InlineData("\tcounty_fertility_growth_mult = small_gain", 40, "county_fertility_growth_mult", "small_gain")]
    [InlineData("a=b", 2, "a", "b")]
    [InlineData("x = { a = my_val }", 12, "a", "my_val")]
    [InlineData("a = @const", 5, "a", "@const")]
    [InlineData("a = @const", 8, "a", "@const")]
    [InlineData("a = my_val # Kommentar", 6, "a", "my_val")]
    [InlineData("x = { a = my_val}", 12, "a", "my_val")]
    public void FindsTheNameAndItsKey(string line, int column, string key, string name)
    {
        var reference = ScriptWords.ValueRefAt(line, column);

        Assert.NotNull(reference);
        Assert.Equal(key, reference.Value.Key);
        Assert.Equal(name, reference.Value.Name);
    }

    [Fact]
    public void ReturnsStartAndLengthIncludingTheAtSign() => Assert.Equal((4, 6), ScriptWords.ValueRefAt("a = @const", 7) is { } r ? (r.Start, r.Length) : default);

    [Theory]
    [InlineData("# a = my_val", 8)]
    [InlineData("b = \"a = my_val\"", 12)]
    [InlineData("a >= my_val", 7)]
    [InlineData("a ?= my_val", 7)]
    [InlineData("a = scope:target", 12)]
    [InlineData("a = scope:target", 6)]
    [InlineData("a = root.owner", 6)]
    [InlineData("a = 0.1", 4)]
    [InlineData("a = 10", 4)]
    [InlineData("a = \"text\"", 6)]
    [InlineData("= my_val", 3)]
    [InlineData("my_val", 2)]
    [InlineData("a = @", 4)]
    [InlineData("a = my_val", 1)]
    [InlineData("a = my_val", 30)]
    public void ReturnsNullWhereNoValueReferenceIs(string line, int column) => Assert.Null(ScriptWords.ValueRefAt(line, column));
}
