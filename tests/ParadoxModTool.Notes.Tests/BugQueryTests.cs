using Xunit;

namespace ParadoxModTool.Notes.Tests;

[Collection("NoteKeys")]
public class BugQueryTests : IDisposable
{
    // Projekt "main" mit Dev-Branch "beta"; "solo" ist eine eigene Mod; "fremd" gehört zu einem anderen Spiel (unbekannt).
    private static readonly Dictionary<string, string> Roots = new() { ["main"] = "main", ["beta"] = "main", ["solo"] = "solo" };

    private readonly string _dir = Directory.CreateTempSubdirectory("pmt-bugquery-").FullName;

    public BugQueryTests() => NoteKeys.SetResolver(null);

    public void Dispose()
    {
        NoteKeys.SetResolver(null);
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private BugProjects Projects => new(
        id => Roots.TryGetValue(id, out var root) ? root : null,
        id => new[] { Path.Combine(_dir, id) },
        root => Roots.Where(p => p.Value == root).Select(p => p.Key).OrderBy(k => k == root ? 0 : 1).ToList());

    private static BugReport Report(string? modId, params string[] keys)
    {
        var report = new BugReport { ModId = modId, Title = string.Join(",", keys) };
        report.Links.AddRange(keys.Select(k => BugLink.Create(k, 3, null)));
        return report;
    }

    [Fact]
    public void ForFileKey_FindetBezugAusBranchInHauptmod()
    {
        var data = new BugData { Reports = { Report("beta", "mod:beta/events/a.txt") } };

        Assert.Single(BugQueries.ForFileKey(data, "mod:main/EVENTS/A.txt", Projects));
        Assert.Single(BugQueries.ForFileKey(data, "mod:beta/events/a.txt", Projects));
        Assert.Empty(BugQueries.ForFileKey(data, "mod:solo/events/a.txt", Projects));
        Assert.Empty(BugQueries.ForFileKey(data, "mod:main/events/b.txt", Projects));
    }

    [Fact]
    public void ForFileKey_UnbekannteModZaehltNicht()
    {
        var data = new BugData { Reports = { Report("fremd", "mod:fremd/events/a.txt") } };

        Assert.Empty(BugQueries.ForFileKey(data, "mod:main/events/a.txt", Projects));
        Assert.Single(BugQueries.ForFileKey(data, "mod:fremd/events/a.txt", Projects));
        Assert.Empty(BugQueries.ForProject(data, null, Projects));
        Assert.Equal(0, BugQueries.UnseenCount(data, "main", Projects));
    }

    [Fact]
    public void ForFileKey_VollerPfadOhneGrossKlein_UndLiefertDenPassendenBezug()
    {
        var report = Report(null, @"C:\v\a.txt", "mod:main/b.txt");
        var data = new BugData { Reports = { report } };

        var match = Assert.Single(BugQueries.ForFileKey(data, @"c:\V\A.TXT", Projects));

        Assert.Same(report.Links[0], match.Link);
    }

    [Fact]
    public void ForFileKey_OffeneZuerst_DannNachAnlegezeit()
    {
        var done = Report("main", "mod:main/a.txt");
        done.Status = BugStatus.Done;
        done.CreatedUtc = new DateTime(2026, 1, 1);
        var later = Report("main", "mod:main/a.txt");
        later.CreatedUtc = new DateTime(2026, 3, 1);
        var earlier = Report("main", "mod:main/a.txt");
        earlier.CreatedUtc = new DateTime(2026, 2, 1);
        var data = new BugData { Reports = { done, later, earlier } };

        Assert.Equal(new[] { earlier, later, done }, BugQueries.ForFileKey(data, "mod:main/a.txt", Projects).Select(m => m.Report));
    }

    [Fact]
    public void ForProject_UeberBesitzerModsHinweg_OhneProjektNurModIdNull()
    {
        var inMain = Report("main");
        var inBeta = Report("beta");
        var solo = Report("solo");
        var none = Report(null);
        var data = new BugData { Reports = { inMain, inBeta, solo, none } };

        Assert.Equal(new[] { inMain, inBeta }.ToHashSet(), BugQueries.ForProject(data, "main", Projects).ToHashSet());
        Assert.Equal(new[] { none }, BugQueries.ForProject(data, null, Projects));
    }

    [Fact]
    public void UnseenCount_ZaehltUngeseheneOhneEntfernte()
    {
        var unseen = Report("beta");
        unseen.Seen = false;
        var removed = Report("main");
        removed.Seen = false;
        removed.Removed = true;
        var data = new BugData { Reports = { unseen, removed, Report("main") } };

        Assert.Equal(1, BugQueries.UnseenCount(data, "main", Projects));
    }

    [Fact]
    public void ProjectOfFile_UeberDenSchluesselHook()
    {
        NoteKeys.SetResolver(path => path.EndsWith("a.txt", StringComparison.Ordinal) ? NoteKeys.ModKey("beta", "a.txt") : null);

        Assert.Equal("main", BugQueries.ProjectOfFile(@"C:\m\a.txt", Projects));
        Assert.Null(BugQueries.ProjectOfFile(@"C:\m\b.txt", Projects));
    }

    [Fact]
    public void PreferredLink_BezugDieserDateiSonstDerErste()
    {
        var report = Report("main", "mod:main/a.txt", "mod:main/b.txt");

        Assert.Same(report.Links[1], BugQueries.PreferredLink(report, "mod:beta/b.txt", Projects));
        Assert.Same(report.Links[0], BugQueries.PreferredLink(report, "mod:main/c.txt", Projects));
        Assert.Same(report.Links[0], BugQueries.PreferredLink(report, null, Projects));
        Assert.Null(BugQueries.PreferredLink(Report("main"), null, Projects));
    }

    [Fact]
    public void FilesFor_NurVorhandeneDateien_BevorzugteModZuerst()
    {
        foreach (var mod in new[] { "main", "beta" })
        {
            Directory.CreateDirectory(Path.Combine(_dir, mod, "events"));
            File.WriteAllText(Path.Combine(_dir, mod, "events", "a.txt"), "x");
        }

        var link = BugLink.Create("mod:main/events/a.txt", 1, null);

        Assert.Equal(Path.Combine(_dir, "beta", "events", "a.txt"), Projects.FilesFor(link, "beta")[0]);
        Assert.Equal(2, Projects.FilesFor(link, null).Count);
        Assert.Equal(Path.Combine(_dir, "main", "events", "a.txt"), Projects.FilesFor(link, null)[0]);
        Assert.DoesNotContain(Projects.FilesFor(link, "solo"), f => f.StartsWith(Path.Combine(_dir, "solo"), StringComparison.OrdinalIgnoreCase));
        Assert.Empty(Projects.FilesFor(BugLink.Create("mod:main/events/fehlt.txt", 1, null), null));
        Assert.Empty(Projects.FilesFor(BugLink.Create(Path.Combine(_dir, "fehlt.txt"), 1, null), null));
    }

    [Fact]
    public void Filter_StatusQuelleNeuOhneBezugUndSuche()
    {
        var open = Report("main", "mod:main/a.txt");
        open.Title = "Absturz beim Laden";
        var rejected = Report("main");
        rejected.Status = BugStatus.Rejected;
        rejected.Seen = false;
        rejected.Body = "Quatsch";

        var all = new BugFilter(true, true, true, null, false, false, string.Empty);
        Assert.True(all.Matches(open) && all.Matches(rejected));
        Assert.False((all with { Rejected = false }).Matches(rejected));
        Assert.False((all with { Source = BugSource.GitHub }).Matches(open));
        Assert.Equal(new[] { rejected }, new[] { open, rejected }.Where((all with { OnlyUnseen = true }).Matches));
        Assert.Equal(new[] { rejected }, new[] { open, rejected }.Where((all with { OnlyWithoutLinks = true }).Matches));
        Assert.Equal(new[] { open }, new[] { open, rejected }.Where((all with { Search = "absturz" }).Matches));
        Assert.Equal(new[] { rejected }, new[] { open, rejected }.Where((all with { Search = "QUATSCH" }).Matches));
    }

    [Fact]
    public void ListRow_OrtMitOderOhnePfad()
    {
        var report = Report("main", "mod:main/events/a.txt");

        Assert.Equal("Zeile 3", new BugListRow(report, report.Links[0], showPath: false).LocationText);
        Assert.Equal("events/a.txt, Zeile 3", new BugListRow(report, report.Links[0], showPath: true).LocationText);
        Assert.Equal(string.Empty, new BugListRow(report, null, showPath: true).LocationText);
        Assert.True(new BugListRow(report, null, false).IsLocal);
        Assert.Equal("🐞", BugListRow.IconFor(BugSource.Local));
    }
}
