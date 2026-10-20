namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// Ankerwerte klassischer <c>.gui</c>-Dateien (<c>orientation</c> im Elternelement, <c>origo</c> im Element selbst) als Faktoren der Breite und Höhe
/// (Spec "Fakten → Lage").
/// </summary>
public static class ClassicAnchors
{
    private static readonly Dictionary<string, (double X, double Y)> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UPPER_LEFT"] = (0, 0),
        ["UPPER_RIGHT"] = (1, 0),
        ["LOWER_LEFT"] = (0, 1),
        ["LOWER_RIGHT"] = (1, 1),
        ["CENTER_UP"] = (0.5, 0),
        ["CENTER_DOWN"] = (0.5, 1),
        ["CENTER_LEFT"] = (0, 0.5),
        ["CENTER_RIGHT"] = (1, 0.5),
        ["CENTER"] = (0.5, 0.5),
    };

    // center_lower: laut HOI-IV-Wiki ein Alias von CENTER_DOWN (wie CENTER_UPPER von CENTER_UP). Die übrigen sind nur in Vanilla gesehen, weder
    // dokumentiert noch gemessen: ihre Bedeutung ist geraten.
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["left"] = "CENTER_LEFT",
        ["center_lower"] = "CENTER_DOWN",
        ["center_middle"] = "CENTER",
        ["bottom_left"] = "LOWER_LEFT",
    };

    /// <summary>
    /// Liest einen Ankerwert ohne Rücksicht auf Groß-/Kleinschreibung, mit oder ohne Anführungszeichen, ein angehängtes <c>;</c> fällt weg.
    /// <paramref name="isAlias"/>: einer der nur in Vanilla gesehenen Werte (<c>left</c>, <c>center_lower</c>, <c>center_middle</c>, <c>bottom_left</c>).
    /// </summary>
    public static bool TryParse(string? value, out double fx, out double fy, out bool isAlias)
    {
        fx = 0;
        fy = 0;
        isAlias = false;
        var name = Clean(value);
        if (Aliases.TryGetValue(name, out var target))
        {
            isAlias = true;
            name = target;
        }

        if (!Names.TryGetValue(name, out var factors))
        {
            isAlias = false;
            return false;
        }

        (fx, fy) = factors;
        return true;
    }

    /// <summary>Wert ohne Leerraum, Anführungszeichen und angehängtes <c>;</c>.</summary>
    public static string Clean(string? value) => (value ?? string.Empty).Trim().TrimEnd(';').Trim().Trim('"').Trim();
}
