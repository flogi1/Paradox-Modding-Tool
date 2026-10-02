using ParadoxModTool.ErrorLog;
using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class OosSuspectsTests
{
    private static SafetyRules Rules { get; } =
        DesyncProfile.Load(Path.Combine(AppContext.BaseDirectory, "profiles", "desync", "eu5.json")).Safety!;

    private static readonly string Root = Path.Combine(Path.GetTempPath(), "pmt-suspects");

    private static readonly IReadOnlyDictionary<string, double> NoTerms = new Dictionary<string, double>();
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> NoHits = new Dictionary<string, IReadOnlySet<string>>();

    private static ScannedFile File(string relative, string mod = "Mod") =>
        new(mod, false, relative, Path.Combine(Root, mod, relative.Replace('/', Path.DirectorySeparatorChar)));

    private static SafetyScanResult Scan(IReadOnlyList<ScannedFile> files, params SafetyFinding[] findings) =>
        new(findings, Array.Empty<SafetyNotice>(), files);

    private static SafetyFinding Finding(ScannedFile file, int line, SafetySeverity severity, string rule = "r", string token = "t") =>
        new(file.ModName, file.IsOwnMod, file.RelativePath, file.FullPath, line, rule, token, SafetyContext.Synced, severity, severity, true);

    private static ReportLead Lead(string? fullPath, int? line, params int[] machines) =>
        new(machines, "1444.11.11", "Error: x", "common/a.txt", fullPath, line, "Mod", false, LeadKind.ScriptError);

    private static RankedDiff Diff(string displayPath, DiffWeight weight, params (string Path, string? Value, DiffWeight Weight)[] fields) =>
        new(
            new UnitDiffDetail("countries/database/1", UnitChange.Changed, null, null, null, null, 0, 0, Array.Empty<FieldDiff>(), false, false),
            displayPath,
            "countries",
            weight,
            fields.Select(f => new RankedField(new FieldDiff(f.Path, "0", f.Value, FieldChangeKind.Changed), f.Weight)).ToList());

    private static IReadOnlySet<string> Set(params string[] terms) => new HashSet<string>(terms, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void TermsOf_extracts_identifiers()
    {
        var terms = OosSuspects.TermsOf(
            new[]
            {
                Diff(
                    "country BLG / parliament",
                    DiffWeight.Cause,
                    ("debate/seed", "iv2_debate_modifier", DiffWeight.Cause),
                    ("gold", "gold_income_4711", DiffWeight.Consequence),
                    ("cache_xyz", "1444.11.11", DiffWeight.Noise)),
            },
            Rules);

        Assert.Equal(1.0, terms["parliament"]);
        Assert.Equal(1.0, terms["debate"]);
        Assert.Equal(1.0, terms["iv2_debate_modifier"]);
        Assert.Equal(0.3, terms["gold_income_4711"]);
        Assert.DoesNotContain("seed", terms.Keys);
        Assert.DoesNotContain("blg", terms.Keys);
        Assert.DoesNotContain("4711", terms.Keys);
        Assert.DoesNotContain("cache_xyz", terms.Keys);
        Assert.DoesNotContain(terms.Keys, term => term.Contains("1444", StringComparison.Ordinal));
        Assert.All(terms.Keys, term => Assert.Equal(term.ToLowerInvariant(), term));
    }

    [Fact]
    public void TermsOf_skips_noise_diffs()
    {
        var terms = OosSuspects.TermsOf(new[] { Diff("country / treasury", DiffWeight.Noise, ("budget", "budget_value", DiffWeight.Noise)) }, Rules);

        Assert.Empty(terms);
    }

    [Fact]
    public void TermsOf_keeps_strongest_factor()
    {
        var terms = OosSuspects.TermsOf(
            new[]
            {
                Diff("country / a", DiffWeight.Consequence, ("x", "shared_term", DiffWeight.Consequence)),
                Diff("country / b", DiffWeight.Cause, ("x", "shared_term", DiffWeight.Cause)),
                Diff("country / c", DiffWeight.Consequence, ("x", "shared_term", DiffWeight.Consequence)),
            },
            Rules);

        Assert.Equal(1.0, terms["shared_term"]);
    }

    [Fact]
    public void Lead_scores_hundred()
    {
        var file = File("common/character_interactions/marry_noble.txt");
        var lead = Lead(file.FullPath.Replace('\\', '/').ToUpperInvariant(), 92, 1);

        var result = Assert.Single(OosSuspects.Rank(new[] { lead }, Scan(new[] { file }), NoTerms, NoHits));

        Assert.Equal(100, result.Score);
        Assert.Equal(92, result.Line);
        Assert.Equal(file.FullPath, result.FullPath);
        Assert.Equal(file.RelativePath, result.RelativePath);
        var reason = Assert.Single(result.Reasons);
        Assert.Equal(SuspectReasonKind.LogLead, reason.Kind);
        Assert.Equal(100, reason.Points);
        Assert.Equal(92, reason.Line);
        Assert.Equal(file.RelativePath, reason.Subject);
    }

    [Fact]
    public void Leads_without_a_scanned_file_are_ignored()
    {
        var file = File("a.txt");
        var leads = new[] { Lead(null, 1, 1), Lead(Path.Combine(Root, "other", "b.txt"), 2, 1) };

        Assert.Empty(OosSuspects.Rank(leads, Scan(new[] { file }), NoTerms, NoHits));
    }

    [Fact]
    public void Leads_on_same_file_merge()
    {
        var file = File("a.txt");
        var two = new[] { Lead(file.FullPath, 5, 1), Lead(file.FullPath, 5, 2) };

        var merged = Assert.Single(OosSuspects.Rank(two, Scan(new[] { file }), NoTerms, NoHits));
        var reason = Assert.Single(merged.Reasons);
        Assert.Equal(new[] { 1, 2 }, reason.Machines);
        Assert.Equal(100, merged.Score);

        var three = two.Append(Lead(file.FullPath, 10, 2)).ToArray();
        var more = Assert.Single(OosSuspects.Rank(three, Scan(new[] { file }), NoTerms, NoHits));
        Assert.Equal(200, more.Score);
        Assert.Equal(2, more.Reasons.Count);
        Assert.Equal(5, more.Line);
    }

    [Fact]
    public void Rule_weights()
    {
        var file = File("a.txt");
        var scan = Scan(
            new[] { file },
            Finding(file, 3, SafetySeverity.Risk, "local_dlc", "sacred_sites"),
            Finding(file, 7, SafetySeverity.Check),
            Finding(file, 9, SafetySeverity.Check),
            Finding(file, 11, SafetySeverity.Allowed));

        var result = Assert.Single(OosSuspects.Rank(Array.Empty<ReportLead>(), scan, NoTerms, NoHits));

        // 50 + 15 + 15, aber Regeln bringen je Datei höchstens 50.
        Assert.Equal(50, result.Score);
        Assert.Equal(3, result.Line);
        Assert.Equal(3, result.Reasons.Count);
        Assert.All(result.Reasons, reason => Assert.Equal(SuspectReasonKind.Rule, reason.Kind));
        Assert.Equal(new[] { 50.0, 15.0, 15.0 }, result.Reasons.Select(r => r.Points));
        var risk = result.Reasons[0];
        Assert.Equal(50, risk.Points);
        Assert.Equal(SafetySeverity.Risk, risk.Severity);
        Assert.Equal(3, risk.Line);
        Assert.Equal("local_dlc", risk.RuleId);
        Assert.Equal("sacred_sites", risk.Token);
        Assert.Contains("local_dlc", risk.Subject, StringComparison.Ordinal);
        Assert.Contains("sacred_sites", risk.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public void One_risk_and_one_check_give_fifty()
    {
        var file = File("a.txt");
        var scan = Scan(new[] { file }, Finding(file, 1, SafetySeverity.Risk), Finding(file, 2, SafetySeverity.Check));

        var result = Assert.Single(OosSuspects.Rank(Array.Empty<ReportLead>(), scan, NoTerms, NoHits));

        Assert.Equal(50, result.Score);
    }

    [Fact]
    public void Rule_points_are_capped_per_file()
    {
        var file = File("a.txt");
        var findings = Enumerable.Range(1, 7).Select(line => Finding(file, line, SafetySeverity.Check)).ToArray();

        var result = Assert.Single(OosSuspects.Rank(Array.Empty<ReportLead>(), Scan(new[] { file }, findings), NoTerms, NoHits));

        Assert.Equal(OosSuspects.MaxRulePoints, result.Score);
        Assert.Equal(50, result.Score);
        Assert.Equal(7, result.Reasons.Count);
        Assert.All(result.Reasons, reason => Assert.Equal(15, reason.Points));
    }

    [Fact]
    public void One_lead_outranks_any_number_of_rule_findings()
    {
        var lead = File("lead.txt");
        var rules = File("rules.txt");
        var findings = Enumerable.Range(1, 7).Select(line => Finding(rules, line, SafetySeverity.Check)).ToArray();

        var ranked = OosSuspects.Rank(new[] { Lead(lead.FullPath, 1, 1) }, Scan(new[] { lead, rules }, findings), NoTerms, NoHits);

        Assert.Equal(new[] { "lead.txt", "rules.txt" }, ranked.Select(r => r.RelativePath));
        Assert.Equal(new[] { 100.0, 50.0 }, ranked.Select(r => r.Score));
    }

    [Fact]
    public void Rule_cap_leaves_lead_and_term_points_alone()
    {
        var files = Enumerable.Range(1, 4).Select(i => File($"f{i}.txt")).ToList();
        var findings = new[] { Finding(files[0], 2, SafetySeverity.Risk), Finding(files[0], 3, SafetySeverity.Risk) };
        var terms = new Dictionary<string, double> { ["debate"] = 1.0 };
        var hits = new Dictionary<string, IReadOnlySet<string>> { [files[0].FullPath] = Set("debate") };

        var result = OosSuspects.Rank(new[] { Lead(files[0].FullPath, 1, 1) }, Scan(files, findings), terms, hits)[0];

        Assert.Equal(100 + 50 + 20 * Math.Log(4), result.Score, 9);
    }

    [Fact]
    public void Verdicts_change_the_score_through_severity()
    {
        var file = File("a.txt");
        var finding = Finding(file, 3, SafetySeverity.Risk) with { Severity = SafetySeverity.Allowed };

        Assert.Empty(OosSuspects.Rank(Array.Empty<ReportLead>(), Scan(new[] { file }, finding), NoTerms, NoHits));
    }

    [Fact]
    public void Line_prefers_lead_over_rule()
    {
        var file = File("a.txt");
        var scan = Scan(new[] { file }, Finding(file, 3, SafetySeverity.Risk));

        var result = Assert.Single(OosSuspects.Rank(new[] { Lead(file.FullPath, null, 1), Lead(file.FullPath, 40, 1) }, scan, NoTerms, NoHits));

        Assert.Equal(40, result.Line);
    }

    [Fact]
    public void Term_idf()
    {
        var files = Enumerable.Range(1, 4).Select(i => File($"f{i}.txt")).ToList();
        var terms = new Dictionary<string, double> { ["debate"] = 1.0 };
        var hits = new Dictionary<string, IReadOnlySet<string>> { [files[0].FullPath] = Set("debate") };

        var result = Assert.Single(OosSuspects.Rank(Array.Empty<ReportLead>(), Scan(files), terms, hits));

        Assert.Equal(20 * Math.Log(4), result.Score, 9);
        var reason = Assert.Single(result.Reasons);
        Assert.Equal(SuspectReasonKind.Term, reason.Kind);
        Assert.Equal("debate", reason.Subject);
        Assert.Equal(1, reason.FileCount);
        Assert.Equal(20 * Math.Log(4), reason.Points, 9);

        var everywhere = files.ToDictionary(f => f.FullPath, _ => Set("debate"));
        Assert.Empty(OosSuspects.Rank(Array.Empty<ReportLead>(), Scan(files), terms, everywhere));
    }

    [Fact]
    public void Term_factor_scales_points_and_every_term_counts_once()
    {
        var files = Enumerable.Range(1, 4).Select(i => File($"f{i}.txt")).ToList();
        var terms = new Dictionary<string, double> { ["gold_income"] = 0.3, ["debate"] = 1.0 };
        var hits = new Dictionary<string, IReadOnlySet<string>>
        {
            [files[0].FullPath] = Set("gold_income", "debate"),
            [files[1].FullPath] = Set("debate", "not_a_term"),
        };

        var ranked = OosSuspects.Rank(Array.Empty<ReportLead>(), Scan(files), terms, hits);

        var first = ranked[0];
        Assert.Equal(files[0].RelativePath, first.RelativePath);
        Assert.Equal(20 * 0.3 * Math.Log(4) + 20 * Math.Log(2), first.Score, 9);
        Assert.Equal(2, first.Reasons.Count);
        Assert.Equal(2, first.Reasons.Single(r => r.Subject == "debate").FileCount);
        Assert.Equal(20 * Math.Log(2), ranked[1].Score, 9);
    }

    [Fact]
    public void Order_and_cut()
    {
        var files = Enumerable.Range(1, 60).Select(i => File($"f{i}.txt")).ToList();
        var leads = files.SelectMany((f, i) => Enumerable.Range(0, i + 1).Select(line => Lead(f.FullPath, line + 1, 1))).ToList();

        var ranked = OosSuspects.Rank(leads, Scan(files), NoTerms, NoHits);

        Assert.Equal(OosSuspects.MaxFiles, ranked.Count);
        Assert.Equal(files[59].RelativePath, ranked[0].RelativePath);
        Assert.Equal(ranked.Select(r => r.Score).OrderByDescending(s => s), ranked.Select(r => r.Score));
        Assert.Equal(11 * 100, ranked[^1].Score);
    }

    [Fact]
    public void Ties_sort_by_mod_then_path()
    {
        var b2 = File("b.txt", "beta");
        var a2 = File("z.txt", "alpha");
        var a1 = File("a.txt", "Alpha");
        var files = new[] { b2, a2, a1 };
        var leads = files.Select(f => Lead(f.FullPath, 1, 1)).ToList();

        var ranked = OosSuspects.Rank(leads, Scan(files), NoTerms, NoHits);

        Assert.Equal(new[] { "a.txt", "z.txt", "b.txt" }, ranked.Select(r => r.RelativePath));
    }

    [Fact]
    public void Without_diff()
    {
        var file = File("a.txt");
        var other = File("b.txt");
        var scan = Scan(new[] { file, other }, Finding(other, 2, SafetySeverity.Check));

        var ranked = OosSuspects.Rank(new[] { Lead(file.FullPath, 1, 1) }, scan, NoTerms, NoHits);

        Assert.Equal(new[] { 100.0, 15.0 }, ranked.Select(r => r.Score));
    }

    [Fact]
    public void TermHits_whole_words()
    {
        var files = new[] { File("a.txt"), File("b.txt"), File("c.txt") };
        var texts = new Dictionary<string, string?>
        {
            [files[0].FullPath] = "my_debate_x = 1\n# debate_old\n",
            [files[1].FullPath] = "Debate = yes\n",
            [files[2].FullPath] = null,
        };

        var hits = OosSuspects.TermHits(files, new[] { "debate" }, path => texts[path], CancellationToken.None);

        Assert.DoesNotContain(files[0].FullPath, hits.Keys);
        Assert.Equal(new[] { "debate" }, hits[files[1].FullPath]);
        Assert.DoesNotContain(files[2].FullPath, hits.Keys);
    }

    [Fact]
    public void TermHits_without_terms_reads_nothing()
    {
        var read = 0;

        var hits = OosSuspects.TermHits(new[] { File("a.txt") }, Array.Empty<string>(), _ => { read++; return "x"; }, CancellationToken.None);

        Assert.Empty(hits);
        Assert.Equal(0, read);
    }

    [Fact]
    public void TermHits_reports_progress_per_file()
    {
        var files = new[] { File("a.txt"), File("b.txt"), File("a.txt"), File("c.txt") };
        var values = new List<double>();

        OosSuspects.TermHits(files, new[] { "debate" }, path => path.EndsWith("c.txt", StringComparison.Ordinal) ? null : "debate", CancellationToken.None, new SyncProgress(values.Add));

        Assert.Equal(new[] { 0.25, 0.5, 0.75, 1.0 }, values);
    }

    [Fact]
    public void TermHits_without_terms_reports_done()
    {
        var values = new List<double>();

        OosSuspects.TermHits(new[] { File("a.txt") }, Array.Empty<string>(), _ => "x", CancellationToken.None, new SyncProgress(values.Add));

        Assert.Equal(new[] { 1.0 }, values);
    }

    [Fact]
    public void TermHits_honours_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            OosSuspects.TermHits(new[] { File("a.txt") }, new[] { "debate" }, _ => "debate", cts.Token));
    }

    [Fact]
    public void Term_idf_counts_duplicate_scanned_files_once()
    {
        var files = Enumerable.Range(1, 4).Select(i => File($"f{i}.txt")).ToList();
        files.Add(files[0] with { FullPath = files[0].FullPath.ToUpperInvariant() });
        var terms = new Dictionary<string, double> { ["debate"] = 1.0 };
        var hits = new Dictionary<string, IReadOnlySet<string>> { [files[0].FullPath] = Set("debate") };

        var result = Assert.Single(OosSuspects.Rank(Array.Empty<ReportLead>(), Scan(files), terms, hits));

        Assert.Equal(20 * Math.Log(4), result.Score, 9);
    }

    [Fact]
    public void Real_data_ranks_a_lead_file()
    {
        var folder = Environment.GetEnvironmentVariable("PMT_OOS_SAMPLES");
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        var pair = OosDumpCatalog.Pair(OosDumpCatalog.Scan(new[] { folder }, Array.Empty<string>()))
            .Where(candidate => candidate.Reference is not null && candidate.Desynced is not null)
            .OrderBy(candidate => candidate.OosId16, StringComparer.Ordinal)
            .First();
        using var tree = new TempTree(("common/synthetic/lead_target.txt", "a = {\n  b = yes\n}\nc = 1\n"));
        var dumps = pair.Dumps.Select(info => info.Open()).ToList();
        try
        {
            ErrorEntry? mapped = null;
            var leads = ReportLeads.From(OosTimeline.Build(dumps, Catalogs.Eu5, 7), error =>
            {
                if (mapped is null && error.HasFileReference)
                {
                    mapped = error;
                    return new LeadAttribution("Mod", false, tree.PathOf("common/synthetic/lead_target.txt"));
                }

                return new LeadAttribution(null, false, null);
            });

            Assert.NotEmpty(leads);
            var known = new ScannedFile("Mod", false, "common/synthetic/lead_target.txt", tree.PathOf("common/synthetic/lead_target.txt"));
            var scan = new SafetyScanResult(Array.Empty<SafetyFinding>(), Array.Empty<SafetyNotice>(), new[] { known });

            var ranked = OosSuspects.Rank(leads, scan, NoTerms, NoHits);

            Assert.All(ranked, suspect => Assert.Equal(known.FullPath, suspect.FullPath));
            if (mapped is null)
            {
                Assert.Empty(ranked);
                return;
            }

            var first = Assert.Single(ranked);
            Assert.True(first.Score >= OosSuspects.LeadPoints);
            Assert.Contains(first.Reasons, reason => reason.Kind == SuspectReasonKind.LogLead);
            Assert.Equal(mapped.FileLine, first.Line);
        }
        finally
        {
            dumps.ForEach(dump => dump.Dispose());
        }
    }

    /// <summary>Synchroner IProgress, damit der Test die Werte sofort sieht (Progress&lt;T&gt; meldet über den SynchronizationContext).</summary>
    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
