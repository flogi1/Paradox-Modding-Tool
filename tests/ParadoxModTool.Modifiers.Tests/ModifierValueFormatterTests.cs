using System.Globalization;
using Xunit;

namespace ParadoxModTool.Modifiers.Tests;

public class ModifierValueFormatterTests
{
    private static ModifierFormat Pct(int decimals = 2) => new(true, false, decimals, 1, false, true, false);

    [Theory]
    [InlineData("0.1", "+10 %")]
    [InlineData("-0.25", "-25 %")]
    [InlineData("0.125", "+12.5 %")]
    [InlineData("0", "0 %")]
    [InlineData("-0.00001", "0 %")]
    public void PercentValues(string value, string expected) =>
        Assert.Equal(expected, ModifierValueFormatter.Format(decimal.Parse(value, CultureInfo.InvariantCulture), Pct()));

    [Fact]
    public void DecimalsZeroRoundsHalfAwayFromZero() => Assert.Equal("+13 %", ModifierValueFormatter.Format(0.125m, Pct(0)));

    [Fact]
    public void AlreadyPercentKeepsTheValue() => Assert.Equal("+5 %", ModifierValueFormatter.Format(5m, new(false, true, 2, 1, false, true, false)));

    [Fact]
    public void FactorWithoutUnit() => Assert.Equal("+500", ModifierValueFormatter.Format(0.5m, new(false, false, 0, 1000, false, true, false)));

    [Fact]
    public void NoDifferenceSignDropsThePlus() => Assert.Equal("10 %", ModifierValueFormatter.Format(0.1m, Pct() with { NoDifferenceSign = true }));

    [Fact]
    public void NoDifferenceSignKeepsTheMinus() => Assert.Equal("-10 %", ModifierValueFormatter.Format(-0.1m, Pct() with { NoDifferenceSign = true }));

    [Fact]
    public void WithoutTrimmingAllDecimalsStay() => Assert.Equal("+12.50 %", ModifierValueFormatter.Format(0.125m, Pct() with { TrimTrailingZeros = false }));

    [Fact]
    public void GermanCultureStillUsesAPoint()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            Assert.Equal("+12.5 %", ModifierValueFormatter.Format(0.125m, Pct()));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
