using System.Globalization;
using System.Text.Json;

namespace ParadoxModTool.Localization;

/// <summary>
/// Oberflächentexte einer Sprache aus den community-editierbaren Dateien <c>locales/&lt;sprache&gt;.json</c> (flache Schlüssel,
/// Architektur-Spezifikation Abschnitt 14). Reihenfolge: gewählte Sprache, dann Deutsch (Quellsprache), dann der im Code mitgegebene
/// deutsche Rückfalltext. Fehlt eine Datei oder ist sie kaputt, gelten die Rückfalltexte. Schlüssel mit "_" am Anfang sind Kommentare.
/// </summary>
public sealed class UiTextTable
{
    private static readonly IReadOnlyDictionary<string, string> NoTexts = new Dictionary<string, string>();

    private readonly IReadOnlyDictionary<string, string> _selected;
    private readonly IReadOnlyDictionary<string, string> _source;

    private UiTextTable(string language, IReadOnlyDictionary<string, string> selected, IReadOnlyDictionary<string, string> source)
    {
        Language = language;
        _selected = selected;
        _source = source;
    }

    public static UiTextTable Empty { get; } = new(UiText.SourceLanguage, NoTexts, NoTexts);

    public string Language { get; }

    public static UiTextTable Load(string localesDirectory, string? language)
    {
        var chosen = string.IsNullOrWhiteSpace(language) ? UiText.SourceLanguage : language.Trim();
        var source = Read(Path.Combine(localesDirectory, UiText.SourceLanguage + ".json"));
        var selected = chosen == UiText.SourceLanguage ? source : Read(Path.Combine(localesDirectory, chosen + ".json"));
        return new UiTextTable(chosen, selected, source);
    }

    /// <summary>Alle Texte einer Sprachdatei; leer, wenn sie fehlt oder kein gültiges JSON-Objekt ist.</summary>
    public static IReadOnlyDictionary<string, string> Read(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return NoTexts;
            }

            using var stream = File.OpenRead(path);
            var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stream);
            if (values is null)
            {
                return NoTexts;
            }

            return values
                .Where(pair => !pair.Key.StartsWith('_') && pair.Value.ValueKind == JsonValueKind.String)
                .ToDictionary(pair => pair.Key, pair => pair.Value.GetString()!, StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return NoTexts;
        }
    }

    public string Get(string key, string fallback) =>
        _selected.TryGetValue(key, out var text) || _source.TryGetValue(key, out text) ? text : fallback;

    /// <summary>Wie <see cref="Get"/> mit Platzhaltern {0}, {1}; ist das übersetzte Muster fehlerhaft, gilt das Rückfallmuster.</summary>
    public string Format(string key, string fallback, params object?[] args)
    {
        try
        {
            return string.Format(CultureInfo.CurrentCulture, Get(key, fallback), args);
        }
        catch (FormatException)
        {
            return string.Format(CultureInfo.CurrentCulture, fallback, args);
        }
    }
}

/// <summary>Die Texte der laufenden Sitzung. Die App lädt sie beim Start (<see cref="Load"/>); ohne Laden gelten die Rückfalltexte.</summary>
public static class UiText
{
    public const string SourceLanguage = "de";

    public static UiTextTable Current { get; private set; } = UiTextTable.Empty;

    public static void Load(string localesDirectory, string? language) => Current = UiTextTable.Load(localesDirectory, language);

    public static string Get(string key, string fallback) => Current.Get(key, fallback);

    public static string Format(string key, string fallback, params object?[] args) => Current.Format(key, fallback, args);
}
