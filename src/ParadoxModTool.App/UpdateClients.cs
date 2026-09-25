using System.Net.Http;
using ParadoxModTool.Updates;

namespace ParadoxModTool.App;

/// <summary>
/// HttpClients für Update-Prüfung (kurzes Timeout) und Download (langes Timeout, das Paket hat rund 70 MB). In Debug-Builds leitet die
/// Umgebungsvariable <c>PMT_UPDATE_FEED</c> beide auf einen lokalen Feed-Ordner um (siehe <see cref="LocalFeedHandler"/>).
/// </summary>
internal static class UpdateClients
{
    public static HttpClient Check { get; } = Create(TimeSpan.FromSeconds(10));

    public static HttpClient Download { get; } = Create(TimeSpan.FromMinutes(30));

    private static HttpClient Create(TimeSpan timeout)
    {
#if DEBUG
        if (Environment.GetEnvironmentVariable("PMT_UPDATE_FEED") is { Length: > 0 } feed)
        {
            return new HttpClient(new LocalFeedHandler(feed)) { Timeout = timeout };
        }
#endif
        return new HttpClient { Timeout = timeout };
    }
}
