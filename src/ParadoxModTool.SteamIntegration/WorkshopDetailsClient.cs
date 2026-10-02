using System.Text.Json;

namespace ParadoxModTool.SteamIntegration;

/// <summary>Zustand einer Workshop-Seite aus Sicht der öffentlichen Steam-Schnittstelle.</summary>
public enum WorkshopPageState
{
    Ok,

    /// <summary>Gefunden, aber nicht öffentlich (<c>visibility</c> ungleich 0).</summary>
    Hidden,

    /// <summary><c>result</c> ungleich 1: gelöscht, privat oder falsche ID.</summary>
    NotFound,
}

/// <summary>Öffentliche Daten einer Workshop-Seite; fehlende Werte sind <see langword="null"/>.</summary>
public sealed record WorkshopDetails(
    string Id,
    WorkshopPageState State,
    string? Title,
    string? PreviewUrl,
    long? Subscriptions,
    long? Favorited,
    long? Views,
    DateTimeOffset? TimeUpdated);

/// <summary>
/// Holt Abonnenten, Favoriten, Aufrufe und Vorschaubild über <c>ISteamRemoteStorage/GetPublishedFileDetails</c>. Die Schnittstelle ist öffentlich
/// und braucht keinen API-Schlüssel; ein Aufruf deckt alle IDs ab.
/// </summary>
public static class WorkshopDetailsClient
{
    public const string Endpoint = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";

    public static FormUrlEncodedContent BuildRequest(IReadOnlyList<string> ids)
    {
        var fields = new List<KeyValuePair<string, string>> { new("itemcount", ids.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)) };
        fields.AddRange(ids.Select((id, i) => new KeyValuePair<string, string>($"publishedfileids[{i}]", id)));
        return new FormUrlEncodedContent(fields);
    }

    /// <summary>Liest die Antwort; kaputtes JSON oder eine Antwort ohne Einträge ergibt eine leere Liste. Zahlen dürfen auch als Text kommen.</summary>
    public static IReadOnlyList<WorkshopDetails> Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("response", out var response) || response.ValueKind != JsonValueKind.Object
                || !response.TryGetProperty("publishedfiledetails", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<WorkshopDetails>();
            }

            var result = new List<WorkshopDetails>();
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || Text(item, "publishedfileid") is not { Length: > 0 } id)
                {
                    continue;
                }

                var state = Number(item, "result") != 1 ? WorkshopPageState.NotFound
                    : (Number(item, "visibility") ?? 0) != 0 ? WorkshopPageState.Hidden
                    : WorkshopPageState.Ok;
                if (state == WorkshopPageState.NotFound)
                {
                    result.Add(new WorkshopDetails(id, state, null, null, null, null, null, null));
                    continue;
                }

                result.Add(new WorkshopDetails(
                    id,
                    state,
                    Text(item, "title"),
                    Text(item, "preview_url") is { Length: > 0 } preview ? preview : null,
                    Number(item, "subscriptions"),
                    Number(item, "favorited"),
                    Number(item, "views"),
                    Number(item, "time_updated") is { } seconds ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null));
            }

            return result;
        }
        catch (JsonException)
        {
            return Array.Empty<WorkshopDetails>();
        }
    }

    /// <summary>Ein Aufruf für alle <paramref name="ids"/>; HTTP-Fehler werfen <see cref="HttpRequestException"/>.</summary>
    public static async Task<IReadOnlyList<WorkshopDetails>> FetchAsync(HttpClient http, IReadOnlyList<string> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<WorkshopDetails>();
        }

        using var response = await http.PostAsync(Endpoint, BuildRequest(ids), ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
    }

    private static string? Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) ? value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        } : null;

    private static long? Number(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }
}
