using System.Text.Json.Serialization;

namespace ParadoxModTool.Mods;

/// <summary>Rolle einer Mod in einem Projekt (siehe <see cref="ModInfo.ParentModId"/>). In <c>mods.json</c> als Text gespeichert.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModRole
{
    /// <summary>Hauptmod oder eigenständige Mod.</summary>
    Main,

    /// <summary>Alternative Fassung des Hauptmods (Testversion, Beta); ersetzt ihn und läuft nie gleichzeitig mit ihm.</summary>
    DevBranch,

    /// <summary>Eigener Inhalt, hängt absichtlich vom Hauptmod ab.</summary>
    Submod,
}
