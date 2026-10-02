using Xunit;
using static ParadoxModTool.GitIntegration.Tests.FakeProcessRunner;

namespace ParadoxModTool.GitIntegration.Tests;

public class GhIssuesTests
{
    private static GhClient Client(FakeProcessRunner fake) => new(fake, @"C:\gh\gh.exe");

    private static string[] LastArgs(FakeProcessRunner fake) => fake.Calls[^1].Arguments.ToArray();

    [Fact]
    public async Task ListIssues_SeitenweiseOhnePullRequests()
    {
        var fake = new FakeProcessRunner();
        fake.OnContains("repos/o/r/issues?", Ok(
            """{"number":7,"title":"Absturz","body":"Text\nZeile 2","author":"anna","state":"closed","reason":"not_planned","url":"https://github.com/o/r/issues/7","created":"2026-09-01T10:00:00Z","updated":"2026-09-02T10:00:00Z","closed":"2026-09-02T10:00:00Z","labels":["bug","crash"]}""",
            "",
            """{"number":8,"title":"Offen","body":"","author":"ben","state":"open","reason":"","url":"https://github.com/o/r/issues/8","created":"2026-09-03T10:00:00Z","updated":"2026-09-03T10:00:00Z","closed":null,"labels":[]}"""));

        var issues = await Client(fake).ListIssuesAsync("o/r", default);

        var args = LastArgs(fake);
        Assert.Equal("api", args[0]);
        Assert.Equal("repos/o/r/issues?state=all&per_page=100", args[1]);
        Assert.Contains("--paginate", args);
        Assert.Contains(args, a => a.Contains("select(.pull_request == null)", StringComparison.Ordinal));
        Assert.Equal(2, issues.Count);
        var closed = issues[0];
        Assert.Equal(7, closed.Number);
        Assert.Equal("Text\nZeile 2", closed.Body);
        Assert.Equal("anna", closed.Author);
        Assert.False(closed.IsOpen);
        Assert.Equal("not_planned", closed.StateReason);
        Assert.Equal(new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc), closed.ClosedUtc);
        Assert.Equal(new[] { "bug", "crash" }, closed.Labels);
        Assert.True(issues[1].IsOpen);
        Assert.Null(issues[1].ClosedUtc);
    }

    [Fact]
    public async Task ListIssues_UnlesbareAusgabe_Wirft()
    {
        var fake = new FakeProcessRunner();
        fake.OnContains("repos/o/r/issues?", Ok("kein json"));

        await Assert.ThrowsAsync<GhException>(() => Client(fake).ListIssuesAsync("o/r", default));
    }

    [Fact]
    public async Task ListIssues_Fehler_KommtAlsGhException()
    {
        var fake = new FakeProcessRunner();
        fake.OnContains("repos/o/r/issues?", Fail(1, "HTTP 404: Not Found"));

        var error = await Assert.ThrowsAsync<GhException>(() => Client(fake).ListIssuesAsync("o/r", default));
        Assert.Contains("404", error.Message);
    }

    [Fact]
    public async Task CloseUndReopen_UeberPatch()
    {
        var fake = new FakeProcessRunner();

        await Client(fake).CloseIssueAsync("o/r", 7, notPlanned: true, default);
        Assert.Equal(new[] { "api", "-X", "PATCH", "repos/o/r/issues/7", "-f", "state=closed", "-f", "state_reason=not_planned" }, LastArgs(fake));

        await Client(fake).CloseIssueAsync("o/r", 7, notPlanned: false, default);
        Assert.Equal(new[] { "api", "-X", "PATCH", "repos/o/r/issues/7", "-f", "state=closed", "-f", "state_reason=completed" }, LastArgs(fake));

        await Client(fake).ReopenIssueAsync("o/r", 7, default);
        Assert.Equal(new[] { "api", "-X", "PATCH", "repos/o/r/issues/7", "-f", "state=open" }, LastArgs(fake));
    }

    [Fact]
    public async Task Kommentare_UndLabels()
    {
        var fake = new FakeProcessRunner();
        fake.OnContains("issues/7/comments", Ok("""{"author":"anna","created":"2026-09-02T11:00:00Z","body":"Noch da"}"""));
        fake.OnContains("repos/o/r/labels", Ok("bug", "", " crash "));

        var comment = Assert.Single(await Client(fake).ListIssueCommentsAsync("o/r", 7, default));
        var labels = await Client(fake).ListLabelsAsync("o/r", default);

        Assert.Equal("anna", comment.Author);
        Assert.Equal("Noch da", comment.Body);
        Assert.Equal(new DateTime(2026, 9, 2, 11, 0, 0, DateTimeKind.Utc), comment.CreatedUtc);
        Assert.Equal(new[] { "bug", "crash" }, labels);
    }
}
