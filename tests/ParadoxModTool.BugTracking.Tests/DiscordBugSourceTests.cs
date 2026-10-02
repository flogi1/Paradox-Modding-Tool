using ParadoxModTool.Mods;
using ParadoxModTool.Notes;
using Xunit;

namespace ParadoxModTool.BugTracking.Tests;

public class DiscordBugSourceTests
{
    private readonly FakeHttpHandler _http = new();

    private DiscordBugSource Source() => new(() => new DiscordClient(new HttpClient(_http), "geheim.token", (_, _) => Task.CompletedTask));

    private static BugSourceEntry Entry(bool archive = true, params string[] filter) => new()
    {
        Kind = BugSourceKinds.Discord,
        Key = "100/222",
        OpenTagId = "1",
        DoneTagId = "2",
        RejectedTagId = "3",
        ArchiveClosed = archive,
        Labels = filter.ToList(),
    };

    private static string Thread(string id, string parent, string tags) =>
        $$$"""{"id":"{{{id}}}","guild_id":"100","parent_id":"{{{parent}}}","name":"Bug {{{id}}}","type":11,"applied_tags":[{{{tags}}}],"last_message_id":"{{{id}}}","thread_metadata":{"archived":false,"archive_timestamp":"2026-09-01T10:00:00+00:00"}}""";

    private const string Forum = """{"id":"222","guild_id":"100","name":"bugs","type":15,"available_tags":[{"id":"1","name":"Offen"},{"id":"2","name":"Erledigt"},{"id":"3","name":"Verworfen"},{"id":"8","name":"Crash"}]}""";

    [Theory]
    [InlineData(new string[0], BugStatus.Open)]
    [InlineData(new[] { "1" }, BugStatus.Open)]
    [InlineData(new[] { "2" }, BugStatus.Done)]
    [InlineData(new[] { "2", "3" }, BugStatus.Rejected)]
    [InlineData(new[] { "8" }, BugStatus.Open)]
    public void StatusOf_NurAusTags_VerworfenVorErledigt(string[] tags, BugStatus expected)
    {
        Assert.Equal(expected, DiscordBugSource.StatusOf(tags, Entry()));
    }

    [Fact]
    public void Snowflake_UndUrl()
    {
        Assert.Equal(new DateTime(2015, 1, 1, 0, 0, 0, DateTimeKind.Utc), DiscordBugSource.SnowflakeTime("0"));
        Assert.Equal(new DateTime(2015, 1, 1, 0, 0, 1, DateTimeKind.Utc), DiscordBugSource.SnowflakeTime((1000UL << 22).ToString()));
        Assert.Equal("https://discord.com/channels/100/900", DiscordBugSource.ThreadUrl("100", "900"));
    }

    [Fact]
    public async Task Fetch_AktiveUndArchivierte_DesChannels_MitFilterUndStartnachricht()
    {
        _http.On("GET", "guilds/100/threads/active", 200, $$"""{"threads":[{{Thread("900", "222", "\"8\"")}},{{Thread("901", "333", "")}}]}""");
        _http.On("GET", "channels/222/threads/archived/public", 200, $$"""{"threads":[{{Thread("902", "222", "\"2\"")}},{{Thread("900", "222", "\"8\"")}}],"has_more":false}""");
        _http.On("GET", "channels/900/messages/900", 200, """{"id":"900","author":{"username":"anna"},"content":"Absturz beim Laden","timestamp":"2026-09-01T10:00:00+00:00"}""");

        var all = await Source().FetchAsync(Entry(), default);
        var filtered = await Source().FetchAsync(Entry(true, "8"), default);

        Assert.Equal(new[] { "900", "902" }, all.Select(b => b.ExternalId));
        var first = all[0];
        Assert.Equal("Bug 900", first.Title);
        Assert.Equal("Absturz beim Laden", first.Body);
        Assert.Equal("anna", first.Author);
        Assert.Equal("https://discord.com/channels/100/900", first.Url);
        Assert.Equal(BugStatus.Done, all[1].Status);
        Assert.Equal(string.Empty, all[1].Body);
        Assert.Equal(new[] { "900" }, filtered.Select(b => b.ExternalId));
    }

    [Fact]
    public async Task SetStatus_BehaeltFremdeTags_UndArchiviert()
    {
        _http.On("GET", "channels/222", 200, Forum);
        _http.On("GET", "channels/900", 200, Thread("900", "222", "\"1\",\"8\""));
        _http.On("PATCH", "channels/900", 200, "{}");
        var report = new BugReport { Source = BugSource.Discord, SourceKey = "100/222", ExternalId = "900" };

        await Source().SetStatusAsync(Entry(), report, BugStatus.Rejected, default);

        var patches = _http.Requests.Where(r => r.Method == "PATCH").Select(r => r.Body).ToList();
        Assert.Equal(new[] { """{"archived":false,"applied_tags":["8","3"]}""", """{"archived":true}""" }, patches);
    }

    [Fact]
    public async Task SetStatus_WiederOeffnen_OhneArchivieren()
    {
        _http.On("GET", "channels/222", 200, Forum);
        _http.On("GET", "channels/900", 200, Thread("900", "222", "\"3\""));
        _http.On("PATCH", "channels/900", 200, "{}");

        await Source().SetStatusAsync(Entry(), new BugReport { ExternalId = "900" }, BugStatus.Open, default);

        Assert.Equal("""{"archived":false,"applied_tags":["1"]}""", Assert.Single(_http.Requests, r => r.Method == "PATCH").Body);
    }

    [Fact]
    public async Task SetStatus_GeloeschterTag_Meldet()
    {
        _http.On("GET", "channels/222", 200, """{"id":"222","guild_id":"100","type":15,"available_tags":[{"id":"1","name":"Offen"}]}""");

        var error = await Assert.ThrowsAsync<BugSourceException>(() => Source().SetStatusAsync(Entry(), new BugReport { ExternalId = "900" }, BugStatus.Done, default));

        Assert.Equal(BugTexts.DiscordTagMissing(BugTexts.Status(BugStatus.Done)), error.Message);
        Assert.DoesNotContain(_http.Requests, r => r.Method == "PATCH");
    }

    [Fact]
    public async Task SetStatus_MehrAlsFuenfTags_Meldet()
    {
        _http.On("GET", "channels/222", 200, Forum);
        _http.On("GET", "channels/900", 200, Thread("900", "222", "\"4\",\"5\",\"6\",\"7\",\"8\""));

        var error = await Assert.ThrowsAsync<BugSourceException>(() => Source().SetStatusAsync(Entry(), new BugReport { ExternalId = "900" }, BugStatus.Done, default));

        Assert.Equal(BugTexts.DiscordTooManyTags, error.Message);
    }

    [Fact]
    public async Task Diskussion_OhneStartnachricht_AeltesteZuerst()
    {
        _http.On("GET", "channels/900/messages?limit=100", 200,
            """[{"id":"903","author":{"username":"c"},"content":"drei","timestamp":"2026-09-03T10:00:00+00:00"},{"id":"902","author":{"username":"b"},"content":"zwei","timestamp":"2026-09-02T10:00:00+00:00"},{"id":"900","author":{"username":"a"},"content":"start","timestamp":"2026-09-01T10:00:00+00:00"}]""");

        var comments = await Source().LoadDiscussionAsync(Entry(), new BugReport { ExternalId = "900" }, default);

        Assert.Equal(new[] { "zwei", "drei" }, comments.Select(c => c.Body));
    }

    [Fact]
    public async Task Fetch_OhneToken_MeldetEinrichten()
    {
        var error = await Assert.ThrowsAsync<BugSourceException>(() => new DiscordBugSource(() => null).FetchAsync(Entry(), default));

        Assert.Equal(BugTexts.DiscordNotSetUp, error.Message);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(500)]
    public async Task Fehler_WerdenNachStatusUebersetzt(int status)
    {
        _http.On("GET", "guilds/100/threads/active", status, """{"message":"kaputt"}""");

        var error = await Assert.ThrowsAsync<BugSourceException>(() => Source().FetchAsync(Entry(), default));

        var expected = status switch
        {
            401 => BugTexts.DiscordTokenInvalid,
            403 => BugTexts.DiscordForbidden,
            404 => BugTexts.DiscordNotFound,
            _ => BugTexts.DiscordFailed(500, "kaputt"),
        };
        Assert.Equal(expected, error.Message);
    }

    [Fact]
    public async Task Fehlermeldung_EnthaeltNieDenToken()
    {
        _http.On("GET", "guilds/100/threads/active", 500, """{"message":"interner Fehler"}""");

        var error = await Assert.ThrowsAsync<BugSourceException>(() => Source().FetchAsync(Entry(), default));

        Assert.DoesNotContain("geheim", error.Message);
        Assert.DoesNotContain("Bot ", error.Message);
    }

    [Fact]
    public async Task Anhaenge_DerStartnachrichtUndDerAntworten()
    {
        _http.On("GET", "guilds/100/threads/active", 200, "{\"threads\":[" + Thread("900", "222", "") + "]}");
        _http.On("GET", "channels/222/threads/archived/public", 200, """{"threads":[],"has_more":false}""");
        _http.On("GET", "channels/900/messages/900", 200,
            """{"id":"900","author":{"username":"anna"},"content":"Start","timestamp":"2026-09-01T10:00:00+00:00","attachments":[{"filename":"a.png","url":"https://cdn.discordapp.com/a.png","content_type":"image/png"}]}""");
        _http.On("GET", "channels/900/messages?limit=100", 200,
            """[{"id":"901","author":{"username":"ben"},"content":"hier auch","timestamp":"2026-09-02T10:00:00+00:00","attachments":[{"filename":"b.png","url":"https://cdn.discordapp.com/b.png","content_type":"image/png"}]}]""");

        var bug = Assert.Single(await Source().FetchAsync(Entry(), default));
        var reply = Assert.Single(await Source().LoadDiscussionAsync(Entry(), new BugReport { ExternalId = "900" }, default));

        Assert.Equal("a.png", Assert.Single(bug.Attachments!).FileName);
        Assert.Equal("b.png", Assert.Single(reply.Attachments!).FileName);
    }
}
