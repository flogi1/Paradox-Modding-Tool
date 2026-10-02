using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ParadoxModTool.Notes;

namespace ParadoxModTool.BugTracking;

/// <summary>Fehler der Discord-API. <see cref="StatusCode"/> 0 = keine Antwort (Netz, Zeitlimit). Die Meldung enthält nie den Token.</summary>
public sealed class DiscordException : Exception
{
    public DiscordException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

public sealed record DiscordUser(string Id, string Name);

public sealed record DiscordApplication(string Id, long Flags);

public sealed record DiscordGuild(string Id, string Name);

public sealed record DiscordTag(string Id, string Name);

/// <param name="Type">0 = Rolle, 1 = Mitglied.</param>
public sealed record DiscordOverwrite(string Id, int Type, ulong Allow, ulong Deny);

public sealed record DiscordRole(string Id, ulong Permissions);

/// <summary>Channel oder Thread. <see cref="Tags"/> = verfügbare Tags eines Forums, <see cref="AppliedTags"/> = gesetzte Tags eines Threads.</summary>
public sealed record DiscordChannel(string Id, string GuildId, string ParentId, string Name, int Type, IReadOnlyList<DiscordTag> Tags, IReadOnlyList<string> AppliedTags, IReadOnlyList<DiscordOverwrite> Overwrites, bool Archived, string ArchiveTimestamp, string LastMessageId);

public sealed record DiscordMessage(string Id, string Author, string Content, DateTime TimestampUtc, IReadOnlyList<BugAttachment> Attachments);

/// <summary>
/// Dünne Schicht über die Discord-REST-API v10 mit Bot-Token. 429 wartet <c>retry_after</c> ab (höchstens 5 Versuche); alle Fehler kommen als
/// <see cref="DiscordException"/>. <c>delay</c> ist im Test austauschbar.
/// </summary>
public sealed class DiscordClient
{
    public const string ApiBase = "https://discord.com/api/v10/";

    public const int ForumChannelType = 15;

    private const int MaxAttempts = 5;

    private readonly HttpClient _http;
    private readonly string _token;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public DiscordClient(HttpClient http, string token, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _http = http;
        _token = token;
        _delay = delay ?? ((wait, cancellation) => Task.Delay(wait, cancellation));
    }

    public async Task<DiscordUser> GetCurrentUserAsync(CancellationToken cancellation)
    {
        using var document = await SendAsync(HttpMethod.Get, "users/@me", null, cancellation);
        return new DiscordUser(Text(document.RootElement, "id"), Text(document.RootElement, "username"));
    }

    public async Task<DiscordApplication> GetApplicationAsync(CancellationToken cancellation)
    {
        using var document = await SendAsync(HttpMethod.Get, "applications/@me", null, cancellation);
        var root = document.RootElement;
        return new DiscordApplication(Text(root, "id"), root.TryGetProperty("flags", out var flags) && flags.ValueKind == JsonValueKind.Number ? flags.GetInt64() : 0);
    }

    public async Task<IReadOnlyList<DiscordGuild>> GetGuildsAsync(CancellationToken cancellation)
    {
        using var document = await SendAsync(HttpMethod.Get, "users/@me/guilds?limit=200", null, cancellation);
        return Items(document.RootElement).Select(e => new DiscordGuild(Text(e, "id"), Text(e, "name"))).ToList();
    }

    public async Task<IReadOnlyList<DiscordChannel>> GetGuildChannelsAsync(string guildId, CancellationToken cancellation)
    {
        using var document = await SendAsync(HttpMethod.Get, $"guilds/{guildId}/channels", null, cancellation);
        return Items(document.RootElement).Select(Channel).ToList();
    }

    public async Task<DiscordChannel> GetChannelAsync(string channelId, CancellationToken cancellation)
    {
        using var document = await SendAsync(HttpMethod.Get, $"channels/{channelId}", null, cancellation);
        return Channel(document.RootElement);
    }

    public async Task<IReadOnlyList<DiscordRole>> GetRolesAsync(string guildId, CancellationToken cancellation)
    {
        using var document = await SendAsync(HttpMethod.Get, $"guilds/{guildId}/roles", null, cancellation);
        return Items(document.RootElement).Select(e => new DiscordRole(Text(e, "id"), Bits(Text(e, "permissions")))).ToList();
    }

    public async Task<IReadOnlyList<string>> GetMemberRolesAsync(string guildId, string userId, CancellationToken cancellation)
    {
        using var document = await SendAsync(HttpMethod.Get, $"guilds/{guildId}/members/{userId}", null, cancellation);
        return Items(document.RootElement, "roles").Select(e => e.GetString() ?? string.Empty).Where(id => id.Length > 0).ToList();
    }

    /// <summary>Alle aktiven Threads des Servers (der Aufrufer filtert nach Channel).</summary>
    public async Task<IReadOnlyList<DiscordChannel>> GetActiveThreadsAsync(string guildId, CancellationToken cancellation)
    {
        using var document = await SendAsync(HttpMethod.Get, $"guilds/{guildId}/threads/active", null, cancellation);
        return Items(document.RootElement, "threads").Select(Channel).ToList();
    }

    /// <summary>Alle öffentlichen archivierten Threads eines Channels, seitenweise über <c>has_more</c> und <c>before</c>.</summary>
    public async Task<IReadOnlyList<DiscordChannel>> GetArchivedThreadsAsync(string channelId, CancellationToken cancellation)
    {
        var all = new List<DiscordChannel>();
        string? before = null;
        while (true)
        {
            var path = $"channels/{channelId}/threads/archived/public?limit=100" + (before is null ? string.Empty : "&before=" + Uri.EscapeDataString(before));
            using var document = await SendAsync(HttpMethod.Get, path, null, cancellation);
            var page = Items(document.RootElement, "threads").Select(Channel).ToList();
            all.AddRange(page);
            var more = document.RootElement.TryGetProperty("has_more", out var hasMore) && hasMore.ValueKind == JsonValueKind.True;
            var next = page.Count > 0 ? page[^1].ArchiveTimestamp : string.Empty;
            if (!more || next.Length == 0 || next == before)
            {
                return all;
            }

            before = next;
        }
    }

    /// <summary>Eine Nachricht; <see langword="null"/>, wenn es sie nicht (mehr) gibt.</summary>
    public async Task<DiscordMessage?> GetMessageAsync(string channelId, string messageId, CancellationToken cancellation)
    {
        try
        {
            using var document = await SendAsync(HttpMethod.Get, $"channels/{channelId}/messages/{messageId}", null, cancellation);
            return Message(document.RootElement);
        }
        catch (DiscordException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    /// <summary>Die letzten 100 Nachrichten eines Channels bzw. Threads (neueste zuerst, wie Discord sie liefert).</summary>
    public async Task<IReadOnlyList<DiscordMessage>> GetMessagesAsync(string channelId, CancellationToken cancellation)
    {
        using var document = await SendAsync(HttpMethod.Get, $"channels/{channelId}/messages?limit=100", null, cancellation);
        return Items(document.RootElement).Select(Message).ToList();
    }

    /// <summary>Setzt <c>archived</c> und (falls angegeben) <c>applied_tags</c> eines Threads in einer Anfrage.</summary>
    public async Task ModifyThreadAsync(string threadId, bool archived, IReadOnlyList<string>? appliedTags, CancellationToken cancellation)
    {
        var body = new Dictionary<string, object> { ["archived"] = archived };
        if (appliedTags is not null)
        {
            body["applied_tags"] = appliedTags;
        }

        using var _ = await SendAsync(HttpMethod.Patch, $"channels/{threadId}", JsonSerializer.Serialize(body), cancellation);
    }

    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, string? json, CancellationToken cancellation)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, ApiBase + path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bot", _token);
            request.Headers.TryAddWithoutValidation("User-Agent", "DiscordBot (https://github.com/flogi1/Paradox-Modding-Tool, 1.0)");
            if (json is not null)
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellation);
            }
            catch (HttpRequestException ex)
            {
                throw new DiscordException(0, ex.Message);
            }
            catch (TaskCanceledException ex) when (!cancellation.IsCancellationRequested)
            {
                throw new DiscordException(0, ex.Message);
            }

            using (response)
            {
                var text = await response.Content.ReadAsStringAsync(cancellation);
                var status = (int)response.StatusCode;
                if (status == 429 && attempt < MaxAttempts)
                {
                    await _delay(RetryAfter(text, response), cancellation);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new DiscordException(status, ErrorMessage(text, status));
                }

                try
                {
                    return JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
                }
                catch (JsonException)
                {
                    throw new DiscordException(status, DiscordTexts.ResponseUnreadable);
                }
            }
        }
    }

    private static TimeSpan RetryAfter(string text, HttpResponseMessage response)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.TryGetProperty("retry_after", out var seconds) && seconds.ValueKind == JsonValueKind.Number)
            {
                return TimeSpan.FromSeconds(Math.Clamp(seconds.GetDouble(), 0.1, 60));
            }
        }
        catch (JsonException)
        {
            // Kein JSON: auf den Header ausweichen.
        }

        return response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(1);
    }

    private static string ErrorMessage(string text, int status)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind == JsonValueKind.Object && Text(document.RootElement, "message") is { Length: > 0 } message)
            {
                return message;
            }
        }
        catch (JsonException)
        {
            // Kein JSON: nur der Status.
        }

        return "HTTP " + status.ToString(CultureInfo.InvariantCulture);
    }

    private static DiscordChannel Channel(JsonElement e)
    {
        var meta = e.TryGetProperty("thread_metadata", out var m) && m.ValueKind == JsonValueKind.Object ? m : default;
        var archived = meta.ValueKind == JsonValueKind.Object && meta.TryGetProperty("archived", out var a) && a.ValueKind == JsonValueKind.True;
        return new DiscordChannel(
            Text(e, "id"),
            Text(e, "guild_id"),
            Text(e, "parent_id"),
            Text(e, "name"),
            e.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.Number ? type.GetInt32() : -1,
            Items(e, "available_tags").Select(t => new DiscordTag(Text(t, "id"), Text(t, "name"))).ToList(),
            Items(e, "applied_tags").Select(t => t.GetString() ?? string.Empty).Where(id => id.Length > 0).ToList(),
            Items(e, "permission_overwrites").Select(o => new DiscordOverwrite(
                Text(o, "id"),
                o.TryGetProperty("type", out var kind) && kind.ValueKind == JsonValueKind.Number ? kind.GetInt32() : 0,
                Bits(Text(o, "allow")),
                Bits(Text(o, "deny")))).ToList(),
            archived,
            meta.ValueKind == JsonValueKind.Object ? Text(meta, "archive_timestamp") : string.Empty,
            Text(e, "last_message_id"));
    }

    private static DiscordMessage Message(JsonElement e)
    {
        var author = e.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.Object ? a : default;
        var name = author.ValueKind == JsonValueKind.Object ? (Text(author, "global_name") is { Length: > 0 } global ? global : Text(author, "username")) : string.Empty;
        var timestamp = DateTimeOffset.TryParse(Text(e, "timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at.UtcDateTime : DateTime.MinValue;
        var attachments = Items(e, "attachments")
            .Select(a => new BugAttachment(Text(a, "filename"), Text(a, "url"), Text(a, "content_type")))
            .Where(a => a.Url.Length > 0)
            .ToList();
        return new DiscordMessage(Text(e, "id"), name, Text(e, "content"), timestamp, attachments);
    }

    private static string Text(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static List<JsonElement> Items(JsonElement e, string? name = null)
    {
        var array = name is null ? e : e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var value) ? value : default;
        return array.ValueKind == JsonValueKind.Array ? array.EnumerateArray().ToList() : new List<JsonElement>();
    }

    private static ulong Bits(string text) => ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var bits) ? bits : 0;
}
