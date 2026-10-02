using System.Globalization;
using ParadoxModTool.Mods;
using ParadoxModTool.Notes;

namespace ParadoxModTool.BugTracking;

/// <summary>
/// Beiträge eines Discord-Forum-Channels als Meldungen. <c>client</c> liefert den Client mit dem gespeicherten Token oder <see langword="null"/>,
/// wenn kein Token eingerichtet ist (wird bei jedem Aufruf neu gefragt). Status nur über die drei zugeordneten Tags.
/// </summary>
public sealed class DiscordBugSource : IBugSource
{
    private const int MaxTags = 5;

    private readonly Func<DiscordClient?> _client;

    public DiscordBugSource(Func<DiscordClient?> client) => _client = client;

    public string Kind => BugSourceKinds.Discord;

    public BugSource Source => BugSource.Discord;

    public static (string GuildId, string ChannelId) SplitKey(string key)
    {
        var parts = key.Split('/');
        return parts.Length == 2 ? (parts[0], parts[1]) : throw new BugSourceException(BugTexts.SourceUnknown);
    }

    public static string ThreadUrl(string guildId, string threadId) => $"https://discord.com/channels/{guildId}/{threadId}";

    /// <summary>Zeitpunkt aus einer Discord-ID (Snowflake: Millisekunden seit 2015 in den oberen Bits).</summary>
    public static DateTime SnowflakeTime(string id) =>
        ulong.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? DateTimeOffset.FromUnixTimeMilliseconds((long)(value >> 22) + 1420070400000L).UtcDateTime
            : DateTime.MinValue;

    /// <summary>Verworfen-Tag vor Erledigt-Tag, sonst Offen. Archiviert allein ändert nichts.</summary>
    public static BugStatus StatusOf(IReadOnlyList<string> tags, BugSourceEntry entry) =>
        Has(tags, entry.RejectedTagId) ? BugStatus.Rejected
        : Has(tags, entry.DoneTagId) ? BugStatus.Done
        : BugStatus.Open;

    /// <summary>Lesbarer Grund zu einem Discord-Fehler; enthält nie den Token.</summary>
    public static string MessageFor(DiscordException ex) => ex.StatusCode switch
    {
        0 => BugTexts.DiscordNetwork(ex.Message),
        401 => BugTexts.DiscordTokenInvalid,
        403 => BugTexts.DiscordForbidden,
        404 => BugTexts.DiscordNotFound,
        _ => BugTexts.DiscordFailed(ex.StatusCode, ex.Message),
    };

    public Task<IReadOnlyList<RemoteBug>> FetchAsync(BugSourceEntry entry, CancellationToken cancellation) =>
        RunAsync<IReadOnlyList<RemoteBug>>(async discord =>
        {
            var (guildId, channelId) = SplitKey(entry.Key);
            var active = (await discord.GetActiveThreadsAsync(guildId, cancellation)).Where(thread => thread.ParentId == channelId);
            var archived = await discord.GetArchivedThreadsAsync(channelId, cancellation);
            var threads = active.Concat(archived)
                .GroupBy(thread => thread.Id)
                .Select(group => group.First())
                .Where(thread => entry.Labels.Count == 0 || thread.AppliedTags.Any(tag => entry.Labels.Contains(tag)))
                .ToList();
            var result = new List<RemoteBug>();
            foreach (var thread in threads)
            {
                var starter = await discord.GetMessageAsync(thread.Id, thread.Id, cancellation);
                result.Add(new RemoteBug(
                    thread.Id,
                    ThreadUrl(guildId, thread.Id),
                    thread.Name,
                    starter?.Content ?? string.Empty,
                    starter?.Author ?? string.Empty,
                    StatusOf(thread.AppliedTags, entry),
                    SnowflakeTime(thread.Id),
                    SnowflakeTime(thread.LastMessageId.Length > 0 ? thread.LastMessageId : thread.Id),
                    null,
                    starter?.Attachments));
            }

            return result;
        });

    public Task SetStatusAsync(BugSourceEntry entry, BugReport report, BugStatus status, CancellationToken cancellation) =>
        RunAsync(async discord =>
        {
            var (_, channelId) = SplitKey(entry.Key);
            var target = status switch
            {
                BugStatus.Done => entry.DoneTagId,
                BugStatus.Rejected => entry.RejectedTagId,
                _ => entry.OpenTagId,
            };
            var forum = await discord.GetChannelAsync(channelId, cancellation);
            if (target.Length == 0 || forum.Tags.All(tag => tag.Id != target))
            {
                throw new BugSourceException(BugTexts.DiscordTagMissing(BugTexts.Status(status)));
            }

            var thread = await discord.GetChannelAsync(report.ExternalId, cancellation);
            var statusTags = new[] { entry.OpenTagId, entry.DoneTagId, entry.RejectedTagId };
            var tags = thread.AppliedTags.Where(tag => !statusTags.Contains(tag)).Append(target).ToList();
            if (tags.Count > MaxTags)
            {
                throw new BugSourceException(BugTexts.DiscordTooManyTags);
            }

            await discord.ModifyThreadAsync(report.ExternalId, archived: false, tags, cancellation);
            if (entry.ArchiveClosed && status != BugStatus.Open)
            {
                await discord.ModifyThreadAsync(report.ExternalId, archived: true, null, cancellation);
            }

            return true;
        });

    public Task<IReadOnlyList<BugComment>> LoadDiscussionAsync(BugSourceEntry entry, BugReport report, CancellationToken cancellation) =>
        RunAsync<IReadOnlyList<BugComment>>(async discord => (await discord.GetMessagesAsync(report.ExternalId, cancellation))
            .Where(message => message.Id != report.ExternalId)
            .OrderBy(message => message.TimestampUtc)
            .Select(message => new BugComment(message.Author, message.TimestampUtc, message.Content, message.Attachments))
            .ToList());

    private static bool Has(IReadOnlyList<string> tags, string id) => id.Length > 0 && tags.Contains(id);

    private async Task<T> RunAsync<T>(Func<DiscordClient, Task<T>> action)
    {
        var discord = _client() ?? throw new BugSourceException(BugTexts.DiscordNotSetUp);
        try
        {
            return await action(discord);
        }
        catch (DiscordException ex)
        {
            throw new BugSourceException(MessageFor(ex), ex);
        }
    }
}
