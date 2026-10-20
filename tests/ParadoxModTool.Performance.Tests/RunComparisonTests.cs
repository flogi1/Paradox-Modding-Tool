using Xunit;

namespace ParadoxModTool.Performance.Tests;

public sealed class RunComparisonTests
{
    private static readonly string ModRoot = @"C:\mod";
    private static readonly string VanillaRoot = @"C:\van";
    private static readonly IReadOnlySet<RowOrigin> ModSide = OriginClassifier.ModSide;
    private static readonly IReadOnlySet<RowOrigin> AllOrigins = Enum.GetValues<RowOrigin>().ToHashSet();

    private static string Full(string root, string relative) =>
        Path.Combine(root, "in_game", relative.Replace('/', Path.DirectorySeparatorChar));

    // common/mod_*.txt exists in the mod root only, common/van_*.txt in the vanilla root only.
    private static OriginClassifier Classifier() =>
        new(ModRoot, Array.Empty<string>(), new[] { VanillaRoot }, path =>
            path.StartsWith(Full(ModRoot, "common/mod_"), StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(Full(VanillaRoot, "common/van_"), StringComparison.OrdinalIgnoreCase));

    private static ProfilingSummary Summary(params string[] rows)
    {
        var csv = "File Location;Call Count;Self Time;Total Time\n" + string.Join("\n", rows) + "\n";
        return new ProfilingSummary(ProfilingDumpReader.Read(new StringReader(csv)), Classifier());
    }

    private static RunComparison Compare(ProfilingSummary a, ProfilingSummary b, RunDuration? durationA = null, RunDuration? durationB = null) =>
        new(a, b, durationA ?? new RunDuration(12, "k", true), durationB ?? new RunDuration(12, "k", true), new NoiseSettings());

    private static ComparisonRow Single(string a, string b)
    {
        var row = Compare(Summary(a), Summary(b)).Rows(AllOrigins).Single();
        Assert.Equal("effect @ common/mod_a.txt:1", row.Key);
        return row;
    }

    [Fact]
    public void NoiseSettingsDefaults()
    {
        var noise = new NoiseSettings();

        Assert.Equal(0.10, noise.RelativeThreshold);
        Assert.Equal(0.05, noise.AbsoluteFloor);
        Assert.Equal(0.25, noise.CallsChangeThreshold);
    }

    [Fact]
    public void ClearDropIsBetter()
    {
        var row = Single("effect @ common/mod_a.txt:1;1000;5;10", "effect @ common/mod_a.txt:1;1000;4;8.5");

        Assert.Equal(Verdict.Better, row.Verdict);
        Assert.Equal(-0.15, row.DeltaRatio!.Value, 6);
        Assert.Equal(10.0, row.TotalA);
        Assert.Equal(8.5, row.TotalB);
        Assert.Equal(1000, row.CallsA);
        Assert.Equal(1000, row.CallsB);
        Assert.Equal(RowOrigin.Mod, row.Origin);
    }

    [Fact]
    public void SmallRelativeChangeIsUnchanged()
    {
        var row = Single("effect @ common/mod_a.txt:1;1000;5;10", "effect @ common/mod_a.txt:1;1000;5;9.5");

        Assert.Equal(Verdict.Unchanged, row.Verdict);
        Assert.Equal(-0.05, row.DeltaRatio!.Value, 6);
    }

    [Fact]
    public void ChangeBelowAbsoluteFloorIsUnchangedEvenWhenDoubled()
    {
        var row = Single("effect @ common/mod_a.txt:1;1000;0.04;0.04", "effect @ common/mod_a.txt:1;1000;0.08;0.08");

        Assert.Equal(Verdict.Unchanged, row.Verdict);
        Assert.Equal(1.0, row.DeltaRatio!.Value, 6);
    }

    [Fact]
    public void ClearRiseIsWorse()
    {
        var row = Single("effect @ common/mod_a.txt:1;1000;5;10", "effect @ common/mod_a.txt:1;1000;6;12");

        Assert.Equal(Verdict.Worse, row.Verdict);
        Assert.Equal(0.2, row.DeltaRatio!.Value, 6);
    }

    [Fact]
    public void CallsDropFlagsBehaviourChangedInsteadOfBetter()
    {
        var row = Single("effect @ common/mod_a.txt:1;1000;5;10", "effect @ common/mod_a.txt:1;600;3;6");

        Assert.Equal(Verdict.BehaviorChanged, row.Verdict);
        Assert.Equal(-0.4, row.DeltaRatio!.Value, 6);
    }

    [Fact]
    public void CallsChangeExactlyAtThresholdIsNotBehaviourChanged()
    {
        var row = Single("effect @ common/mod_a.txt:1;1000;5;10", "effect @ common/mod_a.txt:1;750;3;6");

        Assert.Equal(Verdict.Better, row.Verdict);
    }

    [Fact]
    public void ZeroCallsInANeverFlagsBehaviourChanged()
    {
        var row = Single("effect @ common/mod_a.txt:1;0;0;0", "effect @ common/mod_a.txt:1;500;3;6");

        Assert.Equal(Verdict.Worse, row.Verdict);
        Assert.Null(row.DeltaRatio);
    }

    [Fact]
    public void ZeroTotalInAUsesOnlyTheAbsoluteFloor()
    {
        var small = Single("effect @ common/mod_a.txt:1;10;0;0", "effect @ common/mod_a.txt:1;10;0.05;0.05");
        var large = Single("effect @ common/mod_a.txt:1;10;0;0", "effect @ common/mod_a.txt:1;10;0.5;0.5");

        Assert.Equal(Verdict.Unchanged, small.Verdict);
        Assert.Equal(Verdict.Worse, large.Verdict);
        Assert.Null(large.DeltaRatio);
    }

    [Fact]
    public void RowMissingInBAndNewInB()
    {
        var a = Summary(
            "effect @ common/mod_a.txt:1;1000;5;10",
            "effect @ common/mod_gone.txt:2;40;2;4");
        var b = Summary(
            "effect @ common/mod_a.txt:1;1000;5;10",
            "effect @ common/van_new.txt:3;7;1;3");

        var rows = Compare(a, b).Rows(AllOrigins).ToDictionary(r => r.Key);

        var gone = rows["effect @ common/mod_gone.txt:2"];
        Assert.Equal(Verdict.MissingInB, gone.Verdict);
        Assert.Equal(40, gone.CallsA);
        Assert.Null(gone.CallsB);
        Assert.Equal(4.0, gone.TotalA);
        Assert.Null(gone.TotalB);
        Assert.Null(gone.DeltaRatio);
        Assert.Equal(RowOrigin.Mod, gone.Origin);

        var added = rows["effect @ common/van_new.txt:3"];
        Assert.Equal(Verdict.NewInB, added.Verdict);
        Assert.Null(added.CallsA);
        Assert.Equal(7, added.CallsB);
        Assert.Null(added.TotalA);
        Assert.Equal(3.0, added.TotalB);
        Assert.Null(added.DeltaRatio);
        Assert.Equal(RowOrigin.Vanilla, added.Origin);
    }

    [Fact]
    public void ModSelfSumsEveryModSideRowOfEachRunIncludingUnmatchedOnes()
    {
        var a = Summary(
            "effect @ common/mod_a.txt:1;1000;5;10",
            "effect @ common/mod_gone.txt:2;40;2;4",
            "effect @ common/van_x.txt:3;9;100;100");
        var b = Summary(
            "effect @ common/mod_a.txt:1;1000;3;8",
            "effect @ common/mod_new.txt:4;7;1;3",
            "effect @ common/van_x.txt:3;9;100;100");

        var comparison = Compare(a, b);

        Assert.Equal(7.0, comparison.ModSelfA, 6);
        Assert.Equal(4.0, comparison.ModSelfB, 6);
        Assert.Equal((4.0 - 7.0) / 7.0, comparison.ModSelfDeltaRatio!.Value, 6);
    }

    [Fact]
    public void ModSelfDeltaRatioIsNullWhenAIsZero()
    {
        var comparison = Compare(Summary("effect @ common/van_x.txt:3;9;1;1"), Summary("effect @ common/mod_a.txt:1;9;1;1"));

        Assert.Equal(0.0, comparison.ModSelfA);
        Assert.Equal(1.0, comparison.ModSelfB);
        Assert.Null(comparison.ModSelfDeltaRatio);
    }

    [Fact]
    public void RowsFilterByOriginAndSortByTheLargerTotalOfEitherSide()
    {
        var a = Summary(
            "effect @ common/mod_small.txt:1;10;1;2",
            "effect @ common/mod_gone.txt:2;10;1;30",
            "effect @ common/van_x.txt:3;10;1;99");
        var b = Summary(
            "effect @ common/mod_small.txt:1;10;1;2",
            "effect @ common/mod_new.txt:4;10;1;20",
            "effect @ common/van_x.txt:3;10;1;99");

        var comparison = Compare(a, b);

        Assert.Equal(
            new[] { "effect @ common/mod_gone.txt:2", "effect @ common/mod_new.txt:4", "effect @ common/mod_small.txt:1" },
            comparison.Rows(ModSide).Select(r => r.Key));
        Assert.Equal("effect @ common/van_x.txt:3", comparison.Rows(AllOrigins).First().Key);
        Assert.Empty(comparison.Rows(new HashSet<RowOrigin> { RowOrigin.OtherMod }));
    }

    [Fact]
    public void AMatchedRowThatGrewFromTinyToLargeSortsBeforeALargerStableRow()
    {
        var a = Summary(
            "effect @ common/mod_big.txt:1;10;1;10",
            "effect @ common/mod_regressed.txt:2;10;1;0.1");
        var b = Summary(
            "effect @ common/mod_big.txt:1;10;1;10",
            "effect @ common/mod_regressed.txt:2;10;1;20");

        var rows = Compare(a, b).Rows(ModSide);

        Assert.Equal(new[] { "effect @ common/mod_regressed.txt:2", "effect @ common/mod_big.txt:1" }, rows.Select(r => r.Key));
        Assert.Equal(Verdict.Worse, rows[0].Verdict);
        Assert.Equal(new[] { "common/mod_regressed.txt", "common/mod_big.txt" }, Compare(a, b).RowsByFile(ModSide).Select(r => r.Key));
    }

    [Theory]
    [InlineData(10.0, 8.5, Verdict.Better)]
    [InlineData(10.0, 9.5, Verdict.Unchanged)]
    [InlineData(10.0, 12.0, Verdict.Worse)]
    [InlineData(0.0, 0.04, Verdict.Unchanged)]
    [InlineData(0.0, 0.5, Verdict.Worse)]
    public void ModSelfVerdictAppliesTheNoiseRuleToTheModSelfTimes(double modSelfA, double modSelfB, Verdict expected)
    {
        var comparison = Compare(
            Summary($"effect @ common/mod_a.txt:1;1;{modSelfA.ToString(System.Globalization.CultureInfo.InvariantCulture)};1"),
            Summary($"effect @ common/mod_a.txt:1;1;{modSelfB.ToString(System.Globalization.CultureInfo.InvariantCulture)};1"));

        Assert.Equal(modSelfA, comparison.ModSelfA, 6);
        Assert.Equal(modSelfB, comparison.ModSelfB, 6);
        Assert.Equal(expected, comparison.ModSelfVerdict);
    }

    [Fact]
    public void ModSelfVerdictIgnoresTheCallCountChangeThatFlagsRows()
    {
        var comparison = Compare(Summary("effect @ common/mod_a.txt:1;1000;10;10"), Summary("effect @ common/mod_a.txt:1;100;5;5"));

        Assert.Equal(Verdict.BehaviorChanged, comparison.Rows(ModSide).Single().Verdict);
        Assert.Equal(Verdict.Better, comparison.ModSelfVerdict);
    }

    [Fact]
    public void SameMonthsOnlyWhenBothKnownAndEqual()
    {
        var s = Summary("effect @ common/mod_a.txt:1;1;1;1");

        Assert.True(Compare(s, s, new RunDuration(12, "k", true), new RunDuration(12, "k", true)).SameMonths);
        Assert.False(Compare(s, s, new RunDuration(12, "k", true), new RunDuration(24, "k", true)).SameMonths);
        Assert.False(Compare(s, s, new RunDuration(null, "k", false), new RunDuration(12, "k", true)).SameMonths);
        Assert.False(Compare(s, s, new RunDuration(12, "k", true), new RunDuration(null, "k", false)).SameMonths);
        Assert.False(Compare(s, s, new RunDuration(null, "k", false), new RunDuration(null, "k", false)).SameMonths);
    }

    [Fact]
    public void RowsByFileSurvivesALineShiftThatSplitsTheExactKeyRows()
    {
        var a = Summary(
            "effect @ common/mod_a.txt:10;100;1;2.0",
            "effect @ common/mod_other.txt:1;5;1;3");
        var b = Summary(
            "effect @ common/mod_a.txt:12;100;1;1.5",
            "effect @ common/mod_other.txt:1;5;1;3");

        var comparison = Compare(a, b);

        var exact = comparison.Rows(ModSide).ToDictionary(r => r.Key);
        Assert.Equal(Verdict.MissingInB, exact["effect @ common/mod_a.txt:10"].Verdict);
        Assert.Equal(Verdict.NewInB, exact["effect @ common/mod_a.txt:12"].Verdict);

        var byFile = comparison.RowsByFile(ModSide).ToDictionary(r => r.Key);
        Assert.Equal(2, byFile.Count);
        var moved = byFile["common/mod_a.txt"];
        Assert.Equal(Verdict.Better, moved.Verdict);
        Assert.Equal(2.0, moved.TotalA);
        Assert.Equal(1.5, moved.TotalB);
        Assert.Equal(-0.25, moved.DeltaRatio!.Value, 6);
        Assert.Equal(RowOrigin.Mod, moved.Origin);
        Assert.Equal(Verdict.Unchanged, byFile["common/mod_other.txt"].Verdict);
    }

    [Fact]
    public void RowsByFileSumsCallsAndTotalPerPathAndAppliesTheSameRules()
    {
        var a = Summary(
            "effect @ common/mod_a.txt:1;100;1;4",
            "trigger @ common/mod_a.txt:9;100;1;6",
            "effect @ common/mod_gone.txt:1;10;1;7",
            "effect @ common/mod_calls.txt:1;100;1;4",
            "effect @ common/mod_calls.txt:2;100;1;4");
        var b = Summary(
            "effect @ common/mod_a.txt:2;100;1;5",
            "trigger @ common/mod_a.txt:9;100;1;7",
            "effect @ common/van_new.txt:1;10;1;3",
            "effect @ common/mod_calls.txt:1;50;1;2",
            "effect @ common/mod_calls.txt:2;50;1;2");

        var rows = Compare(a, b).RowsByFile(AllOrigins).ToDictionary(r => r.Key);

        var summed = rows["common/mod_a.txt"];
        Assert.Equal(200, summed.CallsA);
        Assert.Equal(200, summed.CallsB);
        Assert.Equal(10.0, summed.TotalA);
        Assert.Equal(12.0, summed.TotalB);
        Assert.Equal(Verdict.Worse, summed.Verdict);
        Assert.Equal(Verdict.BehaviorChanged, rows["common/mod_calls.txt"].Verdict);
        Assert.Equal(Verdict.MissingInB, rows["common/mod_gone.txt"].Verdict);
        Assert.Equal(Verdict.NewInB, rows["common/van_new.txt"].Verdict);
        Assert.Equal(RowOrigin.Vanilla, rows["common/van_new.txt"].Origin);
        Assert.Equal(
            new[] { "common/mod_a.txt", "common/mod_calls.txt", "common/mod_gone.txt", "common/van_new.txt" },
            Compare(a, b).RowsByFile(AllOrigins).Select(r => r.Key));
    }

    [Fact]
    public void RealPairComparesTheIv2FilesAsModSide()
    {
        // The IV2 mod's own files are the ones with iv2 in the name; everything else is vanilla.
        var classifier = new OriginClassifier(ModRoot, Array.Empty<string>(), new[] { VanillaRoot },
            path => path.Contains("iv2", StringComparison.OrdinalIgnoreCase)
                ? path.StartsWith(ModRoot, StringComparison.OrdinalIgnoreCase)
                : path.StartsWith(VanillaRoot, StringComparison.OrdinalIgnoreCase));
        var dumpA = ProfilingDumpReader.ReadFile(RepoFiles.Data("eu5/iv2_before.csv"));
        var dumpB = ProfilingDumpReader.ReadFile(RepoFiles.Data("eu5/iv2_after.csv"));

        var comparison = new RunComparison(
            new ProfilingSummary(dumpA, classifier), new ProfilingSummary(dumpB, classifier),
            RunDuration.From(dumpA, null), RunDuration.From(dumpB, null), new NoiseSettings());

        // ModSelf is the Self sum of exactly the iv2 rows, independent of any row matching.
        Assert.Equal(dumpA.Rows.Where(r => r.Path.Contains("iv2", StringComparison.OrdinalIgnoreCase)).Sum(r => r.Self), comparison.ModSelfA, 6);
        Assert.Equal(dumpB.Rows.Where(r => r.Path.Contains("iv2", StringComparison.OrdinalIgnoreCase)).Sum(r => r.Self), comparison.ModSelfB, 6);
        Assert.True(comparison.ModSelfA > 0);
        Assert.NotNull(comparison.ModSelfDeltaRatio);
        Assert.False(comparison.SameMonths);
        Assert.DoesNotContain(comparison.Rows(new HashSet<RowOrigin> { RowOrigin.Vanilla }), r => r.Key.Contains("iv2", StringComparison.OrdinalIgnoreCase));

        // Exact-key rows: the matched mod-side rows with a non-Unchanged verdict. The research bureaucracy script value
        // moved from line 101 to 67, so it splits into MissingInB + NewInB here (RowsByFile pairs it below).
        var exact = comparison.Rows(ModSide).ToDictionary(r => r.Key);
        Assert.Equal(Verdict.BehaviorChanged, exact["named_script_value @ common/script_values/02_iv2_script_values_progress.txt:5"].Verdict);
        Assert.Equal(Verdict.Worse, exact["trigger @ common/generic_action_ai_lists/iv2_generic_action_list_religion.txt:6"].Verdict);
        // Total 0.075 -> 0 with one call: only just over the absolute floor, so this Better sits at the noise edge.
        Assert.Equal(Verdict.Better, exact["effect @ common/generic_actions/iv2_ga_idea_groups_army.txt:1445"].Verdict);
        Assert.Equal(Verdict.MissingInB, exact["script_value @ common/bureaucracies/iv2_research_system_bureaucracies.txt:101"].Verdict);
        Assert.Equal(Verdict.NewInB, exact["script_value @ common/bureaucracies/iv2_research_system_bureaucracies.txt:67"].Verdict);

        // Per file the moved bureaucracy row is paired: Total 1.67 -> 1.392 is a real improvement.
        var bureaucracy = comparison.RowsByFile(ModSide).Single(r => r.Key == "common/bureaucracies/iv2_research_system_bureaucracies.txt");
        Assert.Equal(Verdict.Better, bureaucracy.Verdict);
        Assert.Equal(1.67, bureaucracy.TotalA!.Value, 6);
        Assert.Equal(1.392, bureaucracy.TotalB!.Value, 6);
    }
}
