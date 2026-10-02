using ParadoxModTool.Mods;

namespace ParadoxModTool.ModOverview;

/// <summary>
/// Vorschaubild einer Mod: das lokale Bild aus der Metadaten-Datei (<c>picture</c>) hat Vorrang, sonst das zwischengespeicherte Steam-Bild
/// unter <c>overview\thumbs\&lt;WorkshopID&gt;.&lt;Endung&gt;</c>.
/// </summary>
public static class ThumbnailCache
{
    private static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    /// <summary>Voller Pfad des lokalen Bildes, wenn es gesetzt ist und existiert.</summary>
    public static string? LocalPicture(ModMetadata? metadata)
    {
        if (metadata is null || string.IsNullOrWhiteSpace(metadata.Picture))
        {
            return null;
        }

        try
        {
            var path = Path.GetFullPath(Path.Combine(metadata.Folder, metadata.Picture));
            return File.Exists(path) ? path : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>Zieldatei für das Steam-Bild; die Endung kommt aus der Adresse, sonst <c>.jpg</c> (Steam-Bildadressen haben meist keine).</summary>
    public static string CachedFile(string thumbsDir, string workshopId, string previewUrl)
    {
        var extension = Uri.TryCreate(previewUrl, UriKind.Absolute, out var uri) ? Path.GetExtension(uri.AbsolutePath).ToLowerInvariant() : string.Empty;
        return Path.Combine(thumbsDir, workshopId + (Extensions.Contains(extension) ? extension : ".jpg"));
    }

    /// <summary>Neu laden, wenn die Datei fehlt oder Steam die Seite seit dem letzten Abruf geändert hat.</summary>
    public static bool NeedsDownload(string cachedFile, SteamCacheEntry entry, DateTimeOffset? previousTimeUpdated) =>
        !File.Exists(cachedFile) || entry.TimeUpdated != previousTimeUpdated;

    public static string? Resolve(ModMetadata? metadata, string thumbsDir, SteamCacheEntry? entry)
    {
        if (LocalPicture(metadata) is { } local)
        {
            return local;
        }

        if (entry?.PreviewUrl is not { Length: > 0 } url)
        {
            return null;
        }

        var cached = CachedFile(thumbsDir, entry.Id, url);
        return File.Exists(cached) ? cached : null;
    }

    /// <summary>Lädt in eine Nachbardatei und ersetzt dann das Ziel, damit nie ein halbes Bild liegen bleibt.</summary>
    public static async Task DownloadAsync(HttpClient http, string url, string targetFile, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
        var temp = targetFile + ".tmp";
        var bytes = await http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
        await File.WriteAllBytesAsync(temp, bytes, ct).ConfigureAwait(false);
        File.Move(temp, targetFile, overwrite: true);
    }
}
