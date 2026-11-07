using System.Text.RegularExpressions;

namespace ParadoxModTool.FlagTracker;

/// <summary>Flag-Befehle von CK III (add_/has_/remove_ ... _flag) und die Leser für Definitions-Flags.</summary>
public static class Ck3FlagCommands
{
    private static readonly Regex FlagCommandPattern = new(@"^(add|has|remove)_[a-z_]*flag$", RegexOptions.Compiled);

    /// <summary>
    /// Leser für Definitions-Flags: Das Flag steht als <c>flag = X</c> in der Definition (traits, buildings, laws, legitimacy)
    /// bzw. als Token in <c>personal_tenet_parameters</c> (religion/tenet_types). Keine Scope-Flag-Befehle.
    /// </summary>
    internal static readonly string[] DefinitionFlagReaders =
    {
        "has_building_with_flag",
        "has_construction_with_flag",
        "has_trait_with_flag",
        "has_trait_flag",
        "has_realm_law_flag",
        "has_title_law_flag",
        "has_legitimacy_flag",
        "has_personal_tenet_flag",
    };

    /// <summary>
    /// Namen, die auf das Muster passen, aber kein Flag-Name-Befehl sind (Wert ist kein frei gewählter Flag-Name,
    /// sondern z. B. ein Event-, Cooldown- oder POI-Typ); gelten auch für ScriptDocs-Namen.
    /// </summary>
    internal static readonly string[] NotNameFlags =
    {
        "has_court_event_flag",
        "add_internal_flag",
        "add_inspiration_cooldown_flag",
        "has_any_point_of_interest_flag",
        "has_wishes_to_schism_caliphate_flag",
    };

    /// <summary>
    /// Mitgelieferte Befehle, gesammelt aus CK III 1.20.1.2 (Crozier) am 2026-10-27
    /// (alle .txt unter game/, Kommentare entfernt, ohne Definitions-Flag-Leser und NotNameFlags).
    /// </summary>
    public static IReadOnlyList<string> Shipped { get; } = new[]
    {
        "add_character_flag",
        "add_dead_character_flag",
        "add_relation_flag",
        "has_character_flag",
        "has_dead_character_flag",
        "has_relation_flag",
        "remove_character_flag",
        "remove_relation_flag",
        "remove_trait_flag",
    };

    /// <summary>Passt der Name auf add_/has_/remove_ ... _flag und ist weder Definitions-Flag-Leser noch NotNameFlag?</summary>
    public static bool IsFlagCommand(string name) =>
        FlagCommandPattern.IsMatch(name) && Array.IndexOf(DefinitionFlagReaders, name) < 0 && Array.IndexOf(NotNameFlags, name) < 0;

    /// <summary>Familie = Teil nach dem Verb, z. B. add_character_flag wird character_flag.</summary>
    public static string Family(string name) => name[(name.IndexOf('_') + 1)..];

    /// <summary>Rolle aus dem Verb: add = Set, has = Read, remove = Remove.</summary>
    public static FlagRole Role(string name) =>
        name.StartsWith("add_", StringComparison.Ordinal) ? FlagRole.Set
        : name.StartsWith("has_", StringComparison.Ordinal) ? FlagRole.Read
        : FlagRole.Remove;
}
