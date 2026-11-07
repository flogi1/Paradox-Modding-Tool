namespace ParadoxModTool.FlagTracker;

/// <summary>Befehle von HOI IV: Flags (7 Familien), Variablen, temporäre Variablen, Arrays und Event-Targets (Erhebung Runde 2, Abschnitte 1, 2, 2.4, 2.5).</summary>
internal static class Hoi4Commands
{
    /// <summary>Flag-Familien; Befehle <c>set_/has_/clr_/modify_&lt;f&gt;_flag</c>, Familie <c>&lt;f&gt;_flag</c>.</summary>
    public static readonly string[] FlagFamilies = { "country", "state", "global", "character", "unit_leader", "project", "mio" };

    /// <summary>Iteratoren über Arrays (Feld <c>array</c>, Art Array).</summary>
    public static readonly string[] ArrayIterators =
    {
        "for_each_scope_loop", "any_of_scopes", "all_of_scopes", "for_each_loop", "random_scope_in_array",
        "find_highest_in_array", "find_lowest_in_array", "any_of", "all_of",
    };

    /// <summary>Felder in <c>common/decisions</c>, deren Wert eine Variable lesen kann (<c>cost = ENG_cost?50</c>).</summary>
    public static readonly string[] ValueReadFields = { "cost", "days_remove", "days_re_enable", "days_mission_timeout" };

    private static readonly string[] FlagField = { "flag" };
    private static readonly string[] VarField = { "var" };
    private static readonly string[] ArrayField = { "array" };
    private static readonly string[] NoField = Array.Empty<string>();

    public static Dictionary<string, FlagCommand> Build()
    {
        var map = new Dictionary<string, FlagCommand>(StringComparer.OrdinalIgnoreCase);

        foreach (var family in FlagFamilies)
        {
            var f = family + "_flag";
            map[$"set_{f}"] = new FlagCommand($"set_{f}", FlagKind.ScopeFlag, FlagRole.Set, f, FlagField);
            map[$"has_{f}"] = new FlagCommand($"has_{f}", FlagKind.ScopeFlag, FlagRole.Read, f, FlagField);
            map[$"clr_{f}"] = new FlagCommand($"clr_{f}", FlagKind.ScopeFlag, FlagRole.Remove, f, FlagField);
            map[$"modify_{f}"] = new FlagCommand($"modify_{f}", FlagKind.ScopeFlag, FlagRole.Set, f, FlagField);
        }

        void Variable(string command, FlagKind kind, FlagRole role) =>
            map[command] = new FlagCommand(command, kind, role, null, VarField, KeyAsName: true, ValueSideReads: true);

        foreach (var (infix, kind) in new[] { ("", FlagKind.Variable), ("temp_", FlagKind.TempVariable) })
        {
            foreach (var verb in new[] { "set", "add_to", "subtract_from", "multiply", "divide", "modulo", "clamp", "round" })
            {
                Variable($"{verb}_{infix}variable", kind, FlagRole.Set);
            }

            Variable($"set_{infix}variable_to_random", kind, FlagRole.Set);
            Variable($"randomize_{infix}variable", kind, FlagRole.Set);
            Variable($"check_{infix}variable", kind, FlagRole.Read);
            Variable($"has_{infix}variable", kind, FlagRole.Read);
            Variable($"clear_{infix}variable", kind, FlagRole.Remove);
        }

        Variable("career_profile_set_temp_variable", FlagKind.TempVariable, FlagRole.Set);

        void ArrayCommand(string command, FlagKind kind, FlagRole role) =>
            map[command] = new FlagCommand(command, kind, role, null, ArrayField, KeyAsName: true);

        ArrayCommand("add_to_array", FlagKind.Array, FlagRole.Set);
        ArrayCommand("remove_from_array", FlagKind.Array, FlagRole.Remove);
        ArrayCommand("clear_array", FlagKind.Array, FlagRole.Remove);
        ArrayCommand("is_in_array", FlagKind.Array, FlagRole.Read);
        ArrayCommand("add_to_temp_array", FlagKind.TempArray, FlagRole.Set);
        ArrayCommand("clear_temp_array", FlagKind.TempArray, FlagRole.Remove);

        void Target(string command, FlagKind kind, FlagRole role) =>
            map[command] = new FlagCommand(command, kind, role, null, NoField);

        Target("save_event_target_as", FlagKind.EventTarget, FlagRole.Set);
        Target("save_global_event_target_as", FlagKind.GlobalEventTarget, FlagRole.Set);
        Target("has_event_target", FlagKind.EventTarget, FlagRole.Read);
        Target("clear_global_event_target", FlagKind.GlobalEventTarget, FlagRole.Remove);

        return map;
    }

    /// <summary>Schleifenvariablen: Felder <c>value</c> und <c>index</c> der Array-Schleifen, <c>value</c> von <c>for_loop_effect</c>; Art TempVariable (gesetzt), ohne Feld die Vorgaben <c>v</c> bzw. <c>i</c>.</summary>
    public static IReadOnlyList<IteratorCommand> LoopVariables()
    {
        var rows = new List<IteratorCommand>();
        foreach (var command in new[] { "any_of", "all_of", "for_each_loop", "find_highest_in_array", "find_lowest_in_array" })
        {
            rows.Add(new IteratorCommand(command, "value", FlagKind.TempVariable, "v"));
            rows.Add(new IteratorCommand(command, "index", FlagKind.TempVariable, "i"));
        }

        rows.Add(new IteratorCommand("for_loop_effect", "value", FlagKind.TempVariable, "v"));
        return rows;
    }

    public static IReadOnlyList<IteratorCommand> Iterators() =>
        ArrayIterators.Select(c => new IteratorCommand(c, "array", FlagKind.Array)).ToList();
}
