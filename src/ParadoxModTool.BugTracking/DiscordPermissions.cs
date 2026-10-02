using System.Globalization;

namespace ParadoxModTool.BugTracking;

/// <summary>Rechte-Bits von Discord und die Berechnung der Rechte eines Mitglieds in einem Channel (Basis → @everyone → Rollen → Mitglied).</summary>
public static class DiscordPermissions
{
    public const ulong Administrator = 1UL << 3;
    public const ulong ViewChannel = 1UL << 10;
    public const ulong ReadMessageHistory = 1UL << 16;
    public const ulong ManageThreads = 1UL << 34;

    /// <summary>Was der Bot im Forum braucht: ansehen, Verlauf lesen, Threads verwalten.</summary>
    public const ulong Required = ViewChannel | ReadMessageHistory | ManageThreads;

    private static readonly ulong[] RequiredBits = { ViewChannel, ReadMessageHistory, ManageThreads };

    public static ulong Compute(string guildId, string userId, IReadOnlyList<DiscordRole> roles, IReadOnlyList<string> memberRoles, IReadOnlyList<DiscordOverwrite> overwrites)
    {
        var permissions = roles.FirstOrDefault(role => role.Id == guildId)?.Permissions ?? 0;
        foreach (var role in roles.Where(role => memberRoles.Contains(role.Id)))
        {
            permissions |= role.Permissions;
        }

        if ((permissions & Administrator) != 0)
        {
            return ulong.MaxValue;
        }

        if (overwrites.FirstOrDefault(o => o.Type == 0 && o.Id == guildId) is { } everyone)
        {
            permissions = (permissions & ~everyone.Deny) | everyone.Allow;
        }

        ulong allow = 0, deny = 0;
        foreach (var overwrite in overwrites.Where(o => o.Type == 0 && o.Id != guildId && memberRoles.Contains(o.Id)))
        {
            allow |= overwrite.Allow;
            deny |= overwrite.Deny;
        }

        permissions = (permissions & ~deny) | allow;
        if (overwrites.FirstOrDefault(o => o.Type == 1 && o.Id == userId) is { } member)
        {
            permissions = (permissions & ~member.Deny) | member.Allow;
        }

        return permissions;
    }

    /// <summary>Die nötigen Rechte, die in <paramref name="permissions"/> fehlen.</summary>
    public static IReadOnlyList<ulong> Missing(ulong permissions) => RequiredBits.Where(bit => (permissions & bit) == 0).ToList();

    public static string InviteUrl(string applicationId) =>
        $"https://discord.com/oauth2/authorize?client_id={applicationId}&scope=bot&permissions={Required.ToString(CultureInfo.InvariantCulture)}";
}
