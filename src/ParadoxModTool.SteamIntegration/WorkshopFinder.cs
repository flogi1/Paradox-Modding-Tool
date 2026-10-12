using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ParadoxModTool.Mods;

namespace ParadoxModTool.SteamIntegration;

/// <summary>Eine gefundene Workshop-Seite: ID, Titel und Herkunft (für die Anzeige in der Auswahl).</summary>
public sealed record WorkshopMatch(string Id, string Title, string Source);

/// <summary>Vergleicht Titel: ohne Groß-/Kleinschreibung, mit zusammengefassten Leerzeichen.</summary>
public static class WorkshopMatcher
{
    public static string Normalize(string text) => Regex.Replace(text.Trim(), @"\s+", " ").ToLowerInvariant();

    /// <summary>Die Einträge, deren Titel dem Namen genau entspricht (nach <see cref="Normalize"/>).</summary>
    public static IReadOnlyList<WorkshopMatch> Exact(IEnumerable<WorkshopMatch> candidates, string name)
    {
        var wanted = Normalize(name);
        return wanted.Length == 0 ? Array.Empty<WorkshopMatch>() : candidates.Where(c => Normalize(c.Title) == wanted).ToList();
    }
}

/// <summary>Zieht die Workshop-ID aus einer Eingabe: eine Zahl oder eine Workshop-Adresse (<c>…filedetails/?id=123</c>).</summary>
public static class WorkshopIdText
{
    private static readonly Regex FileDetails = new(@"filedetails/?\?(?:[^#\s]*&)?id=(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Die Zahl, wenn die Eingabe eine Zahl oder eine Workshop-Adresse ist; sonst <see langword="null"/>.</summary>
    public static string? Extract(string? input)
    {
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.All(char.IsAsciiDigit))
        {
            return text;
        }

        var match = FileDetails.Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }
}

/// <summary>
/// Sucht die Workshop-ID einer Mod lokal: Steam legt abonnierte Workshop-Mods unter <c>steamapps\workshop\content\&lt;appid&gt;\&lt;id&gt;\</c> ab, der Ordnername ist die ID. Ein Eintrag passt,
/// wenn der Name in seiner Metadaten-Datei dem Namen der Mod entspricht oder seine <c>id</c> der Mod-ID (beim klassischen Format nur der Name, siehe <see cref="ReadClassic"/>).
/// </summary>
public static class LocalWorkshopScanner
{
    public static IReadOnlyList<WorkshopMatch> Find(IEnumerable<string> libraryFolders, string appId, string metadataFile, string modName, string? modId)
    {
        var matches = new List<WorkshopMatch>();
        var wantedName = WorkshopMatcher.Normalize(modName);
        foreach (var library in libraryFolders)
        {
            var content = Path.Combine(library, "steamapps", "workshop", "content", appId);
            if (!Directory.Exists(content))
            {
                continue;
            }

            foreach (var folder in Directory.EnumerateDirectories(content))
            {
                var id = Path.GetFileName(folder);
                if (id.Length == 0 || !id.All(char.IsAsciiDigit) || matches.Any(m => m.Id == id))
                {
                    continue;
                }

                var (name, metadataId) = ModMetadata.IsClassic(metadataFile)
                    ? ReadClassic(folder)
                    : ReadMetadata(Path.Combine(folder, metadataFile.Replace('/', Path.DirectorySeparatorChar)));
                var byName = wantedName.Length > 0 && name is not null && WorkshopMatcher.Normalize(name) == wantedName;
                var byId = !string.IsNullOrWhiteSpace(modId) && string.Equals(metadataId, modId.Trim(), StringComparison.OrdinalIgnoreCase);
                if (byName || byId)
                {
                    matches.Add(new WorkshopMatch(id, string.IsNullOrWhiteSpace(name) ? id : name, SteamTexts.SourceSubscribed));
                }
            }
        }

        return matches;
    }

    /// <summary>
    /// Klassisches Format (EU IV, CK III): im Workshop-Ordner liegt keine Geschwisterdatei, sondern die <see cref="ModMetadata.ClassicWorkshopDescriptorFile"/> im Ordner selbst.
    /// Sie kennt keine Mod-ID; es zählt nur der Name.
    /// </summary>
    private static (string? Name, string? Id) ReadClassic(string folder) =>
        (ModMetadata.ReadClassicFile(folder, ModMetadata.ClassicWorkshopDescriptorFile)?.Name, null);

    private static (string? Name, string? Id) ReadMetadata(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return (null, null);
            }

            using var document = JsonDocument.Parse(
                File.ReadAllText(path),
                new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (null, null);
            }

            string? name = null;
            string? id = null;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                if (string.Equals(property.Name, "name", StringComparison.OrdinalIgnoreCase))
                {
                    name = property.Value.GetString();
                }
                else if (string.Equals(property.Name, "id", StringComparison.OrdinalIgnoreCase))
                {
                    id = property.Value.GetString();
                }
            }

            return (name, id);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return (null, null);
        }
    }
}

/// <summary>Liest die öffentliche Workshop-Seite eines Steam-Profils (<c>myworkshopfiles</c>) und findet darin die hochgeladenen Items.</summary>
public static class WorkshopProfilePage
{
    private static readonly Regex BindMouseHover = new(
        @"SharedFileBindMouseHover\(\s*""sharedfile_(\d+)""\s*,\s*\w+\s*,\s*(\{.*?\})\s*\)\s*;",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex IdThenTitle = new(
        @"data-publishedfileid=""(\d+)""(?:(?!data-publishedfileid=).)*?class=""workshopItemTitle[^""]*""[^>]*>([^<]*)<",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public static string Url(string steamId64, string appId, int page) =>
        $"https://steamcommunity.com/profiles/{steamId64}/myworkshopfiles/?appid={appId}&p={page}";

    /// <summary>Die Items der Seite (ID und Titel), ohne Doppelte. Sucht zuerst die Daten-Blöcke der Seite, sonst die HTML-Elemente.</summary>
    public static IReadOnlyList<WorkshopMatch> Parse(string html)
    {
        var items = new List<WorkshopMatch>();

        void Add(string id, string title)
        {
            if (items.All(i => i.Id != id))
            {
                items.Add(new WorkshopMatch(id, WebUtility.HtmlDecode(title).Trim(), SteamTexts.SourceProfile));
            }
        }

        foreach (Match match in BindMouseHover.Matches(html))
        {
            try
            {
                using var document = JsonDocument.Parse(match.Groups[2].Value);
                if (document.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                {
                    Add(match.Groups[1].Value, title.GetString() ?? string.Empty);
                }
            }
            catch (JsonException)
            {
                // Ein unlesbarer Block wird übersprungen.
            }
        }

        if (items.Count == 0)
        {
            foreach (Match match in IdThenTitle.Matches(html))
            {
                Add(match.Groups[1].Value, match.Groups[2].Value);
            }
        }

        return items;
    }
}

/// <summary>Holt die Workshop-Seiten eines Profils Seite für Seite. Den Abruf selbst macht ein übergebener Delegat (die App mit <c>HttpClient</c>, Tests mit Fake-Seiten).</summary>
public static class WorkshopProfileClient
{
    /// <summary>Ruft die Seiten 1 bis <paramref name="maxPages"/> ab und hört auf, sobald eine Seite nichts Neues bringt. Fehler des Abrufs werden durchgereicht.</summary>
    public static async Task<IReadOnlyList<WorkshopMatch>> ListAsync(
        string steamId64,
        string appId,
        Func<string, CancellationToken, Task<string>> fetch,
        CancellationToken cancellation,
        int maxPages = 5)
    {
        if (string.IsNullOrEmpty(steamId64) || !steamId64.All(char.IsAsciiDigit))
        {
            throw new ArgumentException(SteamTexts.SteamIdNotNumber, nameof(steamId64));
        }

        if (string.IsNullOrEmpty(appId) || !appId.All(char.IsAsciiDigit))
        {
            throw new ArgumentException(SteamTexts.AppIdNotNumber, nameof(appId));
        }

        var all = new List<WorkshopMatch>();
        for (var page = 1; page <= maxPages; page++)
        {
            var html = await fetch(WorkshopProfilePage.Url(steamId64, appId, page), cancellation);
            var fresh = WorkshopProfilePage.Parse(html).Where(item => all.All(known => known.Id != item.Id)).ToList();
            if (fresh.Count == 0)
            {
                break;
            }

            all.AddRange(fresh);
        }

        return all;
    }
}
