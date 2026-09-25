using System.Text.Json;

namespace ParadoxModTool.LaunchControl;

/// <summary>Ordner eines Spiels, an denen PMT erkennt, zu welchem Spiel eine Datei gehört (Spec: Abgleich mit Spielordner und Mod-Ordnern).</summary>
public sealed record GameFolders(string GameId, IReadOnlyList<string> Roots, string? SteamAppId);

/// <summary>Was das Spielprofil beiträgt: Kennung, Standard-Mod-Ordner (Umgebungsvariablen aufgelöst) und Steam-App-ID.</summary>
public sealed record GameProfileFolders(string GameId, string? DefaultModRoot, string? SteamAppId);

public static class EditorGameMatcher
{
    /// <summary>
    /// Spiel der Datei (Plan 4 Ruling 2): der längste passende Ordner gewinnt, ohne Groß-/Kleinschreibung, mit "/" und "\"; ein Workshop-Ordner
    /// <c>steamapps\workshop\content\&lt;appid&gt;</c> zählt über die Steam-App-ID. Null, wenn keiner passt.
    /// </summary>
    public static string? GameFor(string filePath, IEnumerable<GameFolders> games)
    {
        var full = Normalize(filePath);
        string? best = null;
        var bestLength = -1;
        foreach (var game in games)
        {
            foreach (var root in game.Roots.Where(r => !string.IsNullOrWhiteSpace(r)))
            {
                var prefix = Normalize(root).TrimEnd('\\') + "\\";
                if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && prefix.Length > bestLength)
                {
                    best = game.GameId;
                    bestLength = prefix.Length;
                }
            }

            if (game.SteamAppId is { Length: > 0 } appId)
            {
                var marker = $"\\steamapps\\workshop\\content\\{appId}\\";
                var at = full.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (at >= 0 && at + marker.Length > bestLength)
                {
                    best = game.GameId;
                    bestLength = at + marker.Length;
                }
            }
        }

        return best;
    }

    private static string Normalize(string path) => path.Trim().Replace('/', '\\');
}

/// <summary>
/// Liest die Ordner aller Spiele für <see cref="EditorGameMatcher"/>, nur lesend (Plan 4 Ruling 2): Installationsordner aus
/// <c>&lt;spiel&gt;\session.json</c>, Dokumentenordner des Spiels und Ordner der eingestellten playsets.json, registrierte Mods aus <c>&lt;spiel&gt;\mods.json</c>. Kaputte oder gesperrte
/// Dateien werden übergangen; es wird nie geschrieben (anders als <c>ModRegistry.Load</c>, das <c>mods.json.bad</c> anlegt).
/// </summary>
public static class GameFolderReader
{
    public static IReadOnlyList<GameFolders> Read(string gamesDirectory, IEnumerable<GameProfileFolders> profiles)
    {
        var result = new List<GameFolders>();
        foreach (var profile in profiles)
        {
            var roots = new List<string>();
            if (!string.IsNullOrWhiteSpace(profile.DefaultModRoot))
            {
                var modRoot = profile.DefaultModRoot.Replace('/', '\\').TrimEnd('\\');
                roots.Add(Path.GetDirectoryName(modRoot) ?? modRoot);
            }

            var directory = Path.Combine(gamesDirectory, profile.GameId);
            roots.AddRange(ReadJson(Path.Combine(directory, "session.json"), SessionRoots) ?? Array.Empty<string>());
            roots.AddRange(ReadJson(Path.Combine(directory, "mods.json"), ModPaths) ?? Array.Empty<string>());
            result.Add(new GameFolders(profile.GameId, roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), profile.SteamAppId));
        }

        return result;
    }

    /// <summary>Installationsordner zur Spiel-Exe: ihr Ordner, bei <c>binaries</c> (EU V) dessen Elternordner.</summary>
    public static string? InstallRoot(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || Path.GetDirectoryName(executablePath.Replace('/', '\\')) is not { Length: > 0 } folder)
        {
            return null;
        }

        return string.Equals(Path.GetFileName(folder), "binaries", StringComparison.OrdinalIgnoreCase) ? Path.GetDirectoryName(folder) : folder;
    }

    /// <summary>Installationsordner (aus der Spiel-Exe) und Ordner der eingestellten playsets.json (echter Dokumentenordner, auch bei OneDrive).</summary>
    private static IReadOnlyList<string>? SessionRoots(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var roots = new List<string>();
        if (root.TryGetProperty("GameExecutablePath", out var exe) && exe.ValueKind == JsonValueKind.String && InstallRoot(exe.GetString()) is { } install)
        {
            roots.Add(install);
        }

        if (root.TryGetProperty("PlaysetsFilePath", out var playsets) && playsets.ValueKind == JsonValueKind.String
            && playsets.GetString() is { Length: > 0 } file && Path.GetDirectoryName(file.Replace('/', '\\')) is { Length: > 0 } documents)
        {
            roots.Add(documents);
        }

        return roots;
    }

    private static IReadOnlyList<string>? ModPaths(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("mods", out var mods) || mods.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var paths = new List<string>();
        foreach (var mod in mods.EnumerateArray())
        {
            if (mod.ValueKind == JsonValueKind.Object && mod.TryGetProperty("paths", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                paths.AddRange(list.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.String).Select(p => p.GetString()!).Where(p => p.Length > 0));
            }
        }

        return paths;
    }

    private static T? ReadJson<T>(string path, Func<JsonElement, T?> read)
        where T : class
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            return read(document.RootElement);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
