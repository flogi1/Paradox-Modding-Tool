using System.Text.Json;
using ParadoxModTool.Mods;
using ParadoxModTool.SteamIntegration;

namespace ParadoxModTool.ModOverview;

/// <summary>
/// Findet den Installationsordner eines Spiels, ohne dessen PMT zu fragen: zuerst die in dessen <c>session.json</c> eingestellte Spiel-Exe, sonst das
/// <c>appmanifest_&lt;AppID&gt;.acf</c> in den Steam-Bibliotheken.
/// </summary>
public static class GameInstallLocator
{
    public static string? Find(string gameDirectory, IEnumerable<string> libraryFolders, string? appId) =>
        FromSession(gameDirectory) ?? FromSteam(libraryFolders, appId);

    /// <summary>Aus <c>GameExecutablePath</c> in <c>&lt;gameDirectory&gt;\session.json</c>; <see langword="null"/>, wenn Datei, Wert oder Ordner fehlen.</summary>
    public static string? FromSession(string gameDirectory)
    {
        try
        {
            var file = Path.Combine(gameDirectory, "session.json");
            if (!File.Exists(file))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("GameExecutablePath", out var exe) || exe.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return GameContentRoot.InstallRoot(exe.GetString()) is { } root && Directory.Exists(root) ? root : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Der Ordner <c>steamapps\common\&lt;installdir&gt;</c> der ersten Bibliothek mit passendem Manifest, wenn er existiert.</summary>
    public static string? FromSteam(IEnumerable<string> libraryFolders, string? appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return null;
        }

        foreach (var library in libraryFolders)
        {
            var manifest = Path.Combine(library, "steamapps", $"appmanifest_{appId}.acf");
            if (!File.Exists(manifest))
            {
                continue;
            }

            string? installDir;
            try
            {
                installDir = VdfNode.Parse(File.ReadAllText(manifest))["AppState"]?["installdir"]?.Value;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(installDir) || installDir.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || installDir.Contains(".."))
            {
                continue;
            }

            var game = Path.Combine(library, "steamapps", "common", installDir);
            if (Directory.Exists(game))
            {
                return game;
            }
        }

        return null;
    }
}
