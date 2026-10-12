using ParadoxModTool.Localization;

namespace ParadoxModTool.Categories;

/// <summary>Die sieben eingebauten Kategorien. Ihre Kennungen entsprechen den früheren Enum-Namen, damit gespeicherte Sessions und Theme-Farben weiter passen.</summary>
public static class CategoryDefaults
{
    /// <summary>Fallback-Kategorie für Dateien ohne passende Regel. Kann nicht gelöscht werden.</summary>
    public const string OtherId = "Other";

    public const string TextId = "Text";
    public const string GuiId = "Gui";
    public const string LocalizationId = "Localization";
    public const string DefinesId = "Defines";
    public const string ImportantId = "Important";
    public const string HelperFileId = "HelperFile";

    public static IReadOnlyList<string> BuiltInIds { get; } =
        [TextId, GuiId, LocalizationId, DefinesId, ImportantId, HelperFileId, OtherId];

    public static bool IsBuiltIn(string id) => BuiltInIds.Contains(id);

    /// <summary>Der deutsche Standardname einer eingebauten Kategorie (der Rückfall und der gespeicherte Name, solange sie nicht umbenannt ist); sonst <see langword="null"/>.</summary>
    private static string? GermanName(string id) => Create().FirstOrDefault(c => c.Id == id)?.Name;

    /// <summary>Schlüssel des Namens einer eingebauten Kategorie in den Sprachdateien (<c>category.builtin.text</c>, <c>category.builtin.helperFile</c> ...).</summary>
    public static string NameKey(string id) => "category.builtin." + char.ToLowerInvariant(id[0]) + id[1..];

    /// <summary>
    /// Der Name zum Anzeigen: eine eingebaute Kategorie, die nicht umbenannt wurde (Name leer oder der deutsche Standardname), erscheint in der
    /// Oberflächensprache; ein umbenannter oder eigener Name bleibt, wie er ist.
    /// </summary>
    public static string DisplayName(CategoryDefinition category)
    {
        if (GermanName(category.Id) is { } german && (string.IsNullOrWhiteSpace(category.Name) || category.Name == german))
        {
            return UiText.Get(NameKey(category.Id), german);
        }

        return category.Name;
    }

    /// <summary>
    /// Der Name zum Speichern, wenn der Nutzer <paramref name="typed"/> eingegeben hat: Der gezeigte Name einer eingebauten Kategorie wird wieder der
    /// deutsche Standardname (damit sie bei einem Sprachwechsel mitwechselt); alles andere bleibt.
    /// </summary>
    public static string NameToStore(string id, string typed)
    {
        if (GermanName(id) is { } german && typed.Trim() == UiText.Get(NameKey(id), german))
        {
            return german;
        }

        return typed;
    }

    public static List<CategoryDefinition> Create() =>
    [
        new() { Id = TextId, Name = "Textdateien", ColorHex = "#1E90FF", Extensions = [".txt"] },
        new() { Id = GuiId, Name = "GUI-Dateien", ColorHex = "#FFA500", Extensions = [".gui", ".gfx", ".vml"] },
        new() { Id = LocalizationId, Name = "Lokalisierung (YML)", ColorHex = "#32CD32", Extensions = [".yml", ".yaml"] },
        new() { Id = DefinesId, Name = "Defines (LUA)", ColorHex = "#9370DB", Extensions = [".lua"] },
        new() { Id = ImportantId, Name = "Wichtig", ColorHex = "#FFD700" },
        new() { Id = HelperFileId, Name = "Hilfsdatei", ColorHex = "#00CED1" },
        new() { Id = OtherId, Name = "Sonstige", ColorHex = "#808080" },
    ];
}
