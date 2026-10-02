using ParadoxModTool.Mods;

namespace ParadoxModTool.ModOverview;

/// <summary>Die Links, die eine Kachel zeigt; <see langword="null"/> = nicht vorhanden (ausgegraut).</summary>
/// <param name="GitHubFromRepo">Der GitHub-Link kommt aus einem eingetragenen Repo und ist daher im Dialog nur zur Anzeige.</param>
public sealed record EffectiveLinks(string? Workshop, string? Forum, string? Discord, string? GitHub, bool GitHubFromRepo);

/// <summary>Setzt die Links einer Mod zusammen: Workshop aus der Workshop-ID, GitHub aus dem ersten aktiven Repo, sonst aus <c>links.json</c>.</summary>
public static class ModLinks
{
    /// <param name="workshopId">Bekannte Workshop-ID (auch aus der Metadaten-Datei); ohne Angabe die aus dem Register.</param>
    public static EffectiveLinks For(ModInfo mod, ModLinkEntry? entry, string? workshopId = null)
    {
        var id = workshopId is { Length: > 0 } ? workshopId : mod.SteamWorkshopId;
        var workshop = id is { Length: > 0 } ? "https://steamcommunity.com/sharedfiles/filedetails/?id=" + id : null;
        var repo = mod.GitHubRepos.FirstOrDefault(r => r.Enabled && r.Repo.Length > 0)?.Repo;
        return new EffectiveLinks(
            workshop,
            entry?.ForumUrl,
            entry?.DiscordUrl,
            repo is not null ? "https://github.com/" + repo : entry?.GitHubUrl,
            repo is not null);
    }
}
