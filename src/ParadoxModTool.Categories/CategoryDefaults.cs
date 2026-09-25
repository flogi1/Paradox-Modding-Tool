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
