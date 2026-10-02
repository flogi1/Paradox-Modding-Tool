using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.Desync.Tests;

/// <summary>
/// Vergleicht die echten OOS-Paare aus dem Ordner in <c>PMT_OOS_SAMPLES</c> (<c>&lt;id&gt;_0.zip</c> und <c>&lt;id&gt;_0 client.zip</c>)
/// einmal je Testlauf, nacheinander. Ohne Variable oder Ordner gibt es keine Paare und die Tests bestehen ohne Prüfung; ist der
/// Ordner da, müssen die Paare 11c0 und 9591 darin gefunden werden.
/// </summary>
public sealed class RealDumpFixture
{
    public RealDumpFixture()
    {
        var folder = Environment.GetEnvironmentVariable("PMT_OOS_SAMPLES");
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        SamplesFound = true;
        Profile = DesyncProfile.Load(Path.Combine(AppContext.BaseDirectory, "profiles", "desync", "eu5.json"));
        var pairs = OosDumpCatalog.Pair(OosDumpCatalog.Scan(new[] { folder }, Array.Empty<string>()))
            .Where(pair => pair.Reference is not null && pair.Desynced is not null)
            .OrderBy(pair => pair.OosId16, StringComparer.Ordinal);
        foreach (var pair in pairs)
        {
            using var reference = pair.Reference!.Open();
            using var desynced = pair.Desynced!.Open();
            var watch = Stopwatch.StartNew();
            var result = DesyncComparison.Run(reference, desynced, Profile, null, CancellationToken.None);
            watch.Stop();
            using var process = Process.GetCurrentProcess();
            var peakWorkingSet = process.PeakWorkingSet64;
            var ranked = DesyncRanking.Rank(result.Units, Profile, new SaveNameResolver(result.Names, Profile));
            Runs.Add(new RealRun(pair.OosId16, result, ranked, watch.Elapsed, peakWorkingSet));
        }
    }

    /// <summary>Die Variable ist gesetzt und der Ordner vorhanden.</summary>
    public bool SamplesFound { get; }

    public DesyncProfile Profile { get; } = null!;

    public List<RealRun> Runs { get; } = new();
}

/// <param name="PeakWorkingSet">Höchster Arbeitsspeicher des Testprozesses bis nach diesem Paar (nur zur Ausgabe: andere Testklassen
/// laufen parallel im selben Prozess).</param>
public sealed record RealRun(string OosId16, DesyncDiffResult Result, IReadOnlyList<RankedDiff> Ranked, TimeSpan Elapsed, long PeakWorkingSet);

/// <summary>Übersicht der echten Paare aus <c>PMT_OOS_SAMPLES</c> (nur Metadaten, Logs und Kopf der Spielstände); ohne Ordner ohne Prüfung.</summary>
public class RealDumpSummaryTests
{
    [Fact]
    public void Real_pairs_show_no_mod_warning_and_name_each_machines_mod_source()
    {
        var folder = Environment.GetEnvironmentVariable("PMT_OOS_SAMPLES");
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        var pairs = OosDumpCatalog.Pair(OosDumpCatalog.Scan(new[] { folder }, Array.Empty<string>()))
            .Where(pair => pair.Reference is not null && pair.Desynced is not null)
            .ToList();
        Assert.Contains(pairs, pair => pair.OosId16.StartsWith("11c0", StringComparison.Ordinal));
        foreach (var pair in pairs)
        {
            var dumps = pair.Dumps.Select(info => info.Open()).ToList();
            try
            {
                var summary = OosSummaryBuilder.Build(pair, dumps);

                Assert.DoesNotContain(summary.Warnings, warning => warning.Kind == SummaryWarningKind.DifferentMods);
                var host = Assert.Single(summary.Machines[0].Mods);
                var client = Assert.Single(summary.Machines[1].Mods);
                Assert.Equal("Better Tactical Map Mode", host.Name);
                Assert.Equal((ModSourceKind.Workshop, "3599740945"), (host.Kind, host.WorkshopId));
                Assert.Equal("Better Tactical Map Mode", client.Name);
                Assert.Equal(ModSourceKind.Local, client.Kind);
            }
            finally
            {
                dumps.ForEach(dump => dump.Dispose());
            }
        }
    }
}

public class RealDumpTests : IClassFixture<RealDumpFixture>
{
    /// <summary>Zeitgrenze nur im Release-Build (Spec: unter 30 s je Paar); im Debug-Build wird die Zeit nur ausgegeben.</summary>
#if DEBUG
    private static readonly TimeSpan? Limit = null;
#else
    private static readonly TimeSpan? Limit = TimeSpan.FromSeconds(30);
#endif

    private static readonly Regex Positional = new(@"(^|/)\[\d+\]", RegexOptions.CultureInvariant);
    private static readonly Regex IdSegment = new(@"(?<=^|/)\d+(?=/|$)", RegexOptions.CultureInvariant);

    private readonly RealDumpFixture _fixture;
    private readonly ITestOutputHelper _output;

    public RealDumpTests(RealDumpFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public void Real_pairs_finish_in_time_and_find_the_known_examples()
    {
        if (_fixture.SamplesFound)
        {
            Assert.Contains(_fixture.Runs, run => run.OosId16.StartsWith("11c0", StringComparison.Ordinal));
            Assert.Contains(_fixture.Runs, run => run.OosId16.StartsWith("9591", StringComparison.Ordinal));
        }

        foreach (var run in _fixture.Runs)
        {
            _output.WriteLine($"{run.OosId16}: {run.Elapsed.TotalSeconds:F1} s (Run.Duration {run.Result.Duration.TotalSeconds:F1} s), " +
                $"Spitze Arbeitsspeicher des Testprozesses bisher {run.PeakWorkingSet / 1e6:F0} MB, " +
                $"{run.Result.Units.Count} Einheiten, TooLarge {run.Result.Units.Count(unit => unit.Change == UnitChange.TooLarge)}, " +
                $"Zustand {run.Result.ReferenceState}/{run.Result.DesyncedState}");
        }

        foreach (var run in _fixture.Runs)
        {
            var result = run.Result;
            Assert.Equal(SaveReadState.Complete, result.ReferenceState);
            Assert.Equal(SaveReadState.Complete, result.DesyncedState);
            Assert.Equal(0, result.Units.Count(unit => unit.Change == UnitChange.TooLarge));
            if (Limit is { } limit)
            {
                Assert.True(run.Elapsed < limit, $"{run.OosId16}: {run.Elapsed.TotalSeconds:F1} s");
            }

            var causes = run.Ranked.Where(diff => diff.Weight == DiffWeight.Cause).ToList();
            if (run.OosId16.StartsWith("11c0", StringComparison.Ordinal))
            {
                var country = Assert.Single(causes, diff => diff.Unit.Path == "countries/database/1384");
                Assert.Contains(country.Fields, field =>
                    field.Field.Path == "government/parliament/parliament_issue_support" && field.Weight == DiffWeight.Cause
                    && field.Field.ReferenceValue == "0.47488" && field.Field.DesyncedValue == "0.2453");
            }

            if (run.OosId16.StartsWith("9591", StringComparison.Ordinal))
            {
                Assert.Contains(causes, diff =>
                    diff.Unit.Path.StartsWith("construction_manager/database/", StringComparison.Ordinal)
                    && diff.Unit.Change == UnitChange.Changed
                    && diff.Fields.Any(field => field.Weight == DiffWeight.Cause && IsNoneVersusBlock(field.Field)));
                Assert.Contains(causes, diff =>
                    diff.Unit.Path.StartsWith("subunit_manager/database/", StringComparison.Ordinal)
                    && diff.Unit.Change == UnitChange.SlotReused);
            }
        }
    }

    [Fact]
    public void Real_pairs_keep_the_cause_class_small()
    {
        Assert.True(!_fixture.SamplesFound || _fixture.Runs.Count > 0, "PMT_OOS_SAMPLES ist gesetzt, aber es wurden keine Paare gefunden.");
        var failures = new List<string>();
        foreach (var run in _fixture.Runs)
        {
            var causes = run.Ranked.Count(diff => diff.Weight == DiffWeight.Cause);
            _output.WriteLine($"== {run.OosId16}: {causes} Ursachen, " +
                $"{run.Ranked.Count(diff => diff.Weight == DiffWeight.Consequence)} Folgen, " +
                $"{run.Ranked.Count(diff => diff.Weight == DiffWeight.Noise)} Rauschen");
            foreach (var manager in run.Ranked.GroupBy(diff => diff.Manager).OrderByDescending(group => group.Count()))
            {
                var kinds = string.Join(", ", manager.GroupBy(diff => diff.Unit.Change).Select(kind => $"{kind.Key} {kind.Count()}"));
                _output.WriteLine($"  {(manager.Key.Length == 0 ? "(oberste Ebene)" : manager.Key)}: " +
                    $"Ursache {manager.Count(diff => diff.Weight == DiffWeight.Cause)}, " +
                    $"Folge {manager.Count(diff => diff.Weight == DiffWeight.Consequence)}, " +
                    $"Rauschen {manager.Count(diff => diff.Weight == DiffWeight.Noise)}, " +
                    $"positionell [i] {manager.Count(diff => Positional.IsMatch(diff.Unit.Path))}; {kinds}");
            }

            // Größte Beiträger der Ursachen: Zahl der Ursache-Einheiten je Einheit bzw. Ursache-Feld, Ids als *, für die Folge-Muster.
            var contributors = run.Ranked
                .Where(diff => diff.Weight == DiffWeight.Cause)
                .SelectMany(diff => diff.Fields.Count == 0
                    ? new[] { IdSegment.Replace(diff.Unit.Path, "*") + $" ({diff.Unit.Change})" }
                    : diff.Fields.Where(field => field.Weight == DiffWeight.Cause)
                        .Select(field => IdSegment.Replace(diff.Unit.Path + "/" + field.Field.Path, "*"))
                        .Distinct(StringComparer.Ordinal))
                .GroupBy(path => path)
                .OrderByDescending(group => group.Count())
                .Take(25);
            foreach (var contributor in contributors)
            {
                _output.WriteLine($"    {contributor.Count(),6}  {contributor.Key}");
            }

            if (causes > 500)
            {
                failures.Add($"{run.OosId16}: {causes} Ursachen");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void The_11c0_paradox_report_names_out_of_sync_version_dlc_difference_and_parliament_issue_support()
    {
        if (!_fixture.SamplesFound)
        {
            return;
        }

        var run = Assert.Single(_fixture.Runs, run => run.OosId16.StartsWith("11c0", StringComparison.Ordinal));
        var facts = BuildFacts(run);
        _output.WriteLine("Ursachen: " + string.Join(" | ", facts.Causes.Select(cause =>
            cause.DisplayPath + " [" + string.Join(", ", cause.Fields.Select(field => field.Path)) + "]")));
        var entries = ParadoxFormReport.Build(facts, _fixture.Profile.ParadoxForm!);
        var description = entries.Single(entry => entry.Label == "Description").Value;
        var summary = entries.Single(entry => entry.Label == "Summary").Value;
        _output.WriteLine(summary);
        _output.WriteLine(description);

        // Die stärkste Ursache ist die oberste Ebene (ohne Pfad): die Zusammenfassung nennt die erste Ursache mit Pfad.
        Assert.DoesNotContain("  diverges", summary);
        Assert.Matches(@"— \S[^—]* diverges$", summary);
        Assert.DoesNotContain("\n-  (", description);
        Assert.Contains("\n- top level (", description);

        Assert.Contains("Out of Sync", description);
        Assert.Contains("1.0.3", description);
        Assert.Contains("Sacred Sites", description);
        Assert.Contains("parliament_issue_support", description);
        Assert.Equal("Out of Sync", entries.Single(entry => entry.Label == "Bug Type").Value);
    }

    [Fact]
    public void The_2e55_mod_author_report_lists_the_marry_noble_lead()
    {
        if (!_fixture.SamplesFound)
        {
            return;
        }

        var run = Assert.Single(_fixture.Runs, run => run.OosId16.StartsWith("2e55", StringComparison.Ordinal));
        var report = ModAuthorReport.Build(BuildFacts(run), _fixture.Profile.Advice);
        _output.WriteLine(report);

        Assert.Contains("marry_noble.txt:92", report);
        // Der Text des Fehlers steht in den Folgezeilen des Blocks "Script system error!"; gleiche Hinweise beider Maschinen stehen einmal da.
        Assert.Contains("Undefined event target", report);
        var leadLines = report.Split('\n')
            .SkipWhile(line => line != "## Where it comes from").Skip(1)
            .TakeWhile(line => line.StartsWith("- ", StringComparison.Ordinal))
            .ToList();
        Assert.True(leadLines.Count >= 2, "Mindestens zwei verschiedene Hinweise erwartet.");
        Assert.Contains(leadLines, line => line.Contains("marry_noble.txt:92", StringComparison.Ordinal)
            && line.Contains("machines 1, 2", StringComparison.Ordinal));
        var lines = report.Split('\n').Where(line => line.Length > 0).ToList();
        Assert.Equal(lines.Count, lines.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Tatsachen des Berichts zu einem echten Paar; Hinweise ohne Zuordnung zu einem Mod (wie die App ohne Fehlerlog-Sitzung).</summary>
    private ReportFacts BuildFacts(RealRun run)
    {
        var folder = Environment.GetEnvironmentVariable("PMT_OOS_SAMPLES")!;
        var pair = OosDumpCatalog.Pair(OosDumpCatalog.Scan(new[] { folder }, Array.Empty<string>()))
            .Single(candidate => candidate.OosId16 == run.OosId16);
        var dumps = pair.Dumps.Select(info => info.Open()).ToList();
        try
        {
            var summary = OosSummaryBuilder.Build(pair, dumps);
            var leads = ReportLeads.From(OosTimeline.Build(dumps, Catalogs.Eu5, 7), _ => new LeadAttribution(null, false, null));
            return ReportFacts.Build(summary, leads, run.Ranked, pair, _fixture.Profile);
        }
        finally
        {
            dumps.ForEach(dump => dump.Dispose());
        }
    }

    private static bool IsNoneVersusBlock(FieldDiff field) =>
        (field.ReferenceValue == "none" && field.DesyncedValue?.StartsWith('{') == true)
        || (field.DesyncedValue == "none" && field.ReferenceValue?.StartsWith('{') == true);
}
