using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ParadoxModTool.LaunchControl;

/// <summary>
/// Anzeigenamen der DLCs eines Spiels für das Playset-Fenster. Quelle sind die DLC-Ordner der Spieldateien (neben der Spiel-.exe, in <c>dlc</c> oder
/// <c>../game/dlc</c>): <c>.dlc</c> (EU IV, CK III: <c>name = "..."</c>), <c>.dlc.json</c> (Vic 3: <c>displayName.en</c>; EU V: <c>localizable_name</c>, aufgelöst über die
/// englischen Sprachdateien im DLC-Ordner). Ohne Treffer wird aus der Kennung ein lesbarer Name gebaut ("d008_fate_of_the_phoenix" wird "Fate Of The Phoenix").
/// Die Kennung im Playset darf ein Präfix tragen (Launcher: <c>ck3_dlc004_ep1</c> für den Ordner <c>dlc004_ep1</c>).
/// </summary>
public sealed class DlcNameResolver
{
    private static readonly Regex DlcFileName = new("^\\s*name\\s*=\\s*\"([^\"]*)\"", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex LocalizationLine = new("^\\s*([A-Za-z0-9_.\\-]+):\\d*\\s+\"(.*)\"\\s*$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex Markup = new("#!|#\\w+ ?", RegexOptions.Compiled);
    private static readonly Regex IdPrefix = new("^(?:[a-z0-9]+_)?(?:dlc|d)\\d+_", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly Dictionary<string, string> _byFolder;

    private DlcNameResolver(Dictionary<string, string> byFolder) => _byFolder = byFolder;

    /// <summary>Liest die DLC-Namen aus den Spieldateien neben <paramref name="gameExecutablePath"/>; ohne Pfad oder Ordner bleibt nur der Rückfall.</summary>
    public static DlcNameResolver Load(string? gameExecutablePath)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(gameExecutablePath) || Path.GetDirectoryName(Path.GetFullPath(gameExecutablePath)) is not { } exeFolder)
        {
            return new DlcNameResolver(names);
        }

        foreach (var dlcRoot in new[] { Path.Combine(exeFolder, "dlc"), Path.GetFullPath(Path.Combine(exeFolder, "..", "game", "dlc")), Path.Combine(exeFolder, "game", "dlc") })
        {
            if (!Directory.Exists(dlcRoot))
            {
                continue;
            }

            foreach (var folder in Directory.EnumerateDirectories(dlcRoot))
            {
                if (ReadName(folder) is { Length: > 0 } name)
                {
                    names.TryAdd(Path.GetFileName(folder), name);
                }
            }
        }

        return new DlcNameResolver(names);
    }

    /// <summary>Der Anzeigename zur Kennung (aus den Spieldateien, sonst aus der Kennung gebaut).</summary>
    public string Name(string id)
    {
        if (_byFolder.TryGetValue(id, out var direct))
        {
            return direct;
        }

        foreach (var (folder, name) in _byFolder)
        {
            if (id.EndsWith("_" + folder, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return Readable(id);
    }

    /// <summary>"d008_fate_of_the_phoenix" wird "Fate Of The Phoenix": Nummernpräfix weg, Unterstriche zu Leerzeichen, Wörter groß.</summary>
    public static string Readable(string id)
    {
        var text = IdPrefix.Replace(id, string.Empty).Replace('_', ' ').Trim();
        return text.Length == 0 ? id : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.ToLowerInvariant());
    }

    private static string? ReadName(string folder)
    {
        try
        {
            if (Directory.EnumerateFiles(folder, "*.dlc").FirstOrDefault() is { } classic
                && DlcFileName.Match(File.ReadAllText(classic)) is { Success: true } match)
            {
                return match.Groups[1].Value;
            }

            if (Directory.EnumerateFiles(folder, "*.dlc.json").FirstOrDefault() is { } json && JsonNode.Parse(File.ReadAllText(json)) is JsonObject root)
            {
                if (root["displayName"]?["en"] is JsonValue display && display.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }

                if (root["localizable_name"] is JsonValue key && key.TryGetValue<string>(out var keyName))
                {
                    return Localized(Path.GetDirectoryName(folder)!, keyName);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            // Ein unlesbarer DLC-Ordner bekommt den Rückfallnamen.
        }

        return null;
    }

    /// <summary>Sucht den Schlüssel in den englischen Sprachdateien unter den DLC-Ordnern (EU V legt sie unter D000_shared ab).</summary>
    private static string? Localized(string dlcRoot, string key)
    {
        foreach (var file in Directory.EnumerateFiles(dlcRoot, "*_l_english.yml", SearchOption.AllDirectories))
        {
            foreach (Match line in LocalizationLine.Matches(File.ReadAllText(file)))
            {
                if (line.Groups[1].Value == key)
                {
                    return Markup.Replace(line.Groups[2].Value, string.Empty).Trim();
                }
            }
        }

        return null;
    }
}
