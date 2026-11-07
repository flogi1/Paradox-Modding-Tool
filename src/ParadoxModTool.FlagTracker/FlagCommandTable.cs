namespace ParadoxModTool.FlagTracker;

/// <summary>
/// Wo der Name steht: im Wert des Befehls (cmd = NAME) oder in Blockform in einem der NameFields (cmd = { name = NAME }); Feldnamen ohne
/// Rücksicht auf Groß/Klein. KeyAsName: in Blockform ist auch der Schlüssel des ersten nicht reservierten Kindes der Name (HOI IV, EU IV).
/// SecondNameRead: jedes weitere Namensfeld nach dem ersten ist eine Lesestelle (EU IV). ValueSideReads: Werte auf der Wertseite
/// lesen Variablen (HOI IV, Stellaris). KeyAsName und ValueSideReads wertet der Leser aus (HOI IV, EU IV), SecondNameRead ebenso (EU IV).
/// </summary>
public sealed record FlagCommand(string Command, FlagKind Kind, FlagRole Role, string? Family, IReadOnlyList<string> NameFields,
    bool KeyAsName = false, bool SecondNameRead = false, bool ValueSideReads = false);

/// <summary>
/// Listen-Iterator: <c>Command = { Field = NAME }</c> liest die Liste NAME der Art Kind. Befehl und Feld ohne Rücksicht auf Groß/Klein.
/// Als Schleifenvariable (<see cref="FlagCommandTable.FindLoopVariables"/>) ist Default der Name, den das Spiel ohne das Feld nimmt (HOI IV <c>v</c>, <c>i</c>).
/// </summary>
public sealed record IteratorCommand(string Command, string Field, FlagKind Kind, string? Default = null);

/// <summary>Wie die Lokalisierung (<c>.yml</c>) und <c>.gui</c> gelesen werden.</summary>
public enum LocStyle
{
    /// <summary>Jomini-Datenfunktionen wie <c>Var('x')</c> und <c>GetGlobalVariable('x')</c> (EU V, CK III, Victoria 3).</summary>
    DataFunctions,

    /// <summary>HOI IV <c>[?x]</c> in der Lokalisierung; <c>.gui</c> ohne Lesestellen.</summary>
    Hoi4Brackets,

    /// <summary>EU IV <c>[Scope.var.GetValue]</c> und <c>[X.GetName]</c>.</summary>
    Eu4Brackets,

    /// <summary>Stellaris <c>[scope.var]</c> und <c>[event_target:X…]</c>.</summary>
    StellarisBrackets,
}

/// <summary>Befehlstabelle eines Spiels: welche Skriptbefehle Variablen, Listen und Flags setzen, lesen oder entfernen.</summary>
public sealed class FlagCommandTable
{
    private static readonly string[] NameField = { "name" };
    private static readonly string[] FlagField = { "flag" };
    private static readonly string[][] Ck3TenetFolders = { new[] { "common", "religion", "tenet_types" } };
    private static readonly string[][] Ck3DefinitionFolders =
    {
        new[] { "common", "buildings" },
        new[] { "common", "traits" },
        new[] { "common", "laws" },
        new[] { "common", "legitimacy" },
    };
    private static readonly IReadOnlyList<IteratorCommand> JominiIterators = BuildJominiIterators();
    private static readonly string[][] Hoi4ValueReadFolders = { new[] { "common", "dynamic_modifiers" } };
    private static readonly string[][] Hoi4ValueReadFieldFolders = { new[] { "common", "decisions" } };

    private readonly Dictionary<string, FlagCommand> _commands;
    private readonly IReadOnlyList<IteratorCommand> _iteratorRows;
    private readonly Dictionary<string, IReadOnlyList<IteratorCommand>> _iterators;
    private readonly IReadOnlyList<IteratorCommand> _loopVariableRows;
    private readonly Dictionary<string, IReadOnlyList<IteratorCommand>> _loopVariables;

    private FlagCommandTable(
        Dictionary<string, FlagCommand> commands,
        IReadOnlyList<IteratorCommand> iterators,
        bool hasSymbolValues,
        string[][]? definitionFlagFolders = null,
        string[][]? tenetFolders = null,
        bool namesIgnoreCase = false,
        bool atSuffixPatterns = false,
        bool hoi4VariableNames = false,
        bool metaBracketPatterns = false,
        bool eventTargetPrefix = false,
        string[][]? valueReadFolders = null,
        string[]? valueReadFields = null,
        string[][]? valueReadFieldFolders = null,
        IReadOnlyList<IteratorCommand>? loopVariables = null,
        LocStyle localisationStyle = LocStyle.DataFunctions,
        bool conditionalBlocks = false,
        bool inlineScripts = false,
        IReadOnlyList<(string BlockKey, string[] Folder)>? definitionFlagBlocks = null,
        string[]? valueSideFields = null,
        bool scopedVariableNames = false,
        bool composedFlagCommands = false,
        IReadOnlySet<string>? builtInVariables = null)
    {
        BuiltInVariables = builtInVariables ?? new HashSet<string>();
        InlineScripts = inlineScripts;
        DefinitionFlagBlocks = definitionFlagBlocks ?? Array.Empty<(string, string[])>();
        ValueSideFields = valueSideFields ?? Array.Empty<string>();
        ScopedVariableNames = scopedVariableNames;
        ComposedFlagCommands = composedFlagCommands;
        _commands = new Dictionary<string, FlagCommand>(commands, StringComparer.OrdinalIgnoreCase);
        _iteratorRows = iterators;
        _iterators = iterators
            .GroupBy(i => i.Command, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<IteratorCommand>)g.ToList(), StringComparer.OrdinalIgnoreCase);
        HasSymbolValues = hasSymbolValues;
        DefinitionFlagFolders = definitionFlagFolders ?? Array.Empty<string[]>();
        TenetParameterFolders = tenetFolders ?? Array.Empty<string[]>();
        NamesIgnoreCase = namesIgnoreCase;
        AtSuffixPatterns = atSuffixPatterns;
        Hoi4VariableNames = hoi4VariableNames;
        MetaBracketPatterns = metaBracketPatterns;
        EventTargetPrefix = eventTargetPrefix;
        ValueReadFolders = valueReadFolders ?? Array.Empty<string[]>();
        ValueReadFields = valueReadFields ?? Array.Empty<string>();
        ValueReadFieldFolders = valueReadFieldFolders ?? Array.Empty<string[]>();
        LocalisationStyle = localisationStyle;
        ConditionalBlocks = conditionalBlocks;
        _loopVariableRows = loopVariables ?? Array.Empty<IteratorCommand>();
        _loopVariables = _loopVariableRows
            .GroupBy(i => i.Command, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<IteratorCommand>)g.ToList(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Tabelle für EU V.</summary>
    public static FlagCommandTable Eu5 { get; } = new(BuildVariableCommands(), JominiIterators, hasSymbolValues: true);

    /// <summary>Tabelle für Victoria 3: Variablen und Listen wie EU V, aber ohne Flag-Befehle, Symbolwerte und Definitions-Flags (Flags sind dort Variablen).</summary>
    public static FlagCommandTable Vic3 { get; } = new(BuildVariableCommands(), JominiIterators, hasSymbolValues: false);

    /// <summary>
    /// Tabelle für HOI IV: Flags in 7 Familien, Variablen (Name auch als Schlüssel, Wertseite gelesen), temporäre Variablen, Arrays,
    /// Event-Targets; Namen ohne Rücksicht auf Groß/Klein, <c>@</c>-Anhänge und <c>[X]</c> als Muster, Lokalisierung <c>[?x]</c>.
    /// </summary>
    public static FlagCommandTable Hoi4 { get; } = new(
        Hoi4Commands.Build(),
        Hoi4Commands.Iterators(),
        hasSymbolValues: false,
        namesIgnoreCase: true,
        atSuffixPatterns: true,
        hoi4VariableNames: true,
        metaBracketPatterns: true,
        eventTargetPrefix: true,
        valueReadFolders: Hoi4ValueReadFolders,
        valueReadFields: Hoi4Commands.ValueReadFields,
        valueReadFieldFolders: Hoi4ValueReadFieldFolders,
        loopVariables: Hoi4Commands.LoopVariables(),
        localisationStyle: LocStyle.Hoi4Brackets,
        builtInVariables: Hoi4BuiltInVariables.Names);

    /// <summary>
    /// Tabelle für EU IV: feste Flag-Liste in 6 Familien, Variablen (Name in <c>which</c>/<c>variable_name</c> oder als Schlüssel, jedes weitere
    /// Namensfeld gelesen), Event-Targets; Namen ohne Rücksicht auf Groß/Klein, <c>@</c>-Anhänge als Muster, <c>[[P] … ]</c>-Blöcke,
    /// Lokalisierung <c>[Scope.var.GetValue]</c> und <c>[X.GetName]</c>.
    /// </summary>
    public static FlagCommandTable Eu4 { get; } = new(
        Eu4Commands.Build(),
        Array.Empty<IteratorCommand>(),
        hasSymbolValues: false,
        namesIgnoreCase: true,
        atSuffixPatterns: true,
        eventTargetPrefix: true,
        localisationStyle: LocStyle.Eu4Brackets,
        conditionalBlocks: true);

    /// <summary>
    /// Tabelle für CK III: Variablenzeilen von EU V ohne Symbolwerte, die mitgelieferten Flag-Befehle, jeder passende Name aus
    /// <paramref name="extraFlagCommands"/> (ScriptDocs) und die Leser für Definitions-Flags.
    /// </summary>
    public static FlagCommandTable Ck3(IEnumerable<string> extraFlagCommands)
    {
        var map = BuildVariableCommands();
        foreach (var name in Ck3FlagCommands.Shipped.Concat(extraFlagCommands))
        {
            if (Ck3FlagCommands.IsFlagCommand(name))
            {
                map[name] = new FlagCommand(name, FlagKind.ScopeFlag, Ck3FlagCommands.Role(name), Ck3FlagCommands.Family(name), FlagField);
            }
        }

        foreach (var name in Ck3FlagCommands.DefinitionFlagReaders)
        {
            map[name] = new FlagCommand(name, FlagKind.DefinitionFlag, FlagRole.Read, null, FlagField);
        }

        return new(map, JominiIterators, hasSymbolValues: false, Ck3DefinitionFolders, Ck3TenetFolders);
    }

    /// <summary>
    /// Tabelle für Stellaris: Flag-Befehle <c>(set|set_timed|has|remove)_&lt;x&gt;_flag</c> aus der mitgelieferten Liste und jedem passenden Namen aus
    /// <paramref name="extraFlagCommands"/> (ScriptDocs), ohne Sperrliste; Variablen (Name in <c>which</c>/<c>variable</c>, <c>value</c> gelesen), Event-Targets,
    /// Definitions-Flags aus <c>policy_flags</c> und <c>flags</c>, Inline-Skripte, <c>@</c>-Anhänge als Muster, <c>[[P] … ]</c>-Blöcke; Namen groß/klein-genau.
    /// </summary>
    public static FlagCommandTable Stellaris(IEnumerable<string> extraFlagCommands) => new(
        StellarisCommands.Build(extraFlagCommands),
        Array.Empty<IteratorCommand>(),
        hasSymbolValues: false,
        atSuffixPatterns: true,
        eventTargetPrefix: true,
        localisationStyle: LocStyle.StellarisBrackets,
        conditionalBlocks: true,
        inlineScripts: true,
        definitionFlagBlocks: StellarisDefinitionFlagBlocks,
        valueSideFields: StellarisCommands.ValueSideFields,
        scopedVariableNames: true,
        composedFlagCommands: true);

    private static readonly (string BlockKey, string[] Folder)[] StellarisDefinitionFlagBlocks =
    {
        ("policy_flags", new[] { "common", "policies" }),
        ("flags", new[] { "common", "solar_system_initializers" }),
        ("flags", new[] { "common", "governments" }),
        ("flags", new[] { "common", "prescripted_flags" }),
        ("flags", new[] { "common", "astral_rifts" }),
    };

    /// <summary>Tabelle für die Spiel-ID ("eu5", "ck3", "vic3", "hoi4", "eu4", "stellaris"), sonst null.</summary>
    public static FlagCommandTable? For(string gameId, IEnumerable<string>? extraFlagCommands = null) => gameId switch
    {
        "eu5" => Eu5,
        "ck3" => Ck3(extraFlagCommands ?? Array.Empty<string>()),
        "vic3" => Vic3,
        "hoi4" => Hoi4,
        "eu4" => Eu4,
        "stellaris" => Stellaris(extraFlagCommands ?? Array.Empty<string>()),
        _ => null,
    };

    /// <summary>
    /// True, wenn der Index Flag- und Variablennamen ohne Rücksicht auf Groß/Klein vergleicht (HOI IV, EU IV); sonst genau.
    /// Befehls- und Feldnamen gelten unabhängig davon immer ohne Rücksicht auf Groß/Klein.
    /// </summary>
    public bool NamesIgnoreCase { get; }

    /// <summary>True, wenn <c>flag:X</c> als Lesestelle eines Symbolwerts zählt (EU V).</summary>
    public bool HasSymbolValues { get; }

    /// <summary>Ordnerfolgen ab der Wurzel bzw. dem EU-V-Phasenordner (z. B. common/buildings), in denen ein <c>flag = X</c> in beliebiger Tiefe unter einem obersten Block (außerhalb von Flag-Befehlen) ein Definitions-Flag setzt.</summary>
    public IReadOnlyList<string[]> DefinitionFlagFolders { get; }

    /// <summary>Ordnerfolgen ab der Wurzel bzw. dem EU-V-Phasenordner, in denen die Tokens von <c>personal_tenet_parameters = { ... }</c> Definitions-Flags setzen.</summary>
    public IReadOnlyList<string[]> TenetParameterFolders { get; }

    /// <summary>
    /// True, wenn ein <c>@</c>-Anhang mit Bereichswort, Event-Target, Variable oder Parameter einen Namen zum Muster macht
    /// (<see cref="NameShapes.AtSuffix"/>), und Schlüssel, die mit <c>@</c> beginnen (Skriptkonstanten), übersprungen werden (HOI IV, EU IV, Stellaris).
    /// </summary>
    public bool AtSuffixPatterns { get; }

    /// <summary>True, wenn Namen von Variablen, temporären Variablen, Arrays und temporären Arrays durch <see cref="NameShapes.Hoi4VariableName"/> gehen und <c>var:</c> die ganze Kette liest (HOI IV).</summary>
    public bool Hoi4VariableNames { get; }

    /// <summary>True, wenn ein <c>[X]</c> im Namen (HOI IV <c>meta_effect</c>/<c>meta_trigger</c>) zu <c>*</c> wird und die Stelle ein Muster ist.</summary>
    public bool MetaBracketPatterns { get; }

    /// <summary>True, wenn <c>event_target:X</c> in einem Schlüssel oder Wert ein gelesenes Event-Target ist (Name bis zum ersten <c>.</c>; HOI IV, EU IV, Stellaris).</summary>
    public bool EventTargetPrefix { get; }

    /// <summary>
    /// Ordnerfolgen (HOI IV <c>common/dynamic_modifiers</c>), in denen jeder Wert eines direkten Kindes eines obersten Blocks eine gelesene Variable ist,
    /// außer bei <see cref="NameShapes.IsNonNameValue"/> und den Feldern <c>icon</c> und <c>custom_modifier_tooltip</c>.
    /// </summary>
    public IReadOnlyList<string[]> ValueReadFolders { get; }

    /// <summary>Felder (HOI IV <c>cost</c>, <c>days_remove</c>, …), deren Wert in <see cref="ValueReadFieldFolders"/> eine gelesene Variable ist, außer bei <see cref="NameShapes.IsNonNameValue"/>.</summary>
    public IReadOnlyList<string> ValueReadFields { get; }

    /// <summary>Ordnerfolgen, in denen <see cref="ValueReadFields"/> gelten (HOI IV <c>common/decisions</c>; anderswo ist z. B. <c>cost = air</c> kein Name).</summary>
    public IReadOnlyList<string[]> ValueReadFieldFolders { get; }

    /// <summary>Wie Lokalisierung und <c>.gui</c> gelesen werden.</summary>
    public LocStyle LocalisationStyle { get; }

    /// <summary>
    /// True, wenn <c>[[P] … ]</c>-Blöcke gelten (EU IV, Stellaris): Der Leser schneidet ein führendes <c>[[P]</c>/<c>[[!P]</c> und ein
    /// überzähliges <c>]</c> am Ende von Schlüsseln und Werten ab; ein Name mit <c>[[P]text]</c> wird Vorlage (siehe <see cref="ParamResolver"/>).
    /// </summary>
    public bool ConditionalBlocks { get; }

    /// <summary>
    /// True, wenn Stellaris-Inline-Skripte gelten: Jede Datei unter <c>common/inline_scripts</c> ist eine Definition <c>inline_script:&lt;pfad&gt;</c>,
    /// und <c>inline_script = { script = pfad … }</c> bzw. <c>inline_script = "pfad"</c> ruft sie auf (siehe <see cref="ScriptReader.ReadCalls"/>).
    /// </summary>
    public bool InlineScripts { get; }

    /// <summary>
    /// Blöcke, deren schlüssellose Werte Definitions-Flags setzen, je mit der Ordnerfolge, in der sie gelten (in beliebiger Tiefe; Stellaris
    /// <c>policy_flags</c> in common/policies, <c>flags</c> in common/solar_system_initializers usw.). Blockschlüssel ohne Rücksicht auf Groß/Klein.
    /// </summary>
    public IReadOnlyList<(string BlockKey, string[] Folder)> DefinitionFlagBlocks { get; }

    /// <summary>Felder, auf die <see cref="FlagCommand.ValueSideReads"/> beschränkt ist (Stellaris <c>value</c>); leer = jedes Feld außer <c>tooltip</c> und <c>compare</c> (HOI IV).</summary>
    public IReadOnlyList<string> ValueSideFields { get; }

    /// <summary>
    /// True, wenn ein Variablenname eine Bereichskette tragen kann (Stellaris <c>root.x</c>, <c>from.x</c>, <c>event_target:t.x</c>): Es zählt das letzte
    /// Glied nach <c>.</c>; ist es kein Name (<see cref="NameShapes.IsNonNameValue"/>, z. B. <c>from.owner</c>), oder ist der Name selbst keiner
    /// (<c>value:sv|…|</c>, <c>trigger:x</c>, <c>@c</c>), entsteht keine Variablen-Fundstelle.
    /// </summary>
    public bool ScopedVariableNames { get; }

    /// <summary>True, wenn Schlüssel wie <c>set_$SCOPE_TYPE$_flag</c> Flag-Befehle ohne Familie sind (Stellaris; Rolle aus dem Verb).</summary>
    public bool ComposedFlagCommands { get; }

    /// <summary>
    /// Eingebaute Variablen und Arrays (HOI IV, <see cref="Hoi4BuiltInVariables"/>; sonst leer), ohne Rücksicht auf Groß/Klein: Eine gelesene Variable
    /// oder ein gelesenes Array mit diesem Namen (bei <c>x@key</c> der Teil vor dem <c>@</c>) ergibt keine Fundstelle. Setzen bleibt eine Fundstelle.
    /// </summary>
    public IReadOnlySet<string> BuiltInVariables { get; }

    /// <summary>Ist <paramref name="name"/> (schon durch <see cref="NameShapes.Hoi4VariableName"/> geformt) bzw. sein Teil vor dem <c>@</c> eingebaut?</summary>
    internal bool IsBuiltInVariable(string name)
    {
        if (BuiltInVariables.Count == 0)
        {
            return false;
        }

        var at = name.IndexOf('@');
        return BuiltInVariables.Contains(at > 0 ? name[..at] : name);
    }

    /// <summary>
    /// Stabiler Fingerabdruck der Tabelle (SHA-256, erste 16 Hex-Zeichen) aus jedem Befehl mit Art, Rolle, Familie, Namensfeldern und Schaltern,
    /// den Ordnern für Definitions-Flags und Tenets, den Listen-Iteratoren, <see cref="HasSymbolValues"/>, <see cref="NamesIgnoreCase"/> und den
    /// Schaltern und Listen für HOI IV und weitere Spiele (<see cref="AtSuffixPatterns"/> bis <see cref="LocalisationStyle"/>, <see cref="BuiltInVariables"/>).
    /// Gespeicherte Leseergebnisse mit anderem Fingerabdruck werden nicht wiederverwendet.
    /// </summary>
    public string Fingerprint => _fingerprint ??= ComputeFingerprint();

    private string? _fingerprint;

    private string ComputeFingerprint()
    {
        var lines = _commands.Values
            .Select(c => $"cmd|{c.Command}|{c.Kind}|{c.Role}|{c.Family}|{string.Join(",", c.NameFields)}|{c.KeyAsName}|{c.SecondNameRead}|{c.ValueSideReads}")
            .Concat(DefinitionFlagFolders.Select(f => "def|" + string.Join("/", f)))
            .Concat(TenetParameterFolders.Select(f => "tenet|" + string.Join("/", f)))
            .Concat(_iteratorRows.Select(i => $"iter|{i.Command}|{i.Field}|{i.Kind}"))
            .Append("symbols|" + HasSymbolValues)
            .Append("ignorecase|" + NamesIgnoreCase)
            .Append("atsuffix|" + AtSuffixPatterns)
            .Append("hoi4names|" + Hoi4VariableNames)
            .Append("metabrackets|" + MetaBracketPatterns)
            .Append("eventtargets|" + EventTargetPrefix)
            .Concat(ValueReadFolders.Select(f => "valuefolder|" + string.Join("/", f)))
            .Concat(ValueReadFields.Select(f => "valuefield|" + f))
            .Concat(ValueReadFieldFolders.Select(f => "valuefieldfolder|" + string.Join("/", f)))
            .Append("loc|" + LocalisationStyle)
            .Append("conditionalblocks|" + ConditionalBlocks)
            .Concat(_loopVariableRows.Select(i => $"loopvar|{i.Command}|{i.Field}|{i.Kind}|{i.Default}"))
            .Append("inlinescripts|" + InlineScripts)
            .Concat(DefinitionFlagBlocks.Select(b => $"defblock|{b.BlockKey}|{string.Join("/", b.Folder)}"))
            .Concat(ValueSideFields.Select(f => "valuesidefield|" + f))
            .Append("scopedvariables|" + ScopedVariableNames)
            .Append("composedflags|" + ComposedFlagCommands)
            .Concat(BuiltInVariables.Select(n => "builtin|" + n.ToLowerInvariant()))
            .OrderBy(l => l, StringComparer.Ordinal);
        var text = string.Join("\n", lines);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant()[..16];
    }

    /// <summary>Befehl zum Namen (ohne Rücksicht auf Groß/Klein) oder null; bei <see cref="ComposedFlagCommands"/> auch <c>set_$X$_flag</c> und Verwandte.</summary>
    public FlagCommand? Find(string command) =>
        _commands.TryGetValue(command, out var c) ? c
        : ComposedFlagCommands ? StellarisCommands.Composed(command)
        : null;

    /// <summary>Erste Iterator-Zeile des Befehls (ohne Rücksicht auf Groß/Klein) oder null, wenn er kein Listen-Iterator ist.</summary>
    public IteratorCommand? FindIterator(string command) => _iterators.TryGetValue(command, out var rows) ? rows[0] : null;

    /// <summary>
    /// Alle Iterator-Zeilen des Befehls in Tabellenreihenfolge (leer, wenn er kein Listen-Iterator ist). Ein Befehl kann mehrere Felder haben,
    /// z. B. <c>any_in_list</c> mit <c>variable</c>, <c>global_variable</c> und <c>local_variable</c>.
    /// </summary>
    public IReadOnlyList<IteratorCommand> FindIterators(string command) =>
        _iterators.TryGetValue(command, out var rows) ? rows : Array.Empty<IteratorCommand>();

    /// <summary>
    /// Schleifenvariablen des Befehls (leer, wenn keine): <c>Command = { Field = NAME }</c> setzt NAME der Art Kind an dieser Zeile
    /// (HOI IV <c>any_of = { value = v index = i }</c>, <c>for_loop_effect = { value = n }</c>). Fehlt das Feld, gilt der Vorgabename <see cref="IteratorCommand.Default"/> (<c>v</c>, <c>i</c>) an der Zeile des Befehls als gesetzt.
    /// </summary>
    public IReadOnlyList<IteratorCommand> FindLoopVariables(string command) =>
        _loopVariables.TryGetValue(command, out var rows) ? rows : Array.Empty<IteratorCommand>();

    /// <summary>
    /// Iteratoren der Jomini-Spiele: <c>every/random/ordered/any</c> × <c>in_list</c>, <c>in_global_list</c>, <c>in_local_list</c> mit Feld
    /// <c>variable</c> (Art aus dem Befehl); dazu für <c>*_in_list</c> die älteren Felder <c>global_variable</c> und <c>local_variable</c>.
    /// </summary>
    private static IReadOnlyList<IteratorCommand> BuildJominiIterators()
    {
        var rows = new List<IteratorCommand>();
        foreach (var verb in new[] { "every", "random", "ordered", "any" })
        {
            rows.Add(new IteratorCommand($"{verb}_in_list", "variable", FlagKind.VariableList));
            rows.Add(new IteratorCommand($"{verb}_in_list", "global_variable", FlagKind.GlobalVariableList));
            rows.Add(new IteratorCommand($"{verb}_in_list", "local_variable", FlagKind.LocalVariableList));
            rows.Add(new IteratorCommand($"{verb}_in_global_list", "variable", FlagKind.GlobalVariableList));
            rows.Add(new IteratorCommand($"{verb}_in_local_list", "variable", FlagKind.LocalVariableList));
        }

        return rows;
    }

    private static Dictionary<string, FlagCommand> BuildVariableCommands()
    {
        var map = new Dictionary<string, FlagCommand>(StringComparer.OrdinalIgnoreCase);

        void Add(string command, FlagKind kind, FlagRole role) =>
            map[command] = new FlagCommand(command, kind, role, null, NameField);

        foreach (var (infix, kind) in new[] { ("", FlagKind.Variable), ("global_", FlagKind.GlobalVariable), ("local_", FlagKind.LocalVariable) })
        {
            Add($"set_{infix}variable", kind, FlagRole.Set);
            Add($"change_{infix}variable", kind, FlagRole.Set);
            Add($"clamp_{infix}variable", kind, FlagRole.Set);
            Add($"has_{infix}variable", kind, FlagRole.Read);
            Add($"remove_{infix}variable", kind, FlagRole.Remove);
        }

        Add("add_to_variable_list", FlagKind.VariableList, FlagRole.Set);
        Add("is_target_in_variable_list", FlagKind.VariableList, FlagRole.Read);
        Add("variable_list_size", FlagKind.VariableList, FlagRole.Read);
        Add("has_variable_list", FlagKind.VariableList, FlagRole.Read);
        Add("remove_list_variable", FlagKind.VariableList, FlagRole.Remove);
        Add("clear_variable_list", FlagKind.VariableList, FlagRole.Remove);

        foreach (var (infix, kind) in new[] { ("global_", FlagKind.GlobalVariableList), ("local_", FlagKind.LocalVariableList) })
        {
            Add($"add_to_{infix}variable_list", kind, FlagRole.Set);
            Add($"is_target_in_{infix}variable_list", kind, FlagRole.Read);
            Add($"{infix}variable_list_size", kind, FlagRole.Read);
            Add($"has_{infix}variable_list", kind, FlagRole.Read);
            Add($"remove_list_{infix}variable", kind, FlagRole.Remove);
            Add($"clear_{infix}variable_list", kind, FlagRole.Remove);
        }

        return map;
    }
}
