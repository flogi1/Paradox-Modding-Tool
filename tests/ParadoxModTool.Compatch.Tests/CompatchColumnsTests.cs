using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class CompatchColumnsTests
{
    [Theory]
    [InlineData(false, false, new[] { CompatchColumn.A, CompatchColumn.Result, CompatchColumn.B })]
    [InlineData(true, false, new[] { CompatchColumn.A, CompatchColumn.Result, CompatchColumn.B, CompatchColumn.Vanilla })]
    [InlineData(false, true, new[] { CompatchColumn.A, CompatchColumn.B, CompatchColumn.Result })]
    [InlineData(true, true, new[] { CompatchColumn.A, CompatchColumn.B, CompatchColumn.Vanilla, CompatchColumn.Result })]
    public void Result_stands_in_the_middle_or_after_all_compared_columns(bool showVanilla, bool resultRight, CompatchColumn[] expected)
    {
        Assert.Equal(expected, CompatchColumns.Order(showVanilla, resultRight));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Divider_only_before_a_result_on_the_right(bool resultRight, bool expected)
    {
        Assert.Equal(expected, CompatchColumns.DividerBefore(CompatchColumn.Result, resultRight));
        Assert.False(CompatchColumns.DividerBefore(CompatchColumn.B, resultRight));
    }
}
