using System.Globalization;
using Xunit;

namespace ParadoxModTool.ScriptDocs.Tests;

public class ScriptWordsNumberTests
{
    [Theory]
    [InlineData("\tglobal_tax_modifier = 0.1", 23, "global_tax_modifier", "0.1")]
    [InlineData("a=-0.25", 4, "a", "-0.25")]
    [InlineData("a=-0.25", 2, "a", "-0.25")]
    [InlineData("a = .5", 5, "a", "0.5")]
    [InlineData("a = 10", 4, "a", "10")]
    [InlineData("x = { a = 0.1 }", 11, "a", "0.1")]
    public void FindsTheNumberAndItsKey(string line, int column, string key, string value)
    {
        var number = ScriptWords.NumberAt(line, column);

        Assert.NotNull(number);
        Assert.Equal(key, number.Value.Key);
        Assert.Equal(decimal.Parse(value, CultureInfo.InvariantCulture), number.Value.Value);
    }

    [Fact]
    public void ReturnsStartAndLengthOfTheNumber() => Assert.Equal((4, 4), ScriptWords.NumberAt("a = -0.5 ", 6) is { } n ? (n.Start, n.Length) : default);

    [Theory]
    [InlineData("# a = 0.1", 7)]
    [InlineData("b = \"a = 0.1\"", 10)]
    [InlineData("a >= 0.1", 6)]
    [InlineData("a > 0.1", 5)]
    [InlineData("a ?= 0.1", 6)]
    [InlineData("a != 0.1", 6)]
    [InlineData("a = yes", 5)]
    [InlineData("tag2 = FRA", 3)]
    [InlineData("= 0.1", 3)]
    [InlineData("a = 1444.11.11", 6)]
    [InlineData("a = 0.1b", 5)]
    [InlineData("a = 0.1", 1)]
    [InlineData("a = 0.1", 20)]
    public void ReturnsNullWhereNoModifierValueIs(string line, int column) => Assert.Null(ScriptWords.NumberAt(line, column));

    [Fact]
    public void GermanCultureStillReadsAPoint()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            Assert.Equal(0.1m, ScriptWords.NumberAt("a = 0.1", 5)!.Value.Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
