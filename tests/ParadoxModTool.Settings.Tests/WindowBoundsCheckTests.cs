using ParadoxModTool.Session;
using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class WindowBoundsCheckTests
{
    private static bool Usable(WindowBoundsState? saved) => WindowBoundsCheck.IsUsable(saved, 0, 0, 1920, 1080, 520, 360);

    [Fact]
    public void NullIsNotUsable() => Assert.False(Usable(null));

    [Fact]
    public void WindowOnScreenIsUsable() => Assert.True(Usable(new WindowBoundsState { Left = 100, Top = 100, Width = 760, Height = 560 }));

    [Fact]
    public void TooSmallIsNotUsable() => Assert.False(Usable(new WindowBoundsState { Left = 100, Top = 100, Width = 200, Height = 560 }));

    [Fact]
    public void OffScreenIsNotUsable() => Assert.False(Usable(new WindowBoundsState { Left = 5000, Top = 100, Width = 760, Height = 560 }));

    [Fact]
    public void TitleBarAboveScreenIsNotUsable() => Assert.False(Usable(new WindowBoundsState { Left = 100, Top = -500, Width = 760, Height = 560 }));

    [Fact]
    public void NaNIsNotUsable() => Assert.False(Usable(new WindowBoundsState { Left = double.NaN, Top = 100, Width = 760, Height = 560 }));

    [Fact]
    public void PartlyVisibleTitleBarIsUsable() => Assert.True(Usable(new WindowBoundsState { Left = -200, Top = 10, Width = 760, Height = 560 }));
}
