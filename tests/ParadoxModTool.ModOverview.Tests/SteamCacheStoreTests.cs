using ParadoxModTool.SteamIntegration;
using Xunit;

namespace ParadoxModTool.ModOverview.Tests;

public class SteamCacheStoreTests : TempDirTests
{
    private static readonly DateTime Now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    private string SteamFile => new OverviewPaths(Dir).SteamFile;

    private static WorkshopDetails Details(string id, long subscriptions) =>
        new(id, WorkshopPageState.Ok, "T", null, subscriptions, 1, 2, null);

    [Fact]
    public void FirstSession_NoBaseline()
    {
        var entry = SteamCacheStore.Apply(SteamFile, new[] { Details("1", 100) }, "A", Now)["1"];

        Assert.Equal(100, entry.Subscriptions);
        Assert.Null(entry.BaselineSubscriptions);
        Assert.Null(entry.Trend);
        Assert.Equal(Now, entry.FetchedUtc);
    }

    [Fact]
    public void NewSession_BaselineIsPreviousValue()
    {
        SteamCacheStore.Apply(SteamFile, new[] { Details("1", 100) }, "A", Now);

        var entry = SteamCacheStore.Apply(SteamFile, new[] { Details("1", 220) }, "B", Now)["1"];

        Assert.Equal(100, entry.BaselineSubscriptions);
        Assert.Equal(120, entry.Trend);
    }

    [Fact]
    public void SameSession_KeepsBaseline()
    {
        SteamCacheStore.Apply(SteamFile, new[] { Details("1", 100) }, "A", Now);
        SteamCacheStore.Apply(SteamFile, new[] { Details("1", 220) }, "B", Now);

        var entry = SteamCacheStore.Apply(SteamFile, new[] { Details("1", 230) }, "B", Now)["1"];

        Assert.Equal(100, entry.BaselineSubscriptions);
        Assert.Equal(130, entry.Trend);
        Assert.Equal(230, SteamCacheStore.Load(SteamFile)["1"].Subscriptions);
    }

    [Fact]
    public void MissingIdsUntouched()
    {
        SteamCacheStore.Apply(SteamFile, new[] { Details("1", 100), Details("2", 50) }, "A", Now);

        var all = SteamCacheStore.Apply(SteamFile, new[] { Details("1", 110) }, "B", Now.AddHours(1));

        Assert.Equal(50, all["2"].Subscriptions);
        Assert.Equal("A", all["2"].Session);
        Assert.Equal(Now, all["2"].FetchedUtc);
    }

    [Fact]
    public void CorruptFileIsEmpty()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SteamFile)!);
        File.WriteAllText(SteamFile, "{nope");

        Assert.Empty(SteamCacheStore.Load(SteamFile));
        Assert.Single(SteamCacheStore.Apply(SteamFile, new[] { Details("1", 1) }, "A", Now));
    }
}
