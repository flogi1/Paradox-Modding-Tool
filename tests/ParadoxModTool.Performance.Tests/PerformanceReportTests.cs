using System.Globalization;
using Xunit;

namespace ParadoxModTool.Performance.Tests;

public sealed class PerformanceReportTests
{
    private static readonly string ModRoot = @"C:\mod";
    private static readonly string VanillaRoot = @"C:\van";
    private static readonly RunDuration Twelve = new(12, "k", true);

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

    private static string[] Lines(string text) => text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

    private static string N3(double value) => value.ToString("N3", CultureInfo.CurrentCulture);

    private static string N0(long value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private static string Signed(double ratio) => ratio.ToString("+0%;-0%;0%", CultureInfo.CurrentCulture);

    private static ProfilingSummary SummarySample() => Summary(
        "trigger @ common/mod_a.txt:10;5;1.5;4",
        "effect @ common/mod_b.txt:20;50;0.5;9",
        "trigger @ common/van_a.txt:3;100;4;20",
        "effect @ common/van_b.txt:1;7;2;1");

    [Fact]
    public void SummaryHasHeaderWithRunNameAndMonths()
    {
        var lines = Lines(PerformanceReport.Summary("vorher", SummarySample(), Twelve, 3));

        Assert.Equal(PerformanceTexts.ReportHeader("vorher", 12), lines[0]);
        Assert.Contains("vorher", lines[0]);
        Assert.Contains("12", lines[0]);
    }

    [Fact]
    public void SummaryHeaderSaysMonthsUnknownWithoutDuration()
    {
        var lines = Lines(PerformanceReport.Summary("vorher", SummarySample(), new RunDuration(null, string.Empty, false), 3));

        Assert.Equal(PerformanceTexts.ReportHeader("vorher", null), lines[0]);
    }

    [Fact]
    public void SummaryHasOneLineWithUnitPerOriginAndTheTotals()
    {
        var summary = SummarySample();
        var lines = Lines(PerformanceReport.Summary("vorher", summary, Twelve, 3));

        Assert.Contains(PerformanceTexts.ReportTotals, lines);
        foreach (var origin in Enum.GetValues<RowOrigin>())
        {
            var expected = $"{PerformanceTexts.Origin(origin)}: {N3(summary.SelfByOrigin[origin])} {PerformanceTexts.TimeUnit}";
            Assert.Single(lines, l => l.Trim() == expected);
        }

        Assert.Contains(lines, l => l.Trim() == $"{PerformanceTexts.ReportSum}: {N3(8)} {PerformanceTexts.TimeUnit}");
        Assert.Contains(lines, l => l.Trim() == PerformanceTexts.ReportModSelf(N3(2)));
    }

    [Fact]
    public void SummaryListsOnlyModSideRowsInTheModBlockInRankOrder()
    {
        var lines = Lines(PerformanceReport.Summary("vorher", SummarySample(), Twelve, 3));
        var modStart = Array.IndexOf(lines, PerformanceTexts.ReportTopModTitle(3));
        var allStart = Array.IndexOf(lines, PerformanceTexts.ReportTopAllTitle(3));
        var rows = lines.Skip(modStart + 1).Take(allStart - modStart - 1).ToList();

        Assert.True(modStart > 0 && allStart > modStart);
        Assert.Equal(2, rows.Count); // only mod_b and mod_a are mod-side
        Assert.StartsWith("1.", rows[0]);
        Assert.Contains("effect @ common/mod_b.txt:20", rows[0]);
        Assert.Contains(PerformanceTexts.Origin(RowOrigin.Mod), rows[0]);
        Assert.StartsWith("2.", rows[1]);
        Assert.Contains("trigger @ common/mod_a.txt:10", rows[1]);
        Assert.DoesNotContain(rows, r => r.Contains("van_"));
    }

    [Fact]
    public void SummaryAllOriginsBlockContainsTheVanillaRowInRankOrder()
    {
        var lines = Lines(PerformanceReport.Summary("vorher", SummarySample(), Twelve, 3));
        var rows = lines.Skip(Array.IndexOf(lines, PerformanceTexts.ReportTopAllTitle(3)) + 1).ToList();

        Assert.Equal(3, rows.Count);
        Assert.Equal(
            PerformanceTexts.ReportRowLine(1, "trigger @ common/van_a.txt:3", PerformanceTexts.Origin(RowOrigin.Vanilla), "100", N3(4), N3(20)),
            rows[0]);
        Assert.StartsWith("2.", rows[1]);
        Assert.Contains("effect @ common/mod_b.txt:20", rows[1]);
        Assert.StartsWith("3.", rows[2]);
        Assert.Contains("trigger @ common/mod_a.txt:10", rows[2]);
        Assert.DoesNotContain(lines, l => l.Contains("van_b"));
    }

    [Fact]
    public void SummaryOfAVanillaOnlyRunHasAnEmptyModBlockButStillRanksAllOrigins()
    {
        var summary = Summary("trigger @ common/van_a.txt:3;100;4;20");
        var lines = Lines(PerformanceReport.Summary("basis", summary, Twelve, 3));
        var modStart = Array.IndexOf(lines, PerformanceTexts.ReportTopModTitle(3));
        var allStart = Array.IndexOf(lines, PerformanceTexts.ReportTopAllTitle(3));

        Assert.Equal(modStart + 1, allStart);
        Assert.Contains("van_a.txt:3", lines[^1]);
    }

    [Fact]
    public void SummaryWithTopZeroHasNoRows()
    {
        var lines = Lines(PerformanceReport.Summary("vorher", SummarySample(), Twelve, 0));

        Assert.Equal(PerformanceTexts.ReportTopAllTitle(0), lines[^1]);
        Assert.Equal(PerformanceTexts.ReportTopModTitle(0), lines[^2]);
    }

    private static RunComparison Compare(RunDuration durationA, RunDuration durationB)
    {
        var (a, b) = Pair();
        return new RunComparison(a, b, durationA, durationB, new NoiseSettings());
    }

    // mod_a 10 -> 8.5 (better), mod_b 9 -> 9.2 (unchanged), mod_c only in A, mod_d only in B, mod_s moves from line 5 to 6.
    private static (ProfilingSummary A, ProfilingSummary B) Pair() => (
        Summary(
            "effect @ common/mod_a.txt:1;1000;5;10",
            "effect @ common/mod_b.txt:1;1000;5;9",
            "effect @ common/mod_c.txt:1;10;1;3",
            "effect @ common/mod_s.txt:5;100;2;6",
            "effect @ common/van_a.txt:1;10;1;99"),
        Summary(
            "effect @ common/mod_a.txt:1;1000;4;8.5",
            "effect @ common/mod_b.txt:1;1000;5;9.2",
            "effect @ common/mod_d.txt:1;10;1;2",
            "effect @ common/mod_s.txt:6;100;2;6",
            "effect @ common/van_a.txt:1;10;1;99"));

    [Fact]
    public void ComparisonHasHeaderMonthsNoiseAndModSelf()
    {
        var lines = Lines(PerformanceReport.Comparison("vorher", "nachher", Compare(Twelve, Twelve), 5));

        Assert.Equal(PerformanceTexts.ComparisonHeader("vorher", "nachher"), lines[0]);
        Assert.Contains(PerformanceTexts.MonthsSame(12), lines);
        Assert.Contains(PerformanceTexts.NoiseNote(new NoiseSettings()), lines);
        Assert.Contains(PerformanceTexts.ComparisonModSelf(N3(13), N3(12), Signed(-1.0 / 13), PerformanceTexts.VerdictName(Verdict.Unchanged)), lines);
        Assert.DoesNotContain(lines, l => l == PerformanceTexts.MonthsUnknown);
    }

    [Fact]
    public void ComparisonModSelfLineShowsTheNoiseVerdictWord()
    {
        var better = new RunComparison(Summary("effect @ common/mod_a.txt:1;1;10;10"), Summary("effect @ common/mod_a.txt:1;1;8;8"), Twelve, Twelve, new NoiseSettings());
        var worse = new RunComparison(Summary("effect @ common/mod_a.txt:1;1;10;10"), Summary("effect @ common/mod_a.txt:1;1;12;12"), Twelve, Twelve, new NoiseSettings());

        Assert.Contains(
            PerformanceTexts.ComparisonModSelf(N3(10), N3(8), Signed(-0.2), PerformanceTexts.VerdictName(Verdict.Better)),
            Lines(PerformanceReport.Comparison("a", "b", better, 5)));
        Assert.Contains(
            PerformanceTexts.ComparisonModSelf(N3(10), N3(12), Signed(0.2), PerformanceTexts.VerdictName(Verdict.Worse)),
            Lines(PerformanceReport.Comparison("a", "b", worse, 5)));
    }

    [Fact]
    public void ComparisonWarnsWhenMonthsDiffer()
    {
        var lines = Lines(PerformanceReport.Comparison("vorher", "nachher", Compare(Twelve, new RunDuration(24, "k", true)), 5));

        Assert.Contains(PerformanceTexts.MonthsDiffer(12, 24), lines);
        Assert.DoesNotContain(lines, l => l == PerformanceTexts.MonthsSame(12));
    }

    [Fact]
    public void ComparisonWarnsWhenMonthsAreUnknown()
    {
        var lines = Lines(PerformanceReport.Comparison("vorher", "nachher", Compare(Twelve, new RunDuration(null, string.Empty, false)), 5));

        Assert.Contains(PerformanceTexts.MonthsUnknown, lines);
        Assert.DoesNotContain(lines, l => l == PerformanceTexts.MonthsSame(12));
    }

    [Fact]
    public void ComparisonListsModSideRowsByKeyWithVerdictWordsInSortOrder()
    {
        var lines = Lines(PerformanceReport.Comparison("vorher", "nachher", Compare(Twelve, Twelve), 10));
        var start = Array.IndexOf(lines, PerformanceTexts.ComparisonRowsTitle(10));
        var end = Array.IndexOf(lines, PerformanceTexts.ComparisonFilesTitle(10));
        var rows = lines.Skip(start + 1).Take(end - start - 1).ToList();

        Assert.True(start > 0 && end > start);
        Assert.Equal(6, rows.Count); // mod_a, mod_b, mod_s:5, mod_s:6, mod_c, mod_d
        Assert.Equal(
            PerformanceTexts.ComparisonRowLine(
                1, "effect @ common/mod_a.txt:1", PerformanceTexts.Origin(RowOrigin.Mod), $"{N0(1000)} -> {N0(1000)}",
                $"{N3(10)} -> {N3(8.5)}", Signed(-0.15), PerformanceTexts.VerdictName(Verdict.Better)),
            rows[0]);
        Assert.StartsWith("2.", rows[1]);
        Assert.Contains("mod_b.txt:1", rows[1]);
        Assert.Contains(PerformanceTexts.VerdictName(Verdict.Unchanged), rows[1]);
        Assert.Contains(rows, r => r.Contains("mod_c.txt:1") && r.Contains(PerformanceTexts.VerdictName(Verdict.MissingInB)));
        Assert.Contains(rows, r => r.Contains("mod_d.txt:1") && r.Contains(PerformanceTexts.VerdictName(Verdict.NewInB)));
        Assert.DoesNotContain(rows, r => r.Contains("van_a"));
    }

    [Fact]
    public void ComparisonLimitsBothBlocksToTop()
    {
        var lines = Lines(PerformanceReport.Comparison("vorher", "nachher", Compare(Twelve, Twelve), 2));
        var start = Array.IndexOf(lines, PerformanceTexts.ComparisonRowsTitle(2));
        var end = Array.IndexOf(lines, PerformanceTexts.ComparisonFilesTitle(2));

        Assert.True(start > 0 && end > start);
        Assert.Equal(2, end - start - 1);
        Assert.Equal(2, lines.Length - end - 1);
    }

    [Fact]
    public void FilesBlockShowsPathsAndIsRobustAgainstLineShifts()
    {
        var lines = Lines(PerformanceReport.Comparison("vorher", "nachher", Compare(Twelve, Twelve), 10));
        var files = lines.Skip(Array.IndexOf(lines, PerformanceTexts.ComparisonFilesTitle(10)) + 1).ToList();

        Assert.Equal(5, files.Count); // mod_a, mod_b, mod_c, mod_s, mod_d
        var shifted = Assert.Single(files, f => f.Contains("common/mod_s.txt"));
        Assert.Contains(PerformanceTexts.VerdictName(Verdict.Unchanged), shifted);
        Assert.DoesNotContain("mod_s.txt:", shifted);
    }

    [Fact]
    public void MissingSidesShowADash()
    {
        var lines = Lines(PerformanceReport.Comparison("vorher", "nachher", Compare(Twelve, Twelve), 10));
        var missing = lines.First(l => l.Contains("effect @ common/mod_c.txt:1"));

        Assert.Contains($"{N0(10)} -> -", missing);
        Assert.Contains($"{N3(3)} -> -", missing);
    }
}
