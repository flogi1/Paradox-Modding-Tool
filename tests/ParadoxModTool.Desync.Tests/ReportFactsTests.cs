using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class ReportFactsTests
{
    private static readonly DesyncProfile Profile = DesyncProfile.Parse(
        """
        { "checksums": { "RANDOM_COUNT": {
            "name": { "en": "Random count", "de": "Zufallszähler" },
            "explain": { "en": "The random number generators were called a different number of times.", "de": "Die Zufallsgeneratoren wurden unterschiedlich oft aufgerufen." },
            "causes": [] } } }
        """);

    private static OosSummary Summary(IReadOnlyList<string>? categories = null, string? sessionKind = null) =>
        new("11c0de175691b2bb", null, "1425.2.4.8", null, null, categories ?? [], [], [], sessionKind);

    private static OosPair PairOf(IReadOnlyList<OosDumpInfo> dumps, params int[] missing) =>
        new("11c0de175691b2bb", dumps, 2, missing);

    private static OosDumpInfo Info(string path, int machine) =>
        new(path, "11c0de175691b2bb", machine, 2, null, null, null, [], null);

    private static ReportLead Lead(string? date, LeadKind kind, string message = "m", int machine = 1, string? file = null, int? line = null) =>
        new([machine], date, message, file, null, line, null, false, kind);

    private static RankedDiff Ranked(string path, DiffWeight weight, int fields = 1)
    {
        var diffs = Enumerable.Range(0, fields)
            .Select(i => new FieldDiff($"f{i}", "1", "2", FieldChangeKind.Changed)).ToList();
        var unit = new UnitDiffDetail(path, UnitChange.Changed, null, null, null, null, 10, 10, diffs, false, false);
        return new RankedDiff(unit, path, path.Split('/')[0], weight, diffs.Select(d => new RankedField(d, weight)).ToList());
    }

    [Fact]
    public void Leads_are_newest_first_capped_and_without_plain_entries()
    {
        var leads = new List<ReportLead>
        {
            Lead("1425.2.4.8", LeadKind.ScriptError, "a"),
            Lead("1425.2.4.10", LeadKind.InvalidCommand, "b"),
            Lead("1425.3.1.1", LeadKind.None, "plain"),
            Lead(null, LeadKind.ScriptError, "undated"),
        };
        for (var i = 0; i < 12; i++)
            leads.Add(Lead($"1420.1.1.{i + 1}", LeadKind.ScriptError, "old" + i));

        var facts = ReportFacts.Build(Summary(), leads, null, PairOf([]), Profile);

        Assert.Equal(ReportFacts.MaxLeads, facts.Leads.Count);
        Assert.DoesNotContain(facts.Leads, l => l.Kind == LeadKind.None);
        // 1425.2.4.10 liegt zahlenweise nach 1425.2.4.8, auch wenn der Text davor steht.
        Assert.Equal(["b", "a", "old11", "old10", "old9"], facts.Leads.Take(5).Select(l => l.Message));
    }

    [Fact]
    public void Equal_leads_are_merged_with_the_newest_date_and_all_machines_before_the_cap()
    {
        var leads = new List<ReportLead>();
        for (var i = 0; i < 12; i++)
        {
            leads.Add(Lead($"1425.2.4.{i + 1}", LeadKind.ScriptError, "Undefined event target", machine: 2 - i % 2, file: "common/m.txt", line: 92));
            leads.Add(Lead($"1425.2.4.{i + 1}", LeadKind.ScriptError, "Unset scope", machine: 2, file: "common/m.txt", line: 92));
        }

        leads.Add(Lead("1425.2.4.5", LeadKind.ScriptError, "Undefined event target", machine: 1, file: "common/other.txt", line: 92));
        leads.Add(Lead("1425.2.4.6", LeadKind.ScriptError, "Undefined event target", machine: 1, file: "common/m.txt", line: 93));

        var facts = ReportFacts.Build(Summary(), leads, null, PairOf([]), Profile);

        Assert.Equal(4, facts.Leads.Count);
        Assert.Equal(new ReportLead([1, 2], "1425.2.4.12", "Undefined event target", "common/m.txt", null, 92, null, false, LeadKind.ScriptError),
            facts.Leads[0]);
        Assert.Equal(new ReportLead([2], "1425.2.4.12", "Unset scope", "common/m.txt", null, 92, null, false, LeadKind.ScriptError),
            facts.Leads[1]);
        Assert.Equal([("common/m.txt", 93), ("common/other.txt", 92)], facts.Leads.Skip(2).Select(l => (l.File!, l.Line!.Value)));
    }

    [Fact]
    public void Causes_take_only_cause_weight_units_capped_with_five_fields_each()
    {
        var ranked = new List<RankedDiff> { Ranked("a/1", DiffWeight.Consequence), Ranked("b/1", DiffWeight.Cause, fields: 8) };
        for (var i = 0; i < 12; i++)
            ranked.Add(Ranked($"c/{i}", DiffWeight.Cause));
        ranked.Add(Ranked("z/1", DiffWeight.Noise));

        var facts = ReportFacts.Build(Summary(), [], ranked, PairOf([]), Profile);

        Assert.True(facts.DiffAvailable);
        Assert.Equal(ReportFacts.MaxCauses, facts.Causes.Count);
        Assert.Equal("b/1", facts.Causes[0].DisplayPath);
        Assert.Equal("c/0", facts.Causes[1].DisplayPath);
        Assert.Equal(ReportFacts.MaxFieldsPerCause, facts.Causes[0].Fields.Count);
        Assert.Equal(["f0", "f1", "f2", "f3", "f4"], facts.Causes[0].Fields.Select(f => f.Path));
        Assert.Equal(UnitChange.Changed, facts.Causes[0].Change);
    }

    [Fact]
    public void Cause_fields_come_before_consequence_fields_within_the_cap()
    {
        var diffs = Enumerable.Range(0, 8)
            .Select(i => new FieldDiff($"f{i}", "1", "2", FieldChangeKind.Changed)).ToList();
        var unit = new UnitDiffDetail("b/1", UnitChange.Changed, null, null, null, null, 10, 10, diffs, false, false);
        var fields = diffs.Select((d, i) => new RankedField(d, i >= 6 ? DiffWeight.Cause : DiffWeight.Consequence)).ToList();
        var ranked = new List<RankedDiff> { new(unit, "b/1", "b", DiffWeight.Cause, fields) };

        var facts = ReportFacts.Build(Summary(), [], ranked, PairOf([]), Profile);

        Assert.Equal(["f6", "f7", "f0", "f1", "f2"], facts.Causes[0].Fields.Select(f => f.Path));
    }

    [Fact]
    public void Cause_fields_are_ordered_cause_then_consequence_then_noise()
    {
        var weights = new[] { DiffWeight.Noise, DiffWeight.Consequence, DiffWeight.Noise, DiffWeight.Cause, DiffWeight.Consequence };
        var diffs = weights.Select((_, i) => new FieldDiff($"f{i}", "1", "2", FieldChangeKind.Changed)).ToList();
        var unit = new UnitDiffDetail("b/1", UnitChange.Changed, null, null, null, null, 10, 10, diffs, false, false);
        var fields = diffs.Select((d, i) => new RankedField(d, weights[i])).ToList();
        var ranked = new List<RankedDiff> { new(unit, "b/1", "b", DiffWeight.Cause, fields) };

        var facts = ReportFacts.Build(Summary(), [], ranked, PairOf([]), Profile);

        Assert.Equal(["f3", "f1", "f4", "f0", "f2"], facts.Causes[0].Fields.Select(f => f.Path));
    }

    [Fact]
    public void Without_a_ranked_diff_causes_are_empty_and_diff_is_not_available()
    {
        var facts = ReportFacts.Build(Summary(), [], null, PairOf([]), Profile);

        Assert.Empty(facts.Causes);
        Assert.False(facts.DiffAvailable);
    }

    [Fact]
    public void A_missing_machine_appears_as_missing_dump()
    {
        using var tree = new TempTree();
        var folder = Dumps.Folder(tree, "host", 1, Dumps.HostMetadata11c0);

        var facts = ReportFacts.Build(Summary(), [], null, PairOf([Info(folder, 1)], 3), Profile);

        Assert.Equal(2, facts.Dumps.Count);
        Assert.False(facts.Dumps[0].Missing);
        Assert.Equal(1, facts.Dumps[0].MachineId);
        var missing = facts.Dumps[1];
        Assert.True(missing.Missing);
        Assert.Equal(3, missing.MachineId);
        Assert.Null(missing.Path);
        Assert.Equal(0, missing.SizeBytes);
    }

    [Fact]
    public void Folder_dump_size_is_the_sum_of_its_files_and_zip_size_its_length()
    {
        using var tree = new TempTree();
        var folder = Dumps.Folder(tree, "host", 1, Dumps.HostMetadata11c0, (DumpFileKind.Debug, new string('x', 1000)));
        var expected = Directory.GetFiles(folder).Sum(f => new FileInfo(f).Length);
        var zip = tree.PathOf("client.zip");
        Dumps.Zip(zip, "", 2, Dumps.ClientMetadata11c0, (DumpFileKind.Debug, new string('y', 500)));

        var facts = ReportFacts.Build(Summary(), [], null, PairOf([Info(folder, 1), Info(zip, 2)]), Profile);

        var host = facts.Dumps.Single(d => d.MachineId == 1);
        Assert.True(host.IsFolder);
        Assert.Equal(expected, host.SizeBytes);
        Assert.True(host.SizeBytes > 1000);
        var client = facts.Dumps.Single(d => d.MachineId == 2);
        Assert.False(client.IsFolder);
        Assert.Equal(new FileInfo(zip).Length, client.SizeBytes);
        Assert.Equal(zip, client.Path);
    }

    [Fact]
    public void Checksum_explanations_are_english_with_a_fallback_for_unknown_categories()
    {
        var facts = ReportFacts.Build(Summary(["RANDOM_COUNT", "SOMETHING_NEW"]), [], null, PairOf([]), Profile);

        Assert.Equal(
            ["The random number generators were called a different number of times.", "Checksum category SOMETHING_NEW differed."],
            facts.ChecksumExplanations);
    }

    [Fact]
    public void The_summary_carries_the_session_kind_from_the_metadata()
    {
        using var tree = new TempTree();
        Dumps.Folder(tree, "host", 1, Dumps.HostMetadata11c0);
        Dumps.Folder(tree, "client", 2, Dumps.ClientMetadata11c0);
        var pair = Assert.Single(OosDumpCatalog.Pair(OosDumpCatalog.Scan([tree.Root], [])));
        var dumps = pair.Dumps.Select(d => d.Open()).ToList();
        try
        {
            var summary = OosSummaryBuilder.Build(pair, dumps);

            Assert.Equal("preparation", summary.SessionKind);
        }
        finally
        {
            dumps.ForEach(d => d.Dispose());
        }
    }
}
