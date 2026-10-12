using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>Warum kein Vorschlag entstand: der Schlüsselwert steht nicht im Beispiel, oder hinter ihm folgt keine Nummer.</summary>
public enum SuggestFailure
{
    KeyMissing,
    NumberMissing,
}

/// <summary>Vorgeschlagenes Muster oder der Grund, warum es keins gibt.</summary>
public sealed record PatternSuggestion(string? Pattern, SuggestFailure? Failure);

/// <summary>
/// "Vorschlag" in der Einlesen-Maske (Spec Teil 5b, Abschnitt 1): baut aus einem Beispiel der Mod (Pfad, Blockschlüssel, Lokalisierungsschlüssel) und dem
/// Schlüsselwert (z. B. BYZ) ein Muster. Der Schlüsselwert zählt nur als eigenes Wort (nicht in BYZANTIUM); die erste Zahl hinter ihm wird zur Nummer.
/// </summary>
public static class PatternSuggester
{
    private const string FallbackClass = "[A-Za-z0-9_]+";

    private static readonly Regex SingleClass = new(@"^\^?(\[[^\]]+\](\{\d+(,\d*)?\}|[+*]))\$?$", RegexOptions.CultureInvariant);
    private static readonly Regex Digits = new(@"\d+", RegexOptions.CultureInvariant);
    private static readonly Regex SuffixTail = new(@"^_\d+$", RegexOptions.CultureInvariant);

    /// <summary>Muster für <c>read.keys</c>: ganzer Pfad, Schlüssel als Gruppe <c>key</c>.</summary>
    public static PatternSuggestion Keys(string relativePath, string keyValue, string? keyFieldPattern)
    {
        var parts = Split(relativePath, keyValue);
        if (parts is null)
        {
            return new PatternSuggestion(null, SuggestFailure.KeyMissing);
        }

        var group = "(?<key>" + KeyClass(keyFieldPattern) + ")";
        return new PatternSuggestion("^" + string.Join(group, parts.Select(Regex.Escape)) + "$", null);
    }

    /// <summary>Pfad-Vorlage (Scriban): Schlüssel als <c>{{ keyField }}</c>, sonst wörtlich.</summary>
    public static PatternSuggestion FileTemplate(string relativePath, string keyValue, string keyField)
    {
        var parts = Split(relativePath, keyValue);
        return parts is null
            ? new PatternSuggestion(null, SuggestFailure.KeyMissing)
            : new PatternSuggestion(string.Join(Placeholder(keyField), parts), null);
    }

    /// <summary>
    /// Block- oder Lokalisierungsmuster: Schlüssel als <c>{{ keyField }}</c>, die erste Zahl hinter dem letzten Schlüssel als Gruppe <c>number</c>,
    /// ein direkt folgendes <c>_&lt;Zahl&gt;</c> am Ende als freiwillige Gruppe <c>suffix</c>.
    /// </summary>
    public static PatternSuggestion Numbered(string exampleKey, string keyValue, string keyField)
    {
        var parts = Split(exampleKey, keyValue);
        if (parts is null)
        {
            return new PatternSuggestion(null, SuggestFailure.KeyMissing);
        }

        var tail = parts[^1];
        if (Digits.Match(tail) is not { Success: true } number)
        {
            return new PatternSuggestion(null, SuggestFailure.NumberMissing);
        }

        var rest = tail[(number.Index + number.Length)..];
        var builder = new StringBuilder();
        builder.Append(string.Join(Placeholder(keyField), parts.Take(parts.Count - 1).Select(Regex.Escape)));
        builder.Append(Placeholder(keyField));
        builder.Append(Regex.Escape(tail[..number.Index]));
        builder.Append(@"(?<number>\d+)");
        builder.Append(SuffixTail.IsMatch(rest) ? @"(?<suffix>_\d+)?" : Regex.Escape(rest));
        return new PatternSuggestion(builder.ToString(), null);
    }

    /// <summary>Zeichenklasse des Schlüssels aus dem Muster des Schlüsselfelds (z. B. <c>[A-Z0-9]{2,5}</c>), sonst <c>[A-Za-z0-9_]+</c>.</summary>
    public static string KeyClass(string? keyFieldPattern) =>
        keyFieldPattern is not null && SingleClass.Match(keyFieldPattern) is { Success: true } match ? match.Groups[1].Value : FallbackClass;

    private static string Placeholder(string keyField) => "{{ " + keyField + " }}";

    /// <summary>Text zwischen den Vorkommen des Schlüssels (als eigenes Wort); <see langword="null"/>, wenn er nicht vorkommt.</summary>
    private static List<string>? Split(string text, string keyValue)
    {
        if (keyValue.Length == 0)
        {
            return null;
        }

        var parts = new List<string>();
        var start = 0;
        var index = 0;
        while ((index = text.IndexOf(keyValue, index, StringComparison.Ordinal)) >= 0)
        {
            var end = index + keyValue.Length;
            if ((index == 0 || !IsWordChar(text[index - 1])) && (end == text.Length || !IsWordChar(text[end])))
            {
                parts.Add(text[start..index]);
                start = end;
                index = end;
            }
            else
            {
                index++;
            }
        }

        if (parts.Count == 0)
        {
            return null;
        }

        parts.Add(text[start..]);
        return parts;
    }

    private static bool IsWordChar(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
}
