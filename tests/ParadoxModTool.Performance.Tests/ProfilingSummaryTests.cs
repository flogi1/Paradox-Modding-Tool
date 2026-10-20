using Xunit;

namespace ParadoxModTool.Performance.Tests;

public sealed class ProfilingSummaryTests
{
    private static readonly string ModRoot = @"C:\mod";
    private static readonly string VanillaRoot = @"C:\van";

    private static string Full(string root, string relative) =>
        Path.Combine(root, "in_game", relative.Replace('/', Path.DirectorySeparatorChar));

    private static OriginClassifier Classifier(params string[] existing)
    {
        var set = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        return new OriginClassifier(ModRoot, Array.Empty<string>(), new[] { VanillaRoot }, set.Contains);
    }

    // mod_a: Mod, mod_b: Mod, both_a: Both, van_a: Vanilla, plus a zero row in mod_zero.
    private static ProfilingSummary Create()
    {
        const string csv =
            "File Location;Call Count;Self Time;Total Time\n" +
            "trigger @ common/mod_a.txt:10;5;1.5;4\n" +
            "effect @ common/mod_b.txt:20;50;0.5;9\n" +
            "script_value @ common/both_a.txt:7;2;2;6\n" +
            "trigger @ common/van_a.txt:3;100;4;20\n" +
            "effect @ common/mod_zero.txt:1;0;0;0\n" +
            "script_value @ common/mod_a.txt:10;9;1;3\n";
        var dump = ProfilingDumpReader.Read(new StringReader(csv));
        var classifier = Classifier(
            Full(ModRoot, "common/mod_a.txt"),
            Full(ModRoot, "common/mod_b.txt"),
            Full(ModRoot, "common/mod_zero.txt"),
            Full(ModRoot, "common/both_a.txt"),
            Full(VanillaRoot, "common/both_a.txt"),
            Full(VanillaRoot, "common/van_a.txt"));
        return new ProfilingSummary(dump, classifier);
    }

    [Fact]
    public void SelfByOriginSumsPerOriginAndHasEveryOrigin()
    {
        var summary = Create();

        Assert.Equal(3.0, summary.SelfByOrigin[RowOrigin.Mod], 6);
        Assert.Equal(2.0, summary.SelfByOrigin[RowOrigin.Both], 6);
        Assert.Equal(4.0, summary.SelfByOrigin[RowOrigin.Vanilla], 6);
        Assert.Equal(0.0, summary.SelfByOrigin[RowOrigin.OtherMod]);
        Assert.Equal(0.0, summary.SelfByOrigin[RowOrigin.Unknown]);
        Assert.Equal(Enum.GetValues<RowOrigin>().Length, summary.SelfByOrigin.Count);
        Assert.Equal(9.0, summary.SelfTotal, 6);
    }

    [Fact]
    public void ModSelfIsModPlusBoth()
    {
        Assert.Equal(5.0, Create().ModSelf, 6);
    }

    [Fact]
    public void RowsAreAReadOnlyViewNotTheInternalList()
    {
        var summary = Create();
        var rows = summary.Rows;

        Assert.IsNotType<List<(ProfilingRow Row, RowOrigin Origin)>>(rows);
        Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<(ProfilingRow Row, RowOrigin Origin)>)rows).Add(default));
        Assert.Equal(rows.Count, summary.Rows.Count);
    }

    [Fact]
    public void RankByTotalFiltersByOriginSortsDescendingAndNumbersFromOne()
    {
        var ranked = Create().Rank(RankBy.Total, OriginClassifier.ModSide, 3);

        Assert.Equal(new[] { 1, 2, 3 }, ranked.Select(r => r.Rank));
        Assert.Equal(
            new[] { "effect @ common/mod_b.txt:20", "script_value @ common/both_a.txt:7", "trigger @ common/mod_a.txt:10" },
            ranked.Select(r => r.Row.Key));
        Assert.Equal(new[] { RowOrigin.Mod, RowOrigin.Both, RowOrigin.Mod }, ranked.Select(r => r.Origin));
    }

    [Fact]
    public void ZeroTimeRowsComeLastButAreNotDropped()
    {
        var ranked = Create().Rank(RankBy.Total, OriginClassifier.ModSide, 100);

        Assert.Equal(5, ranked.Count);
        Assert.Equal("effect @ common/mod_zero.txt:1", ranked[^1].Row.Key);
        Assert.Equal(5, ranked[^1].Rank);
    }

    [Fact]
    public void RankByTotalSelfAndCallsEachUseTheirOwnMeasure()
    {
        // many_calls: highest Calls, lowest Self and Total; high_self: highest Self, lowest Total; high_total: highest Total.
        var csv = string.Join(Environment.NewLine, new[]
        {
            "File Location;Call Count;Self Time;Total Time",
            "trigger @ common/many_calls.txt:1;1000;0.1;1",
            "trigger @ common/high_self.txt:1;10;9;5",
            "trigger @ common/high_total.txt:1;20;3;50",
        });
        var summary = new ProfilingSummary(
            ProfilingDumpReader.Read(new StringReader(csv)),
            Classifier(
                Full(ModRoot, "common/many_calls.txt"),
                Full(ModRoot, "common/high_self.txt"),
                Full(ModRoot, "common/high_total.txt")));
        var all = Enum.GetValues<RowOrigin>().ToHashSet();

        Assert.Equal("trigger @ common/high_total.txt:1", summary.Rank(RankBy.Total, all, 1)[0].Row.Key);
        Assert.Equal("trigger @ common/high_self.txt:1", summary.Rank(RankBy.Self, all, 1)[0].Row.Key);
        Assert.Equal("trigger @ common/many_calls.txt:1", summary.Rank(RankBy.Calls, all, 1)[0].Row.Key);
    }

    [Fact]
    public void TiesAreBrokenByKeyOrdinal()
    {
        const string csv =
            "File Location;Call Count;Self Time;Total Time\n" +
            "trigger @ common/mod_b.txt:2;1;1;1\n" +
            "trigger @ common/mod_a.txt:2;1;1;1\n";
        var summary = new ProfilingSummary(
            ProfilingDumpReader.Read(new StringReader(csv)),
            Classifier(Full(ModRoot, "common/mod_a.txt"), Full(ModRoot, "common/mod_b.txt")));

        var ranked = summary.Rank(RankBy.Total, new HashSet<RowOrigin> { RowOrigin.Mod }, 10);

        Assert.Equal(new[] { "trigger @ common/mod_a.txt:2", "trigger @ common/mod_b.txt:2" }, ranked.Select(r => r.Row.Key));
    }

    [Fact]
    public void RankByLocationMergesKindsSharingPathAndLine()
    {
        var ranked = Create().RankByLocation(RankBy.Total, new HashSet<RowOrigin> { RowOrigin.Mod }, 10);

        var merged = ranked[1];
        Assert.Equal(2, merged.Rank);
        Assert.Equal("common/mod_a.txt", merged.Row.Path);
        Assert.Equal(10, merged.Row.Line);
        Assert.Equal("*", merged.Row.Kind);
        Assert.Equal(9, merged.Row.Calls);
        Assert.Equal(2.5, merged.Row.Self, 6);
        Assert.Equal(7.0, merged.Row.Total, 6);
        Assert.Equal(RowOrigin.Mod, merged.Origin);
        Assert.Equal(3, ranked.Count);
        Assert.Equal("common/mod_a.txt:10", merged.Row.Key);
    }

    [Fact]
    public void RealDumpIsWiredThroughTheClassifier()
    {
        var dump = ProfilingDumpReader.ReadFile(RepoFiles.Data("eu5/iv2_before.csv"));
        // Known files of the cut dump: policy_vote only in the mod, hre only in vanilla, alliance in both.
        var summary = new ProfilingSummary(dump, Classifier(
            Full(ModRoot, "common/resolutions/policy_vote.txt"),
            Full(VanillaRoot, "common/international_organization_special_statuses/hre.txt"),
            Full(ModRoot, "common/scripted_relations/alliance.txt"),
            Full(VanillaRoot, "common/scripted_relations/alliance.txt")));

        var top = summary.Rank(RankBy.Total, Enum.GetValues<RowOrigin>().ToHashSet(), 3);

        Assert.Equal(
            new[] { "common/resolutions/policy_vote.txt", "common/international_organization_special_statuses/hre.txt", "common/scripted_relations/alliance.txt" },
            top.Select(r => r.Row.Path));
        Assert.Equal(new[] { RowOrigin.Mod, RowOrigin.Vanilla, RowOrigin.Both }, top.Select(r => r.Origin));
        Assert.True(summary.SelfByOrigin[RowOrigin.Unknown] > 0);
        Assert.True(summary.ModSelf > 0);
    }
}
