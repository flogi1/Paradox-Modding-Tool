using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.FlagTracker.Tests;

public class FlagSiteTests
{
    [Fact]
    public void FullPath_JoinsRootAndRelative()
    {
        var location = new SiteLocation(@"C:\m", "in_game/common/a.txt", 3);

        Assert.Equal(@"C:\m\in_game\common\a.txt", location.FullPath);
    }
}
