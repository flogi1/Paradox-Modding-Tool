using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using ParadoxModTool.Categories;

namespace ParadoxModTool.Editor;

/// <summary>
/// Hält die aktive Kategorie-Liste (frei editierbar, siehe <see cref="CategoryDefinition"/>) und liefert
/// Zuordnung, Anzeigename und Farbe je Kategorie-Kennung. Die Persistierung der Liste übernimmt der Aufrufer
/// (ParadoxModTool.Session.AppSessionState.Categories).
///
/// Farben: Eingebaute Kategorien beziehen ihre Farbe vom aktiven Theme (<see cref="SetThemeColors"/>, samt
/// persönlicher Anpassungen), eigene Kategorien tragen ihre Farbe in der Definition. Ein ungültiger Wert fällt auf
/// die Definitions- bzw. Standardfarbe zurück, statt eine Ausnahme auszulösen.
/// </summary>
public static class FileCategoryClassifier
{
    private static List<CategoryDefinition> _categories = CategoryDefaults.Create();
    private static IReadOnlyDictionary<string, string>? _themeColors;

    private static readonly Dictionary<string, Brush> BrushCache = new();
    private static readonly Dictionary<string, Brush> TabBackgroundCache = new();

    /// <summary>Aktive Kategorien in Regel-Reihenfolge; die Fallback-Kategorie steht zuletzt.</summary>
    public static IReadOnlyList<CategoryDefinition> Categories => _categories;

    /// <summary>
    /// Setzt die Kategorie-Liste und invalidiert die Brush-Caches. Bereits offene Dokumente müssen
    /// danach <see cref="OpenDocument.RefreshAutoCategory"/> aufrufen, damit sich ihre automatische Zuordnung aktualisiert.
    /// </summary>
    public static void SetCategories(IEnumerable<CategoryDefinition>? categories)
    {
        _categories = CategoryMatcher.Normalize(categories);
        BrushCache.Clear();
        TabBackgroundCache.Clear();
    }

    /// <summary>Wirksame Theme-Farben der eingebauten Kategorien (Schlüssel = Kennung).</summary>
    public static void SetThemeColors(IReadOnlyDictionary<string, string>? colors)
    {
        _themeColors = colors;
        BrushCache.Clear();
        TabBackgroundCache.Clear();
    }

    /// <summary>Kennung der automatisch zugeordneten Kategorie eines Pfads.</summary>
    public static string Classify(string? filePath) => CategoryMatcher.Match(_categories, filePath);

    /// <summary>Die Definition zu einer Kennung; eine unbekannte (z. B. gelöschte) Kennung ergibt die Fallback-Kategorie.</summary>
    public static CategoryDefinition Find(string? id) =>
        _categories.FirstOrDefault(c => c.Id == id) ?? _categories.First(c => c.Id == CategoryDefaults.OtherId);

    public static bool Exists(string? id) => _categories.Any(c => c.Id == id);

    public static string DisplayName(string? id) => CategoryDefaults.DisplayName(Find(id));

    /// <summary>Der aktuell wirksame Hex-Farbcode einer Kategorie.</summary>
    public static string ColorHexFor(string? id)
    {
        var definition = Find(id);
        if (CategoryDefaults.IsBuiltIn(definition.Id)
            && _themeColors is not null
            && _themeColors.TryGetValue(definition.Id, out var themed)
            && CategoryMatcher.IsValidHex(themed))
        {
            return themed;
        }

        return CategoryMatcher.IsValidHex(definition.ColorHex) ? definition.ColorHex : "#808080";
    }

    /// <summary>Gefrorener, wiederverwendbarer <see cref="Brush"/> in der Kategoriefarbe — für die Markierung am Tab.</summary>
    public static Brush ColorFor(string? id)
    {
        var key = Find(id).Id;
        if (BrushCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var brush = new SolidColorBrush(ParseColor(key));
        brush.Freeze();
        BrushCache[key] = brush;
        return brush;
    }

    /// <summary>
    /// Halbtransparente Variante der Kategoriefarbe für die Einstellung "ganze Reiter einfärben". Bewusst nicht
    /// deckend, damit der Tab-Text bei jeder Farbe lesbar bleibt.
    /// </summary>
    public static Brush TabBackgroundFor(string? id)
    {
        var key = Find(id).Id;
        if (TabBackgroundCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var color = ParseColor(key);
        var brush = new SolidColorBrush(Color.FromArgb(70, color.R, color.G, color.B));
        brush.Freeze();
        TabBackgroundCache[key] = brush;
        return brush;
    }

    private static Color ParseColor(string id)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(ColorHexFor(id))!;
        }
        catch (FormatException)
        {
            return Colors.Gray;
        }
    }
}
