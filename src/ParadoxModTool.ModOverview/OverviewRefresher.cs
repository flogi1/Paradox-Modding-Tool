using ParadoxModTool.SteamIntegration;

namespace ParadoxModTool.ModOverview;

/// <summary>Ergebnis eines Steam-Abrufs: <see cref="Reached"/> ist falsch, wenn Steam nicht erreichbar war (der Zwischenspeicher bleibt dann unverändert).</summary>
public sealed record SteamRefreshResult(bool Reached, int Updated, int ThumbnailsDownloaded);

/// <summary>Holt die Steam-Werte aller Workshop-Seiten in einem Aufruf, schreibt sie in <c>steam.json</c> und lädt neue oder geänderte Vorschaubilder.</summary>
public static class OverviewRefresher
{
    public static async Task<SteamRefreshResult> RefreshSteamAsync(
        HttpClient http,
        OverviewPaths paths,
        IEnumerable<string> workshopIds,
        string session,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var ids = workshopIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0)
        {
            return new SteamRefreshResult(true, 0, 0);
        }

        IReadOnlyList<WorkshopDetails> fetched;
        try
        {
            fetched = await WorkshopDetailsClient.FetchAsync(http, ids, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            return new SteamRefreshResult(false, 0, 0);
        }

        var before = SteamCacheStore.Load(paths.SteamFile);
        var after = SteamCacheStore.Apply(paths.SteamFile, fetched, session, nowUtc);
        var downloaded = 0;
        foreach (var details in fetched)
        {
            if (details.PreviewUrl is not { Length: > 0 } url || !after.TryGetValue(details.Id, out var entry))
            {
                continue;
            }

            var file = ThumbnailCache.CachedFile(paths.ThumbsDirectory, details.Id, url);
            var previous = before.TryGetValue(details.Id, out var old) ? old.TimeUpdated : null;
            if (!ThumbnailCache.NeedsDownload(file, entry, previous))
            {
                continue;
            }

            try
            {
                await ThumbnailCache.DownloadAsync(http, url, file, ct).ConfigureAwait(false);
                downloaded++;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                // Ein Bild ist nur Beiwerk; ohne es zeigt die Kachel den Platzhalter.
            }
        }

        return new SteamRefreshResult(true, fetched.Count, downloaded);
    }
}
