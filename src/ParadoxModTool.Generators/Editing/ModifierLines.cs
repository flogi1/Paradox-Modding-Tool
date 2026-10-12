using System.Globalization;

namespace ParadoxModTool.Generators.Editing;

/// <summary>
/// Bearbeitet den Text eines Modifikatorfelds (Zeilen <c>schlüssel = wert</c>, Spec 2026-10-05-generator-packages-lookups, Abschnitt 2): einfügen,
/// entfernen, auf höhere Stufen kopieren und Werte skalieren. Kommentare, Leerzeilen und andere Zeilen bleiben unverändert stehen.
/// </summary>
public static class ModifierLines
{
    /// <summary>Startwert eines neu eingefügten Modifiers: <c>yes</c> bei ja/nein, <c>-0.1</c> bei schädlichen, sonst <c>0.1</c>.</summary>
    public static string StartValue(bool boolean, bool bad) => boolean ? "yes" : bad ? "-0.1" : "0.1";

    /// <summary>Ob eine Zeile <c>key = …</c> im Text steht (Groß-/Kleinschreibung egal, Einrückung und Kommentar ignoriert).</summary>
    public static bool HasKey(string text, string key) => Lines(text).Any(line => IsKeyLine(line, key));

    /// <summary>Die Schlüssel der Zeilen <c>key = …</c> in Textreihenfolge, ohne Kommentare und ohne Doppelte (Groß-/Kleinschreibung egal).</summary>
    public static IReadOnlyList<string> Keys(string text)
    {
        var keys = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in Lines(text))
        {
            var comment = line.IndexOf('#');
            var content = (comment >= 0 ? line[..comment] : line).Trim();
            var equals = content.IndexOf('=');
            if (equals > 0 && content[..equals].Trim() is { Length: > 0 } key && seen.Add(key))
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    /// <summary>Hängt <c>key = value</c> als neue Zeile an; leerer Text wird ersetzt, ein vorhandener Schlüssel lässt den Text unverändert.</summary>
    public static string Insert(string text, string key, string value)
    {
        if (HasKey(text, key))
        {
            return text;
        }

        var line = $"{key} = {value}";
        var rest = Normalize(text).TrimEnd();
        return rest.Length == 0 ? line : rest + "\n" + line;
    }

    /// <summary>Entfernt alle Zeilen <c>key = …</c>; alle anderen Zeilen bleiben in ihrer Reihenfolge.</summary>
    public static string Remove(string text, string key) => string.Join("\n", Lines(text).Where(line => !IsKeyLine(line, key)));

    /// <summary>Wert × <paramref name="toLevel"/> / <paramref name="fromLevel"/> als <c>decimal</c>, ohne Nullen am Ende; nicht-numerische Werte unverändert.</summary>
    public static string Scale(string value, int fromLevel, int toLevel)
    {
        if (fromLevel <= 0 || !decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return value;
        }

        return (number * toLevel / fromLevel).ToString("0.############", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Schreibt den Modifier in alle Stufen über <paramref name="insertLevel"/> (1-basiert), skaliert oder unverändert; Stufen, die den Schlüssel schon haben,
    /// und alle Stufen bis einschließlich <paramref name="insertLevel"/> bleiben, wie sie sind.
    /// </summary>
    public static IReadOnlyList<string> CopyToHigherLevels(IReadOnlyList<string> levelTexts, int insertLevel, string key, string value, bool scale) =>
        levelTexts.Select((text, i) => i + 1 > insertLevel ? Insert(text, key, scale ? Scale(value, insertLevel, i + 1) : value) : text).ToList();

    /// <summary>Entfernt den Schlüssel aus allen Stufen.</summary>
    public static IReadOnlyList<string> RemoveFromAll(IReadOnlyList<string> levelTexts, string key) => levelTexts.Select(text => Remove(text, key)).ToList();

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    private static string[] Lines(string text) => Normalize(text).Split('\n');

    private static bool IsKeyLine(string line, string key)
    {
        var comment = line.IndexOf('#');
        var content = (comment >= 0 ? line[..comment] : line).Trim();
        var equals = content.IndexOf('=');
        return equals > 0 && string.Equals(content[..equals].Trim(), key, StringComparison.OrdinalIgnoreCase);
    }
}
