using Xunit;

namespace ParadoxModTool.Notes.Tests;

public class BugAttachmentTests
{
    private static RemoteBug Remote(params BugAttachment[] attachments) =>
        new("7", "u", "t", "b", "a", BugStatus.Open, new DateTime(2026, 9, 1), new DateTime(2026, 9, 2), null, attachments);

    [Theory]
    [InlineData("image/png", "x.bin", true)]
    [InlineData("", "shot.JPG", true)]
    [InlineData("", "shot.webp", true)]
    [InlineData("text/plain", "error.log", false)]
    [InlineData("", "error.log", false)]
    public void IsImage_NachTypOderEndung(string contentType, string fileName, bool expected)
    {
        Assert.Equal(expected, new BugAttachment(fileName, "https://cdn.discordapp.com/a/" + fileName, contentType).IsImage);
    }

    [Theory]
    [InlineData("https://cdn.discordapp.com/attachments/1/2/a.png?ex=1", true)]
    [InlineData("https://media.discordapp.net/attachments/1/2/a.png", true)]
    [InlineData("https://github.com/user-attachments/assets/abc", true)]
    [InlineData("https://private-user-images.githubusercontent.com/1/a.png", true)]
    [InlineData("http://cdn.discordapp.com/a.png", false)]
    [InlineData("https://tracker.example.com/pixel.png", false)]
    [InlineData("https://githubusercontent.com.evil.example/a.png", false)]
    [InlineData("kein link", false)]
    public void CanPreview_NurDiscordUndGitHubPerHttps(string url, bool expected)
    {
        Assert.Equal(expected, BugAttachment.CanPreview(url));
    }

    [Fact]
    public void ImageLinks_AusMarkdownUndHtml_OhneDoppelteUndOhneHttp()
    {
        var text = """
            Absturz beim Laden
            ![Screenshot 1](https://github.com/user-attachments/assets/aaa)
            <img width="800" alt="Fehler" src="https://github.com/user-attachments/assets/bbb" />
            ![](https://github.com/user-attachments/assets/aaa)
            ![alt](http://example.com/unsicher.png)
            <img src='https://raw.githubusercontent.com/o/r/main/pic.png'>
            """;

        var links = BugImageLinks.Extract(text);

        Assert.Equal(
            new[] { "https://github.com/user-attachments/assets/aaa", "https://github.com/user-attachments/assets/bbb", "https://raw.githubusercontent.com/o/r/main/pic.png" },
            links.Select(l => l.Url));
        Assert.Equal(new[] { "Screenshot 1", "Fehler", "pic.png" }, links.Select(l => l.FileName));
        Assert.All(links, l => Assert.True(l.IsImage));
        Assert.Empty(BugImageLinks.Extract(string.Empty));
    }

    [Fact]
    public void Sync_UebernimmtAnhaenge_NeueSignaturZaehltNichtAlsAenderung()
    {
        var data = new BugData();
        BugSync.Apply(data, BugSource.Discord, "1/2", "main", new[] { Remote(new BugAttachment("a.png", "https://cdn.discordapp.com/attachments/1/2/a.png?ex=1&hm=x", "image/png")) });

        var sameFile = BugSync.Apply(data, BugSource.Discord, "1/2", "main", new[] { Remote(new BugAttachment("a.png", "https://cdn.discordapp.com/attachments/1/2/a.png?ex=2&hm=y", "image/png")) });
        var added = BugSync.Apply(data, BugSource.Discord, "1/2", "main", new[] { Remote(new BugAttachment("a.png", "https://cdn.discordapp.com/attachments/1/2/a.png?ex=3", "image/png"), new BugAttachment("b.log", "https://cdn.discordapp.com/attachments/1/2/b.log", "text/plain")) });

        Assert.Equal(new BugSyncResult(0, 0, 0), sameFile);
        Assert.Equal(new BugSyncResult(0, 1, 0), added);
        var report = Assert.Single(data.Reports);
        Assert.Equal(2, report.Attachments.Count);
        Assert.EndsWith("ex=3", report.Attachments[0].Url);
    }

    [Fact]
    public void Sync_FrischeAdresseWirdAuchOhneAenderungGespeichert()
    {
        var data = new BugData();
        BugSync.Apply(data, BugSource.Discord, "1/2", "main", new[] { Remote(new BugAttachment("a.png", "https://cdn.discordapp.com/a.png?ex=1", "image/png")) });

        BugSync.Apply(data, BugSource.Discord, "1/2", "main", new[] { Remote(new BugAttachment("a.png", "https://cdn.discordapp.com/a.png?ex=2", "image/png")) });

        Assert.EndsWith("ex=2", data.Reports[0].Attachments[0].Url);
    }

    [Fact]
    public void Speicher_Rundlauf_UndAlteDateiOhneAnhaenge()
    {
        var dir = Directory.CreateTempSubdirectory("pmt-attach-").FullName;
        try
        {
            var path = Path.Combine(dir, BugReportStore.FileName);
            var report = new BugReport { Attachments = { new BugAttachment("a.png", "https://cdn.discordapp.com/a.png", "image/png") } };
            BugReportStore.Save(new BugData { Reports = { report } }, path);

            Assert.Equal("a.png", Assert.Single(Assert.Single(BugReportStore.Load(path).Reports).Attachments).FileName);

            File.WriteAllText(path, """{ "Version": 2, "Reports": [ { "Id": "x", "Title": "alt" } ] }""");
            Assert.Empty(Assert.Single(BugReportStore.Load(path).Reports).Attachments);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
