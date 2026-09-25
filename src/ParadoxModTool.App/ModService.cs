using System.IO;
using ParadoxModTool.Mods;
using ParadoxModTool.Notes;

namespace ParadoxModTool.App;

/// <summary>
/// Hält das Mod-Verzeichnis dieses Spiels und die aktive Mod. Die aktive Mod wird aus der offenen Datei, den Explorer-Mod-Ordnern und dem Vanilla-Diff-Mod-Ordner
/// erkannt (siehe <see cref="ModDetector"/>); <see cref="Refresh"/> ist der einzige Ort, der die Erkennung aufruft.
/// </summary>
internal sealed class ModService
{
    public ModService(string gameDirectory, string metadataFile)
    {
        MetadataFile = metadataFile;
        Registry = ModRegistry.Load(gameDirectory);
        NoteKeys.SetResolver(ModKeyOf);
        SyncNoteKeys();
    }

    public ModRegistry Registry { get; }

    /// <summary>Pfad der Metadaten-Datei relativ zum Mod-Ordner (aus dem Spielprofil).</summary>
    public string MetadataFile { get; }

    /// <summary>Die erkannte Mod; <see langword="null"/>, wenn keine erkannt ist.</summary>
    public ModInfo? ActiveMod { get; private set; }

    /// <summary>Der Mod-Ordner der offenen Datei, wenn er (noch) nicht als Mod geführt wird; sonst <see langword="null"/>. Dann ist <see cref="ActiveMod"/> leer.</summary>
    public string? UnmanagedFolder { get; private set; }

    /// <summary>Erkennt die aktive Mod neu. Gibt den Ordner einer noch unbekannten Mod zurück (Rückfrage "Als Mod anlegen?"), sonst <see langword="null"/>.</summary>
    public string? Refresh(IEnumerable<string?> candidates, IEnumerable<string> vanillaRoots)
    {
        var detection = ModDetector.Detect(Registry, candidates, MetadataFile, vanillaRoots);
        ActiveMod = detection.Kind == ModDetectionKind.Known ? detection.Mod : null;
        UnmanagedFolder = detection.Kind is ModDetectionKind.Unknown or ModDetectionKind.Ignored ? detection.Folder : null;
        return detection.Kind == ModDetectionKind.Unknown ? detection.Folder : null;
    }

    /// <summary>Der Notiz-Schlüssel einer Datei in einer geführten Mod (<c>mod:&lt;id&gt;/…</c>), sonst <see langword="null"/> (dann gilt der absolute Pfad).</summary>
    private string? ModKeyOf(string fullPath)
    {
        if (Registry.Resolve(fullPath) is not { } mod || Registry.ToRelative(mod.Id, fullPath) is not { Length: > 0 } relative)
        {
            return null;
        }

        return NoteKeys.ModKey(mod.Id, relative);
    }

    /// <summary>Stellt Notizen, Lesezeichen und Bug-Bezüge auf Mod-Schlüssel um, soweit ihre Dateien jetzt in einer geführten Mod liegen. Nach jeder Änderung des Verzeichnisses aufrufen; ohne Treffer wird nichts geschrieben.</summary>
    public void SyncNoteKeys()
    {
        Func<string, string?> map = key => NoteKeys.IsModKey(key) ? null : ModKeyOf(key);
        MigrateNoteKeys(map);
        MigrateBugKeys(map, null);
    }

    /// <summary>
    /// Vor dem Entfernen einer Mod aufrufen. Notizen und Lesezeichen gehen auf absolute Pfade (erster Ordner der Mod). Bug-Bezüge eines Dev-Branches
    /// oder einer Submod wandern auf den Hauptmod (dort liegen dieselben relativen Pfade), die eines Hauptmods auf absolute Pfade.
    /// </summary>
    public void ReleaseNoteKeys(ModInfo mod)
    {
        if (mod.ParentModId is { } parentId && Registry.Get(parentId) is { } parent)
        {
            MigrateBugKeys(
                key => NoteKeys.TryParseModKey(key, out var id, out var relative) && id == mod.Id ? NoteKeys.ModKey(parent.Id, relative) : null,
                id => id == mod.Id ? parent.Id : null);
        }

        if (mod.Paths.FirstOrDefault() is not { } root)
        {
            return;
        }

        Func<string, string?> toAbsolute = key =>
            NoteKeys.TryParseModKey(key, out var id, out var relative) && id == mod.Id
                ? Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)))
                : null;
        MigrateNoteKeys(toAbsolute);
        if (mod.ParentModId is null || Registry.Get(mod.ParentModId) is null)
        {
            MigrateBugKeys(toAbsolute, id => id == mod.Id ? string.Empty : null);
        }
    }

    /// <summary>Projekt-Auflösung für die Bug-Abfragen aus dem Register dieses Spiels; unbekannte Mods (anderes Spiel) gehören zu keinem Projekt.</summary>
    public BugProjects CreateBugProjects() => new(
        id => Registry.Get(id) is { } mod ? ModProjects.Root(Registry, mod).Id : null,
        id => Registry.Get(id)?.Paths ?? new List<string>(),
        rootId => Registry.Get(rootId) is { } root ? ModProjects.Members(Registry, root).Select(m => m.Id).ToList() : new List<string>());

    /// <summary>Projekt der aktiven Mod (Id ihres Hauptmods); <see langword="null"/> ohne erkannte Mod.</summary>
    public string? ActiveProjectId => ActiveMod is { } mod ? ModProjects.Root(Registry, mod).Id : null;

    private static void MigrateNoteKeys(Func<string, string?> map)
    {
        try
        {
            NoteKeyMigration.Run(FileNoteStore.GetDefaultNotesFilePath(), BookmarkStore.GetDefaultBookmarksFilePath(), map);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Der Umzug wird beim nächsten Start oder bei der nächsten Änderung wiederholt.
        }
    }

    private static void MigrateBugKeys(Func<string, string?> keyMap, Func<string, string?>? ownerMap)
    {
        try
        {
            BugKeyMigration.Run(BugReportStore.GetDefaultPath(), keyMap, ownerMap);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Wie oben: nächster Start oder nächste Änderung.
        }
    }
}
