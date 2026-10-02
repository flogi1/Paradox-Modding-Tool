namespace ParadoxModTool.VersionManager;

/// <summary>
/// Vom Nutzer gewählter Speicherort für Snapshots und Vanilla-Historie (Nutzer-Feedback Task 14).
/// <see cref="ParentFolder"/> ist der Oberordner, unter dem "EU V Vanilla History" und
/// "[Modname] Snapshots" angelegt werden (<see langword="null"/> = Standard-Oberordner, siehe
/// <see cref="StorageLocations.GetDefaultParentFolder"/>). <see cref="InsideModFolder"/> legt
/// beides stattdessen direkt im Mod-Ordner ab - nicht empfohlen, da Steam beim Hochladen den
/// gesamten Mod-Ordnerinhalt mitnimmt.
/// </summary>
public sealed record StorageSettings(string? ParentFolder, bool InsideModFolder, string? VanillaHistoryFolderName = null);

/// <summary>Berechnet die tatsächlichen Ablageordner für Snapshots und Vanilla-Historie.</summary>
public static class StorageLocations
{
    /// <summary>Ordnername der Vanilla-Historie, wenn das Spielprofil keinen eigenen festlegt (Standard der früheren Fassung).</summary>
    public const string VanillaHistoryFolderName = "EU V Vanilla History";
    public const string SnapshotsFolderSuffix = " Snapshots";

    /// <summary>Ordnername der Vanilla-Historie im Mod-Ordner (Rückfall, bewusst identisch zur früheren Fassung).</summary>
    public const string InModHistoryFolderName = ".pmt-vanilla-history";

    /// <summary>Ordnername der Snapshots im Mod-Ordner (Rückfall, bewusst identisch zur früheren Fassung).</summary>
    public const string InModSnapshotsFolderName = "snapshots";

    public const string PatchRecipesFolderSuffix = " Patch Recipes";

    /// <summary>Ordnername der Patch-Rezepte im Mod-Ordner (identisch zu PatchRecipeStore.FolderName).</summary>
    public const string InModPatchRecipesFolderName = ".pmt-patches";

    public static string GetDefaultParentFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Paradox Modding Tool");

    /// <summary>Ordner für die Vanilla-Historie - unabhängig vom Mod, da Vanilla-Dateien für alle Mods gleich sind.</summary>
    public static string ResolveVanillaHistoryDirectory(string modRoot, StorageSettings settings) =>
        settings.InsideModFolder
            ? Path.Combine(modRoot, InModHistoryFolderName)
            : Path.Combine(EffectiveParent(settings), settings.VanillaHistoryFolderName ?? VanillaHistoryFolderName);

    /// <summary>Ordner für die Snapshots dieses Mods: "[Modname] Snapshots".</summary>
    public static string ResolveSnapshotsDirectory(string modRoot, StorageSettings settings) =>
        settings.InsideModFolder
            ? Path.Combine(modRoot, InModSnapshotsFolderName)
            : Path.Combine(EffectiveParent(settings), GetModName(modRoot) + SnapshotsFolderSuffix);

    /// <summary>Ordner für die Patch-Rezepte dieses Mods: "[Modname] Patch Recipes" (im Mod-Ordner der frühere Ort ".pmt-patches").</summary>
    public static string ResolvePatchRecipesDirectory(string modRoot, StorageSettings settings) =>
        settings.InsideModFolder
            ? Path.Combine(modRoot, InModPatchRecipesFolderName)
            : Path.Combine(EffectiveParent(settings), GetModName(modRoot) + PatchRecipesFolderSuffix);

    public static string GetModName(string modRoot)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(modRoot));
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrEmpty(cleaned) ? "Mod" : cleaned;
    }

    private static string EffectiveParent(StorageSettings settings) =>
        string.IsNullOrWhiteSpace(settings.ParentFolder) ? GetDefaultParentFolder() : settings.ParentFolder;
}
