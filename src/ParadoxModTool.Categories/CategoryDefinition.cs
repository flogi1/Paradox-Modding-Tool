namespace ParadoxModTool.Categories;

/// <summary>
/// Eine Dateikategorie: Name, Farbe und optionale Regeln, nach denen Dateien automatisch
/// zugeordnet werden. Kategorien ohne Regeln (z. B. "Wichtig") werden nur manuell vergeben.
/// </summary>
public sealed class CategoryDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Farbe (#RRGGBB). Bei eingebauten Kategorien liefert das aktive Theme die Farbe und dieser Wert
    /// dient nur als Rückfall; bei eigenen Kategorien gilt er für alle Themes.
    /// </summary>
    public string ColorHex { get; set; } = "#808080";

    /// <summary>Dateiendungen in Kleinbuchstaben mit führendem Punkt, z. B. ".txt".</summary>
    public List<string> Extensions { get; set; } = new();

    /// <summary>Glob-Muster (* und ?), siehe <see cref="CategoryMatcher"/>.</summary>
    public List<string> Patterns { get; set; } = new();

    public CategoryDefinition Clone() => new()
    {
        Id = Id,
        Name = Name,
        ColorHex = ColorHex,
        Extensions = new List<string>(Extensions),
        Patterns = new List<string>(Patterns),
    };
}
