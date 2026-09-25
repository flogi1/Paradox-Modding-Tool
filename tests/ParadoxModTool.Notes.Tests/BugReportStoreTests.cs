using Xunit;

namespace ParadoxModTool.Notes.Tests;

public class BugReportStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pmt-bugreports-").FullName;

    private string NewPath => Path.Combine(_dir, BugReportStore.FileName);

    private string LegacyPath => Path.Combine(_dir, BugReportStore.LegacyFileName);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private const string LegacyJson = """
        {
          "mod:abc/events/a.txt": [
            { "Id": "b1", "Title": "Offen", "Description": "Text", "Done": false, "CreatedUtc": "2026-09-01T10:00:00Z", "Line": 5, "EndLine": 9 },
            { "Id": "b2", "Title": "Fertig", "Description": "", "Done": true, "CreatedUtc": "2026-09-02T10:00:00Z", "DoneUtc": "2026-09-03T10:00:00Z" }
          ],
          "C:\\vanilla\\common\\x.txt": [
            { "Id": "b3", "Title": "Vanilla", "Description": "", "Done": false, "CreatedUtc": "2026-09-04T10:00:00Z", "Line": 2 }
          ]
        }
        """;

    [Fact]
    public void Convert_UebernimmtAlleFelder()
    {
        var data = BugLegacy.Convert(LegacyJson);

        Assert.Equal(BugData.CurrentVersion, data.Version);
        var open = data.Reports.Single(r => r.Id == "b1");
        Assert.Equal("abc", open.ModId);
        Assert.Equal("Offen", open.Title);
        Assert.Equal("Text", open.Body);
        Assert.Equal(BugStatus.Open, open.Status);
        Assert.Equal(BugSource.Local, open.Source);
        Assert.True(open.Seen);
        var link = Assert.Single(open.Links);
        Assert.Equal("mod:abc/events/a.txt", link.Key);
        Assert.Equal(5, link.Line);
        Assert.Equal(9, link.EndLine);

        var done = data.Reports.Single(r => r.Id == "b2");
        Assert.Equal(BugStatus.Done, done.Status);
        Assert.Equal(new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc), done.StatusChangedUtc);
        Assert.Null(Assert.Single(done.Links).Line);

        var vanilla = data.Reports.Single(r => r.Id == "b3");
        Assert.Null(vanilla.ModId);
        Assert.Equal(@"C:\vanilla\common\x.txt", Assert.Single(vanilla.Links).Key);
    }

    [Fact]
    public void Load_OhneNeueDatei_UebernimmtAlteBugsJson()
    {
        File.WriteAllText(LegacyPath, LegacyJson);

        var data = BugReportStore.Load(NewPath);

        Assert.Equal(3, data.Reports.Count);
        Assert.False(File.Exists(NewPath));
    }

    [Fact]
    public void Load_OhneBeideDateien_Leer()
    {
        Assert.Empty(BugReportStore.Load(NewPath).Reports);
    }

    [Fact]
    public void Load_KaputteAlteDatei_Leer()
    {
        File.WriteAllText(LegacyPath, "{ kaputt");

        Assert.Empty(BugReportStore.Load(NewPath).Reports);
    }

    [Fact]
    public void Update_SchreibtNurDieNeueDatei()
    {
        File.WriteAllText(LegacyPath, LegacyJson);
        var before = File.ReadAllBytes(LegacyPath);

        BugReportStore.Update(NewPath, data => data.Reports.Add(new BugReport { Title = "neu" }));

        Assert.Equal(before, File.ReadAllBytes(LegacyPath));
        var saved = BugReportStore.Load(NewPath);
        Assert.Equal(4, saved.Reports.Count);
        Assert.Contains(saved.Reports, r => r.Title == "neu");
    }

    [Fact]
    public void SaveUndLoad_RundlaufBehaeltAlleFelder()
    {
        var report = new BugReport
        {
            ModId = "abc",
            Source = BugSource.GitHub,
            SourceKey = "o/r",
            ExternalId = "12",
            Url = "https://example.invalid/12",
            Title = "t",
            Body = "b",
            Author = "a",
            Status = BugStatus.Rejected,
            StatusChangedUtc = new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc),
            Removed = true,
            Seen = false,
            Links = { BugLink.Create("mod:abc/a.txt", 3, 4) },
        };
        BugReportStore.Save(new BugData { Reports = { report } }, NewPath);

        var loaded = Assert.Single(BugReportStore.Load(NewPath).Reports);

        Assert.Equal(report.Id, loaded.Id);
        Assert.Equal("abc", loaded.ModId);
        Assert.Equal(BugSource.GitHub, loaded.Source);
        Assert.Equal("o/r", loaded.SourceKey);
        Assert.Equal("12", loaded.ExternalId);
        Assert.Equal(report.Url, loaded.Url);
        Assert.Equal("a", loaded.Author);
        Assert.Equal(BugStatus.Rejected, loaded.Status);
        Assert.Equal(report.StatusChangedUtc, loaded.StatusChangedUtc);
        Assert.True(loaded.Removed);
        Assert.False(loaded.Seen);
        Assert.Equal(4, Assert.Single(loaded.Links).EndLine);
        Assert.Contains("\"Rejected\"", File.ReadAllText(NewPath));
    }

    [Fact]
    public void Load_KaputteDatei_LegtKopieAn()
    {
        File.WriteAllText(NewPath, "{ kaputt");

        Assert.Empty(BugReportStore.Load(NewPath).Reports);
        Assert.Equal("{ kaputt", File.ReadAllText(Path.Combine(_dir, "bugreports.broken.json")));
    }

    [Fact]
    public void Load_NullFelder_WerdenAufgefuellt()
    {
        File.WriteAllText(NewPath, """{ "Version": 2, "Reports": [ { "Id": "x", "Title": null, "Links": null } ] }""");

        var report = Assert.Single(BugReportStore.Load(NewPath).Reports);

        Assert.Equal(string.Empty, report.Title);
        Assert.Empty(report.Links);
    }

    [Fact]
    public void Update_LiestFrisch_ZweiAenderungenBleibenErhalten()
    {
        BugReportStore.Update(NewPath, data => data.Reports.Add(new BugReport { Title = "eins" }));
        BugReportStore.Update(NewPath, data => data.Reports.Add(new BugReport { Title = "zwei" }));

        Assert.Equal(new[] { "eins", "zwei" }, BugReportStore.Load(NewPath).Reports.Select(r => r.Title));
    }

    [Fact]
    public void Update_GesperrteDatei_WirftStattLeerZuSchreiben()
    {
        BugReportStore.Update(NewPath, data => data.Reports.Add(new BugReport { Title = "bleibt" }));

        using (new FileStream(NewPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.ThrowsAny<IOException>(() => BugReportStore.Update(NewPath, data => data.Reports.Clear()));
        }

        Assert.Equal("bleibt", Assert.Single(BugReportStore.Load(NewPath).Reports).Title);
    }
}
