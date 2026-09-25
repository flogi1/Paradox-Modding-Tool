using Xunit;

namespace ParadoxModTool.Notes.Tests;

public class BugEditingTests
{
    [Fact]
    public void AddLocal_LegtOffeneGeseheneMeldungAn()
    {
        var data = new BugData();

        var report = BugEditing.AddLocal(data, "t", "b", "main", BugLink.Create("mod:main/a.txt", 2, null));

        Assert.Same(report, Assert.Single(data.Reports));
        Assert.Equal(BugStatus.Open, report.Status);
        Assert.Equal(BugSource.Local, report.Source);
        Assert.True(report.Seen);
        Assert.Equal("main", report.ModId);
        Assert.Single(report.Links);
        Assert.Empty(BugEditing.AddLocal(data, "ohne", string.Empty, null, null).Links);
    }

    [Fact]
    public void AddLink_KeinDoppelterBezug_UebernimmtModFuerMeldungOhneProjekt()
    {
        var data = new BugData();
        var report = BugEditing.AddLocal(data, "t", string.Empty, null, BugLink.Create(@"C:\v\a.txt", null, null));

        Assert.True(BugEditing.AddLink(data, report.Id, BugLink.Create("mod:beta/a.txt", 4, null)));
        Assert.False(BugEditing.AddLink(data, report.Id, BugLink.Create("MOD:beta/A.txt", 4, null)));
        Assert.False(BugEditing.AddLink(data, "gibtsnicht", BugLink.Create("mod:beta/a.txt", 5, null)));

        Assert.Equal(2, report.Links.Count);
        Assert.Equal("beta", report.ModId);
    }

    [Fact]
    public void RemoveLink_EntferntGleichenBezug()
    {
        var data = new BugData();
        var report = BugEditing.AddLocal(data, "t", string.Empty, "main", BugLink.Create("mod:main/a.txt", 4, null));

        Assert.True(BugEditing.RemoveLink(data, report.Id, BugLink.Create("mod:main/a.txt", 4, null)));
        Assert.Empty(report.Links);
        Assert.False(BugEditing.RemoveLink(data, report.Id, BugLink.Create("mod:main/a.txt", 4, null)));
    }

    [Fact]
    public void SetStatus_SetztZeitpunktNurBeiAenderung()
    {
        var data = new BugData();
        var report = BugEditing.AddLocal(data, "t", string.Empty, null, null);

        Assert.True(BugEditing.SetStatus(data, report.Id, BugStatus.Rejected));
        var stamp = report.StatusChangedUtc;
        Assert.NotNull(stamp);
        Assert.True(BugEditing.SetStatus(data, report.Id, BugStatus.Rejected));
        Assert.Equal(stamp, report.StatusChangedUtc);
        Assert.True(BugEditing.SetStatus(data, report.Id, BugStatus.Open));
        Assert.True(report.IsOpen);
        Assert.False(BugEditing.SetStatus(data, "gibtsnicht", BugStatus.Done));
    }

    [Fact]
    public void UpdateLocal_NurLokaleMeldungen()
    {
        var data = new BugData();
        var local = BugEditing.AddLocal(data, "alt", "alt", null, null);
        var external = new BugReport { Source = BugSource.GitHub, Title = "extern" };
        data.Reports.Add(external);

        Assert.True(BugEditing.UpdateLocal(data, local.Id, "neu", "text"));
        Assert.Equal("neu", local.Title);
        Assert.Equal("text", local.Body);
        Assert.False(BugEditing.UpdateLocal(data, external.Id, "x", "y"));
        Assert.Equal("extern", external.Title);
    }

    [Fact]
    public void Delete_UndMarkSeen()
    {
        var data = new BugData();
        var report = new BugReport { Seen = false };
        data.Reports.Add(report);

        Assert.True(BugEditing.MarkSeen(data, report.Id));
        Assert.True(report.Seen);
        Assert.False(BugEditing.MarkSeen(data, report.Id));
        Assert.True(BugEditing.Delete(data, report.Id));
        Assert.Empty(data.Reports);
        Assert.False(BugEditing.Delete(data, report.Id));
    }
}
