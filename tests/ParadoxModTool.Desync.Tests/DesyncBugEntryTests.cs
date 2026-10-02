using ParadoxModTool.Notes;
using Xunit;

namespace ParadoxModTool.Desync.Tests;

/// <summary>NoteKeys.SetResolver ist prozessweit; die Klasse setzt ihn und stellt ihn in Dispose zurück (und löscht den Ordner der Meldungsdatei).</summary>
public sealed class DesyncBugEntryTests : IDisposable
{
    private readonly string _modRoot = Path.Combine(Path.GetTempPath(), "pmt-desync-bug-" + Guid.NewGuid().ToString("N"));

    private readonly string _storeFolder = Path.Combine(Path.GetTempPath(), "pmt-desync-bugstore-" + Guid.NewGuid().ToString("N"));

    public DesyncBugEntryTests()
    {
        var root = Path.GetFullPath(_modRoot);
        NoteKeys.SetResolver(full =>
            full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                ? NoteKeys.ModKey("testmod", full[(root.Length + 1)..])
                : null);
    }

    public void Dispose()
    {
        NoteKeys.SetResolver(null);
        if (Directory.Exists(_storeFolder))
        {
            Directory.Delete(_storeFolder, recursive: true);
        }
    }

    private string InMod(string relative) => Path.Combine(_modRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    private static ReportFacts Facts(params ReportLead[] leads) =>
        new(new OosSummary("11c0de175691b2bb", "11c0de175691b2bb99999999aaaaaaaa", "1425.2.4.8", 100, 120,
                ["SAVE_HASH"], [], [], "preparation"),
            leads, [], true, [], []);

    private static ReportLead Lead(string? fullPath, int? line, bool own = true, string message = "Error in effect") =>
        new([2], "1425.2.4.7", message, "common/foo.txt", fullPath, line, "Cool Mod", own, LeadKind.ScriptError);

    [Fact]
    public void Only_own_mod_leads_with_a_resolved_file_become_links()
    {
        var facts = Facts(
            Lead(InMod("common/a.txt"), 5),
            Lead(InMod("common/a.txt"), 5, message: "Same place again"),
            Lead(InMod("common/b.txt"), 9),
            Lead(InMod("common/c.txt"), 1, own: false),
            Lead(null, 3));

        var entry = DesyncBugEntry.Build(facts, []);

        Assert.Equal(
            ["mod:testmod/common/a.txt", "mod:testmod/common/b.txt"],
            entry.Links.Select(l => l.Key));
        Assert.Equal([5, 9], entry.Links.Select(l => l.Line));
        Assert.All(entry.Links, l => Assert.Null(l.EndLine));
    }

    [Fact]
    public void Title_is_the_summary_line_without_the_out_of_sync_prefix()
    {
        var facts = Facts(Lead(null, null));

        var entry = DesyncBugEntry.Build(facts, []);

        Assert.StartsWith("Out of Sync ", ParadoxFormReport.SummaryLine(facts), StringComparison.Ordinal);
        Assert.Equal("Desync: " + ParadoxFormReport.SummaryLine(facts)["Out of Sync ".Length..], entry.Title);
        Assert.Equal(ModAuthorReport.Build(facts, []), entry.Body);
    }

    [Fact]
    public void Mod_id_comes_from_the_first_link()
    {
        var entry = DesyncBugEntry.Build(Facts(Lead(InMod("common/a.txt"), 5)), []);

        Assert.Equal("testmod", entry.ModId);
    }

    [Fact]
    public void Without_links_there_is_no_mod_id()
    {
        var entry = DesyncBugEntry.Build(Facts(Lead(null, 3)), []);

        Assert.Empty(entry.Links);
        Assert.Null(entry.ModId);
    }

    [Fact]
    public void Adding_creates_one_local_report_with_all_links_and_the_report_as_body()
    {
        var entry = DesyncBugEntry.Build(Facts(Lead(InMod("common/a.txt"), 5), Lead(InMod("common/b.txt"), 9)), []);
        var data = new BugData();

        var report = entry.AddTo(data, entry.ModId);

        Assert.Same(report, Assert.Single(data.Reports));
        Assert.Equal(entry.Title, report.Title);
        Assert.Equal(entry.Body, report.Body);
        Assert.Equal(BugSource.Local, report.Source);
        Assert.Equal("testmod", report.ModId);
        Assert.Equal(["mod:testmod/common/a.txt", "mod:testmod/common/b.txt"], report.Links.Select(l => l.Key));
    }

    [Fact]
    public void An_added_entry_survives_a_round_trip_through_the_bug_store_file()
    {
        var entry = DesyncBugEntry.Build(Facts(Lead(InMod("common/a.txt"), 5), Lead(InMod("common/b.txt"), 9)), []);
        Directory.CreateDirectory(_storeFolder);
        var path = Path.Combine(_storeFolder, BugReportStore.FileName);

        BugReportStore.Update(path, data => entry.AddTo(data, entry.ModId));
        var report = Assert.Single(BugReportStore.Load(path).Reports);

        Assert.Equal(entry.Title, report.Title);
        Assert.Equal(entry.Body, report.Body);
        Assert.Equal(BugSource.Local, report.Source);
        Assert.Equal("testmod", report.ModId);
        Assert.Equal(["mod:testmod/common/a.txt", "mod:testmod/common/b.txt"], report.Links.Select(l => l.Key));
        Assert.Equal([5, 9], report.Links.Select(l => l.Line));
    }

    [Fact]
    public void Adding_without_links_creates_a_report_without_links()
    {
        var entry = DesyncBugEntry.Build(Facts(Lead(null, 3)), []);
        var data = new BugData();

        var report = entry.AddTo(data, null);

        Assert.Empty(report.Links);
        Assert.Null(report.ModId);
    }

    [Fact]
    public void A_chosen_mod_overrides_the_derived_one()
    {
        var entry = DesyncBugEntry.Build(Facts(Lead(InMod("common/a.txt"), 5)), []);
        var data = new BugData();

        var report = entry.AddTo(data, "othermod");

        Assert.Equal("testmod", entry.ModId);
        Assert.Equal("othermod", report.ModId);
        Assert.Single(report.Links);
    }
}
