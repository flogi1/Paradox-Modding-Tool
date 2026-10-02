using ParadoxModTool.Mods;
using ParadoxModTool.Notes;
using Xunit;
using static ParadoxModTool.ModOverview.Tests.TestMods;

namespace ParadoxModTool.ModOverview.Tests;

public class OverviewBugAlertsTests
{
    private static BugReport Report(string modId, bool seen = false, bool removed = false, int day = 1) =>
        new() { ModId = modId, Seen = seen, Removed = removed, Title = modId + day, CreatedUtc = new DateTime(2026, 9, day, 0, 0, 0, DateTimeKind.Utc) };

    [Fact]
    public void NewReportsGroupedByProjectRoot()
    {
        var main = Mod("Main");
        var addon = Mod("Addon", role: ModRole.Submod, root: main);
        var bugs = new BugData
        {
            Reports = { Report("Addon"), Report("Main", seen: true), Report("Main", removed: true), Report("unknown"), Report("Main", day: 2) },
        };

        var group = Assert.Single(OverviewBugAlerts.New(Snapshot(main, addon), bugs));

        Assert.Same(main, group.Root);
        Assert.Equal(new[] { "Main2", "Addon1" }, group.Reports.Select(r => r.Title));
    }

    [Fact]
    public void OrderGameThenRootThenNewestFirst()
    {
        var ckA = Mod("A", "ck3");
        var euB = Mod("B", "eu4");
        var euC = Mod("C", "eu4");
        var bugs = new BugData { Reports = { Report("A"), Report("C"), Report("B") } };

        var groups = OverviewBugAlerts.New(Snapshot(ckA, euC, euB), bugs);

        Assert.Equal(new[] { "B", "C", "A" }, groups.Select(g => g.Root.Mod.Name));
    }

    [Fact]
    public void MoreThanThreeCollapse()
    {
        var main = Mod("Main");
        var three = new BugData { Reports = { Report("Main"), Report("Main"), Report("Main") } };
        var four = new BugData { Reports = { Report("Main"), Report("Main"), Report("Main"), Report("Main") } };

        Assert.False(OverviewBugAlerts.New(Snapshot(main), three)[0].Collapsed);
        Assert.True(OverviewBugAlerts.New(Snapshot(main), four)[0].Collapsed);
    }
}
