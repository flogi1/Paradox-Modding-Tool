using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

public class LaunchOptionMemoryTests
{
    private static readonly string[] Options = { "", "-debug_mode", "--ignore-disable-mods-on-crash", "-debug_mode --ignore-disable-mods-on-crash" };

    [Fact]
    public void SavedArgumentsSelectTheirOption()
    {
        Assert.Equal(1, LaunchOptionMemory.IndexOf(Options, "-debug_mode"));
        Assert.Equal(3, LaunchOptionMemory.IndexOf(Options, "-debug_mode --ignore-disable-mods-on-crash"));
        Assert.Equal(0, LaunchOptionMemory.IndexOf(Options, ""));
    }

    [Fact]
    public void NothingSavedOrUnknownArgumentsSelectTheFirstOption()
    {
        Assert.Equal(0, LaunchOptionMemory.IndexOf(Options, null));
        Assert.Equal(0, LaunchOptionMemory.IndexOf(Options, "-removed_option"));
    }

    [Fact]
    public void NoOptionsSelectNothing()
    {
        Assert.Equal(-1, LaunchOptionMemory.IndexOf(Array.Empty<string>(), "-debug_mode"));
    }
}
