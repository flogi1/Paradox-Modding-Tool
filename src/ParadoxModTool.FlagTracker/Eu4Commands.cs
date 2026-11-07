namespace ParadoxModTool.FlagTracker;

/// <summary>Befehle von EU IV: Flags (6 Familien, feste Liste), Variablen und Event-Targets (Erhebung Runde 2, EU IV Abschnitte 1-3 und 8).</summary>
internal static class Eu4Commands
{
    /// <summary>Flag-Familien; Befehle <c>set_/has_/clr_&lt;f&gt;_flag</c> und <c>had_&lt;f&gt;_flag = { flag = X }</c>, Familie <c>&lt;f&gt;_flag</c>.</summary>
    public static readonly string[] FlagFamilies = { "country", "province", "global", "ruler", "heir", "consort" };

    /// <summary>Variablenbefehle, die setzen (<c>export_to_variable</c> nur über <c>variable_name</c>/<c>which</c>, nie über <c>value</c>).</summary>
    public static readonly string[] VariableSetCommands =
    {
        "set_variable", "change_variable", "subtract_variable", "multiply_variable", "divide_variable", "modulo_variable", "round_variable",
        "export_to_variable",
    };

    /// <summary>Variablenbefehle, die lesen.</summary>
    public static readonly string[] VariableReadCommands = { "check_variable", "is_variable_equal" };

    private static readonly string[] FlagField = { "flag" };
    private static readonly string[] VariableFields = { "which", "variable_name" };
    private static readonly string[] NoField = Array.Empty<string>();

    public static Dictionary<string, FlagCommand> Build()
    {
        var map = new Dictionary<string, FlagCommand>(StringComparer.OrdinalIgnoreCase);

        void Flag(string command, string family, FlagRole role) =>
            map[command] = new FlagCommand(command, FlagKind.ScopeFlag, role, family, FlagField);

        foreach (var family in FlagFamilies)
        {
            var f = family + "_flag";
            Flag($"set_{f}", f, FlagRole.Set);
            Flag($"has_{f}", f, FlagRole.Read);
            Flag($"clr_{f}", f, FlagRole.Remove);
            Flag($"had_{f}", f, FlagRole.Read);
        }

        Flag("set_permanent_province_flag", "province_flag", FlagRole.Set);

        void Variable(string command, FlagRole role) =>
            map[command] = new FlagCommand(command, FlagKind.Variable, role, null, VariableFields, KeyAsName: true, SecondNameRead: true);

        foreach (var command in VariableSetCommands)
        {
            Variable(command, FlagRole.Set);
        }

        foreach (var command in VariableReadCommands)
        {
            Variable(command, FlagRole.Read);
        }

        void Target(string command, FlagKind kind, FlagRole role) =>
            map[command] = new FlagCommand(command, kind, role, null, NoField);

        Target("save_event_target_as", FlagKind.EventTarget, FlagRole.Set);
        Target("save_global_event_target_as", FlagKind.GlobalEventTarget, FlagRole.Set);
        Target("clear_global_event_target", FlagKind.GlobalEventTarget, FlagRole.Remove);
        Target("has_saved_event_target", FlagKind.EventTarget, FlagRole.Read);
        Target("has_saved_global_event_target", FlagKind.GlobalEventTarget, FlagRole.Read);

        return map;
    }
}
