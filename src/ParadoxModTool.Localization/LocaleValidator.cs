using System.Text.RegularExpressions;

namespace ParadoxModTool.Localization;

/// <summary>Ein Text, dessen Platzhalter nicht zur deutschen Referenz passen (Reihenfolge egal, Formatangaben zählen mit).</summary>
public sealed record PlaceholderMismatch(string Key, IReadOnlyList<string> Expected, IReadOnlyList<string> Found);

/// <summary>Prüfbericht einer Sprachdatei gegen die deutsche Referenz.</summary>
public sealed record LocaleReport(
    int Total,
    int Translated,
    IReadOnlyList<string> MissingKeys,
    IReadOnlyList<string> UnknownKeys,
    IReadOnlyList<PlaceholderMismatch> PlaceholderMismatches,
    IReadOnlyList<string> LongTexts,
    LocaleParseError? ParseError,
    bool MachineTranslated)
{
    /// <summary>JSON-Fehler, unbekannte Keys oder falsche Platzhalter. Fehlende Keys und lange Texte sind keine Fehler.</summary>
    public bool HasErrors => ParseError is not null || UnknownKeys.Count > 0 || PlaceholderMismatches.Count > 0;

    /// <summary>Übersetzter Anteil in Prozent, abgerundet (99,9 % zeigt 99).</summary>
    public int Percent => Total == 0 ? 100 : Translated * 100 / Total;
}

/// <summary>
/// Prüft Sprachdateien gegen die deutsche Referenz (<c>de.json</c> im Programmordner): fehlende und unbekannte Keys, Platzhalter und auffällig
/// lange Texte. Ohne UI-Abhängigkeit; Sprachseite, Tests und LocaleTool nutzen denselben Code.
/// </summary>
public static partial class LocaleValidator
{
    /// <summary>Texte, die mehr als so viel länger sind als der englische, gelten als Hinweis "lang".</summary>
    private const double LongFactor = 1.6;

    /// <summary>Kürzere englische Texte (etwa "OK") werden nicht auf Länge geprüft.</summary>
    private const int LongMinimumEnglishLength = 10;

    public static LocaleReport Check(string path, IReadOnlyDictionary<string, string> reference, IReadOnlyDictionary<string, string> english)
    {
        var file = UiTextTable.ReadFile(path);
        if (file.Error is not null)
        {
            return new LocaleReport(reference.Count, 0, [], [], [], [], file.Error, file.MachineTranslated);
        }

        var missing = new List<string>();
        var mismatches = new List<PlaceholderMismatch>();
        var longTexts = new List<string>();
        foreach (var (key, source) in reference)
        {
            if (!file.Texts.TryGetValue(key, out var text) || text.Length == 0)
            {
                missing.Add(key);
                continue;
            }

            var expected = Placeholders(source);
            var found = Placeholders(text);
            if (!expected.SequenceEqual(found, StringComparer.Ordinal))
            {
                mismatches.Add(new PlaceholderMismatch(key, expected, found));
            }

            if (english.TryGetValue(key, out var englishText) && englishText.Length >= LongMinimumEnglishLength && text.Length > englishText.Length * LongFactor)
            {
                longTexts.Add(key);
            }
        }

        var unknown = file.Texts.Keys.Where(key => !reference.ContainsKey(key)).ToList();
        return new LocaleReport(reference.Count, reference.Count - missing.Count, missing, unknown, mismatches, longTexts, null, file.MachineTranslated);
    }

    /// <summary>Bericht für eine Sprache aus <paramref name="sources"/>; <see langword="null"/>, wenn es keine Datei für sie gibt.</summary>
    public static LocaleReport? CheckLanguage(LocaleSources sources, string code) =>
        sources.Resolve(code) is { } file
            ? Check(file.Path, UiTextTable.Read(sources.ProgramFile(UiText.SourceLanguage)), UiTextTable.Read(sources.ProgramFile("en")))
            : null;

    /// <summary>Platzhalter wie {0}, {1:g}, {2,-5} eines Textes, sortiert; maskierte Klammern {{ }} zählen nicht.</summary>
    public static IReadOnlyList<string> Placeholders(string text) =>
        PlaceholderPattern().Matches(text.Replace("{{", string.Empty).Replace("}}", string.Empty))
            .Select(match => match.Value)
            .Order(StringComparer.Ordinal)
            .ToList();

    [GeneratedRegex(@"\{\d+(,-?\d+)?(:[^{}]*)?\}")]
    private static partial Regex PlaceholderPattern();
}
