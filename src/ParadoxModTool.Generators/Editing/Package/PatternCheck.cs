using System.Text.RegularExpressions;
using Scriban;
using Scriban.Parsing;

namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>
/// Sofort-Prüfung der Muster und Pfade in "Einlesen" und "Nachschlagen" (Spec Teil 5b); dieselben Prüfungen nutzt "Prüfen". Scriban-Teile werden erst
/// gelesen, dann durch einen Platzhalter ersetzt und der Rest als Regex übersetzt. <see langword="null"/> = in Ordnung, sonst die Meldung.
/// </summary>
public static class PatternCheck
{
    private static readonly Regex ScribanPart = new(@"\{\{.*?\}\}", RegexOptions.CultureInvariant | RegexOptions.Singleline);

    /// <summary><c>read.keys</c>: Regex auf Mod-Pfade (ohne Scriban) mit Gruppe <c>key</c>.</summary>
    public static string? Keys(string pattern) => RegexProblem(pattern, "key");

    /// <summary>Block- und Lokalisierungsmuster (Scriban + Regex) mit Gruppe <c>number</c>.</summary>
    public static string? Numbered(string template) => ScribanProblem(template) ?? RegexProblem(Strip(template), "number");

    /// <summary>
    /// Wie <see cref="Numbered(string)"/>; mit <paramref name="suffixAlone"/> (zählbare oberste Ebene) genügt statt <c>number</c> auch die Gruppe <c>suffix</c>:
    /// dann wählt die Endung allein das Element (siehe <c>ElementAddress</c>).
    /// </summary>
    public static string? Numbered(string template, bool suffixAlone)
    {
        if (!suffixAlone || ScribanProblem(template) is not null || Compile(Strip(template), out _) is not { } regex)
        {
            return Numbered(template);
        }

        return regex.GetGroupNames().Contains("suffix", StringComparer.Ordinal) ? null : Numbered(template);
    }

    /// <summary>Muster aus Scriban und Regex ohne Pflichtgruppe (Treffer-Muster, Schlüssel-Muster, Blöcke von Kopf-Feldern).</summary>
    public static string? Matching(string template) =>
        template.Trim().Length == 0 ? GeneratorsTexts.PatternEmpty : ScribanProblem(template) ?? (Compile(Strip(template), out var problem) is null ? problem : null);

    /// <summary><c>read.values[].pattern</c>: Scriban + Regex mit Gruppe <c>value</c>.</summary>
    public static string? Value(string template) => ScribanProblem(template) ?? RegexProblem(Strip(template), "value");

    /// <summary><c>read.keys</c> als Objekt: <c>file</c> ist ein reiner Regex ohne Pflichtgruppe.</summary>
    public static string? KeysFile(string pattern) => pattern.Trim().Length == 0 ? GeneratorsTexts.PatternEmpty : Compile(pattern, out var problem) is null ? problem : null;

    /// <summary><c>read.keys.block</c>: Regex mit Gruppe <c>key</c> oder je Schlüsselfeld einer Gruppe dieses Namens.</summary>
    public static string? KeysBlock(string pattern, IReadOnlyList<string> keyFields)
    {
        if (pattern.Trim().Length == 0)
        {
            return GeneratorsTexts.PatternEmpty;
        }

        if (Compile(pattern, out var problem) is not { } regex)
        {
            return problem;
        }

        var groups = regex.GetGroupNames();
        return groups.Contains("key", StringComparer.Ordinal) || keyFields.All(f => groups.Contains(f, StringComparer.Ordinal))
            ? null
            : GeneratorsTexts.PatternGroupMissing(string.Join(" / ", new[] { "key" }.Concat(keyFields)));
    }

    /// <summary><c>tagFromFile</c>: Regex mit mindestens einer Gruppe (Gruppe 1 ist der Tag).</summary>
    public static string? TagFromFile(string pattern)
    {
        if (Compile(pattern, out var problem) is not { } regex)
        {
            return problem;
        }

        return regex.GetGroupNumbers().Length > 1 ? null : GeneratorsTexts.PatternNeedsGroup;
    }

    /// <summary>Pfad-Vorlage: nicht leer, Scriban lesbar, relativ ohne <c>..</c>.</summary>
    public static string? Path(string template)
    {
        if (template.Trim().Length == 0)
        {
            return GeneratorsTexts.PatternEmpty;
        }

        return ScribanProblem(template) ?? (SafeRelativePath.IsSafe(Strip(template).Trim()) ? null : GeneratorsTexts.PathUnsafe(template));
    }

    private static string Strip(string template) => ScribanPart.Replace(template, "x");

    private static string? ScribanProblem(string template)
    {
        // Scriban nimmt ein offenes "{{ tag" ohne Fehler hin; übrig gebliebene Klammern nach dem Ersetzen sind aber immer ein Tippfehler.
        if (Strip(template).Contains("{{", StringComparison.Ordinal) || Strip(template).Contains("}}", StringComparison.Ordinal))
        {
            return GeneratorsTexts.PatternUnclosed;
        }

        var parsed = Template.Parse(template);
        return parsed.Messages.FirstOrDefault(m => m.Type == ParserMessageType.Error) is { } message ? GeneratorsTexts.PatternInvalid(message.Message) : null;
    }

    private static string? RegexProblem(string pattern, string group)
    {
        if (pattern.Trim().Length == 0)
        {
            return GeneratorsTexts.PatternEmpty;
        }

        if (Compile(pattern, out var problem) is not { } regex)
        {
            return problem;
        }

        return regex.GetGroupNames().Contains(group, StringComparer.Ordinal) ? null : GeneratorsTexts.PatternGroupMissing(group);
    }

    private static Regex? Compile(string pattern, out string? problem)
    {
        try
        {
            problem = null;
            return new Regex(pattern, RegexOptions.CultureInvariant);
        }
        catch (ArgumentException ex)
        {
            problem = GeneratorsTexts.PatternInvalid(ex.Message);
            return null;
        }
    }
}
