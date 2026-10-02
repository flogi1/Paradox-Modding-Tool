using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Notes;

/// <summary>
/// Ein Anhang einer Meldung oder Antwort (Screenshot, Logdatei). Gehört dem Dienst: jeder Abgleich liefert frische Adressen, weil Discord-Links
/// nach etwa einem Tag ablaufen. Gespeichert wird nur die Adresse, nie die Datei.
/// </summary>
public sealed record BugAttachment(string FileName, string Url, string ContentType)
{
    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp" };

    private static readonly string[] PreviewHosts = { "cdn.discordapp.com", "media.discordapp.net", "github.com" };

    [JsonIgnore]
    public bool IsImage =>
        ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
        || ImageExtensions.Any(extension => FileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Darf PMT das Bild selbst laden? Nur https-Adressen von Discord und GitHub; alles andere (z. B. Zählpixel in einem Issue-Text) erscheint
    /// nur als Link.
    /// </summary>
    public static bool CanPreview(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && (PreviewHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase) || uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase));

    /// <summary>Dieselbe Datei, auch wenn die Adresse neu signiert ist (Discord ändert bei jedem Abruf die Parameter nach dem <c>?</c>).</summary>
    public bool SameFile(BugAttachment other) =>
        FileName == other.FileName && ContentType == other.ContentType && string.Equals(WithoutQuery(Url), WithoutQuery(other.Url), StringComparison.Ordinal);

    private static string WithoutQuery(string url) => url.Split('?')[0];
}

/// <summary>Bildlinks aus Markdown- oder HTML-Text (GitHub-Issues): <c>![alt](url)</c> und <c>&lt;img src="url"&gt;</c>, nur https, ohne Doppelte.</summary>
public static partial class BugImageLinks
{
    [GeneratedRegex(@"!\[(?<alt>[^\]]*)\]\((?<url>https://[^\s)]+)(?:\s+""[^""]*"")?\)")]
    private static partial Regex MarkdownImage();

    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlImage();

    [GeneratedRegex(@"\bsrc\s*=\s*[""'](?<url>https://[^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlSource();

    [GeneratedRegex(@"\balt\s*=\s*[""'](?<alt>[^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlAlt();

    public static IReadOnlyList<BugAttachment> Extract(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<BugAttachment>();
        }

        var found = new List<(int Index, string Url, string Alt)>();
        foreach (Match match in MarkdownImage().Matches(text))
        {
            found.Add((match.Index, match.Groups["url"].Value, match.Groups["alt"].Value));
        }

        foreach (Match tag in HtmlImage().Matches(text))
        {
            if (HtmlSource().Match(tag.Value) is { Success: true } source)
            {
                found.Add((tag.Index, source.Groups["url"].Value, HtmlAlt().Match(tag.Value).Groups["alt"].Value));
            }
        }

        return found
            .OrderBy(item => item.Index)
            .GroupBy(item => item.Url, StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(item => new BugAttachment(item.Alt.Trim().Length > 0 ? item.Alt.Trim() : FileNameOf(item.Url), item.Url, "image/*"))
            .ToList();
    }

    private static string FileNameOf(string url)
    {
        var path = url.Split('?')[0].TrimEnd('/');
        var slash = path.LastIndexOf('/');
        return slash >= 0 ? path[(slash + 1)..] : path;
    }
}
