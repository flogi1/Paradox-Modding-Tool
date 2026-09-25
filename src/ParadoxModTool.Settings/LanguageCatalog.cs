using System.Globalization;
using ParadoxModTool.Localization;

namespace ParadoxModTool.Settings;

/// <summary>Eine wählbare Oberflächensprache: Code (Dateiname in <c>locales</c>) und Name in der Sprache selbst.</summary>
public sealed record UiLanguageOption(string Code, string Name);

/// <summary>Die Oberflächensprachen, für die es eine Datei in <c>locales</c> gibt. Deutsch (Quellsprache) ist immer dabei und steht vorn.</summary>
public static class LanguageCatalog
{
    public static IReadOnlyList<UiLanguageOption> Find(string localesDirectory)
    {
        var codes = Directory.Exists(localesDirectory)
            ? Directory.EnumerateFiles(localesDirectory, "*.json")
                .Select(path => Path.GetFileNameWithoutExtension(path).ToLowerInvariant())
                .Where(code => code.Length > 0 && !code.StartsWith('_'))
            : Enumerable.Empty<string>();

        return codes.Append(UiText.SourceLanguage)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code == UiText.SourceLanguage ? 0 : 1)
            .ThenBy(code => code, StringComparer.Ordinal)
            .Select(code => new UiLanguageOption(code, DisplayName(code)))
            .ToList();
    }

    /// <summary>Leer wird zur Quellsprache, sonst Kleinbuchstaben ohne Leerzeichen.</summary>
    public static string Normalize(string? code) =>
        string.IsNullOrWhiteSpace(code) ? UiText.SourceLanguage : code.Trim().ToLowerInvariant();

    /// <summary>Name der Sprache in ihr selbst (Deutsch, English, Français); unbekannte Codes bleiben stehen.</summary>
    public static string DisplayName(string code)
    {
        switch (code)
        {
            case "de":
                return "Deutsch";
            case "en":
                return "English";
        }

        // Nur vordefinierte Kulturen: für einen erfundenen Code wie "xx-nope" baut .NET sonst einen Namen aus dem Code selbst.
        var culture = CultureInfo.GetCultures(CultureTypes.AllCultures).FirstOrDefault(c => string.Equals(c.Name, code, StringComparison.OrdinalIgnoreCase));
        if (culture is null || culture.NativeName.Length == 0)
        {
            return code;
        }

        return char.ToUpper(culture.NativeName[0], culture) + culture.NativeName[1..];
    }
}
