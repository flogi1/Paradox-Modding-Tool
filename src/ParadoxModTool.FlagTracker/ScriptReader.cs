using System.Text.RegularExpressions;
using ParadoxModTool.Scopes;

namespace ParadoxModTool.FlagTracker;

/// <summary>Liest Paradox-Skript (EU V, CK III, Victoria 3, HOI IV, EU IV, Stellaris) und sammelt Variablen-, Listen-, Flag-, Event-Target- und Präfix-Fundstellen.</summary>
public sealed class ScriptReader
{
    private static readonly Regex PrefixRead = new(
        @"(?<![A-Za-z0-9_])(var|global_var|local_var|flag):([A-Za-z0-9_$]+)", RegexOptions.Compiled);

    /// <summary>HOI IV <c>var:</c> mit ganzer Kette (<c>var:global.x</c>, <c>var:SWI:x</c>, <c>var:x^num</c>); der Name kommt aus <see cref="NameShapes.Hoi4VariableName"/>.</summary>
    private static readonly Regex Hoi4VarRead = new(@"(?<![A-Za-z0-9_])var:([A-Za-z0-9_$.:@^?]+)", RegexOptions.Compiled);

    /// <summary><c>event_target:X</c>; der Name reicht bis zum ersten <c>.</c>.</summary>
    private static readonly Regex EventTargetRead = new(@"(?<![A-Za-z0-9_])event_target:([A-Za-z0-9_$@:]+)", RegexOptions.Compiled);

    /// <summary>Führende <c>[[P]</c>/<c>[[!P]</c> (auch mehrere) eines Schlüssels oder Werts (EU IV <c>[[P] … ]</c>-Blöcke).</summary>
    private static readonly Regex ConditionalOpen = new(@"^(\[\[!?[A-Za-z0-9_]+\])+", RegexOptions.Compiled);

    /// <summary>Bereichswörter, die als zweites Namensfeld (SecondNameRead) kein Name sind.</summary>
    private static readonly HashSet<string> SecondNameScopeWords = new(StringComparer.OrdinalIgnoreCase) { "PREV", "ROOT", "FROM", "THIS" };

    private const string VariablePrefix = "variable:";

    /// <summary><c>[X]</c> in einem Namen (HOI IV <c>meta_effect</c>/<c>meta_trigger</c>).</summary>
    private static readonly Regex MetaBracket = new(@"\[[^\[\]]*\]", RegexOptions.Compiled);

    /// <summary>
    /// Reservierte Felder der Blockformen (spec Runde 2, Festlegung 6, dazu <c>array</c> und <c>index</c>): Ihr Schlüssel ist nie der Name.
    /// </summary>
    private static readonly HashSet<string> ReservedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "var", "value", "tooltip", "compare", "min", "max", "integer", "array", "index", "which", "variable_name", "who", "with",
    };

    /// <summary>Felder, deren Wert bei ValueSideReads nie gelesen wird: <c>tooltip</c> ist ein Lokalisierungsschlüssel, <c>compare</c> ein Vergleichswort.</summary>
    private static readonly HashSet<string> ValueSideSkipFields = new(StringComparer.OrdinalIgnoreCase) { "tooltip", "compare" };

    /// <summary>Felder in den ValueReadFolders, deren Wert kein Variablenname ist (Grafik bzw. Lokalisierungsschlüssel).</summary>
    private static readonly HashSet<string> ValueReadFolderSkipFields = new(StringComparer.OrdinalIgnoreCase) { "icon", "custom_modifier_tooltip" };

    private static readonly string[] ScriptedEffects = { "common", "scripted_effects" };
    private static readonly string[] ScriptedTriggers = { "common", "scripted_triggers" };

    /// <summary>Phasenordner von EU V, unter denen der Spielinhalt liegt (<c>in_game/common/...</c>).</summary>
    private static readonly string[] PhaseFolders = { "in_game", "main_menu", "loading_screen" };

    /// <summary>DLC-Ordner von EU V (<c>dlc/&lt;name&gt;/in_game/...</c>), dieselben Ordnernamen wie in der GFX-Prüfung.</summary>
    private static readonly string[] DlcFolders = { "dlc", "integrated_dlc", "builtin_dlc" };

    private readonly FlagCommandTable _table;

    public ScriptReader(FlagCommandTable table) => _table = table;

    public FileScan Read(string relativePath, string text)
    {
        var scan = new FileScan();
        var ctx = new Context(scan, ComputeLineStarts(text));
        var segments = relativePath.Replace('\\', '/').Split('/');
        var isEffectFile = IsUnder(segments, ScriptedEffects);
        var isTriggerFile = IsUnder(segments, ScriptedTriggers);
        var isDefinitionFlagFile = _table.DefinitionFlagFolders.Any(run => IsUnder(segments, run));
        var isTenetFile = _table.TenetParameterFolders.Any(run => IsUnder(segments, run));
        var isValueReadFile = _table.ValueReadFolders.Any(run => IsUnder(segments, run));
        var valueReadFields = _table.ValueReadFieldFolders.Any(run => IsUnder(segments, run));
        var flagBlocks = _table.DefinitionFlagBlocks.Where(b => IsUnder(segments, b.Folder)).Select(b => b.BlockKey).ToArray();
        var inlineScript = InlineScriptName(segments);
        if (inlineScript is not null)
        {
            scan.Definitions.Add(new DefinitionInfo(inlineScript, false, 1));
        }

        foreach (var node in Parse(text))
        {
            if (IsConstantKey(node.Key))
            {
                continue;
            }

            if (isValueReadFile && node.Children is not null)
            {
                foreach (var child in node.Children)
                {
                    if (child.Key is not null && child.Scalar is not null && !ValueReadFolderSkipFields.Contains(child.Key))
                    {
                        AddValueRead(child.Scalar, ctx.LineOf(child.Start), null, ctx);
                    }
                }
            }

            string? definition = inlineScript;
            if (inlineScript is null && (isEffectFile || isTriggerFile) && node.Key is not null)
            {
                definition = node.Key;
                scan.Definitions.Add(new DefinitionInfo(node.Key, isTriggerFile, ctx.LineOf(node.Start)));
            }

            Walk(node, definition, ctx, isDefinitionFlagFile, isTenetFile, valueReadFields, flagBlocks);
        }

        return scan;
    }

    /// <summary>Präfix der Definitionsnamen von Inline-Skripten (<see cref="FlagCommandTable.InlineScripts"/>).</summary>
    public const string InlineScriptPrefix = "inline_script:";

    private const string InlineScriptKey = "inline_script";

    private static readonly string[] InlineScripts = { "common", "inline_scripts" };

    /// <summary>
    /// Bei <see cref="FlagCommandTable.InlineScripts"/> und einer Datei unter <c>common/inline_scripts</c>: <c>inline_script:</c> plus Pfad darunter
    /// ohne Endung, mit <c>/</c> (<c>common/inline_scripts/contracts/h.txt</c> → <c>inline_script:contracts/h</c>); sonst null.
    /// </summary>
    private string? InlineScriptName(string[] segments)
    {
        if (!_table.InlineScripts || !IsUnder(segments, InlineScripts))
        {
            return null;
        }

        var start = ContentStart(segments);
        var path = string.Join("/", segments[(start + InlineScripts.Length)..]);
        var dot = path.LastIndexOf('.');
        return InlineScriptPrefix + (dot > path.LastIndexOf('/') ? path[..dot] : path);
    }

    /// <summary>Aufgerufener eines Inline-Skript-Pfads: ohne Leerraum und Anführungszeichen am Rand, <c>\</c> zu <c>/</c>, ohne <c>.txt</c>.</summary>
    private static string InlineCallee(string path)
    {
        path = path.Trim().Trim('"').Trim().Replace('\\', '/');
        if (path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^4];
        }

        return InlineScriptPrefix + path;
    }

    /// <summary>
    /// Sammelt jeden Eintrag, dessen Schlüssel in <paramref name="calleeNames"/> steht, als Aufruf.
    /// Ein Block ergibt die skalaren Kinder als Parameter (ein Block-Kind wird null, bei doppelten Schlüsseln gilt der letzte);
    /// <c>eff = yes</c> ergibt Params null. Zeile ist die des aufgerufenen Schlüssels.
    /// </summary>
    /// <remarks>
    /// In Scripted-Effect/Trigger-Dateien ist der oberste Schlüssel die Definition selbst und kein Aufruf; nur seine Kinder werden
    /// durchsucht, und CallerDefinition ist dieser Name. Der Block eines Aufrufs wird nicht weiter durchsucht, denn seine Kinder sind Parameter.
    /// </remarks>
    public IReadOnlyList<CallSite> ReadCalls(string relativePath, string text, IReadOnlySet<string> calleeNames)
    {
        var calls = new List<CallSite>();
        var ctx = new Context(new FileScan(), ComputeLineStarts(text));
        var segments = relativePath.Replace('\\', '/').Split('/');
        var inlineScript = InlineScriptName(segments);
        var isDefinitionFile = inlineScript is null && (IsUnder(segments, ScriptedEffects) || IsUnder(segments, ScriptedTriggers));

        foreach (var node in Parse(text))
        {
            if (isDefinitionFile && node.Key is not null)
            {
                if (node.Children is not null)
                {
                    foreach (var child in node.Children)
                    {
                        CollectCalls(child, node.Key, calleeNames, ctx, calls);
                    }
                }
            }
            else
            {
                CollectCalls(node, inlineScript, calleeNames, ctx, calls);
            }
        }

        return calls;
    }

    /// <summary>
    /// Bei <see cref="FlagCommandTable.InlineScripts"/>: <c>inline_script = "p"</c> (Params null) bzw. <c>inline_script = { script = p P = v … }</c>
    /// (Params ohne <c>script</c>) als Aufruf von <c>inline_script:p</c>, wenn dieser Name in <paramref name="calleeNames"/> steht. Ein Pfad mit <c>$</c>
    /// ergibt immer einen Aufruf mit diesem Pfad als Aufgerufenem (dynamischer Aufruf; <see cref="ParamResolver"/> macht die Vorlagen aller passenden
    /// Inline-Skripte dort zu Mustern). True, wenn der Knoten ein <c>inline_script</c>-Eintrag ist (dann nicht weiter durchsuchen).
    /// </summary>
    private bool TryInlineCall(ScriptNode node, string? definition, IReadOnlySet<string> calleeNames, Context ctx, List<CallSite> calls)
    {
        if (!_table.InlineScripts || !string.Equals(node.Key, InlineScriptKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? path = node.Scalar;
        Dictionary<string, string?>? parameters = null;
        if (node.Children is not null)
        {
            parameters = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var child in node.Children)
            {
                if (child.Key is null)
                {
                    continue;
                }

                if (string.Equals(child.Key, "script", StringComparison.OrdinalIgnoreCase))
                {
                    path = child.Scalar;
                }
                else
                {
                    parameters[child.Key] = child.Scalar;
                }
            }
        }

        if (path is null || path.Trim().Length == 0)
        {
            return true;
        }

        var callee = InlineCallee(path);
        if (callee.Contains('$') || calleeNames.Contains(callee))
        {
            calls.Add(new CallSite(callee, parameters, ctx.LineOf(node.Start), definition));
        }

        return true;
    }

    /// <summary>Skriptbaum des Texts; bei <see cref="FlagCommandTable.ConditionalBlocks"/> ohne die Klammern der <c>[[P] … ]</c>-Blöcke (<see cref="StripConditional"/>).</summary>
    private List<ScriptNode> Parse(string text)
    {
        var nodes = ScriptTree.Parse(text);
        return _table.ConditionalBlocks ? StripConditional(nodes) : nodes;
    }

    /// <summary>
    /// EU IV <c>[[P] … ]</c>: Der Inhalt zählt immer. Von Schlüssel und Wert fallen führende <c>[[P]</c>/<c>[[!P]</c> weg und ein <c>]</c> am Ende,
    /// wenn das Token mehr <c>]</c> als <c>[</c> hat (<c>[[t]set_country_flag = x]</c> → <c>set_country_flag = x</c>). Einträge, von denen dabei
    /// nichts übrig bleibt (die Zeilen <c>[[P]</c> und <c>]</c> der zeilenweisen Form), entfallen. <c>[[P]text]</c> mitten im Namen bleibt stehen.
    /// </summary>
    private static List<ScriptNode> StripConditional(List<ScriptNode> nodes)
    {
        var result = new List<ScriptNode>(nodes.Count);
        foreach (var node in nodes)
        {
            var key = node.Key is null ? null : StripConditional(node.Key);
            var scalar = node.Scalar is null ? null : StripConditional(node.Scalar);
            if (key is { Length: 0 } || scalar is { Length: 0 })
            {
                continue;
            }

            if (key == node.Key && scalar == node.Scalar && node.Children is null)
            {
                result.Add(node);
                continue;
            }

            result.Add(new ScriptNode
            {
                Key = key,
                Scalar = scalar,
                Children = node.Children is null ? null : StripConditional(node.Children),
                Start = node.Start,
                BlockStart = node.BlockStart,
                End = node.End,
            });
        }

        return result;
    }

    private static string StripConditional(string token)
    {
        if (token.IndexOf('[') < 0 && token.IndexOf(']') < 0)
        {
            return token;
        }

        var stripped = ConditionalOpen.Replace(token, string.Empty);
        if (stripped.EndsWith(']') && stripped.Count(c => c == ']') > stripped.Count(c => c == '['))
        {
            stripped = stripped[..^1];
        }

        return stripped;
    }

    private void CollectCalls(ScriptNode node, string? definition, IReadOnlySet<string> calleeNames, Context ctx, List<CallSite> calls)
    {
        if (TryInlineCall(node, definition, calleeNames, ctx, calls))
        {
            return;
        }

        if (node.Key is not null && calleeNames.Contains(node.Key))
        {
            Dictionary<string, string?>? parameters = null;
            if (node.Children is not null)
            {
                parameters = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (var child in node.Children)
                {
                    if (child.Key is not null)
                    {
                        parameters[child.Key] = child.Scalar;
                    }
                }
            }

            calls.Add(new CallSite(node.Key, parameters, ctx.LineOf(node.Start), definition));
            return;
        }

        if (node.Children is not null)
        {
            foreach (var child in node.Children)
            {
                CollectCalls(child, definition, calleeNames, ctx, calls);
            }
        }
    }

    /// <summary>Ein rekursiver Durchlauf; <paramref name="definition"/> ist der umgebende oberste Schlüssel in Scripted-Effect/Trigger-Dateien.</summary>
    /// <remarks>
    /// definitionFlags: Kinder mit <c>flag = X</c> zählen als Definitions-Flag (nicht innerhalb von Flag-Befehlen); tenetParameters: Tokens von
    /// personal_tenet_parameters zählen; valueReadFields: <see cref="FlagCommandTable.ValueReadFields"/> gelten in dieser Datei; flagBlocks: Schlüssel
    /// aus <see cref="FlagCommandTable.DefinitionFlagBlocks"/>, die in dieser Datei gelten (ihre schlüssellosen Werte setzen Definitions-Flags).
    /// </remarks>
    private void Walk(ScriptNode node, string? definition, Context ctx, bool definitionFlags, bool tenetParameters, bool valueReadFields, string[] flagBlocks)
    {
        if (IsConstantKey(node.Key))
        {
            return;
        }

        var line = ctx.LineOf(node.Start);
        if (node.Key is not null)
        {
            ScanPrefixReads(node.Key, definition, line, ctx);
        }

        if (node.Scalar is not null)
        {
            ScanPrefixReads(node.Scalar, definition, line, ctx);
        }

        if (node.Key is not null)
        {
            if (valueReadFields && node.Scalar is not null && _table.ValueReadFields.Contains(node.Key, StringComparer.OrdinalIgnoreCase))
            {
                AddValueRead(node.Scalar, line, definition, ctx);
            }

            if (_table.Find(node.Key) is { } command)
            {
                ReadCommand(node, command, definition, ctx);
            }
            else if (node.Children is not null && _table.FindIterators(node.Key) is { Count: > 0 } iterators)
            {
                foreach (var child in node.Children)
                {
                    if (child.Key is null || child.Scalar is null)
                    {
                        continue;
                    }

                    foreach (var iterator in iterators)
                    {
                        if (string.Equals(iterator.Field, child.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            AddSite(child.Scalar, iterator.Kind, null, FlagRole.Read, ctx.LineOf(child.Start), definition, ctx);
                            break;
                        }
                    }
                }
            }

            if (node.Children is not null)
            {
                AddImplicitLoopVariables(node, definition, line, ctx);
            }
        }

        if (node.Children is not null)
        {
            var isFlagBlock = flagBlocks.Length > 0 && node.Key is not null
                && Array.Exists(flagBlocks, b => string.Equals(b, node.Key, StringComparison.OrdinalIgnoreCase));
            foreach (var child in node.Children)
            {
                if (isFlagBlock && child.Key is null && child.Scalar is not null)
                {
                    AddSite(child.Scalar, FlagKind.DefinitionFlag, null, FlagRole.Set, ctx.LineOf(child.Start), definition, ctx);
                }

                if (definitionFlags && string.Equals(child.Key, "flag", StringComparison.OrdinalIgnoreCase) && child.Scalar is not null)
                {
                    AddSite(child.Scalar, FlagKind.DefinitionFlag, null, FlagRole.Set, ctx.LineOf(child.Start), definition, ctx);
                }
                else if (tenetParameters && node.Key == "personal_tenet_parameters" && child.Key is null && child.Scalar is not null)
                {
                    AddSite(child.Scalar, FlagKind.DefinitionFlag, null, FlagRole.Set, ctx.LineOf(child.Start), definition, ctx);
                }

                if (child.Key is not null && child.Scalar is not null && node.Key is not null && _table.FindLoopVariables(node.Key) is { Count: > 0 } loops
                    && loops.FirstOrDefault(l => string.Equals(l.Field, child.Key, StringComparison.OrdinalIgnoreCase)) is { } loop
                    && !NameShapes.IsNonNameValue(child.Scalar))
                {
                    AddSite(child.Scalar, loop.Kind, null, FlagRole.Set, ctx.LineOf(child.Start), definition, ctx);
                }

                var insideCommand = child.Key is not null && _table.Find(child.Key) is not null;
                Walk(child, definition, ctx, definitionFlags && !insideCommand, tenetParameters, valueReadFields, flagBlocks);
            }
        }
    }

    /// <summary>
    /// Schleifenvariablen ohne ihr Feld (HOI IV <c>for_each_loop = { array = a }</c>): Das Spiel nimmt den Vorgabenamen (<see cref="IteratorCommand.Default"/>,
    /// <c>v</c> bzw. <c>i</c>); er gilt an der Zeile des Befehls als gesetzt. Steht das Feld da, meldet <see cref="Walk"/> seinen Wert.
    /// </summary>
    private void AddImplicitLoopVariables(ScriptNode node, string? definition, int line, Context ctx)
    {
        foreach (var loop in _table.FindLoopVariables(node.Key!))
        {
            if (loop.Default is { } name
                && !node.Children!.Any(c => c.Key is not null && string.Equals(c.Key, loop.Field, StringComparison.OrdinalIgnoreCase)))
            {
                AddSite(name, loop.Kind, null, FlagRole.Set, line, definition, ctx);
            }
        }
    }

    /// <summary>
    /// Name des Befehls: der Wert (<c>cmd = NAME</c>) oder in Blockform das erste NameField; bei <see cref="FlagCommand.KeyAsName"/> sonst der
    /// Schlüssel des ersten Kindes, das kein reserviertes Feld ist (<c>set_variable = { X = 0 }</c>). Bei <see cref="FlagCommand.ValueSideReads"/>
    /// liest jeder weitere skalare Wert eines Kindes (auch der des Schlüssel-Kindes) eine Variable, außer bei <c>tooltip</c>, <c>compare</c>
    /// und <see cref="NameShapes.IsNonNameValue"/>.
    /// </summary>
    private void ReadCommand(ScriptNode node, FlagCommand command, string? definition, Context ctx)
    {
        if (node.Scalar is not null)
        {
            // Präfixwert (has_variable = var:x): die Lesestelle liefert schon ScanPrefixReads, keine zweite Stelle "var:x".
            if (!HasPrefixRead(node.Scalar))
            {
                AddSite(node.Scalar, command.Kind, command.Family, command.Role, ctx.LineOf(node.Start), definition, ctx);
            }

            return;
        }

        if (node.Children is null)
        {
            return;
        }

        ScriptNode? nameField = null;
        foreach (var child in node.Children)
        {
            if (child.Key is not null && child.Scalar is not null && command.NameFields.Contains(child.Key, StringComparer.OrdinalIgnoreCase))
            {
                nameField = child;
                break;
            }
        }

        ScriptNode? nameKey = null;
        if (nameField is not null)
        {
            if (!HasPrefixRead(nameField.Scalar!))
            {
                AddSite(nameField.Scalar!, command.Kind, command.Family, command.Role, ctx.LineOf(nameField.Start), definition, ctx);
            }

            if (command.SecondNameRead)
            {
                ReadFurtherNameFields(node, nameField, command, definition, ctx);
            }
        }
        else if (command.KeyAsName)
        {
            nameKey = node.Children.FirstOrDefault(c => c.Key is not null && !ReservedFields.Contains(c.Key) && !IsConstantKey(c.Key));
            if (nameKey is not null && !HasPrefixRead(nameKey.Key!))
            {
                AddSite(nameKey.Key!, command.Kind, command.Family, command.Role, ctx.LineOf(nameKey.Start), definition, ctx);
            }
        }

        if (!command.ValueSideReads)
        {
            return;
        }

        foreach (var child in node.Children)
        {
            if (child == nameField || child.Key is null || child.Scalar is null || ValueSideSkipFields.Contains(child.Key)
                || (_table.ValueSideFields.Count > 0 && !_table.ValueSideFields.Contains(child.Key, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            AddValueRead(child.Scalar, ctx.LineOf(child.Start), definition, ctx);
        }
    }

    /// <summary>
    /// <see cref="FlagCommand.SecondNameRead"/> (EU IV <c>which = X which = Y</c>): Jedes Namensfeld nach dem ersten liest. <c>PREV</c>, <c>ROOT</c>,
    /// <c>FROM</c>, <c>THIS</c> und Zahlen sind kein Name; <c>event_target:X</c> meldet schon <see cref="ScanPrefixReads"/>; <c>variable:X</c> liest die Variable X.
    /// </summary>
    private void ReadFurtherNameFields(ScriptNode node, ScriptNode first, FlagCommand command, string? definition, Context ctx)
    {
        var seenFirst = false;
        foreach (var child in node.Children!)
        {
            if (child == first)
            {
                seenFirst = true;
                continue;
            }

            if (!seenFirst || child.Key is null || child.Scalar is not { } value
                || !command.NameFields.Contains(child.Key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var line = ctx.LineOf(child.Start);
            if (value.StartsWith(VariablePrefix, StringComparison.OrdinalIgnoreCase))
            {
                AddSite(value[VariablePrefix.Length..], command.Kind, command.Family, FlagRole.Read, line, definition, ctx);
            }
            else if (!SecondNameScopeWords.Contains(value) && !HasPrefixRead(value)
                && !double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                AddSite(value, command.Kind, command.Family, FlagRole.Read, line, definition, ctx);
            }
        }
    }

    /// <summary>Wert, der eine Variable liest: nichts bei <see cref="NameShapes.IsNonNameValue"/> und bei Präfixwerten (die liefert ScanPrefixReads).</summary>
    /// <remarks>Bei <see cref="FlagCommandTable.Hoi4VariableNames"/> auch nichts, wenn das letzte Glied kein Name ist (Bereichsketten wie <c>FROM.FROM</c>, <c>PREV.OWNER</c>).</remarks>
    private void AddValueRead(string value, int line, string? definition, Context ctx)
    {
        if (_table.ScopedVariableNames)
        {
            // root.x, from.owner, event_target:t.x: es zählt das letzte Glied; das Event-Target t meldet ScanPrefixReads.
            // HasPrefixRead braucht es hier nicht: Ein Wert, der nur eine Präfix-Lesestelle ist (event_target:t ohne .x), behält
            // seinen ':' im letzten Glied und fällt durch die Prüfung unten weg.
            var last = ScopedVariableName(value);
            if (last is not null && last.IndexOf(':') < 0)
            {
                AddSite(last, FlagKind.Variable, null, FlagRole.Read, line, definition, ctx);
            }

            return;
        }

        if (NameShapes.IsNonNameValue(value) || HasPrefixRead(value))
        {
            return;
        }

        if (_table.Hoi4VariableNames && NameShapes.IsNonNameValue(NameShapes.Hoi4VariableName(value)))
        {
            return;
        }

        AddSite(value, FlagKind.Variable, null, FlagRole.Read, line, definition, ctx);
    }

    /// <summary>
    /// <see cref="FlagCommandTable.ScopedVariableNames"/>: das letzte Glied nach einem <c>.</c> außerhalb von <c>$…$</c>, oder null, wenn der Wert
    /// oder dieses Glied kein Name ist (<see cref="NameShapes.IsNonNameValue"/>, ein <c>value:</c> irgendwo darin, Bereichswörter wie in <c>from.owner</c>).
    /// </summary>
    internal static string? ScopedVariableName(string value)
    {
        if (NameShapes.IsNonNameValue(value) || value.Contains("value:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var cut = -1;
        var inParam = false;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '$')
            {
                inParam = !inParam;
            }
            else if (value[i] == '.' && !inParam)
            {
                cut = i;
            }
        }

        var last = value[(cut + 1)..];
        return NameShapes.IsNonNameValue(last) ? null : last;
    }

    /// <summary>
    /// Beginnt der Text mit einer Präfix-Lesestelle, die <see cref="ScanPrefixReads"/> schon meldet? Geprüft wird nur der Teil vor dem ersten
    /// <c>@</c> (nur bei <see cref="FlagCommandTable.AtSuffixPatterns"/>): In <c>a@var:b</c> ist <c>a</c> selbst ein Name (Muster <c>a*</c>), nur <c>b</c> eine Präfix-Lesestelle.
    /// </summary>
    private bool HasPrefixRead(string value)
    {
        var at = _table.AtSuffixPatterns ? value.IndexOf('@') : -1;
        var head = at > 0 ? value[..at] : value;
        return head.IndexOf(':') >= 0
            && (PrefixRead.IsMatch(head) || (_table.EventTargetPrefix && EventTargetRead.IsMatch(head)));
    }

    /// <summary>Schlüssel einer Skriptkonstante (<c>@name = …</c>), bei <see cref="FlagCommandTable.AtSuffixPatterns"/> übersprungen.</summary>
    private bool IsConstantKey(string? key) => _table.AtSuffixPatterns && key is not null && key.StartsWith('@');

    private void ScanPrefixReads(string value, string? definition, int line, Context ctx)
    {
        if (value.IndexOf(':') < 0)
        {
            return;
        }

        if (_table.EventTargetPrefix)
        {
            foreach (Match m in EventTargetRead.Matches(value))
            {
                AddSite(m.Groups[1].Value, FlagKind.EventTarget, null, FlagRole.Read, line, definition, ctx);
            }
        }

        if (_table.Hoi4VariableNames)
        {
            foreach (Match m in Hoi4VarRead.Matches(value))
            {
                AddSite(m.Groups[1].Value, FlagKind.Variable, null, FlagRole.Read, line, definition, ctx);
            }
        }

        foreach (Match m in PrefixRead.Matches(value))
        {
            FlagKind kind;
            switch (m.Groups[1].Value)
            {
                case "var" when _table.Hoi4VariableNames: continue;
                case "var": kind = FlagKind.Variable; break;
                case "global_var": kind = FlagKind.GlobalVariable; break;
                case "local_var": kind = FlagKind.LocalVariable; break;
                default:
                    if (!_table.HasSymbolValues)
                    {
                        continue;
                    }

                    kind = FlagKind.SymbolValue;
                    break;
            }

            AddSite(m.Groups[2].Value, kind, null, FlagRole.Read, line, definition, ctx);
        }
    }

    /// <summary>
    /// Fügt eine Fundstelle hinzu. Der Name wird in fester Reihenfolge geformt: <c>@</c>-Anhang (<see cref="NameShapes.AtSuffix"/>), dann <c>[X]</c>
    /// zu <c>*</c>, dann bei Variablen, temporären Variablen und Arrays <see cref="NameShapes.Hoi4VariableName"/>, dann bei Variablen
    /// <see cref="ScopedVariableName"/> (jeweils nur mit dem Schalter der Tabelle; ist der Name danach keiner, entsteht nichts).
    /// Ein Name mit <c>$</c> (oder bei <see cref="FlagCommandTable.ConditionalBlocks"/> mit <c>[[</c>) wird Vorlage, einer mit Platzhalter ein Muster.
    /// Vorher: Eine gelesene Variable oder ein gelesenes Array mit eingebautem Namen (<see cref="FlagCommandTable.BuiltInVariables"/>) ergibt nichts.
    /// </summary>
    private void AddSite(string name, FlagKind kind, string? family, FlagRole role, int line, string? definition, Context ctx)
    {
        // Eingebaute Variable/Array gelesen (HOI IV num_of_factories, party_popularity@communism, global.majors): keine Fundstelle.
        // Geprüft vor dem @-Anhang, denn controller@ROOT wird dort zum Muster controller*.
        if (_table.BuiltInVariables.Count > 0 && role == FlagRole.Read && (kind is FlagKind.Variable or FlagKind.Array)
            && _table.IsBuiltInVariable(NameShapes.Hoi4VariableName(name)))
        {
            return;
        }

        var isPattern = false;
        if (_table.AtSuffixPatterns)
        {
            (name, isPattern) = NameShapes.AtSuffix(name);
        }

        if (_table.MetaBracketPatterns && name.IndexOf('[') >= 0)
        {
            var replaced = MetaBracket.Replace(name, "*");
            isPattern |= replaced != name;
            name = replaced;
        }

        if (_table.Hoi4VariableNames && kind is FlagKind.Variable or FlagKind.TempVariable or FlagKind.Array or FlagKind.TempArray)
        {
            name = NameShapes.Hoi4VariableName(name);
        }

        if (_table.ScopedVariableNames && kind == FlagKind.Variable)
        {
            if (ScopedVariableName(name) is not { } scoped || scoped.IndexOf(':') >= 0)
            {
                return;
            }

            name = scoped;
        }

        if (name.Length == 0)
        {
            return;
        }

        if (name.Contains('$') || (_table.ConditionalBlocks && name.Contains("[[")))
        {
            ctx.Scan.Templates.Add(new TemplateSite(name, kind, family, role, line, definition));
        }
        else
        {
            ctx.Scan.Sites.Add(new RawSite(name, kind, family, role, line, isPattern));
        }
    }

    /// <summary>Index des ersten Segments nach DLC-Präfix (<c>dlc/name/</c>) und Phasenordner.</summary>
    private static int ContentStart(string[] segments)
    {
        var start = segments.Length > 2 && Array.IndexOf(DlcFolders, segments[0]) >= 0 ? 2 : 0;
        if (start < segments.Length && Array.IndexOf(PhaseFolders, segments[start]) >= 0)
        {
            start++;
        }

        return start;
    }

    /// <summary>
    /// Beginnt der Pfad mit der Ordnerfolge <paramref name="run"/>, direkt an der Wurzel oder (EU V) unter einem Phasenordner
    /// (<c>in_game</c>, <c>main_menu</c>, <c>loading_screen</c>), je optional hinter einem DLC-Präfix (<c>dlc/name/</c>). Tiefer liegende Kopien wie <c>backup/common/...</c> zählen nicht.
    /// </summary>
    private static bool IsUnder(string[] segments, string[] run)
    {
        var start = ContentStart(segments);
        if (start + run.Length >= segments.Length)
        {
            return false;
        }

        for (var j = 0; j < run.Length; j++)
        {
            if (segments[start + j] != run[j])
            {
                return false;
            }
        }

        return true;
    }

    private static int[] ComputeLineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        return starts.ToArray();
    }

    private sealed class Context
    {
        private readonly int[] _lineStarts;

        public Context(FileScan scan, int[] lineStarts)
        {
            Scan = scan;
            _lineStarts = lineStarts;
        }

        public FileScan Scan { get; }

        /// <summary>1-basierte Zeile eines Offsets.</summary>
        public int LineOf(int offset)
        {
            var idx = Array.BinarySearch(_lineStarts, offset);
            return (idx >= 0 ? idx : ~idx - 1) + 1;
        }
    }
}
