using System.IO;
using System.Text.Json;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.Storage;

namespace ParadoxModTool.Explorer;

/// <summary>
/// Speichert/lädt die Liste angepinnter Dateien/Ordner in der Verzeichnis-Übersicht
/// (Nutzer-Feedback: "Füge auch in diesem Verzeichnis eine extra Favoritenliste hinzu, wo ich
/// einige Dateien oder Ordner anpinnen kann.") — bewusst eine EIGENE Datei/Liste, getrennt von
/// <c>ParadoxModTool.SearchReplace.SearchFavoritesStore</c> (das sind favorisierte SUCHORDNER
/// für "Suchen in Dateien", ein anderer Zweck). Enthält die Pfade GENAU SO, wie sie beim
/// Anpinnen vorlagen (NICHT normalisiert) - nur so lassen sich Name/Pfad eines Favoriten
/// anzeigen, ohne dass sein übergeordneter Ordner gerade im Baum eingelesen ist. Der Abgleich
/// "ist dieser Baumeintrag ein Favorit?" erfolgt in <c>ExplorerPanel</c> über
/// <see cref="PathNormalization.NormalizeKey"/> auf beiden Seiten (siehe Lessons Learned
/// Punkt 11 der Architektur-Dokumentation), NICHT über einen rohen String-Vergleich. Ob ein
/// Eintrag eine Datei oder ein Ordner ist,
/// wird bewusst NICHT mitgespeichert, sondern beim Anzeigen live per <see cref="Directory.Exists"/>/
/// <see cref="File.Exists"/> geprüft, damit sich ein zwischenzeitlich gelöschter/umbenannter
/// Favorit nicht als falscher Typ in einer veralteten Markierung festsetzt.
/// </summary>
public static class ExplorerFavoritesStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static void Save(IReadOnlyList<string> normalizedPaths, string path)
    {
        var json = JsonSerializer.Serialize(normalizedPaths, Options);
        SharedFile.WriteAllTextAtomic(path, json);
    }

    public static List<string> TryLoad(string path)
    {
        if (!File.Exists(path))
        {
            return new List<string>();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<string>>(json, Options) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
        catch (IOException)
        {
            return new List<string>();
        }
    }

    /// <summary>Vergleicht Favoriten-Pfade über ihren normalisierten Schlüssel (Groß-/Kleinschreibung, Trennzeichen).</summary>
    public sealed class PathComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => x is not null && y is not null && PathNormalization.NormalizeKey(x) == PathNormalization.NormalizeKey(y);

        public int GetHashCode(string obj) => PathNormalization.NormalizeKey(obj).GetHashCode();
    }

    /// <summary>
    /// Speichert die Favoriten zusammengeführt mit dem inzwischen auf der Platte stehenden Stand (ein anderes Fenster kann Favoriten ergänzt haben):
    /// nur die eigenen Änderungen gegenüber <paramref name="baseline"/> werden angewendet. Gibt den gespeicherten Gesamtstand zurück.
    /// </summary>
    public static List<string> SaveMerged(IReadOnlyList<string> baseline, IReadOnlyList<string> current, string path) =>
        SharedFile.Locked(path, () =>
        {
            var merged = CollectionMerge.MergeList(TryLoad(path), baseline, current, new PathComparer());
            Save(merged, path);
            return merged;
        });

    private const string FileName = "explorer-favorites.json";

    /// <summary><c>%APPDATA%\ParadoxModTool</c>, der Grundordner für <see cref="GetFavoritesFilePath"/> und <see cref="LoadForGame"/>.</summary>
    public static string GetAppDataFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ParadoxModTool");

    /// <summary>Favoriten eines Spiels: <c>games\&lt;id&gt;\explorer-favorites.json</c> unter <paramref name="baseFolder"/> (Standard: <see cref="GetAppDataFolder"/>).</summary>
    public static string GetFavoritesFilePath(string gameId, string? baseFolder = null) =>
        Path.Combine(baseFolder ?? GetAppDataFolder(), "games", gameId, FileName);

    /// <summary>Frühere, für alle Spiele gemeinsame Favoritendatei. Wird nur noch für die Übernahme gelesen, nie geschrieben.</summary>
    public static string GetLegacyFavoritesFilePath(string? baseFolder = null) =>
        Path.Combine(baseFolder ?? GetAppDataFolder(), FileName);

    /// <summary>
    /// Lädt die Favoriten eines Spiels (Nutzer-Feedback: Favoriten je Spiel statt einer gemeinsamen Liste). Fehlt die Datei des Spiels,
    /// werden einmalig die Einträge der alten gemeinsamen Datei übernommen, die laut <see cref="GamesForPath"/> zu diesem Spiel gehören,
    /// und sofort in die Spieldatei geschrieben: so ist die Übernahme genau einmal passiert, und das spätere <see cref="SaveMerged"/>
    /// findet sie auf der Platte vor. Eine vorhandene, auch leere Spieldatei bedeutet "alles abgepinnt" - dann wird nie übernommen.
    /// Lässt sich die alte Datei nicht lesen (gesperrt, kein gültiges JSON) oder die Spieldatei nicht schreiben, wirft die Methode
    /// <see cref="IOException"/> bzw. <see cref="UnauthorizedAccessException"/> und schreibt nichts: eine leere Spieldatei würde die Übernahme
    /// für immer verhindern, und übernommene Einträge ohne Datei auf der Platte würfe das nächste <see cref="SaveMerged"/> wieder weg (sie stünden
    /// in dessen Ausgangsstand, aber nicht auf der Platte). Der Aufrufer speichert dann in dieser Sitzung nicht; beim nächsten Start wird erneut übernommen.
    /// </summary>
    public static List<string> LoadForGame(string gameId, IReadOnlyList<GameProfile> allProfiles, string? baseFolder = null)
    {
        var path = GetFavoritesFilePath(gameId, baseFolder);
        if (File.Exists(path))
        {
            return TryLoad(path);
        }

        var legacyPath = GetLegacyFavoritesFilePath(baseFolder);
        if (!File.Exists(legacyPath))
        {
            return new List<string>();
        }

        var seeded = ReadLegacy(legacyPath).Where(p => GamesForPath(p, allProfiles).Contains(gameId, StringComparer.OrdinalIgnoreCase)).ToList();
        return SharedFile.Locked(path, () =>
        {
            // Ein anderes Fenster kann die Übernahme inzwischen erledigt haben.
            if (File.Exists(path))
            {
                return TryLoad(path);
            }

            Save(seeded, path);
            return seeded;
        });
    }

    /// <summary>
    /// Liest die alte gemeinsame Datei; anders als <see cref="TryLoad"/> wird ein Lesefehler (z. B. gesperrte Datei) nicht zur leeren Liste,
    /// sondern als <see cref="IOException"/> gemeldet, damit die Übernahme beim nächsten Start erneut versucht wird. Kaputtes JSON gilt dagegen
    /// als leer: die Datei wird atomar geschrieben, ein erneuter Versuch könnte also nie gelingen und die Favoriten blieben sonst für immer
    /// nur sitzungsweise.
    /// </summary>
    private static List<string> ReadLegacy(string legacyPath)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(legacyPath), Options) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Ordnet einen Favoriten-Pfad den Spielen zu, zu denen er gehört: ein Pfadabschnitt heißt wie der Installationsordner des Spiels
    /// (<see cref="GameProfile.DisplayName"/>), der Pfad enthält <c>Paradox Interactive\&lt;Dokumentenordner&gt;</c> (aus
    /// <see cref="GameProfile.DefaultModRootTemplate"/>) oder <c>steamapps\workshop\content\&lt;SteamAppId&gt;</c>. Verglichen werden
    /// ganze Abschnitte ohne Groß-/Kleinschreibung, damit "Europa Universalis IV" nicht zu "Europa Universalis V" passt. Ein Pfad ohne
    /// Treffer gehört zu allen Spielen.
    /// </summary>
    public static IReadOnlyList<string> GamesForPath(string path, IReadOnlyList<GameProfile> allProfiles)
    {
        var segments = SplitSegments(path);
        var matches = allProfiles.Where(profile => BelongsTo(segments, profile)).Select(profile => profile.Id).ToList();
        return matches.Count > 0 ? matches : allProfiles.Select(profile => profile.Id).ToList();
    }

    private static bool BelongsTo(string[] segments, GameProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.DisplayName) && segments.Contains(profile.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (DocumentsFolderName(profile) is { } docsName && ContainsSequence(segments, "Paradox Interactive", docsName))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(profile.SteamAppId) && ContainsSequence(segments, "steamapps", "workshop", "content", profile.SteamAppId);
    }

    /// <summary>Abschnitt nach "Paradox Interactive" in der Mod-Ordner-Vorlage, z. B. "Crusader Kings III".</summary>
    private static string? DocumentsFolderName(GameProfile profile)
    {
        var segments = SplitSegments(profile.DefaultModRootTemplate);
        var index = Array.FindIndex(segments, s => string.Equals(s, "Paradox Interactive", StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < segments.Length ? segments[index + 1] : null;
    }

    private static string[] SplitSegments(string path) =>
        path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool ContainsSequence(string[] segments, params string[] sequence)
    {
        for (var start = 0; start + sequence.Length <= segments.Length; start++)
        {
            var matches = true;
            for (var i = 0; i < sequence.Length && matches; i++)
            {
                matches = string.Equals(segments[start + i], sequence[i], StringComparison.OrdinalIgnoreCase);
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }
}
