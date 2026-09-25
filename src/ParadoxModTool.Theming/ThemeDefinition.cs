using System.Text.Json.Serialization;

namespace ParadoxModTool.Theming;

/// <summary>Ein Farbschema: alle Token-Farben plus die Farben der Dateikategorien.</summary>
public sealed class ThemeDefinition
{
    public const string LightId = "builtin-light";
    public const string DarkId = "builtin-dark";

    /// <summary>Schlüssel der Dateikategorien (Enum-Namen von <c>FileCategory</c>).</summary>
    public static IReadOnlyList<string> CategoryKeys { get; } =
        ["Text", "Gui", "Localization", "Defines", "Important", "HelperFile", "Other"];

    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Dunkles Theme: steuert die dunkle Windows-Titelleiste und das Basis-Theme beim Ergänzen fehlender Werte.</summary>
    public bool IsDark { get; set; }

    /// <summary>Token-Schlüssel auf Farbwert. Enthält nach <see cref="ThemeNormalizer.Normalize"/> alle Tokens.</summary>
    public Dictionary<string, string> Colors { get; set; } = new();

    /// <summary>Dateikategorie auf Farbwert (#RRGGBB).</summary>
    public Dictionary<string, string> CategoryColors { get; set; } = new();

    /// <summary>Eingebaute Themes sind schreibgeschützt; wird nie gespeichert.</summary>
    [JsonIgnore]
    public bool IsBuiltIn => Id is LightId or DarkId;

    public ThemeDefinition Clone() => new()
    {
        Id = Id,
        Name = Name,
        IsDark = IsDark,
        Colors = new Dictionary<string, string>(Colors),
        CategoryColors = new Dictionary<string, string>(CategoryColors),
    };
}

/// <summary>Die beiden mitgelieferten Themes.</summary>
public static class BuiltInThemes
{
    private static readonly Dictionary<string, string> LightCategories = new()
    {
        ["Text"] = "#1E90FF",
        ["Gui"] = "#FFA500",
        ["Localization"] = "#32CD32",
        ["Defines"] = "#9370DB",
        ["Important"] = "#FFD700",
        ["HelperFile"] = "#00CED1",
        ["Other"] = "#808080",
    };

    private static readonly Dictionary<string, string> DarkCategories = new()
    {
        ["Text"] = "#4DA3FF",
        ["Gui"] = "#FFB347",
        ["Localization"] = "#5FDB5F",
        ["Defines"] = "#B197F0",
        ["Important"] = "#FFD84D",
        ["HelperFile"] = "#33DDE0",
        ["Other"] = "#9A9A9A",
    };

    public static ThemeDefinition CreateLight() => new()
    {
        Id = ThemeDefinition.LightId,
        Name = "Hell",
        IsDark = false,
        Colors = ThemeTokens.All.ToDictionary(t => t.Key, t => t.Light),
        CategoryColors = new Dictionary<string, string>(LightCategories),
    };

    public static ThemeDefinition CreateDark() => new()
    {
        Id = ThemeDefinition.DarkId,
        Name = "Dunkel",
        IsDark = true,
        Colors = ThemeTokens.All.ToDictionary(t => t.Key, t => t.Dark),
        CategoryColors = new Dictionary<string, string>(DarkCategories),
    };

    public static ThemeDefinition Create(bool dark) => dark ? CreateDark() : CreateLight();

    public static ThemeDefinition? TryGet(string? id) => id switch
    {
        ThemeDefinition.LightId => CreateLight(),
        ThemeDefinition.DarkId => CreateDark(),
        _ => null,
    };
}

/// <summary>Macht aus beliebigen (z. B. importierten oder von Hand bearbeiteten) Theme-Daten ein vollständiges, gültiges Theme.</summary>
public static class ThemeNormalizer
{
    /// <summary>
    /// Behält nur bekannte Tokens mit gültigem Farbwert; alles Fehlende oder Ungültige kommt aus dem eingebauten
    /// Basis-Theme (Dunkel bei <see cref="ThemeDefinition.IsDark"/>, sonst Hell). Ein leerer Name wird ersetzt.
    /// </summary>
    public static ThemeDefinition Normalize(ThemeDefinition source)
    {
        var baseTheme = BuiltInThemes.Create(source.IsDark);
        var result = new ThemeDefinition
        {
            Id = source.Id,
            Name = string.IsNullOrWhiteSpace(source.Name) ? "Unbenanntes Theme" : source.Name.Trim(),
            IsDark = source.IsDark,
        };

        foreach (var token in ThemeTokens.All)
        {
            result.Colors[token.Key] = source.Colors.TryGetValue(token.Key, out var value) && HexColor.IsValid(value)
                ? value
                : baseTheme.Colors[token.Key];
        }

        foreach (var key in ThemeDefinition.CategoryKeys)
        {
            result.CategoryColors[key] = source.CategoryColors.TryGetValue(key, out var value) && HexColor.IsValid(value)
                ? value
                : baseTheme.CategoryColors[key];
        }

        return result;
    }
}

/// <summary>Wählt das wirksame Theme und ermittelt die wirksamen Kategorie-Farben.</summary>
public static class ThemeSelection
{
    /// <summary>Auswahl-Kennung für "der Windows-Einstellung folgen".</summary>
    public const string SystemId = "system";

    /// <summary>
    /// Liefert das Theme zur Auswahl: "system" folgt <paramref name="systemIsDark"/>; eine unbekannte Kennung
    /// (z. B. ein gelöschtes eigenes Theme) fällt ebenfalls auf die Windows-Einstellung zurück.
    /// </summary>
    public static ThemeDefinition Resolve(string? selectionId, bool systemIsDark, IEnumerable<ThemeDefinition> customThemes)
    {
        if (BuiltInThemes.TryGet(selectionId) is { } builtIn)
        {
            return builtIn;
        }

        if (selectionId is not null && selectionId != SystemId
            && customThemes.FirstOrDefault(t => t.Id == selectionId) is { } custom)
        {
            return custom;
        }

        return BuiltInThemes.Create(systemIsDark);
    }

    /// <summary>
    /// Wirksame Kategorie-Farben: persönliche Anpassung (nur wirksam, wenn gültig) vor der Farbe des Themes.
    /// Enthält immer alle Kategorien.
    /// </summary>
    public static Dictionary<string, string> EffectiveCategoryColors(ThemeDefinition theme, IReadOnlyDictionary<string, string>? userOverrides)
    {
        var result = new Dictionary<string, string>();
        foreach (var key in ThemeDefinition.CategoryKeys)
        {
            if (userOverrides is not null && userOverrides.TryGetValue(key, out var custom) && HexColor.IsValid(custom))
            {
                result[key] = custom;
            }
            else if (theme.CategoryColors.TryGetValue(key, out var own) && HexColor.IsValid(own))
            {
                result[key] = own;
            }
            else
            {
                result[key] = BuiltInThemes.Create(theme.IsDark).CategoryColors[key];
            }
        }

        return result;
    }
}
