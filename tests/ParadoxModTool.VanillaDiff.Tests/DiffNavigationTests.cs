using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class DiffNavigationTests
{
    [Fact]
    public void Each_run_of_changed_lines_is_one_stop_at_its_first_line()
    {
        Assert.Equal(new[] { 3, 10 }, DiffNavigation.Stops(new[] { 3, 4, 5, 10, 11 }, Array.Empty<int>()));
    }

    [Fact]
    public void Gap_lines_are_stops_unless_they_touch_a_changed_run()
    {
        // Lücke vor Zeile 20: dort fehlt auf dieser Seite, was die andere hat. Lücke vor 6 hängt am Block 3-5 und zählt nicht extra.
        Assert.Equal(new[] { 3, 20 }, DiffNavigation.Stops(new[] { 3, 4, 5 }, new[] { 6, 20 }));
    }

    [Theory]
    [InlineData(1, true, 3)]
    [InlineData(3, true, 10)]
    [InlineData(10, true, null)]
    [InlineData(12, false, 10)]
    [InlineData(10, false, 3)]
    [InlineData(3, false, null)]
    public void Next_moves_to_the_following_or_previous_stop_without_wrapping(int current, bool forward, int? expected)
    {
        Assert.Equal(expected, DiffNavigation.Next(new[] { 3, 10 }, current, forward));
    }
}
