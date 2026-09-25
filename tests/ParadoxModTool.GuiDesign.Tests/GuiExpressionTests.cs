using ParadoxModTool.GuiDesign.Resolve;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiExpressionTests
{
    private static double? Lookup(string name) => name switch
    {
        "width" => 300,
        "margin" => 10,
        _ => null,
    };

    [Theory]
    [InlineData("1 + 2 * 3", 7)]
    [InlineData("(1 + 2) * 3", 9)]
    [InlineData("width - margin * 2", 280)]
    [InlineData("-margin / 4", -2.5)]
    [InlineData("0.5 * width", 150)]
    public void EvaluatesArithmeticWithConstants(string expression, double expected)
    {
        Assert.True(GuiExpression.TryEvaluate(expression, Lookup, out var value));
        Assert.Equal(expected, value, 6);
    }

    [Theory]
    [InlineData("unknown + 1")]
    [InlineData("GetPlayer.GetName")]
    [InlineData("1 +")]
    [InlineData("(1 + 2")]
    [InlineData("1 / 0")]
    public void FailsOnUnknownNamesSyntaxErrorsAndDivisionByZero(string expression)
    {
        Assert.False(GuiExpression.TryEvaluate(expression, Lookup, out _));
    }

    [Fact]
    public void FormatsWithInvariantCulture()
    {
        Assert.Equal("2.5", GuiExpression.Format(2.5));
        Assert.Equal("280", GuiExpression.Format(280));
    }
}
