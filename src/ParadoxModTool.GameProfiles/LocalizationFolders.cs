namespace ParadoxModTool.GameProfiles;

/// <summary>
/// Name des Lokalisierungsordners: <c>localization</c> (Jomini-Spiele: CK III, EU V, Victoria 3) oder <c>localisation</c> (klassische Spiele mit
/// britischer Schreibweise: EU IV, Stellaris, Hearts of Iron IV).
/// </summary>
public static class LocalizationFolders
{
    public static IReadOnlyList<string> Names { get; } = new[] { "localization", "localisation" };

    /// <summary>Ist <paramref name="name"/> ein Lokalisierungsordner (Groß-/Kleinschreibung egal)?</summary>
    public static bool IsName(string? name) => name is not null && Names.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Die vorhandenen Lokalisierungsordner direkt unter <paramref name="root"/>, <c>localization</c> zuerst.</summary>
    public static IEnumerable<string> Existing(string root) =>
        Names.Select(name => Path.Combine(root, name)).Where(Directory.Exists).ToList();
}
