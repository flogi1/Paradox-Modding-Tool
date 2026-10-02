using System.Text.RegularExpressions;
using ParadoxModTool.SteamIntegration;

namespace ParadoxModTool.ModOverview;

/// <summary>Vorschlag: diese Workshop-Seite (von der Steam-Profilseite) gehört wohl zu dieser Mod ohne bekannte ID.</summary>
public sealed record WorkshopIdSuggestion(OverviewMod Mod, string WorkshopId, string Title);

/// <summary>
/// Ordnet eigene Workshop-Seiten eines Spiels den Mods ohne Workshop-ID zu: gleicher Titel wie Register- oder Metadaten-Name (Groß-/Kleinschreibung
/// und Mehrfach-Leerzeichen egal). Nur eindeutige Treffer; IDs, die eine Mod des Spiels schon hat, werden nie vorgeschlagen.
/// </summary>
public static class WorkshopIdSuggestions
{
    public static IReadOnlyList<WorkshopIdSuggestion> Find(IReadOnlyList<OverviewMod> mods, string gameId, IReadOnlyList<WorkshopMatch> published)
    {
        var inGame = mods.Where(m => m.GameId == gameId).ToList();
        var taken = inGame.Select(m => m.WorkshopId).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var free = published.Where(p => !taken.Contains(p.Id)).ToList();
        var result = new List<WorkshopIdSuggestion>();
        foreach (var mod in inGame.Where(m => m.WorkshopId is null))
        {
            var names = new[] { mod.Mod.Name, mod.Metadata?.Name }.OfType<string>().Select(Normalize).Where(n => n.Length > 0).ToHashSet(StringComparer.Ordinal);
            var matches = free.Where(p => names.Contains(Normalize(p.Title))).DistinctBy(p => p.Id).ToList();
            if (matches.Count == 1)
            {
                result.Add(new WorkshopIdSuggestion(mod, matches[0].Id, matches[0].Title.Trim()));
            }
        }

        return result;
    }

    private static string Normalize(string text) => Regex.Replace(text.Trim(), @"\s+", " ").ToLowerInvariant();
}
