using System.Globalization;
using System.Text.RegularExpressions;
using ParadoxModTool.Scopes;

namespace ParadoxModTool.ScriptCheck;

/// <summary>
/// Liest eine Skriptdatei für die Skript-Prüfung: Definitionen (Events, Skripteffekte, -trigger, -werte, On-Actions), Event-Aufrufe,
/// Erwähnungen bekannter Namen und Aufrufformen von Skripteffekten. Welche Ordner und Schlüssel zählen, steht in den <see cref="ScriptCheckRules"/>.
/// Pfade sind relativ zum Mod-Stamm; Zeilen 1-basiert. Unterordner der Definitionsordner unter <c>common</c> zählen mit.
/// </summary>
public sealed class ScriptCheckReader
{
    private const string NamespaceKey = "namespace";
    private const string IdKey = "id";
    private const string InlineScriptKey = "inline_script";
    private const string RandomEventsKey = "random_events";
    private const string OnActionsWrapperKey = "on_actions";
    private const string LocalTriggerWord = "scripted_trigger";
    private const string LocalEffectWord = "scripted_effect";

    private static readonly Regex UpperKey = new("^[A-Z0-9_]+$", RegexOptions.CultureInvariant);
    private static readonly char[] NotInCallNames = { ':', '.', '$', '@' };
    private static readonly IReadOnlySet<string> NoBlocks = new HashSet<string>();

    /// <summary>Funktionsname direkt vor <c>(</c>, dann ein Argument in Anführungszeichen.</summary>
    private static readonly Regex LocCall = new(@"([A-Za-z_][A-Za-z_0-9]*)\(\s*(['""])([^'""\r\n]+)\2", RegexOptions.Compiled);

    private readonly ScriptCheckRules _rules;
    private readonly HashSet<string> _locFunctions;
    private readonly HashSet<string> _eventBlocks;
    private readonly HashSet<string> _eventCallKeys;
    private readonly HashSet<string> _onActionLists;
    private readonly HashSet<string> _selfFiringFields;
    private readonly HashSet<string> _contextKeys;
    private readonly HashSet<string> _transparentKeys;
    private readonly HashSet<string> _engineEvents;
    private readonly HashSet<string> _globalIterators;
    private readonly HashSet<string> _onActionCallLists;
    private readonly HashSet<string> _polledEventBlocks;

    public ScriptCheckReader(ScriptCheckRules rules)
    {
        var performance = rules.Performance;
        _globalIterators = new HashSet<string>(performance?.GlobalIterators.Select(g => g.Name) ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
        _onActionCallLists = new HashSet<string>(performance?.OnActionCallLists ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
        _polledEventBlocks = new HashSet<string>(performance?.PolledEventBlocks ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
        _rules = rules;
        _eventBlocks = new HashSet<string>(rules.EventBlocks, StringComparer.Ordinal);
        _eventCallKeys = new HashSet<string>(rules.EventCallKeys, StringComparer.Ordinal);
        _onActionLists = new HashSet<string>(rules.OnActionEventLists, StringComparer.Ordinal);
        _selfFiringFields = new HashSet<string>(rules.SelfFiringFields, StringComparer.Ordinal);
        _contextKeys = new HashSet<string>(rules.ScriptContextKeys.Concat(rules.CallContextKeys), StringComparer.Ordinal);
        _transparentKeys = new HashSet<string>(rules.TransparentKeys.Concat(rules.ScriptContextKeys), StringComparer.Ordinal);
        _locFunctions = new HashSet<string>(rules.LocCallFunctions, StringComparer.Ordinal);
        _engineEvents = new HashSet<string>(rules.EngineEvents.Select(EventIds.Key), StringComparer.Ordinal);
    }

    /// <summary>Lage eines Knotens: Besitzer (umschließende Definition oder Schlüssel oberster Ebene) und was über den Vorfahren liegt.</summary>
    /// <param name="Conditional">Ein Vorfahr unterhalb des Besitzers ist weder durchsichtig noch Skriptkontext-Schlüssel (Kontrollfluss, Scope-Wechsel, Schleife).</param>
    /// <param name="InContext">Ein Vorfahr ist ein Skript- oder Aufrufkontext-Schlüssel, oder die Datei ist eine Effekt-/Trigger-Datei (unterhalb der obersten Ebene).</param>
    /// <param name="Parent">Schlüssel des direkten Elternknotens; null bei anonymen Blöcken.</param>
    /// <param name="CheckedBlock">Direktes Kind des Besitzers aus <see cref="FileContext.CheckedBlocks"/>, unter dem der Knoten liegt; null = keins.</param>
    /// <param name="Iterator">Innerster umschließender globaler Iterator (Knoten mit Block); null = keiner.</param>
    /// <param name="Top">Lage des Besitzer-Knotens selbst (nicht seiner Kinder).</param>
    /// <param name="OwnerChild">Lage der direkten Kinder des Besitzer-Knotens.</param>
    private readonly record struct Frame(
        string? Owner, CheckKind OwnerKind, bool Conditional, bool InContext, string? Parent = null,
        string? CheckedBlock = null, ScriptNode? Iterator = null, bool Top = false, bool OwnerChild = false);

    private sealed class FileContext
    {
        private readonly int[] _lineStarts;

        public FileContext(CheckKind kind, bool ignored, IReadOnlySet<string> checkedBlocks, IReadOnlySet<string> wrappers, string? nameField, string text)
        {
            Kind = kind;
            Ignored = ignored;
            CheckedBlocks = checkedBlocks;
            Wrappers = wrappers;
            NameField = nameField;
            var starts = new List<int> { 0 };
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    starts.Add(i + 1);
                }
            }

            _lineStarts = starts.ToArray();
        }

        /// <summary>Art der Definitionen in der Datei; <see cref="CheckKind.Other"/> = kein Definitionsordner.</summary>
        public CheckKind Kind { get; }

        /// <summary>Datei in einem Unterordner der Event-Ordner, den das Spiel nicht lädt (Stellaris).</summary>
        public bool Ignored { get; }

        /// <summary>Ständig geprüfte Blöcke der Datei (<see cref="ScriptCheckPerformanceRules.AlwaysCheckedBlocks"/> ihres Ordners, in Event-Dateien <see cref="ScriptCheckPerformanceRules.PolledEventBlocks"/>).</summary>
        public IReadOnlySet<string> CheckedBlocks { get; }

        /// <summary>Hüllschlüssel oberster Ebene (<see cref="CheckedBlockRule.Wrappers"/> der Regeln ihres Ordners), deren Kinder die Besitzer sind.</summary>
        public IReadOnlySet<string> Wrappers { get; }

        /// <summary>Feld, dessen Wert ein Kind einer Hülle benennt (<see cref="CheckedBlockRule.NameField"/>, HOI IV <c>id</c>); null = der Schlüssel des Kindes.</summary>
        public string? NameField { get; }

        /// <summary>Ist der Schlüssel oberster Ebene eine Hülle? <c>*</c> in <see cref="Wrappers"/> = jeder Schlüssel.</summary>
        public bool IsWrapper(string key) => Wrappers.Contains(key) || Wrappers.Contains(CheckedBlockRule.AnyWrapper);

        public int LineOf(int offset)
        {
            var idx = Array.BinarySearch(_lineStarts, offset);
            return (idx >= 0 ? idx : ~idx - 1) + 1;
        }
    }

    /// <summary>Definitionen und Event-Aufrufe der Datei.</summary>
    public CheckScan Read(string relativePath, string text)
    {
        var scan = new CheckScan();
        if (IsLocalisation(relativePath))
        {
            // Lokalisierung ist kein Skript: nur ReadLocMentions liest sie.
            return scan;
        }

        var file = Classify(relativePath, text);
        if (file.Ignored)
        {
            return scan;
        }

        var nodes = ScriptTree.Parse(text);
        foreach (var unit in Units(nodes, file))
        {
            if (unit.Kind != CheckKind.Other && unit.Name is { } name)
            {
                var polled = unit.Kind == CheckKind.Event && IsPolled(unit.Node);
                var selfFiring = unit.Kind == CheckKind.Event && (HasSelfFiringField(unit.Node) || _engineEvents.Contains(EventIds.Key(name)));
                scan.Definitions.Add(new CheckDefinition(name, unit.Kind, file.LineOf(unit.Node.Start), selfFiring, EndLine: file.LineOf(unit.Node.End - 1), Polled: polled));
            }
        }

        if (_rules.InlineScriptEventParams && file.Kind == CheckKind.Event)
        {
            foreach (var node in nodes.Where(n => n.Key == InlineScriptKey && n.Children is not null))
            {
                foreach (var param in node.Children!.Where(c => c.Key is not null && c.Children is null && c.Scalar is { } v && IsEventIdShape(v)))
                {
                    scan.Definitions.Add(new CheckDefinition(param.Scalar!, CheckKind.Event, file.LineOf(param.Start), true, Soft: true, EndLine: file.LineOf(param.Start)));
                }
            }
        }

        var templates = new HashSet<CheckTemplate>();
        var sites = new Dictionary<ScriptNode, int>(ReferenceEqualityComparer.Instance);
        // Ende des zuletzt erkannten geprüften Aufrufs mit Parameterblock (oder Event-Aufrufs mit Block): seine Kinder sind Argumente.
        var checkedArgumentsEnd = -1;
        Walk(nodes, file, (node, frame, depth) =>
        {
            AddTemplates(scan, templates, node.Key, frame);
            AddTemplates(scan, templates, node.Scalar, frame);
            if (node.Key is null)
            {
                return;
            }

            if (_rules.Performance is not null && frame.CheckedBlock is not null && !frame.InContext && node.Start >= checkedArgumentsEnd)
            {
                AddCheckedCall(scan, node, frame, file, ref checkedArgumentsEnd);
            }

            if (IsIteratorSite(node, frame))
            {
                if (frame.Iterator is { } outer && sites.TryGetValue(outer, out var outerIndex) && scan.Iterators[outerIndex].Inner is null)
                {
                    scan.Iterators[outerIndex] = scan.Iterators[outerIndex] with { Inner = node.Key };
                }

                sites[node] = scan.Iterators.Count;
                scan.Iterators.Add(new IteratorSite(node.Key, file.LineOf(node.Start), frame.Owner, frame.OwnerKind, frame.CheckedBlock, null, frame.Iterator is not null));
            }

            if (file.Kind == CheckKind.OnAction && !frame.Top && node.Children is not null && _onActionCallLists.Contains(node.Key))
            {
                foreach (var item in node.Children)
                {
                    // Ohne Schlüssel (on_actions = { x }) oder mit Gewicht (random_on_action = { 50 = x }); Gewichtete sind bedingt wie random_events.
                    var weighted = item.Key is not null && IsNumber(item.Key);
                    if ((item.Key is null || weighted) && item.Children is null && item.Scalar is { } target && !IsNumber(target) && !target.Contains('$'))
                    {
                        scan.OnActionCalls.Add(new CheckRef(target, CheckKind.OnAction, file.LineOf(item.Start), frame.Owner, frame.OwnerKind,
                            frame.Conditional || weighted, true, CheckedBlock: frame.CheckedBlock));
                    }
                }
            }

            var isDefinitionBlock = depth == 0 && file.Kind == CheckKind.Event && _eventBlocks.Count > 0;
            if (_eventCallKeys.Contains(node.Key) && !isDefinitionBlock)
            {
                var target = node.Scalar ?? IdOf(node);
                AddCall(scan, target, file.LineOf(node.Start), frame, frame.Conditional);
            }

            if (file.Kind == CheckKind.OnAction && node.Children is not null && _onActionLists.Contains(node.Key))
            {
                var random = node.Key == RandomEventsKey;
                var conditional = frame.Conditional || random;
                foreach (var item in node.Children)
                {
                    var listed = random
                        ? (item.Key is not null && IsNumber(item.Key) ? item.Scalar : null)
                        : (item.Key is null ? item.Scalar : null);
                    if (listed is not null && !IsNumber(listed))
                    {
                        AddCall(scan, listed, file.LineOf(item.Start), frame, conditional);
                    }
                }
            }
        });

        return scan;
    }

    /// <summary>
    /// Jedes Wort aus Schlüsseln und Werten (<see cref="MentionWords.Of"/>), das ein Name aus <paramref name="names"/> ist. Erwähnungen eines Namens
    /// in seiner eigenen Definition zählen nicht (Events über <see cref="EventIds.Key"/> verglichen). <see cref="CheckRef.CallForm"/> = das Wort ist
    /// der ganze Schlüssel und steht im Skriptkontext.
    /// </summary>
    public IReadOnlyList<CheckRef> ReadMentions(string relativePath, string text, IReadOnlyDictionary<string, CheckKind> names)
    {
        var result = new List<CheckRef>();
        var file = Classify(relativePath, text);
        if (file.Ignored || names.Count == 0)
        {
            return result;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        Walk(ScriptTree.Parse(text), file, (node, frame, _) =>
        {
            foreach (var (token, isKey) in new[] { (node.Key, true), (node.Scalar, false) })
            {
                if (token is null)
                {
                    continue;
                }

                seen.Clear();
                foreach (var word in MentionWords.Of(token))
                {
                    if (!seen.Add(word) || !names.TryGetValue(word, out var kind) || IsSelfMention(frame, word, kind))
                    {
                        continue;
                    }

                    var callForm = isKey && word == token && frame.InContext;
                    result.Add(new CheckRef(word, kind, file.LineOf(node.Start), frame.Owner, frame.OwnerKind, frame.Conditional, callForm, CheckedBlock: frame.CheckedBlock));
                }
            }
        });

        return result;
    }

    /// <summary>Ob der Pfad eine Lokalisierungsdatei (<c>.yml</c>) ist.</summary>
    public static bool IsLocalisation(string relativePath) => relativePath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Eindeutige Aufrufe in der Lokalisierung: In jeder Zeile (ohne Kommentarzeilen, die nach dem Kürzen mit <c>#</c> beginnen) jedes
    /// <c>Funktion('name')</c> (auch mit <c>"</c>), dessen Funktionsname in <see cref="ScriptCheckRules.LocCallFunctions"/> steht und dessen Argument ein Name aus
    /// <paramref name="names"/> ist. Ein bloß gleiches Wort zählt nie. Die Verweise haben keinen Besitzer und sind weder bedingt noch in Aufrufform.
    /// </summary>
    public IReadOnlyList<CheckRef> ReadLocMentions(string relativePath, string text, IReadOnlyDictionary<string, CheckKind> names)
    {
        var result = new List<CheckRef>();
        if (_locFunctions.Count == 0 || names.Count == 0)
        {
            return result;
        }

        var line = 0;
        foreach (var raw in text.Split('\n'))
        {
            line++;
            if (raw.AsSpan().TrimStart().StartsWith("#"))
            {
                continue;
            }

            foreach (Match match in LocCall.Matches(raw))
            {
                if (_locFunctions.Contains(match.Groups[1].Value) && names.TryGetValue(match.Groups[3].Value, out var kind))
                {
                    result.Add(new CheckRef(match.Groups[3].Value, kind, line, null, CheckKind.Other, false, false));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Mod-Durchgang: Schlüssel im Skriptkontext mit Wert <c>yes</c> oder mit einem Block, dessen Kinder nur Großbuchstaben-Schlüssel haben
    /// (Parameter); Schlüssel ohne <c>:</c>, <c>.</c>, <c>$</c>, <c>@</c> und keine Zahl. Eingebaute Namen filtert erst die Auswertung.
    /// Kinder eines Event-Aufrufs (<see cref="ScriptCheckRules.EventCallKeys"/> mit Block) sind Argumente und werden übersprungen.
    /// </summary>
    public IReadOnlyList<CheckRef> ReadBlockCalls(string relativePath, string text)
    {
        var result = new List<CheckRef>();
        var file = Classify(relativePath, text);
        if (file.Ignored)
        {
            return result;
        }

        // Ende des zuletzt erkannten Aufrufs mit Parameterblock: seine Kinder sind Parameter, keine Aufrufe (der Lauf geht in Textreihenfolge).
        var parametersEnd = -1;
        Walk(ScriptTree.Parse(text), file, (node, frame, depth) =>
        {
            if (node.Start < parametersEnd)
            {
                return;
            }

            // Kinder eines Event-Aufrufs (trigger_event = { id = … popup = yes }) sind seine Argumente, keine Aufrufe.
            // Ausnahme: der gleichnamige Definitionsblock oberster Ebene (country_event = { id = … } in EU IV/HOI IV/Stellaris), wie in Read.
            var isDefinitionBlock = depth == 0 && file.Kind == CheckKind.Event && _eventBlocks.Count > 0;
            if (node.Key is { } callKey && node.Children is not null && _eventCallKeys.Contains(callKey) && !isDefinitionBlock)
            {
                parametersEnd = node.End;
                return;
            }

            if (depth == 0 || !frame.InContext || node.Key is not { } key || !IsCallName(key))
            {
                return;
            }

            var isBlockCall = node.Children is { Count: > 0 } children && children.All(IsParameter);
            if (node.Scalar is "yes" or "no" || isBlockCall)
            {
                result.Add(new CheckRef(key, CheckKind.Effect, file.LineOf(node.Start), frame.Owner, frame.OwnerKind, frame.Conditional, true, frame.Parent, frame.CheckedBlock));
            }

            if (isBlockCall)
            {
                parametersEnd = node.End;
            }
        });

        return result;
    }

    /// <summary>
    /// Nimmt eine Aufrufform in einem geprüften Block außerhalb des Skriptkontexts in <see cref="CheckScan.CheckedCalls"/> auf (gleiche Form wie
    /// <see cref="ReadBlockCalls"/>: <c>yes</c>/<c>no</c> oder Parameterblock). Kinder eines Parameterblocks oder eines Event-Aufrufs mit Block
    /// sind Argumente: <paramref name="argumentsEnd"/> überspringt sie.
    /// </summary>
    private void AddCheckedCall(CheckScan scan, ScriptNode node, Frame frame, FileContext file, ref int argumentsEnd)
    {
        var key = node.Key!;
        if (node.Children is not null && _eventCallKeys.Contains(key))
        {
            argumentsEnd = node.End;
            return;
        }

        if (!IsCallName(key))
        {
            return;
        }

        var isBlockCall = node.Children is { Count: > 0 } children && children.All(IsParameter);
        if (node.Scalar is "yes" or "no" || isBlockCall)
        {
            scan.CheckedCalls.Add(new CheckRef(key, CheckKind.Effect, file.LineOf(node.Start), frame.Owner, frame.OwnerKind, frame.Conditional, true, frame.Parent, frame.CheckedBlock));
        }

        if (isBlockCall)
        {
            argumentsEnd = node.End;
        }
    }

    private FileContext Classify(string relativePath, string text)
    {
        var r = _rules;
        var ignored = r.IsUnder(relativePath, r.EventFolders, true) && !r.IsUnder(relativePath, r.EventFolders, r.EventSubfolders);
        var kind = r.IsUnder(relativePath, r.EventFolders, r.EventSubfolders) ? CheckKind.Event
            : r.IsUnder(relativePath, r.EffectFolders, true) ? CheckKind.Effect
            : r.IsUnder(relativePath, r.TriggerFolders, true) ? CheckKind.Trigger
            : r.IsUnder(relativePath, r.ValueFolders, true) ? CheckKind.Value
            : r.IsUnder(relativePath, r.KnownNameFolders, true) ? CheckKind.Named
            : r.IsUnder(relativePath, r.OnActionFolders, true) ? CheckKind.OnAction
            : CheckKind.Other;
        var (checkedBlocks, wrappers, nameField) = CheckedBlocksFor(relativePath, kind);
        return new FileContext(kind, ignored, checkedBlocks, wrappers, nameField, text);
    }

    /// <summary>
    /// Ständig geprüfte Blöcke der Datei: die <see cref="CheckedBlockRule.Blocks"/> jeder Regel, die sie betrifft (<see cref="ScriptCheckRules.Matches"/>),
    /// in Event-Dateien dazu <see cref="ScriptCheckPerformanceRules.PolledEventBlocks"/>; dazu die <see cref="CheckedBlockRule.Wrappers"/> dieser Regeln.
    /// Beides leer ohne Leistungsregeln.
    /// </summary>
    private (IReadOnlySet<string> Blocks, IReadOnlySet<string> Wrappers, string? NameField) CheckedBlocksFor(string relativePath, CheckKind kind)
    {
        if (_rules.Performance is not { } performance)
        {
            return (NoBlocks, NoBlocks, null);
        }

        var set = new HashSet<string>(StringComparer.Ordinal);
        var wrappers = new HashSet<string>(StringComparer.Ordinal);
        string? nameField = null;
        foreach (var rule in performance.AlwaysCheckedBlocks)
        {
            if (_rules.Matches(rule, relativePath))
            {
                set.UnionWith(rule.Blocks);
                wrappers.UnionWith(rule.Wrappers ?? Array.Empty<string>());
                nameField ??= rule.NameField;
            }
        }

        if (kind == CheckKind.Event)
        {
            set.UnionWith(_polledEventBlocks);
        }

        return (set, wrappers, nameField);
    }

    /// <summary>Besucht jeden Knoten mit seiner Lage (<see cref="Frame"/> beschreibt die Vorfahren, nicht den Knoten selbst) und Tiefe (0 = oberste Ebene).</summary>
    private void Walk(List<ScriptNode> nodes, FileContext file, Action<ScriptNode, Frame, int> visit)
    {
        foreach (var unit in Units(nodes, file))
        {
            var node = unit.Node;
            // Kinder eines Hüllschlüssels behalten die Lage, die sie unter der Hülle hätten; nur Besitzer und geprüfte Blöcke ändern sich.
            var top = unit.Outer is { } outer
                ? outer with { Owner = unit.Name, OwnerKind = unit.Kind, Top = unit.Descend }
                : new Frame(unit.Name, unit.Kind, false, false, Top: true);
            visit(node, top, unit.Depth);
            if (unit.Descend && node.Children is not null)
            {
                // Der Schlüssel des Besitzers selbst zählt nie als geprüfter Block oder Iterator.
                var inner = unit.Outer is { } wrapped && node.Key is { } key
                    ? top with
                    {
                        Top = false,
                        OwnerChild = true,
                        Conditional = wrapped.Conditional || !_transparentKeys.Contains(key),
                        InContext = wrapped.InContext || _contextKeys.Contains(key),
                        Parent = key,
                    }
                    : top with
                    {
                        Top = false,
                        OwnerChild = true,
                        InContext = (node.Key is not null && _contextKeys.Contains(node.Key))
                            || file.Kind is CheckKind.Effect or CheckKind.Trigger
                            || unit.Kind is CheckKind.Effect or CheckKind.Trigger,
                        // Der Körper einer Definition ist kein Aufruf: seine Kinder haben keinen Eltern-Aufruf.
                        Parent = null,
                    };
                WalkChildren(node.Children, inner, unit.Depth + 1, file, visit);
            }
        }
    }

    /// <summary>Ein Besitzer-Knoten: Definition (Art ≠ <see cref="CheckKind.Other"/>) oder sonstiger Knoten oberster Ebene.</summary>
    /// <param name="Descend">Ob der Lauf in die Kinder geht (nicht beim HOI-IV-Hüllblock, dessen Kinder eigene Einheiten sind).</param>
    /// <param name="Outer">Nur Kinder eines Hüllschlüssels aus <see cref="FileContext.Wrappers"/>: die Lage, die der Lauf unter der Hülle für sie hätte.</param>
    private readonly record struct Unit(ScriptNode Node, string? Name, CheckKind Kind, int Depth, bool Descend, Frame? Outer = null);

    /// <summary>
    /// Die Besitzer-Knoten der Datei. Sonderformen: lokale Helfer in Event-Dateien (<c>scripted_trigger name = { … }</c>, CK III) zählen als
    /// Trigger/Effekt; der Hüllblock <c>on_actions = { on_x = { … } }</c> (HOI IV) liefert jedes Kind als On-Action; ein Hüllschlüssel aus
    /// <see cref="FileContext.Wrappers"/> (EU IV <c>country_decisions</c>) liefert jedes Kind mit Block als eigenen Besitzer der Art <see cref="CheckKind.Other"/>.
    /// </summary>
    private IEnumerable<Unit> Units(List<ScriptNode> nodes, FileContext file)
    {
        var fileKind = file.Kind;
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (fileKind == CheckKind.Event && node.Key is null && LocalHelperKind(node.Scalar) is { } helperKind
                && i + 1 < nodes.Count && nodes[i + 1] is { Key: { } helper, Children: not null } && IsDefinitionKey(helper))
            {
                yield return new Unit(node, null, CheckKind.Other, 0, false);
                yield return new Unit(nodes[++i], helper, helperKind, 0, true);
                continue;
            }

            if (fileKind == CheckKind.OnAction && node.Key == OnActionsWrapperKey
                && node.Children is { Count: > 0 } wrapped && wrapped.All(c => c.Key is not null && c.Children is not null))
            {
                yield return new Unit(node, node.Key, CheckKind.Other, 0, false);
                foreach (var child in wrapped)
                {
                    yield return new Unit(child, child.Key, CheckKind.OnAction, 1, true);
                }

                continue;
            }

            if (node.Key is { } wrapperKey && file.IsWrapper(wrapperKey) && node.Children is { Count: > 0 } wrappedChildren)
            {
                var definition = DefinitionName(node, fileKind);
                var wrapperName = definition ?? wrapperKey;
                var wrapperKind = definition is null ? CheckKind.Other : fileKind;
                yield return new Unit(node, wrapperName, wrapperKind, 0, false);

                // Wie Walk die Kinder eines Besitzers ohne Hülle sähe.
                var outer = new Frame(wrapperName, wrapperKind, false,
                    _contextKeys.Contains(wrapperKey) || fileKind is CheckKind.Effect or CheckKind.Trigger || wrapperKind is CheckKind.Effect or CheckKind.Trigger);
                foreach (var child in wrappedChildren)
                {
                    // Kinder ohne Block bleiben beim Hüllschlüssel, wie ohne Hülle.
                    yield return child is { Key: { } childKey, Children: not null }
                        ? new Unit(child, ChildName(child, childKey, file.NameField), CheckKind.Other, 1, true, outer)
                        : new Unit(child, wrapperName, wrapperKind, 1, false, outer);
                }

                continue;
            }

            // Sonstige Blöcke oberster Ebene benennt das Namensfeld wie Hüllen-Kinder (HOI IV shared_focus = { id = X } → X).
            var name = DefinitionName(node, fileKind);
            var ownerName = name ?? (node is { Key: { } topKey, Children: not null } ? ChildName(node, topKey, file.NameField) : node.Key);
            yield return new Unit(node, ownerName, name is null ? CheckKind.Other : fileKind, 0, true);
        }
    }

    /// <summary>Name eines Hüllen-Kindes oder sonstigen Blocks oberster Ebene: Wert des Feldes <paramref name="nameField"/> (<c>focus = { id = X }</c> → <c>X</c>), sonst der Schlüssel.</summary>
    private static string ChildName(ScriptNode child, string childKey, string? nameField) =>
        nameField is not null && child.Children?.FirstOrDefault(c => c.Key == nameField && c.Children is null && !string.IsNullOrEmpty(c.Scalar))?.Scalar is { } value
            ? value
            : childKey;

    private static CheckKind? LocalHelperKind(string? scalar) => scalar switch
    {
        LocalTriggerWord => CheckKind.Trigger,
        LocalEffectWord => CheckKind.Effect,
        _ => null,
    };

    /// <remarks>Nur ein direktes Kind des Besitzers aus <see cref="FileContext.CheckedBlocks"/> setzt <see cref="Frame.CheckedBlock"/>; es bleibt für alles darunter. Tiefere gleichnamige Schlüssel zählen nie.</remarks>
    private void WalkChildren(List<ScriptNode> nodes, Frame frame, int depth, FileContext file, Action<ScriptNode, Frame, int> visit)
    {
        foreach (var node in nodes)
        {
            visit(node, frame, depth);
            if (node.Children is null)
            {
                continue;
            }

            var inner = node.Key is null
                ? frame with { Parent = null, OwnerChild = false }
                : frame with
                {
                    OwnerChild = false,
                    Conditional = frame.Conditional || !_transparentKeys.Contains(node.Key),
                    InContext = frame.InContext || _contextKeys.Contains(node.Key),
                    Parent = node.Key,
                    CheckedBlock = frame.CheckedBlock ?? (frame.OwnerChild && file.CheckedBlocks.Contains(node.Key) ? node.Key : null),
                    Iterator = IsGlobalIterator(node) ? node : frame.Iterator,
                };
            WalkChildren(node.Children, inner, depth + 1, file, visit);
        }
    }

    /// <summary>Knoten mit Block, dessen Schlüssel ein globaler Iterator aus <see cref="ScriptCheckPerformanceRules.GlobalIterators"/> ist.</summary>
    private bool IsGlobalIterator(ScriptNode node) => node is { Key: { } key, Children: not null } && _globalIterators.Contains(key);

    /// <summary>Stelle eines globalen Iterators; der Besitzer-Knoten selbst ist nie eine.</summary>
    private bool IsIteratorSite(ScriptNode node, Frame frame) => !frame.Top && IsGlobalIterator(node);

    /// <summary>Name der Definition, die der Knoten oberster Ebene in einer Datei dieser Art ist; sonst null.</summary>
    private string? DefinitionName(ScriptNode node, CheckKind fileKind)
    {
        if (fileKind == CheckKind.Other || node.Key is not { } key || !IsDefinitionKey(key))
        {
            return null;
        }

        if (fileKind != CheckKind.Event)
        {
            return key;
        }

        if (_eventBlocks.Count > 0)
        {
            return _eventBlocks.Contains(key) && IdOf(node) is { } id && !id.Contains('$') ? id : null;
        }

        return key.Contains('.') ? key : null;
    }

    private static bool IsDefinitionKey(string key) => key != NamespaceKey && !key.StartsWith('@') && !key.Contains('$');

    /// <summary>Ob das Spiel das Event abfragt: Feld aus <see cref="ScriptCheckRules.SelfFiringFields"/>, oder ohne <see cref="ScriptCheckRules.TriggeredOnlyField"/> <c>= yes</c>.</summary>
    private bool IsPolled(ScriptNode node)
    {
        var children = node.Children ?? new List<ScriptNode>();
        if (IsTriggeredOnly(children))
        {
            return false;
        }

        return children.Any(c => c.Key is not null && _selfFiringFields.Contains(c.Key)) || _rules.TriggeredOnlyField is not null;
    }

    /// <summary>Selbst feuernd für tote Events (unverändert): Feld aus <see cref="ScriptCheckRules.SelfFiringFields"/>, oder ohne <see cref="ScriptCheckRules.TriggeredOnlyField"/> <c>= yes</c>.</summary>
    private bool HasSelfFiringField(ScriptNode node)
    {
        var children = node.Children ?? new List<ScriptNode>();
        return children.Any(c => c.Key is not null && _selfFiringFields.Contains(c.Key))
            || (_rules.TriggeredOnlyField is not null && !IsTriggeredOnly(children));
    }

    private bool IsTriggeredOnly(List<ScriptNode> children) =>
        _rules.TriggeredOnlyField is { } field && children.Any(c => c.Key == field && c.Scalar == "yes");

    private static void AddTemplates(CheckScan scan, HashSet<CheckTemplate> seen, string? token, Frame frame)
    {
        if (token is null)
        {
            return;
        }

        foreach (var word in TemplateMatcher.WordsOf(token))
        {
            var template = new CheckTemplate(word, frame.Owner, frame.OwnerKind);
            if (seen.Add(template))
            {
                scan.Templates.Add(template);
            }
        }
    }

    /// <summary>Ob der Wert die Form einer Event-ID hat: fester Namespace, der keine Zahl ist (<c>COUNT = 1.5</c>), kein <c>$</c>, nach dem letzten Punkt nur Ziffern.</summary>
    private static bool IsEventIdShape(string value)
    {
        if (value.Contains('$') || EventIds.Namespace(value) is not { } ns || IsNumber(ns))
        {
            return false;
        }

        var tail = value[(value.LastIndexOf('.') + 1)..];
        return tail.Length > 0 && tail.All(char.IsAsciiDigit);
    }

    private static bool IsSelfMention(Frame frame, string word, CheckKind kind)
    {
        if (frame.OwnerKind == CheckKind.Other || frame.OwnerKind != kind || frame.Owner is null)
        {
            return false;
        }

        return kind == CheckKind.Event ? EventIds.Key(frame.Owner) == EventIds.Key(word) : frame.Owner == word;
    }

    private static string? IdOf(ScriptNode node) => node.Children?.FirstOrDefault(c => c.Key == IdKey && c.Scalar is not null)?.Scalar;

    /// <summary>
    /// Nimmt einen Event-Aufruf auf. Ziele mit <c>$</c> nur in der Form <c>namespace.…$…</c> mit festem Namespace (<see cref="EventIds.Namespace"/>);
    /// sie stehen für irgendein Event des Namespace. Andere Ziele mit <c>$</c> (<c>$EVENT$</c>, <c>$NS$.5</c>) entfallen.
    /// </summary>
    private static void AddCall(CheckScan scan, string? target, int line, Frame frame, bool conditional)
    {
        if (string.IsNullOrEmpty(target) || (target.Contains('$') && EventIds.Namespace(target) is null))
        {
            return;
        }

        scan.EventCalls.Add(new CheckRef(target, CheckKind.Event, line, frame.Owner, frame.OwnerKind, conditional, true, CheckedBlock: frame.CheckedBlock));
    }

    /// <summary>Parameter eines Skripteffekt-Aufrufs: <c>GROSS = wert</c> mit mindestens einem Buchstaben (nicht <c>OR = { … }</c>, nicht <c>10 = { … }</c>).</summary>
    private static bool IsParameter(ScriptNode child) =>
        child is { Key: { } key, Scalar: not null, Children: null } && IsParameterName(key);

    /// <summary>Ob der Schlüssel nur aus Großbuchstaben, Ziffern und <c>_</c> besteht und einen Buchstaben hat (Parametername oder Scope wie <c>GER</c>).</summary>
    internal static bool IsParameterName(string key) => UpperKey.IsMatch(key) && key.Any(char.IsAsciiLetter);

    private static bool IsCallName(string key) =>
        key.IndexOfAny(NotInCallNames) < 0 && !IsNumber(key);

    private static bool IsNumber(string s) => decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
}
