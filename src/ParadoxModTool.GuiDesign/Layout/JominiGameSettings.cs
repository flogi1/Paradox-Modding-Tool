using System.Globalization;
using System.Text;
using System.Text.Json;
using ParadoxModTool.GuiDesign.Classic;

namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>Bildschirmeinstellungen eines Jomini-Spiels; Breite und Höhe fehlen, wenn die Datei keine feste Auflösung nennt.</summary>
public sealed record GameScreenSettings(int? Width, int? Height, double UiScale, string? Language);

/// <summary>
/// Liest Auflösung, UI-Skalierung und Sprache aus den Spieleinstellungen im Benutzerordner eines Jomini-Spiels: <c>pdx_settings.txt</c> (Crusader Kings III)
/// oder <c>pdx_settings.json</c> (Victoria 3, Europa Universalis V). Die UI-Skalierung steht unter <c>GUI.scale</c>, nicht unter <c>render_scale</c>.
/// Alle Methoden werfen nicht; unlesbare Eingaben ergeben <see langword="null"/>.
/// </summary>
public static class JominiGameSettings
{
    /// <summary>Liest <c>pdx_settings.txt</c>, sonst <c>pdx_settings.json</c> im Benutzerordner; <see langword="null"/>, wenn keine von beiden lesbar ist.</summary>
    public static GameScreenSettings? TryRead(string userFolder)
    {
        if (string.IsNullOrWhiteSpace(userFolder))
        {
            return null;
        }

        try
        {
            var txt = Path.Combine(userFolder, "pdx_settings.txt");
            if (File.Exists(txt) && ParseTxt(File.ReadAllText(txt, Encoding.UTF8)) is { } fromTxt)
            {
                return fromTxt;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Unlesbar: mit der JSON-Datei weitermachen.
        }

        try
        {
            var json = Path.Combine(userFolder, "pdx_settings.json");
            return File.Exists(json) ? ParseJson(File.ReadAllText(json, Encoding.UTF8)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Liest den Text einer <c>pdx_settings.txt</c>. Die Einträge sind Blöcke <c>"name"={ version=… value=… }</c>, die in Gruppen (<c>"Graphics"</c>,
    /// <c>"GUI"</c>, <c>"System"</c>) liegen; gesucht wird über den Namen des Eintrags, die Sprache in jeder Tiefe. <see langword="null"/>, wenn kein bekannter
    /// Eintrag (GUI-Skalierung, Sprache, Anzeigemodus, Auflösung) vorkommt, etwa bei leerem oder fremdem Text.
    /// </summary>
    public static GameScreenSettings? ParseTxt(string text)
    {
        if (text is null)
        {
            return null;
        }

        var tokens = ClassicScreenSettings.Tokens(text).ToList();
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? guiScale = null;

        // Pfad der offenen Blöcke; ein "value" gehört zum innersten Block (dem Eintragsnamen).
        var path = new List<string>();
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token == "}")
            {
                if (path.Count > 0)
                {
                    path.RemoveAt(path.Count - 1);
                }

                continue;
            }

            if (token == "{")
            {
                // Block ohne Schlüssel: namenlos mitzählen.
                path.Add(string.Empty);
                continue;
            }

            if (token == "=" || i + 2 >= tokens.Count || tokens[i + 1] != "=")
            {
                continue;
            }

            var value = tokens[i + 2];
            i += 2;
            if (value == "{")
            {
                path.Add(token);
                continue;
            }

            if (path.Count == 0 || !token.Equals("value", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entry = path[^1];
            values.TryAdd(entry, value);
            if (entry.Equals("scale", StringComparison.OrdinalIgnoreCase) && path.Count >= 2 && path[^2].Equals("GUI", StringComparison.OrdinalIgnoreCase))
            {
                guiScale ??= value;
            }
        }

        int? width = null;
        int? height = null;
        var resolutionKey = values.GetValueOrDefault("display_mode") switch
        {
            { } mode when mode.Equals("fullscreen", StringComparison.OrdinalIgnoreCase) => "fullscreen_resolution",
            { } mode when mode.Equals("windowed", StringComparison.OrdinalIgnoreCase) => "windowed_resolution",
            _ => null,
        };
        if (resolutionKey is not null && values.TryGetValue(resolutionKey, out var resolution) && Resolution(resolution) is { } size)
        {
            (width, height) = size;
        }

        // Kein bekannter Eintrag gefunden: leere oder fremde Datei, nicht als Einstellungen werten.
        if (guiScale is null && !new[] { "display_mode", "fullscreen_resolution", "windowed_resolution", "language" }.Any(values.ContainsKey))
        {
            return null;
        }

        return new GameScreenSettings(width, height, Scale(guiScale), values.GetValueOrDefault("language"));
    }

    /// <summary>
    /// Liest den Text einer <c>pdx_settings.json</c>: <c>GUI.scale</c> (Zeichenkette oder Zahl) und <c>System.language</c>; eine Auflösung nennt die Datei nicht.
    /// <see langword="null"/> bei ungültigem JSON.
    /// </summary>
    public static GameScreenSettings? ParseJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? scale = null;
            if (Child(root, "GUI") is { } gui && Child(gui, "scale") is { } scaleElement)
            {
                scale = scaleElement.ValueKind switch
                {
                    JsonValueKind.String => scaleElement.GetString(),
                    JsonValueKind.Number => scaleElement.GetRawText(),
                    _ => null,
                };
            }

            string? language = null;
            if (Child(root, "System") is { } system && Child(system, "language") is { ValueKind: JsonValueKind.String } languageElement)
            {
                language = languageElement.GetString();
            }

            return new GameScreenSettings(null, null, Scale(scale), language);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return null;
        }
    }

    private static JsonElement? Child(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var child) ? child : null;

    /// <summary>Skalierung als Zahl mit Punkt; fehlend, nicht lesbar (auch mit Komma) oder nicht positiv ergibt 1.</summary>
    private static double Scale(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale) && scale > 0 && double.IsFinite(scale) ? scale : 1;

    /// <summary>Auflösung in der Form <c>2560x1440</c>.</summary>
    private static (int Width, int Height)? Resolution(string value)
    {
        var parts = value.Split('x', 'X');
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) && width > 0
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height) && height > 0
            ? (width, height)
            : null;
    }
}
