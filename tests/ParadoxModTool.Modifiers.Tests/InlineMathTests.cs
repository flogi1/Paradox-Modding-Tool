using Xunit;

namespace ParadoxModTool.Modifiers.Tests;

public class InlineMathTests
{
    private static decimal? Eval(string expression) => InlineMath.Evaluate(expression, name => name switch
    {
        "base" => 0.1m,
        "step" => 0.05m,
        _ => null,
    });

    [Theory]
    [InlineData("base", 0.1)]
    [InlineData(" base + step ", 0.15)]
    [InlineData("base + step * 2", 0.2)]
    [InlineData("(base + step) * 2", 0.3)]
    [InlineData("-base", -0.1)]
    [InlineData("10 / 4", 2.5)]
    [InlineData("1 - 2 - 3", -4)]
    [InlineData(".5 + 1", 1.5)]
    public void EvaluatesArithmetic(string expression, double expected) => Assert.Equal((decimal)expected, Eval(expression));

    [Theory]
    [InlineData("unknown")]
    [InlineData("base +")]
    [InlineData("1 / 0")]
    [InlineData("abs(base)")]
    [InlineData("(base")]
    [InlineData("base base")]
    [InlineData("")]
    public void ReturnsNullForWhatItCannotEvaluate(string expression) => Assert.Null(Eval(expression));
}
