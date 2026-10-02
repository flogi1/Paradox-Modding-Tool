namespace ParadoxModTool.Mods;

/// <summary>Eine Mod eines Spiels: stabile ID, Anzeigename und die bekannten Ordner (Entwicklungsordner, weitere Kopien).</summary>
public sealed class ModInfo
{
    /// <summary>Stabile Kennung (GUID ohne Bindestriche). Anker für alle Daten der Mod, ändert sich nie.</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public List<string> Paths { get; set; } = new();

    /// <summary>Die Changelog-Datei der Mod, relativ zum Mod-Ordner mit <c>/</c> (z. B. <c>Readme/Changelog.txt</c>); <see langword="null"/>, wenn keine festgelegt ist.</summary>
    public string? ChangelogFile { get; set; }

    /// <summary>Die ID der Workshop-Seite auf Steam (nur Ziffern); <see langword="null"/>, wenn die Mod dort noch nicht veröffentlicht ist.</summary>
    public string? SteamWorkshopId { get; set; }

    /// <summary>Die GitHub-Repositories, in denen Releases dieser Mod veröffentlicht werden.</summary>
    public List<GitHubRepoEntry> GitHubRepos { get; set; } = new();

    /// <summary>Die ZIP-Varianten, die als Anhang eines GitHub-Releases in Frage kommen. Leer = eine Variante "Standard" (siehe Oberfläche).</summary>
    public List<GitHubZipVariant> GitHubZipVariants { get; set; } = new();

    /// <summary>Quellen für Bugreports (GitHub-Repositories, ab Plan 3 Discord-Foren). Nur am Hauptmod eines Projekts belegt.</summary>
    public List<BugSourceEntry> BugSources { get; set; } = new();

    /// <summary>ID des Hauptmods, wenn diese Mod ein Dev-Branch oder eine Submod ist; sonst <see langword="null"/>. Ein Projekt ist der Hauptmod plus alle Mods, die auf ihn zeigen (flach, keine Verschachtelung).</summary>
    public string? ParentModId { get; set; }

    /// <summary>Rolle im Projekt: <see cref="ModRole.Main"/> genau dann, wenn <see cref="ParentModId"/> leer ist.</summary>
    public ModRole Role { get; set; } = ModRole.Main;

    /// <summary>Gesperrt: PMT überträgt nichts in diese Mod und nichts aus ihr (Übertragen, Abgleich). Der Editor bleibt unberührt.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Gesperrte Dateien und Ordner (relativ zum Arbeitsordner, mit <c>/</c>); ein Ordner sperrt alles darunter. Siehe <see cref="ModLocks"/>.</summary>
    public List<string> LockedPaths { get; set; } = new();

    /// <summary>Die Mod ist ein Kompatibilitätspatch (Kompatch) für <see cref="CompatchTargets"/>. Ein Kompatch gehört zu keinem Projekt.</summary>
    public bool IsCompatch { get; set; }

    /// <summary>Die Mods, die der Kompatch zusammenführt (Register, Workshop oder Ordner). Leer, wenn <see cref="IsCompatch"/> aus ist.</summary>
    public List<CompatchTarget> CompatchTargets { get; set; } = new();

    /// <summary>GUI-Editor: Dateiname der Überschreibdatei in <c>&lt;Kontext&gt;/gui</c>; null = Standard aus dem Ordnernamen der Mod.</summary>
    public string? GuiOverrideFile { get; set; }

    /// <summary>GUI-Editor: Dateiname der Typdatei für neue Typen in <c>&lt;Kontext&gt;/gui</c>; null = Standard aus dem Ordnernamen der Mod.</summary>
    public string? GuiTypesFile { get; set; }

    /// <summary>GUI-Editor: Ordner der Abhängigkeits-Mods für den GUI-Index; null = Vorbelegung (<see cref="ModGuiDependencies.Defaults"/>).</summary>
    public List<string>? GuiDependencies { get; set; }
}
