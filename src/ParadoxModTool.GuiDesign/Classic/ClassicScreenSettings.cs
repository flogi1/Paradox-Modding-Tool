using System.Globalization;
using System.Text;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// Liest Auflösung und UI-Skalierung aus der <c>settings.txt</c> eines klassischen Spiels (Benutzerordner, Elternordner des Mod-Ordners). Gelesen werden nur
/// <c>graphics = { size = { x = … y = … } }</c> und im selben Block <c>gui_scale</c> (HOI IV) bzw. <c>game_ui_scale</c> (EU IV). <c>min_gui</c> und
/// alles außerhalb von <c>graphics</c> zählen nicht.
/// </summary>
public static class ClassicScreenSettings
{
    /// <summary>
    /// Pfad der <c>settings.txt</c> zum Mod-Ordner des Spielprofils (<c>%USERPROFILE%\Documents\Paradox Interactive\&lt;Spiel&gt;\mod</c>): sein Elternordner
    /// plus <c>settings.txt</c>; <see langword="null"/> ohne Mod-Ordner.
    /// </summary>
    public static string? PathFor(string? defaultModRootTemplate)
    {
        if (string.IsNullOrWhiteSpace(defaultModRootTemplate))
        {
            return null;
        }

        var modRoot = Environment.ExpandEnvironmentVariables(defaultModRootTemplate).Replace('/', '\\').TrimEnd('\\');
        return Path.GetDirectoryName(modRoot) is { Length: > 0 } userData ? Path.Combine(userData, "settings.txt") : null;
    }

    /// <summary>Breite, Höhe und Skalierung (fehlt die Skalierung: 1); <see langword="null"/>, wenn die Datei fehlt, unlesbar ist oder keine Größe nennt.</summary>
    public static (int Width, int Height, double UiScale)? TryRead(string settingsTxtPath)
    {
        string text;
        try
        {
            if (!File.Exists(settingsTxtPath))
            {
                return null;
            }

            text = File.ReadAllText(settingsTxtPath, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }

        return Parse(text);
    }

    private static (int Width, int Height, double UiScale)? Parse(string text)
    {
        var tokens = Tokens(text).ToList();
        int? width = null;
        int? height = null;
        double scale = 1;

        // Pfad der offenen Blöcke; Werte zählen nur auf der passenden Tiefe (graphics/size bzw. graphics).
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
                // Block ohne Schlüssel (Liste in einer Liste): namenlos mitzählen.
                path.Add(string.Empty);
                continue;
            }

            if (token == "=" || i + 2 >= tokens.Count || tokens[i + 1] != "=")
            {
                continue;
            }

            var value = tokens[i + 2];
            if (value == "{")
            {
                path.Add(token);
                i += 2;
                continue;
            }

            i += 2;
            if (Is(path, "graphics", "size"))
            {
                if (token.Equals("x", StringComparison.OrdinalIgnoreCase) && Int(value) is { } x)
                {
                    width = x;
                }
                else if (token.Equals("y", StringComparison.OrdinalIgnoreCase) && Int(value) is { } y)
                {
                    height = y;
                }
            }
            else if (Is(path, "graphics")
                && (token.Equals("gui_scale", StringComparison.OrdinalIgnoreCase) || token.Equals("game_ui_scale", StringComparison.OrdinalIgnoreCase))
                && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var s) && s > 0 && double.IsFinite(s))
            {
                scale = s;
            }
        }

        return width is > 0 && height is > 0 ? (width.Value, height.Value, scale) : null;
    }

    private static bool Is(List<string> path, params string[] expected) =>
        path.Count == expected.Length && path.Zip(expected).All(p => p.First.Equals(p.Second, StringComparison.OrdinalIgnoreCase));

    private static int? Int(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number
        : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d) ? (int)Math.Round(d) : null;

    /// <summary>Wörter, Zeichenketten (ohne Anführungszeichen), <c>{</c>, <c>}</c> und <c>=</c>; <c>#</c> leitet einen Kommentar bis zum Zeilenende ein.</summary>
    internal static IEnumerable<string> Tokens(string text)
    {
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '#')
            {
                while (i < text.Length && text[i] != '\n')
                {
                    i++;
                }
            }
            else if (c is '{' or '}' or '=')
            {
                yield return c.ToString();
                i++;
            }
            else if (c == '"')
            {
                var end = text.IndexOf('"', i + 1);
                end = end < 0 ? text.Length : end;
                yield return text[(i + 1)..end];
                i = end + 1;
            }
            else
            {
                var start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('{' or '}' or '=' or '"' or '#'))
                {
                    i++;
                }

                yield return text[start..i];
            }
        }
    }
}
