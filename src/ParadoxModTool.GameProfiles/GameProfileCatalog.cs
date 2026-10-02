using System.Text.RegularExpressions;

namespace ParadoxModTool.GameProfiles;

/// <summary>Ein gefundenes Spielprofil samt Ort der Profildatei.</summary>
public sealed record GameProfileEntry(GameProfile Profile, string FilePath);

/// <summary>
/// Findet alle Spielprofile in einem Ordner (<c>profiles\*.json</c> neben der Programmdatei). Ein neues Spiel kommt allein durch eine
/// weitere Profildatei dazu. Unlesbare Dateien und Profile mit ungültiger Kennung werden übersprungen.
/// </summary>
public static class GameProfileCatalog
{
    private static readonly Regex IdPattern = new("^[A-Za-z0-9_-]{1,40}$", RegexOptions.Compiled);

    /// <summary>Alle brauchbaren Profile, nach Erscheinungsdatum sortiert (ohne Datum zuletzt, danach nach Anzeigename). Bei doppelter Kennung gilt die alphabetisch erste Datei.</summary>
    public static IReadOnlyList<GameProfileEntry> Discover(string directory)
    {
        var result = new List<GameProfileEntry>();
        if (!Directory.Exists(directory))
        {
            return result;
        }

        foreach (var file in Directory.GetFiles(directory, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var profile = GameProfileLoader.LoadFromFile(file);
                if (IdPattern.IsMatch(profile.Id) && result.All(e => e.Profile.Id != profile.Id))
                {
                    result.Add(new GameProfileEntry(profile, file));
                }
            }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidDataException)
            {
                // Eine beschädigte Profildatei darf die übrigen Spiele nicht verstecken.
            }
        }

        return result
            .OrderBy(e => e.Profile.ReleaseDate ?? DateOnly.MaxValue)
            .ThenBy(e => e.Profile.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
