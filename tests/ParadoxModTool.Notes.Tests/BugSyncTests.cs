using Xunit;

namespace ParadoxModTool.Notes.Tests;

public class BugSyncTests
{
    private static RemoteBug Remote(string id, string title = "t", BugStatus status = BugStatus.Open) =>
        new(id, "https://example.invalid/" + id, title, "body", "anna", status, new DateTime(2026, 9, 1), new DateTime(2026, 9, 2), null);

    [Fact]
    public void Apply_NeueMeldungUngesehenMitBesitzerUndFeldern()
    {
        var data = new BugData();

        var result = BugSync.Apply(data, BugSource.GitHub, "o/r", "main", new[] { Remote("7", "Absturz", BugStatus.Rejected) });

        Assert.Equal(new BugSyncResult(1, 0, 0), result);
        var report = Assert.Single(data.Reports);
        Assert.Equal(BugSource.GitHub, report.Source);
        Assert.Equal("o/r", report.SourceKey);
        Assert.Equal("7", report.ExternalId);
        Assert.Equal("main", report.ModId);
        Assert.False(report.Seen);
        Assert.Equal("Absturz", report.Title);
        Assert.Equal("anna", report.Author);
        Assert.Equal(BugStatus.Rejected, report.Status);
        Assert.Equal("https://example.invalid/7", report.Url);
    }

    [Fact]
    public void Apply_BehaeltBezuegeSeenUndBesitzer()
    {
        var data = new BugData();
        BugSync.Apply(data, BugSource.GitHub, "o/r", "main", new[] { Remote("7") });
        var report = data.Reports[0];
        report.Seen = true;
        report.ModId = "beta";
        report.Links.Add(BugLink.Create("mod:main/a.txt", 3, null));

        var result = BugSync.Apply(data, BugSource.GitHub, "O/R", "main", new[] { Remote("7", "neuer Titel", BugStatus.Done) });

        Assert.Equal(new BugSyncResult(0, 1, 0), result);
        Assert.Single(data.Reports);
        Assert.Equal("neuer Titel", report.Title);
        Assert.Equal(BugStatus.Done, report.Status);
        Assert.NotNull(report.StatusChangedUtc);
        Assert.True(report.Seen);
        Assert.Equal("beta", report.ModId);
        Assert.Single(report.Links);
    }

    [Fact]
    public void Apply_UnveraendertZaehltNicht()
    {
        var data = new BugData();
        BugSync.Apply(data, BugSource.GitHub, "o/r", "main", new[] { Remote("7") });

        Assert.Equal(new BugSyncResult(0, 0, 0), BugSync.Apply(data, BugSource.GitHub, "o/r", "main", new[] { Remote("7") }));
    }

    [Fact]
    public void Apply_FehlendeWirdEntfernt_BezuegeBleiben()
    {
        var data = new BugData();
        BugSync.Apply(data, BugSource.GitHub, "o/r", "main", new[] { Remote("7"), Remote("8") });
        data.Reports[0].Links.Add(BugLink.Create("mod:main/a.txt", 1, null));

        Assert.Equal(new BugSyncResult(0, 0, 1), BugSync.Apply(data, BugSource.GitHub, "o/r", "main", new[] { Remote("8") }));
        Assert.True(data.Reports[0].Removed);
        Assert.Single(data.Reports[0].Links);
        Assert.Equal(new BugSyncResult(0, 0, 0), BugSync.Apply(data, BugSource.GitHub, "o/r", "main", new[] { Remote("8") }));
    }

    [Fact]
    public void Apply_WiederAufgetaucht()
    {
        var data = new BugData();
        BugSync.Apply(data, BugSource.GitHub, "o/r", "main", new[] { Remote("7") });
        BugSync.Apply(data, BugSource.GitHub, "o/r", "main", Array.Empty<RemoteBug>());

        Assert.Equal(new BugSyncResult(0, 1, 0), BugSync.Apply(data, BugSource.GitHub, "o/r", "main", new[] { Remote("7") }));
        Assert.False(data.Reports[0].Removed);
    }

    [Fact]
    public void Apply_AndereQuellenUndLokaleBleibenUnberuehrt()
    {
        var data = new BugData();
        var local = BugEditing.AddLocal(data, "lokal", string.Empty, "main", null);
        BugSync.Apply(data, BugSource.GitHub, "o/other", "main", new[] { Remote("7") });

        BugSync.Apply(data, BugSource.GitHub, "o/r", "main", new[] { Remote("7") });

        Assert.Equal(3, data.Reports.Count);
        Assert.False(local.Removed);
        Assert.False(data.Reports.Single(r => r.SourceKey == "o/other").Removed);
    }

    [Fact]
    public void MarkSourceRemoved_NurDieseQuelle()
    {
        var data = new BugData();
        BugSync.Apply(data, BugSource.GitHub, "o/r", "main", new[] { Remote("7"), Remote("8") });
        BugSync.Apply(data, BugSource.GitHub, "o/x", "main", new[] { Remote("9") });

        Assert.Equal(2, BugSync.MarkSourceRemoved(data, BugSource.GitHub, "o/r"));
        Assert.Equal(0, BugSync.MarkSourceRemoved(data, BugSource.GitHub, "o/r"));
        Assert.False(data.Reports.Single(r => r.ExternalId == "9").Removed);
    }
}
