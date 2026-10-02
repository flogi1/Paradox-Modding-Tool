using ParadoxModTool.Mods;
using ParadoxModTool.SteamIntegration;

namespace ParadoxModTool.ModOverview.Tests;

/// <summary>Baut Übersichts-Mods im Speicher, ohne Dateien.</summary>
internal static class TestMods
{
    public static OverviewMod Mod(
        string name,
        string game = "eu4",
        ModRole role = ModRole.Main,
        OverviewMod? root = null,
        long? subscribers = null,
        string? releaseDate = null,
        bool published = false,
        bool compatch = false,
        IEnumerable<string>? compatchTargets = null,
        string? descriptorName = null)
    {
        var info = new ModInfo { Id = name, Name = name, Role = role, ParentModId = root?.Mod.Id, IsCompatch = compatch };
        foreach (var target in compatchTargets ?? Array.Empty<string>())
        {
            info.CompatchTargets.Add(new CompatchTarget { ModId = target, DisplayName = target });
        }

        var mod = new OverviewMod
        {
            GameId = game,
            Mod = info,
            Metadata = new ModMetadata(descriptorName ?? name, "1.0", "1.*", "C:\\mods\\" + name),
            Links = new EffectiveLinks(null, null, null, null, false),
            Steam = subscribers is null ? null : new SteamCacheEntry { Id = name, State = WorkshopPageState.Ok, Subscriptions = subscribers },
            LastRelease = releaseDate is null ? null : new OverviewRelease("1.0", releaseDate, false, false),
            IsPublished = published,
        };
        mod.ProjectRoot = root ?? mod;
        return mod;
    }

    public static OverviewSnapshot Snapshot(params OverviewMod[] mods) => new(
        new[]
        {
            new OverviewGame("eu4", "Europa Universalis IV", null),
            new OverviewGame("ck3", "Crusader Kings III", null),
            new OverviewGame("eu5", "Europa Universalis V", null),
        },
        mods,
        Array.Empty<OverviewGameError>(),
        null);
}
