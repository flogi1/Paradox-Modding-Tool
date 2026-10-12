using System.Text;

namespace ParadoxModTool.Generators;

/// <summary>Regeln für mehrsprachige Felder (Spec Abschnitt 1) und die Schreibweise von Texten in .yml-Dateien.</summary>
public static class LanguageFill
{
    public const string English = "english";

    /// <summary>
    /// Text eines Feldes in <paramref name="language"/>: die eigene Übersetzung; sonst Englisch, wenn das Element Übersetzungen hat und
    /// "Englisch für fehlende" an ist, oder wenn "Englisch kopieren" bzw. "Englisch in alle Sprachen" an ist; sonst leer.
    /// </summary>
    public static string Resolve(string english, IReadOnlyDictionary<string, string>? translations, string language, LanguageDefaults rules)
    {
        if (language == English)
        {
            return english;
        }

        if (translations is not null && translations.TryGetValue(language, out var own) && own.Length > 0)
        {
            return own;
        }

        var hasTranslations = translations is not null && translations.Values.Any(t => t.Length > 0);
        return (hasTranslations && rules.EnglishForMissing) || rules.CopyEnglish || rules.ExtendEnglish ? english : string.Empty;
    }

    /// <summary>Die zu schreibenden Sprachen: mit "Englisch in alle Sprachen" alle des Profils, sonst die gewählten.</summary>
    public static IReadOnlyList<string> LanguagesToWrite(IReadOnlyList<string> chosen, IReadOnlyList<string> all, LanguageDefaults rules) =>
        rules.ExtendEnglish && all.Count > 0 ? all : chosen;

    /// <summary>
    /// Text als .yml-Wert nach Paradox-Standard: <c>"</c> wird <c>\"</c>, ein Zeilenumbruch <c>\n</c> (wie in den Vanilla-Dateien).
    /// Schon geschriebene Folgen <c>\"</c>, <c>\n</c> und <c>\\</c> bleiben, wie sie sind.
    /// </summary>
    public static string YamlValue(string text)
    {
        var result = new StringBuilder(text.Length + 8);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\\' && i + 1 < text.Length && text[i + 1] is '"' or 'n' or '\\')
            {
                result.Append(c).Append(text[i + 1]);
                i++;
            }
            else if (c == '"')
            {
                result.Append("\\\"");
            }
            else if (c == '\r')
            {
                result.Append("\\n");
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
            }
            else if (c == '\n')
            {
                result.Append("\\n");
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }
}
