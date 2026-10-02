using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class CompareNavigationTests
{
    private static AlignedRow Row(string? a, string? b, RowOrigin origin = RowOrigin.Same) => new(a, a == b ? a : b, b, null, origin);

    [Fact]
    public void Stops_mark_the_first_row_of_each_difference_run()
    {
        var rows = new[] { Row("x", "x"), Row("a", "b"), Row(null, "c"), Row("y", "y"), Row("d", null) };

        Assert.Equal(new[] { 1, 4 }, CompareNavigation.Stops(rows));
    }

    [Fact]
    public void Rows_the_result_invented_count_as_difference()
    {
        Assert.True(CompareNavigation.IsDifference(Row("x", "x", RowOrigin.Other)));
        Assert.False(CompareNavigation.IsDifference(Row("x", "x")));
    }

    [Theory]
    [InlineData(0, true, 1)]
    [InlineData(1, true, 4)]
    [InlineData(4, true, null)]
    [InlineData(4, false, 1)]
    [InlineData(1, false, null)]
    public void Next_does_not_wrap(int current, bool forward, int? expected)
    {
        Assert.Equal(expected, CompareNavigation.Next(new[] { 1, 4 }, current, forward));
    }

    [Fact]
    public void RowOf_prefers_the_whole_sequence_over_a_repeated_single_line()
    {
        var whole = new[]
        {
            Row("farm = {", "farm = {"),
            Row("cost = 10", "cost = 12"),
            Row("time = 1", "time = 1"),
            Row("}", "}"),
            Row("mine = {", "mine = {"),
            Row("cost = 10", "cost = 12"),
            Row("time = 1", "time = 2"),
            Row("}", "}"),
        };
        var part = new[] { Row("  cost = 10", "cost = 12  "), Row("time = 1", "time = 2") };

        Assert.Equal(5, CompareNavigation.RowOf(whole, part));
    }

    [Fact]
    public void RowOf_falls_back_to_the_first_matching_difference_row()
    {
        var whole = new[] { Row("x", "x"), Row("cost = 1", "cost = 2") };
        var part = new[] { Row("cost = 1", "cost = 2"), Row("gone", "other") };

        Assert.Equal(1, CompareNavigation.RowOf(whole, part));
    }

    [Fact]
    public void TakeRows_replaces_the_result_lines_of_the_marked_rows_with_the_column()
    {
        var rows = new[]
        {
            new AlignedRow("farm = {", "farm = {", "farm = {", null, RowOrigin.Same),
            new AlignedRow("\tcost = 2", "\tcost = 3", "\tcost = 3", null, RowOrigin.FromB),
            new AlignedRow(null, "\ttime = 2", "\ttime = 2", null, RowOrigin.FromB),
            new AlignedRow("\tsize = 1", null, null, null, RowOrigin.Gap),
            new AlignedRow("}", "}", "}", null, RowOrigin.Same),
        };

        Assert.Equal("farm = {\n\tcost = 2\n\tsize = 1\n}", CompareNavigation.TakeRows(rows, 1, 3, CompatchColumn.A));
        Assert.Equal("farm = {\n\tcost = 3\n\ttime = 2\n}", CompareNavigation.TakeRows(rows, 3, 1, CompatchColumn.B));
    }

    [Fact]
    public void Next_visible_prefers_the_following_entry_then_the_previous()
    {
        var before = new[] { "a", "b", "c", "d" };

        Assert.Equal("c", ActionOrder.NextVisible(before, "b", new HashSet<string> { "a", "c", "d" }));
        Assert.Equal("c", ActionOrder.NextVisible(before, "d", new HashSet<string> { "a", "c" }));
        Assert.Null(ActionOrder.NextVisible(before, "x", new HashSet<string> { "a" }));
        Assert.Null(ActionOrder.NextVisible(before, "a", new HashSet<string>()));
    }

    [Fact]
    public void RowOf_returns_null_when_nothing_matches()
    {
        Assert.Null(CompareNavigation.RowOf(new[] { Row("x", "x") }, new[] { Row("a", "b") }));
    }
}
