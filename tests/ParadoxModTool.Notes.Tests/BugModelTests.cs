using Xunit;

namespace ParadoxModTool.Notes.Tests;

[Collection("NoteKeys")]
public class BugModelTests : IDisposable
{
    public BugModelTests() => NoteKeys.SetResolver(null);

    public void Dispose() => NoteKeys.SetResolver(null);

    [Fact]
    public void Create_IgnoriertEndzeileOhneBzwVorStartzeile()
    {
        Assert.Null(BugLink.Create("k", null, 9).EndLine);
        Assert.Null(BugLink.Create("k", 5, 5).EndLine);
        Assert.Null(BugLink.Create("k", 5, 3).EndLine);
        Assert.Equal(9, BugLink.Create("k", 5, 9).EndLine);
    }

    [Fact]
    public void LineText_ZeigtZeileBereichOderNichts()
    {
        Assert.Equal(string.Empty, BugLink.Create("k", null, null).LineText);
        Assert.Equal("Zeile 5", BugLink.Create("k", 5, null).LineText);
        Assert.Equal("Zeilen 5–9", BugLink.Create("k", 5, 9).LineText);
    }

    [Fact]
    public void DisplayPath_ZeigtRelativenTeilOderVollenPfad()
    {
        Assert.Equal("events/a.txt", BugLink.Create("mod:abc/events/a.txt", null, null).DisplayPath);
        Assert.Equal(@"C:\v\a.txt", BugLink.Create(@"C:\v\a.txt", null, null).DisplayPath);
    }

    [Fact]
    public void ForFile_NutztDenNotizSchluessel()
    {
        NoteKeys.SetResolver(_ => NoteKeys.ModKey("abc", "Events/A.txt"));

        var link = BugLink.ForFile(@"C:\m\events\a.txt", 3, null);

        Assert.Equal("mod:abc/events/a.txt", link.Key);
        Assert.Equal("abc", BugLink.ModIdOf(link.Key));
        Assert.Null(BugLink.ModIdOf(@"C:\v\a.txt"));
    }

    [Fact]
    public void SameAs_VergleichtSchluesselOhneGrossKleinUndZeilen()
    {
        var a = BugLink.Create("mod:abc/a.txt", 3, null);

        Assert.True(a.SameAs(BugLink.Create("MOD:abc/A.txt", 3, null)));
        Assert.False(a.SameAs(BugLink.Create("mod:abc/a.txt", 4, null)));
    }

    [Fact]
    public void BodyPreview_ErsteNichtLeereZeile()
    {
        Assert.Equal("zwei", new BugReport { Body = "\n  \n zwei \ndrei" }.BodyPreview);
        Assert.Equal(string.Empty, new BugReport().BodyPreview);
    }

    [Fact]
    public void NeueMeldung_IstOffenGesehenMitEigenerId()
    {
        var a = new BugReport();
        var b = new BugReport();

        Assert.True(a.IsOpen);
        Assert.True(a.Seen);
        Assert.Equal(BugSource.Local, a.Source);
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(32, a.Id.Length);
    }
}
