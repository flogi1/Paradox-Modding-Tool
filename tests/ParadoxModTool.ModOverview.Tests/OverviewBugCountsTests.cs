using ParadoxModTool.Mods;
using ParadoxModTool.Notes;
using Xunit;
using static ParadoxModTool.ModOverview.Tests.TestMods;

namespace ParadoxModTool.ModOverview.Tests;

public class OverviewBugCountsTests
{
    [Fact]
    public void BugCounts_MapToProjectRoot()
    {
        var main = Mod("Main");
        var addon = Mod("Addon", role: ModRole.Submod, root: main);
        var single = Mod("Single");
        var bugs = new BugData
        {
            Reports =
            {
                new BugReport { ModId = "Addon", Status = BugStatus.Open, Seen = true },
                new BugReport { ModId = "Main", Status = BugStatus.Open, Seen = false },
                new BugReport { ModId = "Main", Status = BugStatus.Done, Seen = false },
                new BugReport { ModId = "Main", Status = BugStatus.Open, Seen = false, Removed = true },
                new BugReport { ModId = "unknown", Status = BugStatus.Open, Seen = false },
                new BugReport { ModId = null, Status = BugStatus.Open, Seen = false },
            },
        };

        var counts = OverviewBugCounts.Compute(Snapshot(main, addon, single), bugs);

        Assert.Equal(new BugCount(2, 2), counts[main]);
        Assert.False(counts.ContainsKey(addon));
        Assert.False(counts.ContainsKey(single));
    }
}
