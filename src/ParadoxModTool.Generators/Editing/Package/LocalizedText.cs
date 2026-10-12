using System.Text.Json.Nodes;

namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>
/// Ein mehrsprachiger Text aus package.json für den Editor: einfacher Text (Schlüssel <c>""</c>) oder Tabelle Sprache → Text. Unveränderlich;
/// <see cref="With"/> liefert eine neue Fassung. Kommt zu einem einfachen Text eine Sprache dazu, wird der einfache Text zu <c>en</c>.
/// </summary>
public sealed class LocalizedText
{
    private readonly List<KeyValuePair<string, string>> _values;

    private LocalizedText(List<KeyValuePair<string, string>> values) => _values = values;

    public static LocalizedText Empty { get; } = new(new List<KeyValuePair<string, string>>());

    /// <summary>Sprache → Text in der Reihenfolge aus der Datei; ein einfacher Text steht unter <c>""</c>.</summary>
    public IReadOnlyDictionary<string, string> Values => _values.ToDictionary(p => p.Key, p => p.Value);

    public static LocalizedText From(JsonNode? node)
    {
        var values = new List<KeyValuePair<string, string>>();
        switch (node)
        {
            case JsonObject map:
                foreach (var (language, text) in map)
                {
                    values.Add(new(language, text?.ToString() ?? string.Empty));
                }

                break;
            case JsonValue value:
                values.Add(new(string.Empty, value.ToString()));
                break;
        }

        return new LocalizedText(values);
    }

    public static LocalizedText Plain(string text) => text.Length == 0 ? Empty : new(new List<KeyValuePair<string, string>> { new(string.Empty, text) });

    /// <summary>Wie <see cref="PackageText.Resolve"/>: UI-Sprache, dann <c>en</c>, dann der erste Eintrag.</summary>
    public string Resolve(string uiLanguage)
    {
        foreach (var (language, text) in _values)
        {
            if (string.Equals(language, uiLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }
        }

        foreach (var (language, text) in _values)
        {
            if (string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }
        }

        return _values.Count > 0 ? _values[0].Value : string.Empty;
    }

    /// <summary>Text einer Sprache setzen; ein leerer Text entfernt die Sprache.</summary>
    public LocalizedText With(string language, string text)
    {
        var values = _values.ToList();
        var plain = values.FindIndex(p => p.Key.Length == 0);
        if (plain >= 0 && language.Length > 0)
        {
            // Einfacher Text wird zu Englisch, sobald es Sprachen gibt (sonst gäbe es keine Schreibweise dafür).
            var old = values[plain].Value;
            values.RemoveAt(plain);
            // Ein leerer einfacher Text wird kein leeres Englisch.
            if (old.Length > 0 && values.All(p => !string.Equals(p.Key, "en", StringComparison.OrdinalIgnoreCase)))
            {
                values.Insert(plain, new("en", old));
            }
        }

        var index = values.FindIndex(p => string.Equals(p.Key, language, StringComparison.OrdinalIgnoreCase));
        if (text.Length == 0)
        {
            if (index >= 0)
            {
                values.RemoveAt(index);
            }
        }
        else if (index >= 0)
        {
            values[index] = new(values[index].Key, text);
        }
        else
        {
            values.Add(new(language, text));
        }

        return new LocalizedText(values);
    }

    /// <summary>
    /// Ergebnis des Sprachen-Dialogs: die gefüllten Sprachen in Dialogreihenfolge. Bleibt von einem einfachen Text nur derselbe Text als <c>en</c>,
    /// bleibt er einfach (dieselbe Fassung).
    /// </summary>
    public LocalizedText WithLanguages(IReadOnlyDictionary<string, string> languages)
    {
        var filled = languages.Where(p => p.Value.Length > 0).ToList();
        if (_values.Count == 1 && _values[0].Key.Length == 0 && filled.Count == 1 && filled[0].Key == "en" && filled[0].Value == _values[0].Value)
        {
            return this;
        }

        return new LocalizedText(filled.Select(p => new KeyValuePair<string, string>(p.Key, p.Value)).ToList());
    }

    /// <summary>Sprache, deren Text <see cref="Resolve"/> zeigt (<c>""</c> bei einfachem Text).</summary>
    public string ResolvedLanguage(string uiLanguage)
    {
        if (_values.Any(p => string.Equals(p.Key, uiLanguage, StringComparison.OrdinalIgnoreCase)))
        {
            return _values.First(p => string.Equals(p.Key, uiLanguage, StringComparison.OrdinalIgnoreCase)).Key;
        }

        if (_values.Any(p => string.Equals(p.Key, "en", StringComparison.OrdinalIgnoreCase)))
        {
            return _values.First(p => string.Equals(p.Key, "en", StringComparison.OrdinalIgnoreCase)).Key;
        }

        return _values.Count > 0 ? _values[0].Key : string.Empty;
    }

    /// <summary>Leer → <see langword="null"/>; nur einfacher Text → Text; sonst ein Objekt Sprache → Text.</summary>
    public JsonNode? ToNode()
    {
        if (_values.Count == 0)
        {
            return null;
        }

        if (_values.Count == 1 && _values[0].Key.Length == 0)
        {
            return JsonValue.Create(_values[0].Value);
        }

        var map = new JsonObject();
        foreach (var (language, text) in _values.Where(p => p.Key.Length > 0))
        {
            map[language] = text;
        }

        return map;
    }

    public override string ToString() => Resolve("en");
}
