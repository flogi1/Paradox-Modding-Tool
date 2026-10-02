namespace ParadoxModTool.BugTracking;

/// <param name="TokenValid">Discord nimmt den Token an.</param>
/// <param name="IntentEnabled">"Message Content Intent" ist in der Anwendung eingeschaltet.</param>
/// <param name="Error">Anderer Fehler als ein ungültiger Token (lesbar); sonst <see langword="null"/>.</param>
public sealed record DiscordSetupResult(bool TokenValid, string BotName, bool IntentEnabled, string InviteUrl, IReadOnlyList<string> Servers, string? Error);

/// <summary>Live-Prüfung der Einrichtung für die Einstellungsseite und die Rechteprüfung beim Verknüpfen eines Forums.</summary>
public static class DiscordSetup
{
    public const string PortalUrl = "https://discord.com/developers/applications";

    /// <summary>Anwendungs-Flags GATEWAY_MESSAGE_CONTENT und GATEWAY_MESSAGE_CONTENT_LIMITED.</summary>
    public const long MessageContentFlags = (1L << 18) | (1L << 19);

    public static async Task<DiscordSetupResult> CheckAsync(DiscordClient client, CancellationToken cancellation)
    {
        try
        {
            var user = await client.GetCurrentUserAsync(cancellation);
            var application = await client.GetApplicationAsync(cancellation);
            var guilds = await client.GetGuildsAsync(cancellation);
            return new DiscordSetupResult(true, user.Name, (application.Flags & MessageContentFlags) != 0, DiscordPermissions.InviteUrl(application.Id), guilds.Select(g => g.Name).ToList(), null);
        }
        catch (DiscordException ex) when (ex.StatusCode == 401)
        {
            return new DiscordSetupResult(false, string.Empty, false, string.Empty, Array.Empty<string>(), null);
        }
        catch (DiscordException ex)
        {
            return new DiscordSetupResult(false, string.Empty, false, string.Empty, Array.Empty<string>(), DiscordBugSource.MessageFor(ex));
        }
    }

    /// <summary>Fehlende Rechte des Bots im Channel (leer = alles da).</summary>
    public static async Task<IReadOnlyList<ulong>> MissingPermissionsAsync(DiscordClient client, DiscordChannel channel, CancellationToken cancellation)
    {
        var user = await client.GetCurrentUserAsync(cancellation);
        var roles = await client.GetRolesAsync(channel.GuildId, cancellation);
        var memberRoles = await client.GetMemberRolesAsync(channel.GuildId, user.Id, cancellation);
        return DiscordPermissions.Missing(DiscordPermissions.Compute(channel.GuildId, user.Id, roles, memberRoles, channel.Overwrites));
    }
}
