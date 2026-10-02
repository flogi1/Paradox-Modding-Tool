using System.Net;
using ParadoxModTool.SteamIntegration;
using Xunit;

namespace ParadoxModTool.ModOverview.Tests;

public class OverviewRefresherTests : TempDirTests
{
    private static readonly DateTime Now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Beantwortet Anfragen aus einer Tabelle (URL-Anfang → Antwort) und merkt sich die Anfragen samt Rumpf.</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        public Dictionary<string, Func<HttpResponseMessage>> Responses { get; } = new();

        public List<(string Url, string Body)> Requests { get; } = new();

        public bool Offline { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.RequestUri!.ToString(), body));
            if (Offline)
            {
                throw new HttpRequestException("offline");
            }

            var match = Responses.FirstOrDefault(r => request.RequestUri!.ToString().StartsWith(r.Key, StringComparison.Ordinal));
            return match.Value?.Invoke() ?? new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private static Func<HttpResponseMessage> Details(long timeUpdated) => () => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(
            "{\"response\":{\"publishedfiledetails\":[{\"publishedfileid\":\"111\",\"result\":1,\"visibility\":0,\"subscriptions\":5,"
            + $"\"time_updated\":{timeUpdated},\"preview_url\":\"https://img.test/ugc/1/\"}}]}}}}"),
    };

    private static Func<HttpResponseMessage> Image() => () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1, 2, 3 }) };

    private OverviewPaths Paths => new(Dir);

    [Fact]
    public async Task NoIds_NoRequest()
    {
        var handler = new FakeHandler();

        var result = await OverviewRefresher.RefreshSteamAsync(new HttpClient(handler), Paths, new[] { "", " " }, "A", Now, CancellationToken.None);

        Assert.Equal(new SteamRefreshResult(true, 0, 0), result);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Offline_ReturnsNotReached_CacheUntouched()
    {
        var handler = new FakeHandler { Responses = { [WorkshopDetailsClient.Endpoint] = Details(1000) } };
        await OverviewRefresher.RefreshSteamAsync(new HttpClient(handler), Paths, new[] { "111" }, "A", Now, CancellationToken.None);
        var before = File.ReadAllText(Paths.SteamFile);
        handler.Offline = true;

        var result = await OverviewRefresher.RefreshSteamAsync(new HttpClient(handler), Paths, new[] { "111" }, "B", Now, CancellationToken.None);

        Assert.Equal(new SteamRefreshResult(false, 0, 0), result);
        Assert.Equal(before, File.ReadAllText(Paths.SteamFile));
    }

    [Fact]
    public async Task Success_AppliesCacheAndDownloadsNewThumbnails()
    {
        var handler = new FakeHandler { Responses = { [WorkshopDetailsClient.Endpoint] = Details(1000), ["https://img.test/"] = Image() } };
        var http = new HttpClient(handler);

        var first = await OverviewRefresher.RefreshSteamAsync(http, Paths, new[] { "111" }, "A", Now, CancellationToken.None);
        var second = await OverviewRefresher.RefreshSteamAsync(http, Paths, new[] { "111" }, "A", Now, CancellationToken.None);
        handler.Responses[WorkshopDetailsClient.Endpoint] = Details(2000);
        var third = await OverviewRefresher.RefreshSteamAsync(http, Paths, new[] { "111" }, "A", Now, CancellationToken.None);

        Assert.Equal(new SteamRefreshResult(true, 1, 1), first);
        Assert.Equal(new SteamRefreshResult(true, 1, 0), second);
        Assert.Equal(new SteamRefreshResult(true, 1, 1), third);
        Assert.Equal(5, SteamCacheStore.Load(Paths.SteamFile)["111"].Subscriptions);
        Assert.True(File.Exists(Path.Combine(Paths.ThumbsDirectory, "111.jpg")));
    }

    [Fact]
    public async Task DuplicateIds_OneEntryInRequest()
    {
        var handler = new FakeHandler { Responses = { [WorkshopDetailsClient.Endpoint] = Details(1000), ["https://img.test/"] = Image() } };

        await OverviewRefresher.RefreshSteamAsync(new HttpClient(handler), Paths, new[] { "111", "111" }, "A", Now, CancellationToken.None);

        Assert.Contains("itemcount=1", handler.Requests[0].Body);
    }

    [Fact]
    public async Task FailedImageIsSkipped()
    {
        var handler = new FakeHandler { Responses = { [WorkshopDetailsClient.Endpoint] = Details(1000) } };

        var result = await OverviewRefresher.RefreshSteamAsync(new HttpClient(handler), Paths, new[] { "111" }, "A", Now, CancellationToken.None);

        Assert.Equal(new SteamRefreshResult(true, 1, 0), result);
    }
}
