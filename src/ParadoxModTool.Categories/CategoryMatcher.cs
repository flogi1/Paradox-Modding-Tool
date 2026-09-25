using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Categories;

/// <summary>Ordnet Dateipfade den Kategorien zu und bereinigt Kategorie-Listen.</summary>
public static class CategoryMatcher
{
    private static readonly ConcurrentDictionary<string, Regex> PatternCache = new();

    /// <summary>
    /// Kennung der ersten Kategorie in Listenreihenfolge, deren Endung oder Muster passt; sonst <see cref="CategoryDefaults.OtherId"/>.
    /// Die Regeln der Fallback-Kategorie werden ignoriert.
    /// </summary>
    public static string Match(IReadOnlyList<CategoryDefinition> categories, string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return CategoryDefaults.OtherId;
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var normalizedPath = filePath.Replace('\\', '/');

        foreach (var category in categories)
        {
            if (category.Id == CategoryDefaults.OtherId)
            {
                continue;
            }

            if (extension.Length > 0 && category.Extensions.Contains(extension))
            {
                return category.Id;
            }

            foreach (var pattern in category.Patterns)
            {
                if (PatternMatches(pattern, normalizedPath))
                {
                    return category.Id;
                }
            }
        }

        return CategoryDefaults.OtherId;
    }

    /// <summary>
    /// Ein Muster passt, wenn es den ganzen Pfad oder den Rest ab einer beliebigen Ordnergrenze trifft,
    /// z. B. <c>events/*</c> oder <c>*_decisions.txt</c>. <c>*</c> steht für beliebig viele Zeichen (auch Ordnertrenner), <c>?</c> für genau eines.
    /// Groß-/Kleinschreibung wird ignoriert. Der Pfad muss bereits mit "/" statt "\" vorliegen.
    /// </summary>
    public static bool PatternMatches(string pattern, string normalizedPath)
    {
        var trimmed = pattern.Trim().Replace('\\', '/');
        if (trimmed.Length == 0)
        {
            return false;
        }

        var regex = PatternCache.GetOrAdd(trimmed, static p =>
        {
            var builder = new StringBuilder("(^|/)");
            foreach (var c in p)
            {
                builder.Append(c switch
                {
                    '*' => ".*",
                    '?' => ".",
                    _ => Regex.Escape(c.ToString()),
                });
            }

            builder.Append('$');
            return new Regex(builder.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        });

        return regex.IsMatch(normalizedPath);
    }

    /// <summary>Endung normalisieren: Kleinbuchstaben, führender Punkt. Leer bleibt leer.</summary>
    public static string NormalizeExtension(string extension)
    {
        var trimmed = extension.Trim().ToLowerInvariant();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
    }

    /// <summary>Endungen aus Text (Komma, Semikolon, Leerzeichen oder Zeilenumbruch getrennt): normalisiert, ohne Duplikate.</summary>
    public static List<string> ParseExtensions(string text) =>
        text.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeExtension)
            .Where(e => e.Length > 1)
            .Distinct()
            .ToList();

    /// <summary>Muster aus Text, eines pro Zeile; leere Zeilen und Duplikate entfallen.</summary>
    public static List<string> ParsePatterns(string text) =>
        text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Bereinigt eine Kategorie-Liste: ohne Kennung oder Duplikat entfällt der Eintrag, leere Namen bekommen die Kennung,
    /// ungültige Farben den Standardwert, Endungen werden normalisiert. Die Fallback-Kategorie wird ergänzt und ans Ende gestellt.
    /// Eine <see langword="null"/>-Liste ergibt die Standardliste.
    /// </summary>
    public static List<CategoryDefinition> Normalize(IEnumerable<CategoryDefinition>? source)
    {
        if (source is null)
        {
            return CategoryDefaults.Create();
        }

        var result = new List<CategoryDefinition>();
        var seen = new HashSet<string>();
        foreach (var item in source)
        {
            if (string.IsNullOrWhiteSpace(item.Id) || !seen.Add(item.Id))
            {
                continue;
            }

            var copy = item.Clone();
            copy.Name = string.IsNullOrWhiteSpace(copy.Name) ? copy.Id : copy.Name.Trim();
            copy.ColorHex = IsValidHex(copy.ColorHex) ? copy.ColorHex : "#808080";
            copy.Extensions = copy.Extensions.Select(NormalizeExtension).Where(e => e.Length > 1).Distinct().ToList();
            copy.Patterns = copy.Patterns.Select(p => p.Trim()).Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            result.Add(copy);
        }

        var other = result.FirstOrDefault(c => c.Id == CategoryDefaults.OtherId)
            ?? CategoryDefaults.Create().First(c => c.Id == CategoryDefaults.OtherId);
        result.RemoveAll(c => c.Id == CategoryDefaults.OtherId);
        result.Add(other);
        return result;
    }

    public static bool IsValidHex(string? hex) =>
        hex is { Length: 7 } && hex[0] == '#' && hex.Skip(1).All(Uri.IsHexDigit);

    /// <summary>Neue Kennung für eine eigene Kategorie.</summary>
    public static string NewCustomId() => "c-" + Guid.NewGuid().ToString("N")[..8];
}
