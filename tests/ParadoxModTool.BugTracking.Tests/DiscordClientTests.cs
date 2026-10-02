using Xunit;

namespace ParadoxModTool.BugTracking.Tests;

public class DiscordClientTests
{
    private readonly FakeHttpHandler _http = new();
    private readonly List<TimeSpan> _waits = new();

    private DiscordClient Client() => new(new HttpClient(_http), "geheim.token", (wait, _) =>
    {
        _waits.Add(wait);
        return Task.CompletedTask;
    });

    private static string Thread(string id, string parent, string tags = "", bool archived = false, string archivedAt = "2026-09-01T10:00:00+00:00") =>
        $$$"""{"id":"{{{id}}}","guild_id":"1","parent_id":"{{{parent}}}","name":"Bug {{{id}}}","type":11,"applied_tags":[{{{tags}}}],"last_message_id":"{{{id}}}","thread_metadata":{"archived":{{{(archived ? "true" : "false")}}},"archive_timestamp":"{{{archivedAt}}}"}}""";

    [Fact]
    public async Task Anfragen_TragenBotTokenUndBasis()
    {
        _http.On("GET", "users/@me", 200, """{"id":"42","username":"PMT-Bot"}""");

        var user = await Client().GetCurrentUserAsync(default);

        Assert.Equal(("42", "PMT-Bot"), (user.Id, user.Name));
        var request = Assert.Single(_http.Requests);
        Assert.Equal("Bot geheim.token", request.Auth);
        Assert.Equal(DiscordClient.ApiBase + "users/@me", request.Url);
    }

    [Fact]
    public async Task Channel_LiestTagsUeberschreibungenUndThreadDaten()
    {
        _http.On("GET", "channels/222", 200, """{"id":"222","guild_id":"1","name":"bugs","type":15,"available_tags":[{"id":"5","name":"Offen"}],"permission_overwrites":[{"id":"1","type":0,"allow":"0","deny":"1024"}]}""");
        _http.On("GET", "channels/900", 200, Thread("900", "222", "\"5\",\"7\"", archived: true));

        var forum = await Client().GetChannelAsync("222", default);
        var thread = await Client().GetChannelAsync("900", default);

        Assert.Equal(DiscordClient.ForumChannelType, forum.Type);
        Assert.Equal(("5", "Offen"), (forum.Tags[0].Id, forum.Tags[0].Name));
        Assert.Equal(1024UL, forum.Overwrites[0].Deny);
        Assert.Equal(new[] { "5", "7" }, thread.AppliedTags);
        Assert.True(thread.Archived);
        Assert.Equal("222", thread.ParentId);
    }

    [Fact]
    public async Task ArchivierteThreads_Seitenweise()
    {
        _http.On("GET", "channels/222/threads/archived/public?limit=100", 200,
            $$"""{"threads":[{{Thread("10", "222", archived: true, archivedAt: "2026-09-05T00:00:00+00:00")}},{{Thread("11", "222", archived: true, archivedAt: "2026-09-04T00:00:00+00:00")}}],"has_more":true}""");
        _http.On("GET", "before=2026-09-04T00%3A00%3A00%2B00%3A00", 200, $$"""{"threads":[{{Thread("12", "222", archived: true, archivedAt: "2026-09-03T00:00:00+00:00")}}],"has_more":false}""");

        var threads = await Client().GetArchivedThreadsAsync("222", default);

        Assert.Equal(new[] { "10", "11", "12" }, threads.Select(t => t.Id));
        Assert.Equal(2, _http.Requests.Count);
    }

    [Fact]
    public async Task RateLimit_WartetUndWiederholt()
    {
        var calls = 0;
        _http.On("GET", "users/@me", () => calls++ == 0
            ? FakeHttpHandler.Json(429, """{"message":"You are being rate limited.","retry_after":0.25,"global":false}""")
            : FakeHttpHandler.Json(200, """{"id":"42","username":"PMT-Bot"}"""));

        var user = await Client().GetCurrentUserAsync(default);

        Assert.Equal("42", user.Id);
        Assert.Equal(new[] { TimeSpan.FromSeconds(0.25) }, _waits);
    }

    [Fact]
    public async Task RateLimit_GibtNachFuenfVersuchenAuf()
    {
        _http.On("GET", "users/@me", 429, """{"retry_after":0.1}""");

        var error = await Assert.ThrowsAsync<DiscordException>(() => Client().GetCurrentUserAsync(default));

        Assert.Equal(429, error.StatusCode);
        Assert.Equal(5, _http.Requests.Count);
    }

    [Fact]
    public async Task Fehler_MitStatusUndMeldung_OhneToken()
    {
        _http.On("GET", "users/@me", 401, """{"message":"401: Unauthorized","code":0}""");

        var error = await Assert.ThrowsAsync<DiscordException>(() => Client().GetCurrentUserAsync(default));

        Assert.Equal(401, error.StatusCode);
        Assert.Contains("Unauthorized", error.Message);
        Assert.DoesNotContain("geheim", error.Message);
    }

    [Fact]
    public async Task Startnachricht_404_IstNull()
    {
        Assert.Null(await Client().GetMessageAsync("900", "900", default));
    }

    [Fact]
    public async Task Nachrichten_UndThreadAendern()
    {
        _http.On("GET", "channels/900/messages?limit=100", 200,
            """[{"id":"902","author":{"username":"anna","global_name":"Anna"},"content":"auch","timestamp":"2026-09-02T10:00:00+00:00"},{"id":"900","author":{"username":"ben","global_name":null},"content":"Start","timestamp":"2026-09-01T10:00:00+00:00"}]""");
        _http.On("PATCH", "channels/900", 200, "{}");

        var messages = await Client().GetMessagesAsync("900", default);
        await Client().ModifyThreadAsync("900", archived: false, new[] { "1", "9" }, default);
        await Client().ModifyThreadAsync("900", archived: true, null, default);

        Assert.Equal(("Anna", "auch"), (messages[0].Author, messages[0].Content));
        Assert.Equal("ben", messages[1].Author);
        Assert.Equal("""{"archived":false,"applied_tags":["1","9"]}""", _http.Requests[^2].Body);
        Assert.Equal("""{"archived":true}""", _http.Requests[^1].Body);
    }

    [Fact]
    public async Task Netzfehler_WirdStatusNull()
    {
        var client = new DiscordClient(new HttpClient(new ThrowingHandler()), "t");

        var error = await Assert.ThrowsAsync<DiscordException>(() => client.GetGuildsAsync(default));

        Assert.Equal(0, error.StatusCode);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Host nicht gefunden");
    }

    [Fact]
    public async Task Nachricht_LiestAnhaenge()
    {
        _http.On("GET", "channels/900/messages/900", 200,
            """{"id":"900","author":{"username":"anna"},"content":"Bild anbei","timestamp":"2026-09-01T10:00:00+00:00","attachments":[{"id":"1","filename":"shot.png","url":"https://cdn.discordapp.com/attachments/9/1/shot.png?ex=1","content_type":"image/png"},{"id":"2","filename":"error.log","url":"https://cdn.discordapp.com/attachments/9/2/error.log","content_type":"text/plain; charset=utf-8"}]}""");

        var message = await Client().GetMessageAsync("900", "900", default);

        Assert.Equal(new[] { "shot.png", "error.log" }, message!.Attachments.Select(a => a.FileName));
        Assert.True(message.Attachments[0].IsImage);
        Assert.False(message.Attachments[1].IsImage);
        Assert.Equal("https://cdn.discordapp.com/attachments/9/1/shot.png?ex=1", message.Attachments[0].Url);
    }
}
