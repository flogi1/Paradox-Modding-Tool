using System.Text.RegularExpressions;
using ParadoxModTool.ErrorLog;
using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.Performance.Tests;

public sealed class PerformanceSessionTests
{
    private const string MonthlyRow = "on_actions (on_actions) @ common/on_action/location_pulses.txt:38";
    private const string KnownRow = "on_actions (effect) @ common/on_action/Idea_Variation2_on_action.txt:56";

    private static readonly DateTime Start = new(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);

    private sealed class Fixture : IDisposable
    {
        private readonly TempTree _tree = new();

        /// <param name="catalogFactory">Eigener Katalog-Lieferant; hat Vorrang vor <paramref name="withCatalog"/>.</param>
        public Fixture(string modKey = "m1", bool withCatalog = false, Func<ErrorCatalog>? catalogFactory = null)
        {
            Store = new PerformanceRunStore(_tree.PathOf("performance"));
            Session = new PerformanceSession(Store, "eu5", modKey, new PerformanceSettings { MonthlyPulseRow = MonthlyRow },
                () => new OriginClassifier(@"C:\mod", Array.Empty<string>(), Array.Empty<string>(), p => p.StartsWith(@"C:\mod\", StringComparison.OrdinalIgnoreCase)),
                new NoiseSettings(),
                catalogFactory ?? (withCatalog ? () => ErrorCatalog.Load(RepoFiles.PathOf("profiles/errorlog/eu5.json")) : null));
        }

        public PerformanceRunStore Store { get; }

        public PerformanceSession Session { get; }

        public string TempFile(string name, string content)
        {
            var path = _tree.PathOf(name);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose() => _tree.Dispose();
    }

    private static PerformanceRun NewRun(string name, string modKey = "m1", string? save = "save_1", string? playset = "Playset 1", string? hash = null, int minutes = 0, string gameId = "eu5") =>
        new("", name, gameId, modKey, @"C:\mod", playset, save, 12, null, hash, "-debug_mode", Start.AddMinutes(minutes), "");

    private static string Before => RepoFiles.Data("eu5/iv2_before.csv");

    private static string After => RepoFiles.Data("eu5/iv2_after.csv");

    [Fact]
    public void RunsListOnlyThisModKey()
    {
        using var f = new Fixture();
        var first = f.Session.Import(NewRun("one"), Before);
        var second = f.Session.Import(NewRun("two", minutes: 5), After);
        f.Store.Import(NewRun("other", "m2") with { Id = "other-run" }, Before);
        f.Store.Import(NewRun("foreign game", gameId: "ck3") with { Id = "ck3-run" }, Before);

        f.Session.Refresh();

        Assert.Equal(new[] { second.Id, first.Id }, f.Session.Runs.Select(r => r.Id));
    }

    [Fact]
    public void SelectBuildsSummaryAndDuration()
    {
        using var f = new Fixture();
        var run = f.Session.Import(NewRun("one"), Before);

        f.Session.Select(run.Id);

        Assert.Equal(run.Id, f.Session.Selected!.Id);
        Assert.Equal(12, f.Session.SelectedDuration!.Months);
        Assert.NotNull(f.Session.SelectedSummary);
        Assert.NotEmpty(f.Session.SelectedSummary!.Rows);
        Assert.Same(f.Session.SelectedSummary, f.Session.SelectedSummary);
        Assert.Empty(f.Session.SelectedWarnings);
    }

    [Fact]
    public void SelectedClassifierResolvesModFilesOfTheSelectedRun()
    {
        using var f = new Fixture();
        var run = f.Session.Import(NewRun("one"), Before);
        Assert.Null(f.Session.SelectedClassifier);

        f.Session.Select(run.Id);

        var classifier = f.Session.SelectedClassifier;
        Assert.NotNull(classifier);
        Assert.Same(classifier, f.Session.SelectedClassifier);
        var modRow = f.Session.SelectedSummary!.Rows.First(r => r.Origin == RowOrigin.Mod).Row;
        Assert.StartsWith(@"C:\mod\", classifier!.ResolveModFile(modRow.Path));
    }

    [Fact]
    public void RunsOfOneSessionShareOneClassifier()
    {
        using var f = new Fixture();
        var a = f.Session.Import(NewRun("a"), Before);
        var b = f.Session.Import(NewRun("b", minutes: 5), After);
        var created = 0;
        var session = new PerformanceSession(f.Store, "eu5", "m1", new PerformanceSettings { MonthlyPulseRow = MonthlyRow },
            () =>
            {
                created++;
                return new OriginClassifier(@"C:\mod", Array.Empty<string>(), Array.Empty<string>(), p => p.StartsWith(@"C:\mod\", StringComparison.OrdinalIgnoreCase));
            },
            new NoiseSettings());

        session.Select(a.Id);
        Assert.NotNull(session.SelectedSummary);
        var classifierA = session.SelectedClassifier;
        session.Compare(a.Id, b.Id);
        Assert.NotNull(session.Comparison);
        session.Select(b.Id);
        Assert.NotNull(session.SelectedSummary);

        Assert.Equal(1, created);
        Assert.NotNull(classifierA);
        Assert.Same(classifierA, session.SelectedClassifier);
    }

    [Fact]
    public void ImportOfAMeasuredRunRebuildsClassifierAndSummaries()
    {
        using var f = new Fixture();
        var a = f.Session.Import(NewRun("a"), Before);
        var created = 0;
        var session = new PerformanceSession(f.Store, "eu5", "m1", new PerformanceSettings { MonthlyPulseRow = MonthlyRow },
            () =>
            {
                created++;
                return new OriginClassifier(@"C:\mod", Array.Empty<string>(), Array.Empty<string>(), p => p.StartsWith(@"C:\mod\", StringComparison.OrdinalIgnoreCase));
            },
            new NoiseSettings());

        session.Select(a.Id);
        var summaryBefore = session.SelectedSummary;
        var classifierBefore = session.SelectedClassifier;
        Assert.Equal(1, created);

        session.Import(NewRun("manual", minutes: 3), After, measured: false);
        session.Select(a.Id);
        Assert.Equal(1, created);
        Assert.Same(summaryBefore, session.SelectedSummary);
        Assert.Same(classifierBefore, session.SelectedClassifier);

        session.Import(NewRun("measured", minutes: 5), After);
        session.Select(a.Id);
        Assert.NotNull(session.SelectedSummary);
        Assert.Equal(2, created);
        Assert.NotSame(summaryBefore, session.SelectedSummary);
        Assert.NotSame(classifierBefore, session.SelectedClassifier);
    }

    [Fact]
    public void MeasuredImportAlsoRebuildsTheComparison()
    {
        using var f = new Fixture();
        var a = f.Session.Import(NewRun("a"), Before);
        var b = f.Session.Import(NewRun("b", minutes: 5), After);
        var created = 0;
        var session = new PerformanceSession(f.Store, "eu5", "m1", new PerformanceSettings { MonthlyPulseRow = MonthlyRow },
            () =>
            {
                created++;
                return new OriginClassifier(@"C:\mod", Array.Empty<string>(), Array.Empty<string>(), p => p.StartsWith(@"C:\mod\", StringComparison.OrdinalIgnoreCase));
            },
            new NoiseSettings());

        session.Compare(a.Id, b.Id);
        var before = session.Comparison;
        Assert.NotNull(before);
        Assert.Equal(1, created);

        session.Import(NewRun("c", minutes: 9), After);

        Assert.NotNull(session.Comparison);
        Assert.NotSame(before, session.Comparison);
        Assert.Equal(2, created);
    }

    [Fact]
    public void SelectUnknownIdClears()
    {
        using var f = new Fixture();
        var run = f.Session.Import(NewRun("one"), Before);
        f.Session.Select(run.Id);
        var raised = 0;
        f.Session.Changed += (_, _) => raised++;

        f.Session.Select("does-not-exist");

        Assert.Null(f.Session.Selected);
        Assert.Null(f.Session.SelectedSummary);
        Assert.Null(f.Session.SelectedDuration);
        Assert.Empty(f.Session.SelectedWarnings);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void DeleteSelectedClearsSelectionAndRaisesChanged()
    {
        using var f = new Fixture();
        var one = f.Session.Import(NewRun("one"), Before);
        var two = f.Session.Import(NewRun("two", minutes: 5), After);
        f.Session.Select(one.Id);
        f.Session.Compare(one.Id, two.Id);
        var raised = 0;
        f.Session.Changed += (_, _) => raised++;

        f.Session.Delete(one.Id);

        Assert.Equal(1, raised);
        Assert.Null(f.Session.Selected);
        Assert.Null(f.Session.A);
        Assert.Null(f.Session.Comparison);
        Assert.Empty(f.Session.ComparisonWarnings);
        Assert.Equal(two.Id, f.Session.B!.Id);
        Assert.Equal(two.Id, Assert.Single(f.Session.Runs).Id);
        Assert.False(Directory.Exists(f.Store.FolderOf("m1", one.Id)));
    }

    [Fact]
    public void DeletingAnotherRunKeepsSelection()
    {
        using var f = new Fixture();
        var one = f.Session.Import(NewRun("one"), Before);
        var two = f.Session.Import(NewRun("two", minutes: 5), After);
        f.Session.Select(one.Id);

        f.Session.Delete(two.Id);

        Assert.Equal(one.Id, f.Session.Selected!.Id);
    }

    [Fact]
    public void RenameKeepsSelectionAndComparisonWithNewName()
    {
        using var f = new Fixture();
        var one = f.Session.Import(NewRun("one"), Before);
        var two = f.Session.Import(NewRun("two", minutes: 5), After);
        f.Session.Select(one.Id);
        f.Session.Compare(one.Id, two.Id);
        var comparison = f.Session.Comparison;

        f.Session.Rename(one.Id, "renamed");

        Assert.Equal("renamed", f.Session.Selected!.Name);
        Assert.Equal("renamed", f.Session.A!.Name);
        Assert.Same(comparison, f.Session.Comparison);
        Assert.Equal("renamed", Assert.Single(f.Store.List("m1"), r => r.Id == one.Id).Name);
    }

    [Fact]
    public void RefreshDropsRunsThatVanishedOnDisk()
    {
        using var f = new Fixture();
        var one = f.Session.Import(NewRun("one"), Before);
        var two = f.Session.Import(NewRun("two", minutes: 5), After);
        f.Session.Select(one.Id);
        f.Session.Compare(one.Id, two.Id);

        f.Store.Delete("m1", one.Id);
        f.Session.Refresh();

        Assert.Null(f.Session.Selected);
        Assert.Null(f.Session.A);
        Assert.Null(f.Session.Comparison);
        Assert.Equal(two.Id, f.Session.B!.Id);
    }

    [Fact]
    public void CompareBuildsComparisonWithWarnings()
    {
        using var f = new Fixture();
        var a = f.Session.Import(NewRun("a", save: "save_1"), Before);
        var b = f.Session.Import(NewRun("b", save: "save_2", minutes: 5), After);
        var same = f.Session.Import(NewRun("c", save: "save_1", minutes: 10), After);

        f.Session.Compare(a.Id, b.Id);

        Assert.Equal(a.Id, f.Session.A!.Id);
        Assert.Equal(b.Id, f.Session.B!.Id);
        Assert.NotNull(f.Session.Comparison);
        Assert.True(f.Session.Comparison!.SameMonths);
        Assert.Equal(new[] { PerformanceTexts.WarnSaveDiffers("save_1", "save_2") }, f.Session.ComparisonWarnings);

        f.Session.Compare(a.Id, same.Id);

        Assert.Empty(f.Session.ComparisonWarnings);
    }

    [Fact]
    public void ComparisonWarnsAboutPlaysetAndModFilesAndUnknownSave()
    {
        using var f = new Fixture();
        var a = f.Session.Import(NewRun("a", save: null, playset: "P1", hash: "h1"), Before);
        var b = f.Session.Import(NewRun("b", save: null, playset: "P2", hash: "h2", minutes: 5), After);
        var c = f.Session.Import(NewRun("c", save: "save_1", playset: "P1", hash: null, minutes: 10), After);

        f.Session.Compare(a.Id, b.Id);

        Assert.Equal(new[] { PerformanceTexts.WarnPlaysetDiffers("P1", "P2"), PerformanceTexts.WarnModChangedSinceA }, f.Session.ComparisonWarnings);

        f.Session.Compare(a.Id, c.Id);

        Assert.Equal(new[] { PerformanceTexts.WarnSaveDiffers(null, "save_1") }, f.Session.ComparisonWarnings);
    }

    [Fact]
    public void ComparisonWarnsWhenMonthsDiffer()
    {
        using var f = new Fixture();
        var longDump = f.TempFile("long.csv", File.ReadAllText(After).Replace("location_pulses.txt:38;0;12;", "location_pulses.txt:38;0;24;"));
        var a = f.Session.Import(NewRun("a"), Before);
        var b = f.Session.Import(NewRun("b", minutes: 5), longDump);

        f.Session.Compare(a.Id, b.Id);

        Assert.False(f.Session.Comparison!.SameMonths);
        Assert.Equal(new[] { PerformanceTexts.MonthsDiffer(12, 24) }, f.Session.ComparisonWarnings);
    }

    [Fact]
    public void ComparingWithUnknownIdKeepsOnlyTheKnownSide()
    {
        using var f = new Fixture();
        var a = f.Session.Import(NewRun("a"), Before);

        f.Session.Compare(a.Id, "nope");

        Assert.Equal(a.Id, f.Session.A!.Id);
        Assert.Null(f.Session.B);
        Assert.Null(f.Session.Comparison);
        Assert.Empty(f.Session.ComparisonWarnings);
    }

    [Fact]
    public void ImportCountsMonthsIntoRunJson()
    {
        using var f = new Fixture();
        var raised = 0;
        f.Session.Changed += (_, _) => raised++;

        var run = f.Session.Import(NewRun("one"), Before);

        Assert.Matches(new Regex(@"^\d{8}-\d{6}-[0-9a-f]{4}$"), run.Id);
        Assert.Equal(12, run.CountedMonths);
        Assert.Equal(12, Assert.Single(f.Store.List("m1")).CountedMonths);
        Assert.Equal(run, Assert.Single(f.Session.Runs));
        Assert.Equal(1, raised);
    }

    [Fact]
    public void NewRunIdUsesTheCreationTimeAndFourHexCharacters()
    {
        Assert.Matches(new Regex(@"^20261020-100000-[0-9a-f]{4}$"), PerformanceSession.NewRunId(Start));
    }

    [Fact]
    public void ImportKeepsAGivenId()
    {
        using var f = new Fixture();

        var run = f.Session.Import(NewRun("one") with { Id = "my-id" }, Before);

        Assert.Equal("my-id", run.Id);
    }

    [Fact]
    public void ImportOfUnreadableDumpKeepsRunWithoutMonthsAndReportsWarning()
    {
        using var f = new Fixture();
        var broken = f.TempFile("broken.csv", "nothing;useful\n1;2\n");

        var run = f.Session.Import(NewRun("one"), broken);
        f.Session.Select(run.Id);

        Assert.Null(run.CountedMonths);
        Assert.Equal(run.Id, Assert.Single(f.Session.Runs).Id);
        Assert.Null(f.Session.SelectedSummary);
        Assert.Null(f.Session.SelectedDuration);
        Assert.Single(f.Session.SelectedWarnings);
    }

    [Fact]
    public void NoiseSetterRebuildsComparison()
    {
        using var f = new Fixture();
        var a = f.Session.Import(NewRun("a"), Before);
        var b = f.Session.Import(NewRun("b", minutes: 5), After);
        f.Session.Compare(a.Id, b.Id);
        var all = Enum.GetValues<RowOrigin>().ToHashSet();
        Verdict VerdictOfKnownRow() => f.Session.Comparison!.Rows(all).Single(r => r.Key == KnownRow).Verdict;
        var raised = 0;
        f.Session.Changed += (_, _) => raised++;

        f.Session.Noise = new NoiseSettings(0.10);
        var strict = VerdictOfKnownRow();
        f.Session.Noise = new NoiseSettings(0.50);
        var lenient = VerdictOfKnownRow();

        Assert.Equal(Verdict.Better, strict);
        Assert.Equal(Verdict.Unchanged, lenient);
        Assert.Equal(0.50, f.Session.Comparison!.Noise.RelativeThreshold);
        Assert.Equal(2, raised);
    }

    [Fact]
    public void NoiseSetterWithoutComparisonOnlyRaisesChanged()
    {
        using var f = new Fixture();
        var raised = 0;
        f.Session.Changed += (_, _) => raised++;

        f.Session.Noise = new NoiseSettings(0.30);

        Assert.Null(f.Session.Comparison);
        Assert.Equal(0.30, f.Session.Noise.RelativeThreshold);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void SelectedHintsComputedOnceAndClearedOnMeasuredImport()
    {
        using var f = new Fixture();
        var a = f.Session.Import(NewRun("a"), Before);
        Assert.Empty(f.Session.SelectedHints);
        f.Session.Select(a.Id);

        var first = f.Session.SelectedHints;

        Assert.Same(first, f.Session.SelectedHints);
        f.Session.Import(NewRun("b", minutes: 5), After, measured: false);
        Assert.Same(first, f.Session.SelectedHints);
        f.Session.Import(NewRun("c", minutes: 10), After);
        Assert.NotSame(first, f.Session.SelectedHints);
    }

    [Fact]
    public void SelectedHintsNoteWhenRunHasNoErrorLog()
    {
        using var f = new Fixture(withCatalog: true);
        var manual = f.Session.Import(NewRun("manual"), Before);
        var logged = f.Session.Import(NewRun("logged", minutes: 5), After);
        var lines = Enumerable.Range(0, 120).Select(i =>
            $"[19:{49 + i / 60:00}:{i % 60:00}][jomini_effect.cpp:556]: Unknown effect cmf_show_alert at common/scripted_effects/01_iv2_alert_effects.txt:47");
        File.WriteAllText(Path.Combine(f.Store.FolderOf("m1", logged.Id), RunErrorLog.FileName), string.Join("\n", lines) + "\n");

        f.Session.Select(manual.Id);
        Assert.Equal(PerformanceTexts.HintNoErrorLog, f.Session.SelectedHintsNote);

        f.Session.Select(logged.Id);
        Assert.Null(f.Session.SelectedHintsNote);
        Assert.Empty(f.Session.SelectedWarnings);
        Assert.Contains(f.Session.SelectedHints, h => h.RuleId == "error-spam");

        f.Session.Select(null);
        Assert.Null(f.Session.SelectedHintsNote);
        Assert.Empty(f.Session.SelectedHints);
    }

    [Fact]
    public void SelectedHintsNoteWithoutCatalogFactory()
    {
        using var f = new Fixture();
        var run = f.Session.Import(NewRun("a"), Before);
        File.WriteAllText(Path.Combine(f.Store.FolderOf("m1", run.Id), RunErrorLog.FileName), "x\n");

        f.Session.Select(run.Id);

        Assert.Equal(PerformanceTexts.HintNoErrorLog, f.Session.SelectedHintsNote);
    }

    [Fact]
    public void UnreadableErrorLogDropsErrorRulesAndWarns()
    {
        using var f = new Fixture(withCatalog: true);
        var run = f.Session.Import(NewRun("a"), Before);
        // Ein exklusiv geöffnetes error.log ist nicht lesbar: Die Fehler-Regeln fallen weg, die Warnung nennt die Datei.
        var log = Path.Combine(f.Store.FolderOf("m1", run.Id), RunErrorLog.FileName);
        File.WriteAllText(log, "x\n");
        using var lockHandle = new FileStream(log, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        f.Session.Select(run.Id);

        Assert.NotNull(f.Session.SelectedHints);
        Assert.Contains(f.Session.SelectedWarnings, w => w.Contains(RunErrorLog.FileName));
    }

    [Fact]
    public void FailingHintComputationLeavesNoHintsAndWarnsWithoutThrowing()
    {
        // Ein Fehler beim Berechnen (hier: der Katalog-Lieferant) darf nicht aus SelectedHints fliegen; sonst bricht etwa ein gemessener Import ab.
        var calls = 0;
        using var f = new Fixture(catalogFactory: () =>
        {
            calls++;
            throw new InvalidOperationException("Katalog kaputt");
        });
        var run = f.Session.Import(NewRun("a"), Before);
        File.WriteAllText(Path.Combine(f.Store.FolderOf("m1", run.Id), RunErrorLog.FileName), "x\n");
        f.Session.Select(run.Id);

        var hints = f.Session.SelectedHints;

        Assert.Empty(hints);
        Assert.Same(hints, f.Session.SelectedHints);
        Assert.Equal(1, calls);
        Assert.Contains(PerformanceTexts.HintComputeFailed("Katalog kaputt"), f.Session.SelectedWarnings);
    }
}
