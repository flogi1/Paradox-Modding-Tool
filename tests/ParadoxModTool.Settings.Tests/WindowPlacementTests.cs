using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class WindowPlacementTests
{
    private static readonly ScreenArea Laptop = new(0, 0, 1920, 1040);
    private static readonly ScreenArea Monitor = new(1920, 0, 2560, 1400);

    [Fact]
    public void OpensCenteredOnTheOtherScreen()
    {
        var bounds = WindowPlacement.OnOtherScreen(new[] { Laptop, Monitor }, 960, 520, 1250, 800)!;

        Assert.Equal((2575d, 300d, 1250d, 800d), (bounds.Left, bounds.Top, bounds.Width, bounds.Height));
    }

    [Fact]
    public void UsesTheFirstScreenWhenTheOwnerIsOnTheSecond()
    {
        var bounds = WindowPlacement.OnOtherScreen(new[] { Laptop, Monitor }, 3000, 500, 1250, 800)!;

        Assert.Equal((335d, 120d), (bounds.Left, bounds.Top));
    }

    [Fact]
    public void ShrinksToASmallScreenAndPicksTheLargestOther()
    {
        var small = new ScreenArea(-1024, 0, 1024, 700);
        var bounds = WindowPlacement.OnOtherScreen(new[] { Laptop, small }, 960, 520, 1250, 800)!;
        Assert.Equal((-1004d, 20d, 984d, 660d), (bounds.Left, bounds.Top, bounds.Width, bounds.Height));

        var largest = WindowPlacement.OnOtherScreen(new[] { Laptop, small, Monitor }, 960, 520, 1250, 800)!;
        Assert.Equal(2575d, largest.Left);
    }

    [Fact]
    public void NoOtherScreenGivesNull()
    {
        Assert.Null(WindowPlacement.OnOtherScreen(new[] { Laptop }, 960, 520, 1250, 800));
        Assert.Null(WindowPlacement.OnOtherScreen(Array.Empty<ScreenArea>(), 960, 520, 1250, 800));
    }
}
