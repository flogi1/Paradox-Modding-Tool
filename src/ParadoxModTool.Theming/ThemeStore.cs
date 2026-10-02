using System.Text.Json;

namespace ParadoxModTool.Theming;

/// <summary>Lädt, speichert, löscht, importiert und exportiert eigene Themes als JSON-Dateien (eine Datei je Theme).</summary>
public static class ThemeStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string GetDefaultDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ParadoxModTool", "themes");

    public static string NewCustomId() => "custom-" + Guid.NewGuid().ToString("N")[..12];

    /// <summary>Alle lesbaren eigenen Themes im Ordner (normalisiert, nach Name sortiert). Defekte Dateien werden übersprungen.</summary>
    public static List<ThemeDefinition> LoadAll(string directory)
    {
        var result = new List<ThemeDefinition>();
        if (!Directory.Exists(directory))
        {
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            if (TryRead(file) is { } theme && !theme.IsBuiltIn && !string.IsNullOrEmpty(theme.Id))
            {
                result.Add(theme);
            }
        }

        return result.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static void Save(ThemeDefinition theme, string directory)
    {
        if (theme.IsBuiltIn)
        {
            throw new InvalidOperationException(ThemingTexts.BuiltInReadOnly);
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(FilePath(theme.Id, directory), JsonSerializer.Serialize(ThemeNormalizer.Normalize(theme), Options));
    }

    public static void Delete(string id, string directory)
    {
        var path = FilePath(id, directory);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static void Export(ThemeDefinition theme, string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(ThemeNormalizer.Normalize(theme), Options));

    /// <summary>
    /// Liest eine Theme-Datei zum Import. Das Ergebnis bekommt IMMER eine neue Kennung (ein Import überschreibt nie ein
    /// vorhandenes Theme und kann kein eingebautes vortäuschen). <see langword="null"/>, wenn die Datei kein Theme ist.
    /// </summary>
    public static ThemeDefinition? Import(string path)
    {
        if (TryRead(path) is not { } theme)
        {
            return null;
        }

        theme.Id = NewCustomId();
        return theme;
    }

    private static ThemeDefinition? TryRead(string path)
    {
        try
        {
            var raw = JsonSerializer.Deserialize<ThemeDefinition>(File.ReadAllText(path), Options);
            return raw is null || raw.Colors is null || raw.Colors.Count == 0 ? null : ThemeNormalizer.Normalize(raw);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string FilePath(string id, string directory)
    {
        var safe = new string(id.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return Path.Combine(directory, safe + ".json");
    }
}
