using ParadoxModTool.GitIntegration;
using ParadoxModTool.Mods;
using ParadoxModTool.Notes;
using Xunit;
using static ParadoxModTool.BugTracking.Tests.FakeProcessRunner;

namespace ParadoxModTool.BugTracking.Tests;

public class GitHubBugSourceTests
{
    private static GitHubBugSource Source(FakeProcessRunner fake) => new(() => new GhClient(fake, @"C:\gh\gh.exe"));

    private static BugSourceEntry Entry(params string[] labels) => new() { Kind = BugSourceKinds.GitHub, Key = "o/r", Labels = labels.ToList() };

    private static string IssueLine(int number, string state, string reason, params string[] labels) =>
        $$"""{"number":{{number}},"title":"T{{number}}","body":"B","author":"anna","state":"{{state}}","reason":"{{reason}}","url":"https://github.com/o/r/issues/{{number}}","created":"2026-09-01T10:00:00Z","updated":"2026-09-02T10:00:00Z","closed":null,"labels":[{{string.Join(",", labels.Select(l => $"\"{l}\""))}}]}""";

    [Theory]
    [InlineData(true, "", BugStatus.Open)]
    [InlineData(false, "completed", BugStatus.Done)]
    [InlineData(false, "", BugStatus.Done)]
    [InlineData(false, "not_planned", BugStatus.Rejected)]
    [InlineData(false, "NOT_PLANNED", BugStatus.Rejected)]
    public void StatusOf_ZustandUndGrund(bool open, string reason, BugStatus expected)
    {
        var issue = new GhIssue(1, "t", "b", "a", open, reason, "u", DateTime.UtcNow, DateTime.UtcNow, null, Array.Empty<string>());

        Assert.Equal(expected, GitHubBugSource.StatusOf(issue));
    }

    [Fact]
    public async Task Fetch_LabelFilterMitOder_UndFelder()
    {
        var fake = new FakeProcessRunner();
        fake.OnContains("repos/o/r/issues?", Ok(IssueLine(1, "open", "", "Bug"), IssueLine(2, "closed", "not_planned", "crash"), IssueLine(3, "open", "", "feature")));

        var all = await Source(fake).FetchAsync(Entry(), default);
        var filtered = await Source(fake).FetchAsync(Entry("bug", "crash"), default);

        Assert.Equal(3, all.Count);
        Assert.Equal(new[] { "1", "2" }, filtered.Select(r => r.ExternalId));
        Assert.Equal(BugStatus.Rejected, filtered[1].Status);
        Assert.Equal("https://github.com/o/r/issues/1", filtered[0].Url);
        Assert.Equal("T1", filtered[0].Title);
        Assert.Equal("anna", filtered[0].Author);
    }

    [Fact]
    public async Task Fetch_OhneGh_MeldetEinrichten()
    {
        var source = new GitHubBugSource(() => null);

        var error = await Assert.ThrowsAsync<BugSourceException>(() => source.FetchAsync(Entry(), default));
        Assert.Equal(BugTexts.GhMissing, error.Message);
    }

    [Fact]
    public async Task Fetch_GhFehler_WirdBugSourceException()
    {
        var fake = new FakeProcessRunner();
        fake.OnContains("repos/o/r/issues?", Fail(1, "HTTP 401: Bad credentials"));

        var error = await Assert.ThrowsAsync<BugSourceException>(() => Source(fake).FetchAsync(Entry(), default));
        Assert.Contains("401", error.Message);
    }

    [Theory]
    [InlineData(BugStatus.Done, "state_reason=completed")]
    [InlineData(BugStatus.Rejected, "state_reason=not_planned")]
    [InlineData(BugStatus.Open, "state=open")]
    public async Task SetStatus_SchicktPassendenPatch(BugStatus status, string expectedLastArg)
    {
        var fake = new FakeProcessRunner();
        var report = new BugReport { Source = BugSource.GitHub, SourceKey = "o/r", ExternalId = "7" };

        await Source(fake).SetStatusAsync(Entry(), report, status, default);

        var args = fake.Calls.Single().Arguments;
        Assert.Equal("repos/o/r/issues/7", args[3]);
        Assert.Equal(expectedLastArg, args[^1]);
    }

    [Fact]
    public async Task Diskussion_UndLabels()
    {
        var fake = new FakeProcessRunner();
        fake.OnContains("issues/7/comments", Ok("""{"author":"ben","created":"2026-09-03T10:00:00Z","body":"Bei mir auch"}"""));
        fake.OnContains("repos/o/r/labels", Ok("bug"));
        var report = new BugReport { Source = BugSource.GitHub, SourceKey = "o/r", ExternalId = "7" };

        var comment = Assert.Single(await Source(fake).LoadDiscussionAsync(Entry(), report, default));

        Assert.Equal("ben", comment.Author);
        Assert.Equal(new[] { "bug" }, await Source(fake).LoadLabelsAsync("o/r", default));
    }

    [Fact]
    public async Task Bildlinks_AusIssueTextUndKommentaren()
    {
        var fake = new FakeProcessRunner();
        var body = @"Absturz\n![shot](https://github.com/user-attachments/assets/aaa)";
        fake.OnContains("repos/o/r/issues?", Ok($$"""{"number":1,"title":"T","body":"{{body}}","author":"anna","state":"open","reason":"","url":"u","created":"2026-09-01T10:00:00Z","updated":"2026-09-01T10:00:00Z","closed":null,"labels":[]}"""));
        fake.OnContains("issues/1/comments", Ok("""{"author":"ben","created":"2026-09-02T10:00:00Z","body":"<img src=\"https://github.com/user-attachments/assets/bbb\">"}"""));

        var bug = Assert.Single(await Source(fake).FetchAsync(Entry(), default));
        var comment = Assert.Single(await Source(fake).LoadDiscussionAsync(Entry(), new BugReport { Source = BugSource.GitHub, SourceKey = "o/r", ExternalId = "1" }, default));

        Assert.Equal("https://github.com/user-attachments/assets/aaa", Assert.Single(bug.Attachments!).Url);
        Assert.Equal("https://github.com/user-attachments/assets/bbb", Assert.Single(comment.Attachments!).Url);
    }
}
