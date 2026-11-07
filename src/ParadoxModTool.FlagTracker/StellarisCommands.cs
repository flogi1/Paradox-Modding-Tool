using System.Text.RegularExpressions;

namespace ParadoxModTool.FlagTracker;

/// <summary>Befehle von Stellaris: Flags (offene Familienliste wie CK III), Variablen, Event-Targets und Definitions-Flags (Erhebung Runde 2, Stellaris Abschnitte 1-6).</summary>
public static class StellarisCommands
{
    private static readonly Regex FlagCommandPattern = new(@"^(set|set_timed|has|remove)_([a-z_]+)_flag$", RegexOptions.Compiled);

    /// <summary>Befehlsname mit Parameter statt Familie (<c>set_$SCOPE_TYPE$_flag</c>): Rolle aus dem Verb, Familie unbekannt.</summary>
    private static readonly Regex ComposedFlagCommandPattern = new(@"^(set|set_timed|has|remove)_\$[A-Za-z0-9_]+\$_flag$", RegexOptions.Compiled);

    /// <summary>Liest das Policy-Flag aus <c>policy_flags</c> (Definitions-Flag, kein Scope-Flag).</summary>
    internal const string PolicyFlagReader = "has_policy_flag";

    /// <summary>Liest ein Beziehungs-Flag in Gegenrichtung; passt nicht auf das Muster.</summary>
    private const string ReverseRelationFlag = "reverse_has_relation_flag";

    /// <summary>
    /// Namen, die auf das Muster passen, aber kein Flag-Befehl sind: <c>change_country_flag</c> (Wappen), <c>has_any_flag</c>,
    /// <c>is_occupied_flag</c> und die Scripted Effects/Trigger aus Stellaris 4.5.2 (Cygnus), deren Name auf das Muster passt
    /// (oberste Schlüssel in common/scripted_effects und common/scripted_triggers, gesammelt am 2026-10-28). Die Erhebung nennt 26
    /// solche Definitionen; die übrigen (<c>remove_shroud_flags</c>, <c>clear_*_flags</c> …) passen nicht auf das Muster.
    /// </summary>
    internal static readonly string[] Denied =
    {
        "change_country_flag",
        "has_any_flag",
        "is_occupied_flag",
        "has_synth_queen_dialogue_flag",
        "remove_loaning_fleet_flag",
        "remove_sabotage_starbase_flag",
        "set_apex_predator_flag",
        "set_espionage_diplo_incident_success_flag",
        "set_evopred_flag",
    };

    /// <summary>
    /// Mitgelieferte Flag-Befehle: jeder passende Schlüssel aus Stellaris 4.5.2 (Cygnus), alle .txt der Installation, Kommentare entfernt,
    /// ohne <see cref="Denied"/> und <c>has_policy_flag</c>, gesammelt am 2026-10-28; dazu <c>reverse_has_relation_flag</c>.
    /// </summary>
    public static IReadOnlyList<string> Shipped { get; } = new[]
    {
        "has_ambient_object_flag", "has_archaeology_flag", "has_army_flag", "has_astral_rift_flag", "has_carrier_flag", "has_country_flag",
        "has_deposit_flag", "has_design_flag", "has_espionage_operation_flag", "has_federation_flag", "has_first_contact_flag", "has_fleet_flag",
        "has_global_flag", "has_leader_flag", "has_megastructure_flag", "has_planet_flag", "has_pop_faction_flag", "has_pop_group_flag",
        "has_relation_flag", "has_ship_flag", "has_situation_flag", "has_species_flag", "has_spynetwork_flag", "has_star_flag",
        "has_starbase_flag", "has_storm_flag", "has_war_flag",
        "remove_astral_rift_flag", "remove_carrier_flag", "remove_country_flag", "remove_federation_flag", "remove_fleet_flag",
        "remove_global_flag", "remove_leader_flag", "remove_megastructure_flag", "remove_planet_flag", "remove_pop_faction_flag",
        "remove_pop_group_flag", "remove_relation_flag", "remove_ship_flag", "remove_situation_flag", "remove_species_flag",
        "remove_spynetwork_flag", "remove_star_flag", "remove_starbase_flag",
        "set_ambient_object_flag", "set_archaeology_flag", "set_army_flag", "set_astral_rift_flag", "set_carrier_flag", "set_country_flag",
        "set_deposit_flag", "set_design_flag", "set_espionage_operation_flag", "set_federation_flag", "set_first_contact_flag", "set_fleet_flag",
        "set_global_flag", "set_leader_flag", "set_megastructure_flag", "set_mission_flag", "set_planet_flag", "set_pop_faction_flag",
        "set_pop_group_flag", "set_relation_flag", "set_ship_flag", "set_situation_flag", "set_species_flag", "set_spynetwork_flag",
        "set_star_flag", "set_starbase_flag", "set_storm_flag",
        "set_timed_carrier_flag", "set_timed_country_flag", "set_timed_espionage_operation_flag", "set_timed_federation_flag",
        "set_timed_first_contact_flag", "set_timed_fleet_flag", "set_timed_global_flag", "set_timed_leader_flag", "set_timed_planet_flag",
        "set_timed_pop_group_flag", "set_timed_relation_flag", "set_timed_ship_flag", "set_timed_situation_flag", "set_timed_species_flag",
        "set_timed_spynetwork_flag", "set_timed_star_flag", "set_timed_starbase_flag", "set_war_flag",
        ReverseRelationFlag,
    };

    /// <summary>Variablenbefehle, die setzen (Name in <c>which</c>, <c>variable</c> oder als Wert).</summary>
    internal static readonly string[] VariableSetCommands =
    {
        "set_variable", "change_variable", "subtract_variable", "multiply_variable", "divide_variable", "round_variable_to_closest",
        "set_variable_to_random_value", "add_variable", "round_variable", "floor_variable", "ceiling_variable",
        "export_trigger_value_to_variable", "export_modifier_to_variable", "export_modifier_duration_to_variable",
        "export_resource_income_to_variable", "export_resource_maximum_to_variable", "export_resource_stockpile_to_variable",
    };

    /// <summary>Variablenbefehle, die lesen.</summary>
    internal static readonly string[] VariableReadCommands = { "check_variable", "check_variable_arithmetic", "is_variable_set" };

    /// <summary>Felder, deren Wert bei den Variablenbefehlen eine Variable liest (spec Runde 2, Festlegung 7).</summary>
    internal static readonly string[] ValueSideFields = { "value" };

    private static readonly string[] FlagField = { "flag" };
    private static readonly string[] VariableFields = { "which", "variable" };
    private static readonly string[] NoField = Array.Empty<string>();

    /// <summary>
    /// Passt der Name auf <c>(set|set_timed|has|remove)_&lt;x&gt;_flag</c> und ist weder gesperrt noch <c>has_policy_flag</c>?
    /// <c>reverse_has_relation_flag</c> zählt ebenfalls.
    /// </summary>
    public static bool IsFlagCommand(string name) =>
        name == ReverseRelationFlag
        || (FlagCommandPattern.IsMatch(name) && name != PolicyFlagReader && Array.IndexOf(Denied, name) < 0);

    /// <summary>Familie = Teil nach dem Verb, z. B. set_timed_star_flag wird star_flag; reverse_has_relation_flag wird relation_flag.</summary>
    public static string Family(string name)
    {
        foreach (var verb in new[] { "reverse_has_", "set_timed_", "set_", "has_", "remove_" })
        {
            if (name.StartsWith(verb, StringComparison.Ordinal))
            {
                return name[verb.Length..];
            }
        }

        return name;
    }

    /// <summary>Rolle aus dem Verb: set und set_timed = Set, has und reverse_has = Read, remove = Remove.</summary>
    public static FlagRole Role(string name) =>
        name.StartsWith("set_", StringComparison.Ordinal) ? FlagRole.Set
        : name.StartsWith("remove_", StringComparison.Ordinal) ? FlagRole.Remove
        : FlagRole.Read;

    /// <summary>Befehl für einen Schlüssel wie <c>set_$SCOPE_TYPE$_flag</c> (Scope-Flag ohne Familie), sonst null.</summary>
    internal static FlagCommand? Composed(string key) =>
        key.IndexOf('$') >= 0 && ComposedFlagCommandPattern.IsMatch(key)
            ? new FlagCommand(key, FlagKind.ScopeFlag, Role(key), null, FlagField)
            : null;

    /// <summary>Befehlstabelle aus den mitgelieferten Flag-Befehlen, jedem passenden Namen aus <paramref name="extraFlagCommands"/>, Variablen und Event-Targets.</summary>
    internal static Dictionary<string, FlagCommand> Build(IEnumerable<string> extraFlagCommands)
    {
        var map = new Dictionary<string, FlagCommand>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in Shipped.Concat(extraFlagCommands))
        {
            if (IsFlagCommand(name))
            {
                map[name] = new FlagCommand(name, FlagKind.ScopeFlag, Role(name), Family(name), FlagField);
            }
        }

        map[PolicyFlagReader] = new FlagCommand(PolicyFlagReader, FlagKind.DefinitionFlag, FlagRole.Read, null, NoField);

        void Variable(string command, FlagRole role) =>
            map[command] = new FlagCommand(command, FlagKind.Variable, role, null, VariableFields, ValueSideReads: true);

        foreach (var command in VariableSetCommands)
        {
            Variable(command, FlagRole.Set);
        }

        foreach (var command in VariableReadCommands)
        {
            Variable(command, FlagRole.Read);
        }

        Variable("clear_variable", FlagRole.Remove);

        void Target(string command, FlagKind kind, FlagRole role) =>
            map[command] = new FlagCommand(command, kind, role, null, NoField);

        Target("save_event_target_as", FlagKind.EventTarget, FlagRole.Set);
        Target("save_global_event_target_as", FlagKind.GlobalEventTarget, FlagRole.Set);
        Target("clear_global_event_target", FlagKind.GlobalEventTarget, FlagRole.Remove);

        return map;
    }
}
