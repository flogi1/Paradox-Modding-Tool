using ParadoxModTool.GameProfiles;
using ParadoxModTool.Mods;
using ParadoxModTool.SteamIntegration;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.ModOverview;

/// <summary>
/// Lädt die Mods aller Spiele für die Übersicht "Meine Mods". Die Register der Spiele werden nur gelesen (nur das PMT des Spiels schreibt sie);
/// ein unlesbares Register überspringt nur dieses Spiel.
/// </summary>
public static class ModOverviewLoader
{
    public static OverviewSnapshot Load(string appDataRoot, IReadOnlyList<GameProfile> profiles, IEnumerable<string> steamLibraryFolders)
    {
        var libraries = steamLibraryFolders.ToList();
        var paths = new OverviewPaths(appDataRoot);
        var links = ModLinkStore.Load(paths.LinksFile);
        var steam = SteamCacheStore.Load(paths.SteamFile);
        var games = new List<OverviewGame>();
        var errors = new List<OverviewGameError>();
        var mods = new List<OverviewMod>();

        foreach (var profile in profiles)
        {
            var gameDirectory = Path.Combine(appDataRoot, "games", profile.Id);
            var installed = GameInstallLocator.Find(gameDirectory, libraries, profile.SteamAppId) is { } root ? GameVersionReader.TryReadVersion(root) : null;
            games.Add(new OverviewGame(profile.Id, profile.DisplayName, installed));

            var registry = ModRegistry.Load(gameDirectory);
            if (registry.LoadWarning is { } warning)
            {
                errors.Add(new OverviewGameError(profile.Id, profile.DisplayName, warning));
                continue;
            }

            var gameMods = registry.Mods
                .Select(mod => Build(profile, registry, mod, installed, links, steam, paths))
                .ToList();
            Link(gameMods);
            mods.AddRange(gameMods);
        }

        var fetched = steam.Values.Select(e => (DateTime?)e.FetchedUtc).DefaultIfEmpty(null).Max();
        return new OverviewSnapshot(games, mods, errors, fetched);
    }

    private static OverviewMod Build(
        GameProfile profile,
        ModRegistry registry,
        ModInfo mod,
        string? installedVersion,
        IReadOnlyList<ModLinkEntry> links,
        IReadOnlyDictionary<string, SteamCacheEntry> steam,
        OverviewPaths paths)
    {
        var metadata = ModMetadata.Find(mod, profile.MetadataFile);
        var releases = ReleaseLog.Load(registry.DataDirectory(mod.Id)).Releases;
        var (changelog, problem) = ReadChangelog(mod);
        var linkEntry = ModLinkStore.Find(links, profile.Id, mod.Id);
        var workshopId = mod.SteamWorkshopId is { Length: > 0 } registered ? registered
            : metadata is not null && ModMetadata.IsClassic(profile.MetadataFile)
                && ModMetadata.ClassicRemoteFileId(metadata.Folder, metadata.SourceFile) is { } remote ? remote
            : linkEntry?.WorkshopId is { Length: > 0 } adopted ? adopted
            : null;
        var steamEntry = workshopId is not null && steam.TryGetValue(workshopId, out var entry) ? entry : null;
        var hasGitHubRelease = releases.Any(r => r.GitHub is { Count: > 0 });
        var hasWorkshop = workshopId is not null;
        var steamBlocked = steamEntry?.State is WorkshopPageState.Hidden or WorkshopPageState.NotFound;

        return new OverviewMod
        {
            GameId = profile.Id,
            Mod = mod,
            Metadata = metadata,
            MissingPaths = mod.Paths.Where(p => !Directory.Exists(p)).ToList(),
            LastRelease = releases.FirstOrDefault() is { } last
                ? new OverviewRelease(last.Version, last.Date, last.Steam is not null, last.GitHub is { Count: > 0 })
                : null,
            HasGitHubRelease = hasGitHubRelease,
            ReleaseStatus = ReleaseStatus.Evaluate(metadata, changelog, releases, ModMetadata.IsClassic(profile.MetadataFile)),
            ReleaseStatusProblem = problem,
            WorkshopId = workshopId,
            Links = ModLinks.For(mod, linkEntry, workshopId),
            Steam = steamEntry,
            Thumbnail = ThumbnailCache.Resolve(metadata, paths.ThumbsDirectory, steamEntry),
            VersionSupport = GameVersionMatch.Check(metadata?.SupportedGameVersion, installedVersion),
            IsPublished = (hasWorkshop || hasGitHubRelease) && !steamBlocked,
        };
    }

    /// <summary>Changelog wie im Releases-Fenster: die eingestellte Datei im ersten Mod-Ordner, in dem sie liegt.</summary>
    private static (IReadOnlyList<ChangelogEntry>? Entries, string? Problem) ReadChangelog(ModInfo mod)
    {
        if (mod.ChangelogFile is not { Length: > 0 } relative)
        {
            return (null, null);
        }

        try
        {
            var path = mod.Paths.Select(p => Path.GetFullPath(Path.Combine(p, relative))).FirstOrDefault(File.Exists);
            return path is null ? (null, null) : (ChangelogParser.Parse(ModTextFile.Read(path).Text), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            return (null, ex.Message);
        }
    }

    /// <summary>Setzt Projekt-Hauptmod und aufgelöste Abhängigkeiten; beides nur innerhalb eines Spiels.</summary>
    private static void Link(IReadOnlyList<OverviewMod> gameMods)
    {
        var byId = gameMods.ToDictionary(m => m.Mod.Id);
        foreach (var mod in gameMods)
        {
            mod.ProjectRoot = mod.Mod.ParentModId is { } parent && byId.TryGetValue(parent, out var root) ? root : mod;
            mod.ResolvedDependencies = (mod.Metadata?.Dependencies ?? Array.Empty<ModDependency>())
                .Select(d => (d, Resolve(d, mod, gameMods)))
                .ToList();
        }
    }

    private static OverviewMod? Resolve(ModDependency dependency, OverviewMod owner, IReadOnlyList<OverviewMod> gameMods)
    {
        var candidates = gameMods.Where(m => !ReferenceEquals(m, owner) && m.Metadata is not null).ToList();
        if (dependency.Id is { Length: > 0 } id && candidates.FirstOrDefault(m => m.Metadata!.Id == id) is { } byId)
        {
            return byId;
        }

        return candidates.FirstOrDefault(m => string.Equals(m.Metadata!.Name, dependency.DisplayName, StringComparison.OrdinalIgnoreCase));
    }
}
