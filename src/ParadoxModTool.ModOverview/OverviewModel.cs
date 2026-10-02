using ParadoxModTool.Mods;

namespace ParadoxModTool.ModOverview;

/// <summary>Ein Spiel der Übersicht; <paramref name="InstalledVersion"/> ist <see langword="null"/>, wenn Spielordner oder Version nicht erkennbar sind.</summary>
public sealed record OverviewGame(string Id, string DisplayName, string? InstalledVersion);

/// <summary>Ein Spiel, dessen Mod-Liste nicht lesbar war; die übrigen Spiele laden trotzdem.</summary>
public sealed record OverviewGameError(string GameId, string DisplayName, string Message);

/// <summary>Die neueste erfasste Release einer Mod.</summary>
public sealed record OverviewRelease(string Version, string Date, bool OnSteam, bool OnGitHub);

/// <summary>Eine Mod aus dem Register eines Spiels mit allem, was Kachel, Liste und Hinweise brauchen.</summary>
public sealed class OverviewMod
{
    public required string GameId { get; init; }

    public required ModInfo Mod { get; init; }

    /// <summary>Metadaten aus dem ersten Mod-Ordner mit lesbarer Datei; <see langword="null"/>, wenn keiner eine hat.</summary>
    public ModMetadata? Metadata { get; init; }

    /// <summary>Eingetragene Mod-Ordner, die es nicht (mehr) gibt.</summary>
    public IReadOnlyList<string> MissingPaths { get; init; } = Array.Empty<string>();

    public OverviewRelease? LastRelease { get; init; }

    public bool HasGitHubRelease { get; init; }

    public ReleaseStatus? ReleaseStatus { get; init; }

    /// <summary>Fehlermeldung, wenn der Changelog nicht lesbar war; zählt für den Release-Hinweis mit.</summary>
    public string? ReleaseStatusProblem { get; init; }

    /// <summary>Workshop-ID: aus dem Register, sonst <c>remote_file_id</c> der klassischen Metadaten-Datei; <see langword="null"/>, wenn keine bekannt ist.</summary>
    public string? WorkshopId { get; init; }

    public required EffectiveLinks Links { get; init; }

    public SteamCacheEntry? Steam { get; init; }

    /// <summary>Voller Pfad des Vorschaubilds; <see langword="null"/> = Platzhalter mit Initialen.</summary>
    public string? Thumbnail { get; init; }

    public VersionSupport VersionSupport { get; init; }

    /// <summary>Die Abhängigkeiten aus den Metadaten; <c>Target</c> ist die eigene Mod im selben Spiel, <see langword="null"/> bei fremden Mods.</summary>
    public IReadOnlyList<(ModDependency Dependency, OverviewMod? Target)> ResolvedDependencies { get; internal set; } = Array.Empty<(ModDependency, OverviewMod?)>();

    /// <summary>Die Hauptmod des Projekts; bei einer Hauptmod oder einer Mod ohne Projekt die Mod selbst.</summary>
    public OverviewMod ProjectRoot { get; internal set; } = null!;

    public bool IsPublished { get; init; }
}

/// <summary>Stand der Übersicht nach einem Laden: Spiele in Profil-Reihenfolge, alle Mods, nicht lesbare Spiele und Zeitpunkt des letzten Steam-Abrufs.</summary>
public sealed record OverviewSnapshot(
    IReadOnlyList<OverviewGame> Games,
    IReadOnlyList<OverviewMod> Mods,
    IReadOnlyList<OverviewGameError> GameErrors,
    DateTime? SteamFetchedUtc);
