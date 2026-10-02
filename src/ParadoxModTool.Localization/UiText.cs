using System.Globalization;
using System.Text.Json;

namespace ParadoxModTool.Localization;

/// <summary>Zeile und Position (beide ab 1) und Meldung eines JSON-Fehlers in einer Sprachdatei.</summary>
public sealed record LocaleParseError(long Line, long Position, string Message);

/// <summary>Inhalt einer Sprachdatei: Texte ohne Kommentar-Keys, der JSON-Fehler (falls kaputt) und ob sie maschinell übersetzt ist.</summary>
public sealed record LocaleFile(IReadOnlyDictionary<string, string> Texts, LocaleParseError? Error, bool MachineTranslated);

/// <summary>
/// Oberflächentexte einer Sprache aus den community-editierbaren Dateien <c>locales/&lt;sprache&gt;.json</c> (flache Schlüssel,
/// Architektur-Spezifikation Abschnitt 14). Reihenfolge: gewählte Sprache (User-Ordner vor Programmordner), dann Englisch, dann Deutsch
/// (Quellsprache), dann der im Code mitgegebene deutsche Rückfalltext; bei Deutsch entfällt Englisch. Englisch und Deutsch als Rückfall kommen
/// immer aus dem Programmordner. Leere Texte zählen als fehlend. Schlüssel mit "_" am Anfang sind Kommentare.
/// </summary>
public sealed class UiTextTable
{
    private const string FallbackLanguage = "en";
    private const string MachineTranslatedKey = "_machineTranslated";

    private static readonly IReadOnlyDictionary<string, string> NoTexts = new Dictionary<string, string>();

    private readonly IReadOnlyList<IReadOnlyDictionary<string, string>> _chain;

    private UiTextTable(string language, IReadOnlyList<IReadOnlyDictionary<string, string>> chain, LocaleParseError? loadError)
    {
        Language = language;
        _chain = chain;
        LoadError = loadError;
    }

    public static UiTextTable Empty { get; } = new(UiText.SourceLanguage, [], null);

    public string Language { get; }

    /// <summary>JSON-Fehler der Datei der gewählten Sprache; <see langword="null"/>, wenn sie fehlt oder gültig ist.</summary>
    public LocaleParseError? LoadError { get; }

    public static UiTextTable Load(string localesDirectory, string? language) => Load(new LocaleSources(localesDirectory, null), language);

    public static UiTextTable Load(LocaleSources sources, string? language)
    {
        var chosen = string.IsNullOrWhiteSpace(language) ? UiText.SourceLanguage : language.Trim();
        var selected = sources.Resolve(chosen)?.Path;
        var paths = new List<string>();
        if (selected is not null)
        {
            paths.Add(selected);
        }

        if (chosen != UiText.SourceLanguage)
        {
            paths.Add(sources.ProgramFile(FallbackLanguage));
        }

        paths.Add(sources.ProgramFile(UiText.SourceLanguage));

        LocaleParseError? loadError = null;
        var chain = new List<IReadOnlyDictionary<string, string>>();
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var file = ReadFile(path);
            if (path == selected)
            {
                loadError = file.Error;
            }

            chain.Add(file.Texts);
        }

        return new UiTextTable(chosen, chain, loadError);
    }

    /// <summary>Alle Texte einer Sprachdatei; leer, wenn sie fehlt oder kein gültiges JSON-Objekt ist.</summary>
    public static IReadOnlyDictionary<string, string> Read(string path) => ReadFile(path).Texts;

    /// <summary>Texte, JSON-Fehler und Maschinen-Markierung einer Sprachdatei. Fehlt die Datei oder ist sie nicht lesbar: leer, ohne Fehler.</summary>
    public static LocaleFile ReadFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new LocaleFile(NoTexts, null, false);
            }

            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new LocaleFile(NoTexts, null, false);
            }

            var texts = new Dictionary<string, string>(StringComparer.Ordinal);
            var machineTranslated = false;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name == MachineTranslatedKey)
                {
                    machineTranslated = property.Value.ValueKind == JsonValueKind.True;
                }
                else if (!property.Name.StartsWith('_') && property.Value.ValueKind == JsonValueKind.String)
                {
                    texts[property.Name] = property.Value.GetString()!;
                }
            }

            return new LocaleFile(texts, null, machineTranslated);
        }
        catch (JsonException ex)
        {
            return new LocaleFile(NoTexts, new LocaleParseError((ex.LineNumber ?? 0) + 1, (ex.BytePositionInLine ?? 0) + 1, ex.Message), false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new LocaleFile(NoTexts, null, false);
        }
    }

    public string Get(string key, string fallback)
    {
        foreach (var texts in _chain)
        {
            if (texts.TryGetValue(key, out var text) && text.Length > 0)
            {
                return text;
            }
        }

        return fallback;
    }

    /// <summary>Wie <see cref="Get"/> mit Platzhaltern {0}, {1}; ist ein Muster fehlerhaft, gilt das nächste der Reihenfolge, zuletzt das Rückfallmuster.</summary>
    public string Format(string key, string fallback, params object?[] args)
    {
        foreach (var texts in _chain)
        {
            if (texts.TryGetValue(key, out var pattern) && pattern.Length > 0)
            {
                try
                {
                    return string.Format(CultureInfo.CurrentCulture, pattern, args);
                }
                catch (FormatException)
                {
                }
            }
        }

        return string.Format(CultureInfo.CurrentCulture, fallback, args);
    }
}

/// <summary>Die Texte der laufenden Sitzung. Die App lädt sie beim Start (<see cref="Load"/>); ohne Laden gelten die Rückfalltexte.</summary>
public static class UiText
{
    public const string SourceLanguage = "de";

    public static UiTextTable Current { get; private set; } = UiTextTable.Empty;

    public static void Load(string localesDirectory, string? language) => Current = UiTextTable.Load(localesDirectory, language);

    public static void Load(LocaleSources sources, string? language) => Current = UiTextTable.Load(sources, language);

    public static string Get(string key, string fallback) => Current.Get(key, fallback);

    public static string Format(string key, string fallback, params object?[] args) => Current.Format(key, fallback, args);
}
