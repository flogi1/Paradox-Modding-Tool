using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class TraceMatcherTests
{
    private const string Undefined = "Undefined event target 'pmt_probe_missing_scope'";
    private const string Unset = "Event target link 'scope:pmt_probe_missing_scope' returned an unset scope";

    private static readonly ModSource Own = new("Own", "own", IsOwn: true, LoadOrder: 0);

    private static ActiveTrace Trace(bool guarded) => new(Own, "own/in_game/common/on_action/a.txt", "3f2a", 41, 41, 43, guarded, Broken: false);

    private static string[] Run(string time, string detail) => new[]
    {
        $"[{time}][jomini_script_system.cpp:256]: Script system error!",
        $"  Error: {detail}",
        "  Script location: common/on_action/a.txt:41",
    };

    private static List<ErrorEntry> Errors(params string[][] runs) => Catalogs.Classify(runs.SelectMany(r => r).ToArray());

    private static TraceHit Hit(string time, string tag, string id = "3f2a") =>
        new(id, time, "common/on_action/a.txt", 41, new TraceScope("Location", "Capital of " + tag, tag), new TraceScope("Country", tag, tag), Array.Empty<(string, TraceScope)>());

    [Fact]
    public void GuardedHitsAreExact()
    {
        var result = TraceMatcher.Match(Trace(guarded: true), Errors(Run("10:00:00", Undefined)), new[] { Hit("10:00:00", "ENG"), Hit("10:00:01", "FRA"), Hit("10:00:02", "ENG") });

        Assert.Equal(TraceMatchKind.Exact, result.Kind);
        Assert.Equal(3, result.HitCount);
        Assert.Equal(new[] { ("ENG", 2), ("FRA", 1) }, result.Scopes.Select(s => (s.Root!.Id, s.Count)));
    }

    [Fact]
    public void UnguardedWithEqualCountsIsExact()
    {
        var result = TraceMatcher.Match(Trace(guarded: false), Errors(Run("10:00:00", Undefined), Run("10:00:00", Undefined)), new[] { Hit("10:00:00", "ENG"), Hit("10:00:00", "FRA") });

        Assert.Equal(TraceMatchKind.Exact, result.Kind);
    }

    [Fact]
    public void TwoMessagesPerCallStillGiveExact()
    {
        var runs = Enumerable.Range(0, 5).SelectMany(_ => new[] { Run("10:00:00", Undefined), Run("10:00:00", Unset) }).ToArray();
        var hits = new[] { "ENG", "FRA", "BUR", "CAS", "POR" }.Select(tag => Hit("10:00:00", tag)).ToList();

        var result = TraceMatcher.Match(Trace(guarded: false), Errors(runs), hits);

        Assert.Equal(TraceMatchKind.Exact, result.Kind);
        Assert.Equal(5, result.ErrorCount);
    }

    [Fact]
    public void FewerErrorsThanHitsGiveCandidates()
    {
        var hits = new[] { Hit("10:00:00", "ENG"), Hit("10:00:00", "FRA"), Hit("10:00:05", "CAS") };

        var result = TraceMatcher.Match(Trace(guarded: false), Errors(Run("10:00:00", Undefined)), hits);

        Assert.Equal(TraceMatchKind.Candidates, result.Kind);
        Assert.Equal(new[] { "ENG", "FRA" }, result.Scopes.Select(s => s.Root!.Id).OrderBy(t => t));
    }

    [Fact]
    public void ErrorsBeforeTheFirstHitDoNotCount()
    {
        var result = TraceMatcher.Match(Trace(guarded: false), Errors(Run("15:20:00", Undefined)), new[] { Hit("15:30:00", "ENG") });

        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(TraceMatchKind.NoHits, result.Kind);
        Assert.Empty(result.Scopes);
    }

    [Fact]
    public void GuardedHitsWithoutErrorsOfThisRunShowNothing()
    {
        var result = TraceMatcher.Match(Trace(guarded: true), Errors(Run("15:20:00", Undefined)), new[] { Hit("15:30:00", "ENG") });

        Assert.Equal(TraceMatchKind.NoHits, result.Kind);
        Assert.Empty(result.Scopes);
        Assert.Equal(1, result.HitCount);
    }

    [Fact]
    public void NoHitsWhenTheIdNeverAppears()
    {
        var result = TraceMatcher.Match(Trace(guarded: true), Errors(Run("10:00:00", Undefined)), new[] { Hit("10:00:00", "ENG", id: "0000") });

        Assert.Equal(TraceMatchKind.NoHits, result.Kind);
        Assert.Equal(0, result.HitCount);
    }
}
