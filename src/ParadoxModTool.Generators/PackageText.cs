using System.Text.Json;

namespace ParadoxModTool.Generators;

/// <summary>
/// Ein Text aus package.json (Name, Beschriftung, Meldung): einfacher Text oder Tabelle UI-Sprache → Text. Gewählt wird die UI-Sprache, dann
/// <c>en</c>, dann der erste Eintrag.
/// </summary>
public static class PackageText
{
    public static string Resolve(JsonElement value, string uiLanguage)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Null => string.Empty,
                _ => value.GetRawText(),
            };
        }

        string? first = null;
        string? english = null;
        foreach (var entry in value.EnumerateObject())
        {
            var text = entry.Value.GetString() ?? string.Empty;
            if (string.Equals(entry.Name, uiLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }

            first ??= text;
            if (string.Equals(entry.Name, "en", StringComparison.OrdinalIgnoreCase))
            {
                english = text;
            }
        }

        return english ?? first ?? string.Empty;
    }
}
