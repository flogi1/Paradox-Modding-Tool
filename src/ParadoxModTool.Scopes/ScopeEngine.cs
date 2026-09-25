namespace ParadoxModTool.Scopes;

public enum ScopeFrameKind
{
    /// <summary>Ein Feld des Objekts (<c>potential</c>): sein Start-Scope kommt aus den gelernten Regeln.</summary>
    Field,

    /// <summary>Ein Block, der den Scope nicht ändert (<c>limit</c>, <c>OR</c>, <c>if</c>).</summary>
    Transparent,

    /// <summary>Ein Block, der den Scope wechselt (Iterator, Event-Target, Kette, <c>root</c>).</summary>
    Transition,
}

public sealed record ScopeFrame(string Label, string? Scope, ScopeFrameKind Kind);

public enum ScopeCheck
{
    /// <summary>Scope oder Name unbekannt: keine Aussage möglich.</summary>
    Unknown,
    Allowed,
    NotAllowed,
}

/// <summary>Ein Trigger oder Effect an einer Stelle, wo er im (sicheren) Scope nicht gilt.</summary>
/// <param name="Start">Position des Namens im Text.</param>
/// <param name="Length">Länge des Namens.</param>
/// <param name="Command">Name des Triggers oder Effects.</param>
/// <param name="Scope">Der Scope an der Stelle.</param>
/// <param name="Supported">Die Scopes, in denen der Name gilt.</param>
public sealed record ScopeIssue(int Start, int Length, string Command, string Scope, IReadOnlyList<string> Supported)
{
    public string Message => $"{Command} gilt nur in {string.Join(", ", Supported)} – hier: {Scope}";
}

/// <summary>Der Scope an einer Stelle im Skript samt dem Weg dorthin.</summary>
public sealed class ScopeContext
{
    public static ScopeContext None(string reason) => new() { UnknownReason = reason };

    /// <summary>Der Scope an der Stelle, oder <see langword="null"/>, wenn er unbekannt ist (siehe <see cref="UnknownReason"/>).</summary>
    public string? CurrentScope { get; init; }

    /// <summary>Warum der Scope unbekannt ist; <see langword="null"/>, wenn er bekannt ist.</summary>
    public string? UnknownReason { get; init; }

    /// <summary>Der Feldpfad (z. B. <c>bureaucracies/*/potential</c>), aus dem der Start-Scope kam.</summary>
    public string? FieldPath { get; init; }

    public string? StartScope { get; init; }

    /// <summary>Zustimmung der gelernten Regel für den Start-Scope (0-1); 0, wenn es keine gab.</summary>
    public double StartSupport { get; init; }

    /// <summary>Sicher: Der Scope stammt aus einer Regel mit mindestens 90 % Zustimmung oder aus einem Wechsel mit eindeutigem Ergebnis (Iterator, Event-Target), und nichts auf dem Weg war unbekannt.</summary>
    public bool Confident { get; init; }

    public IReadOnlyList<ScopeFrame> Frames { get; init; } = Array.Empty<ScopeFrame>();

    /// <summary>Gespeicherte Scopes (<c>save_scope_as</c>), die an dieser Stelle sichtbar sind; ein <see langword="null"/>-Wert heißt: Typ unbekannt.</summary>
    public IReadOnlyDictionary<string, string?> SavedScopes { get; init; } = new Dictionary<string, string?>();

    /// <summary>Gespeicherte Scopes, die das Spiel im aktuellen Feld bereitstellt (<c>actor</c> = country …), aus den gelernten Regeln; Name und Typ.</summary>
    public IReadOnlyDictionary<string, string> LearnedSavedScopes { get; init; } = new Dictionary<string, string>();

    /// <summary>Kurze Beschreibung für die Statusleiste: <c>Scope: country  ·  potential [country] › every_neighbor_country → country › limit</c>.</summary>
    public string Describe()
    {
        var text = $"Scope: {CurrentScope ?? "?"}";
        if (Frames.Count > 0)
        {
            text += "  ·  " + string.Join(" › ", Frames.Select(f => f.Kind switch
            {
                ScopeFrameKind.Field => $"{f.Label} [{f.Scope ?? "?"}]",
                ScopeFrameKind.Transparent => f.Label,
                _ => $"{f.Label} → {f.Scope ?? "?"}",
            }));
        }

        return UnknownReason is null ? text : $"{text}  ·  {UnknownReason}";
    }
}

/// <summary>
/// Bestimmt den Scope an einer Stelle im Skript: Start-Scope des Feldes aus den gelernten Regeln, dann die Wechsel der verschachtelten Blöcke
/// (Iteratoren, Event-Targets, <c>root</c>/<c>prev</c>/<c>this</c>, gespeicherte Scopes). Ist etwas unbekannt, bleibt der Scope <see langword="null"/>
/// statt zu raten.
/// </summary>
public sealed class ScopeEngine
{
    private readonly ScopeDb _db;
    private readonly ScopeRules? _rules;

    public ScopeEngine(ScopeDb db, ScopeRules? rules)
    {
        _db = db;
        _rules = rules;
    }

    /// <summary>Die Nachschlagetabellen, auf denen die Verfolgung beruht (z. B. für die Wegsuche).</summary>
    public ScopeDb Db => _db;

    public ScopeContext Analyze(string text, int caret, string? filePath) =>
        Analyze(ScriptTree.Parse(text), caret, filePath);

    public ScopeContext Analyze(IReadOnlyList<ScriptNode> tree, int caret, string? filePath)
    {
        if (_rules is null)
        {
            return ScopeContext.None("Scope-Regeln noch nicht gelernt");
        }

        var folder = FolderOf(filePath);
        if (folder is null)
        {
            return ScopeContext.None("Datei liegt nicht in common oder events");
        }

        var chain = new List<ScriptNode>();
        IReadOnlyList<ScriptNode> level = tree;
        while (level.FirstOrDefault(n => n.ContainsCaret(caret)) is { } next)
        {
            chain.Add(next);
            level = next.Children!;
        }

        if (chain.Count == 0 || chain[0].Key is null)
        {
            return ScopeContext.None("außerhalb eines Objekts");
        }

        var obj = chain[0];
        var scalars = ScalarsOf(obj);
        if (!TryObjectPath(folder, scalars, out var path, out var pathProblem))
        {
            return ScopeContext.None(pathProblem!);
        }

        var state = Start(path, scalars, withFrames: true);
        for (var i = 1; i < chain.Count; i++)
        {
            CollectSaved(chain[i - 1].Children!, chain[i], state.Current, state.Saved);
            Advance(state, chain[i], i, scalars);
        }

        // Gespeicherte Scopes der Ebene, in der der Cursor selbst steht (Anweisungen vor dem Cursor).
        CollectSaved(chain[^1].Children!, null, state.Current, state.Saved, caret);

        return new ScopeContext
        {
            CurrentScope = state.Current,
            UnknownReason = state.Current is null ? state.Reason ?? "Scope unbekannt" : null,
            FieldPath = state.Path,
            StartScope = state.StartScope,
            StartSupport = state.StartSupport,
            Confident = state.Current is not null && state.Sure,
            Frames = state.Frames!,
            SavedScopes = state.Saved,
            LearnedSavedScopes = _rules.SavedFields.GetValueOrDefault(TopField(state.Path)) ?? EmptyNames,
        };
    }

    /// <summary>
    /// Prüft eine ganze Datei: findet alle Trigger und Effects, die im aktuellen Scope nicht gelten. Gemeldet wird nur, wo der Scope sicher ist
    /// (<see cref="ScopeContext.Confident"/>); bei unsicherem oder unbekanntem Scope schweigt die Prüfung, statt zu raten.
    /// </summary>
    public IReadOnlyList<ScopeIssue> FindIssues(string text, string? filePath) =>
        FindIssues(ScriptTree.Parse(text), filePath);

    public IReadOnlyList<ScopeIssue> FindIssues(IReadOnlyList<ScriptNode> tree, string? filePath)
    {
        var issues = new List<ScopeIssue>();
        if (_rules is null || FolderOf(filePath) is not { } folder)
        {
            return issues;
        }

        foreach (var obj in tree)
        {
            if (obj.Key is null || !obj.IsBlock)
            {
                continue;
            }

            var scalars = ScalarsOf(obj);
            if (TryObjectPath(folder, scalars, out var path, out _))
            {
                WalkChildren(obj.Children!, Start(path, scalars, withFrames: false), 1, scalars, issues);
            }
        }

        return issues;
    }

    /// <summary>Der Zustand der Scope-Verfolgung auf dem Weg vom Objekt zu einer Stelle im Baum.</summary>
    private sealed class WalkState
    {
        public string Path = string.Empty;
        public bool Structural = true;
        public string? FieldScope;
        public double Support;
        public string? Current;

        // Sicher: Der aktuelle Scope ist verlässlich (aus einer sicheren Regel oder einem Wechsel mit eindeutigem Ergebnis).
        public bool Sure;
        public bool FieldSure;
        public string? Reason;
        public string? StartScope;
        public double StartSupport;
        public List<string?> Previous = new();
        public Dictionary<string, string?> Saved = new(StringComparer.Ordinal);
        public List<ScopeFrame>? Frames;

        public WalkState Clone()
        {
            var copy = (WalkState)MemberwiseClone();
            copy.Previous = new List<string?>(Previous);
            copy.Saved = new Dictionary<string, string?>(Saved, StringComparer.Ordinal);
            copy.Frames = Frames is null ? null : new List<ScopeFrame>(Frames);
            return copy;
        }
    }

    private static Dictionary<string, string> ScalarsOf(ScriptNode obj) =>
        obj.Children!
            .Where(c => c.Key is not null && c.Scalar is not null)
            .GroupBy(c => c.Key!)
            .ToDictionary(g => g.Key, g => g.First().Scalar!);

    /// <summary>
    /// Der Feldpfad des Objekts. Ereignisse heißen nach ihrer Kennung ("mod.1"); ihre Art steht im Feld type (country_event, character_event …)
    /// und bildet den Pfad. Ohne type gibt es keinen Pfad.
    /// </summary>
    private static bool TryObjectPath(string folder, IReadOnlyDictionary<string, string> scalars, out string path, out string? problem)
    {
        problem = null;
        if (folder != "events")
        {
            path = $"{folder}/*";
            return true;
        }

        if (scalars.TryGetValue("type", out var eventType))
        {
            path = $"events/{eventType}";
            return true;
        }

        path = string.Empty;
        problem = "Ereignis ohne type";
        return false;
    }

    private WalkState Start(string path, IReadOnlyDictionary<string, string> scalars, bool withFrames)
    {
        var (fieldScope, support) = RuleScope(path, scalars);
        return new WalkState
        {
            Path = path,
            FieldScope = fieldScope,
            Support = support,
            Current = fieldScope,
            Sure = fieldScope is not null,
            FieldSure = fieldScope is not null,
            Reason = fieldScope is null ? UnresolvedReason(path) : null,
            StartScope = fieldScope,
            StartSupport = support,
            Frames = withFrames ? new List<ScopeFrame>() : null,
        };
    }

    /// <summary>Betritt den Block <paramref name="node"/> (Kind auf Ebene <paramref name="depth"/> unter dem Objekt) und aktualisiert den Zustand.</summary>
    private void Advance(WalkState state, ScriptNode node, int depth, IReadOnlyDictionary<string, string> scalars)
    {
        if (node.Key is null)
        {
            state.Frames?.Add(new ScopeFrame("{ }", state.Current, ScopeFrameKind.Transparent));
            return;
        }

        var kind = KeyClassifier.Classify(node.Key, _db);
        if (state.Structural && kind == KeyKind.Structural)
        {
            state.Path += "/" + node.Key;
            (state.FieldScope, state.Support) = RuleScope(state.Path, scalars);
            state.Current = state.FieldScope;
            state.Sure = state.FieldSure = state.FieldScope is not null;
            state.Reason = state.FieldScope is null ? UnresolvedReason(state.Path) : null;
            if (depth == 1 || state.StartScope is null)
            {
                state.StartScope = state.FieldScope;
                state.StartSupport = state.Support;
            }

            state.Frames?.Add(new ScopeFrame(node.Key, state.Current, ScopeFrameKind.Field));
            return;
        }

        state.Structural = false;
        var before = state.Current;
        var chainFromLearnedName = kind == KeyKind.Chain && StartsAtLearnedSavedScope(node.Key, state);
        (state.Current, state.Reason) = Enter(kind, node.Key, state.Current, state.FieldScope, state.Previous, state.Saved, state.Reason, state.Path);
        state.Sure = state.Current is not null && kind switch
        {
            // Ein Iterator oder Event-Target führt unabhängig vom Ausgangs-Scope zu einem bestimmten Scope.
            KeyKind.Iterator => true,
            KeyKind.EventTarget => before is not null && TargetAcceptsScope(node.Key, before),
            KeyKind.Chain => StartsAtEventTarget(node.Key) || chainFromLearnedName || state.Sure,
            KeyKind.Root => state.FieldSure,
            _ => state.Sure,
        };

        if (kind != KeyKind.Transparent)
        {
            state.Previous.Add(before);
        }

        state.Frames?.Add(new ScopeFrame(node.Key, state.Current, kind == KeyKind.Transparent ? ScopeFrameKind.Transparent : ScopeFrameKind.Transition));
    }

    /// <summary>
    /// Geht die Kinder eines Blocks durch: prüft jede Anweisung im Scope des Blocks und steigt in verschachtelte Blöcke ab. Anweisungen direkt im Objekt
    /// (Ebene 1) sind Felder und werden nicht geprüft; Parameterblöcke von Triggern und Effects werden nicht betreten.
    /// </summary>
    private void WalkChildren(
        List<ScriptNode> children,
        WalkState parent,
        int depth,
        IReadOnlyDictionary<string, string> scalars,
        List<ScopeIssue> issues)
    {
        var saved = new Dictionary<string, string?>(parent.Saved, StringComparer.Ordinal);

        foreach (var child in children)
        {
            var kind = child.Key is null ? KeyKind.Transparent : KeyClassifier.Classify(child.Key, _db);

            // Ein Name, der auch ein Modifier ist, kann in einem Modifier-Feld stehen (country_modifier = { … }) und bleibt darum ungeprüft.
            if (depth >= 2 && parent.Sure && parent.Current is not null && child.Key is not null && kind is KeyKind.Parameter or KeyKind.Iterator
                && !_db.IsModifier(child.Key)
                && CheckIn(child.Key, parent.Current) is (ScopeCheck.NotAllowed, var supported))
            {
                issues.Add(new ScopeIssue(child.Start, child.Key.Length, child.Key, parent.Current, supported.OrderBy(s => s, StringComparer.Ordinal).ToList()));
            }

            if (child.IsBlock)
            {
                var next = parent.Clone();
                next.Saved = new Dictionary<string, string?>(saved, StringComparer.Ordinal);
                Advance(next, child, depth, scalars);
                if (kind != KeyKind.Parameter)
                {
                    WalkChildren(child.Children!, next, depth + 1, scalars, issues);
                }
            }
            else if (child.Key is "save_scope_as" or "save_temporary_scope_as" && child.Scalar is { Length: > 0 } name)
            {
                saved[name] = parent.Current;
            }
        }
    }

    /// <summary>Der Schlüssel eines Feldes bis zur obersten Ebene: <c>folder/*/potential/limit</c> → <c>folder/*/potential</c>.</summary>
    internal static string TopField(string path)
    {
        var parts = path.Split('/');
        return parts.Length <= 3 ? path : string.Join('/', parts.Take(3));
    }

    /// <summary>Der gelernte Typ von <c>scope:name</c> im Feld <paramref name="path"/>: erst die Regel des Feldes, dann die des Namens allein.</summary>
    private string? LearnedSaved(string path, string name)
    {
        if (_rules is null)
        {
            return null;
        }

        return _rules.SavedFields.TryGetValue(TopField(path), out var names) && names.TryGetValue(name, out var scope)
            ? scope
            : _rules.SavedNames.GetValueOrDefault(name);
    }

    /// <summary>Beginnt die Kette bei einem <c>scope:name</c>, dessen Typ nicht aus dieser Datei, sondern aus den gelernten Regeln stammt?</summary>
    private bool StartsAtLearnedSavedScope(string chain, WalkState state)
    {
        var first = chain.Split('.')[0];
        if (!first.StartsWith("scope:", StringComparison.Ordinal))
        {
            return false;
        }

        var name = first["scope:".Length..];
        return !(state.Saved.TryGetValue(name, out var known) && known is not null) && LearnedSaved(state.Path, name) is not null;
    }

    /// <summary>
    /// Führt das Event-Target <paramref name="name"/> aus dem Scope <paramref name="scope"/> weiter? Bei unbekanntem Ausgangs-Scope ist nicht einmal sicher, dass das Wort
    /// ein Event-Target meint (<c>value</c> in einem Script-Value etwa), darum gilt der Scope dahinter dann nicht als sicher.
    /// </summary>
    private bool TargetAcceptsScope(string name, string scope) =>
        _db.GetTarget(name) is { } target && (target.Inputs.Count == 0 || target.Inputs.Contains(scope));

    private static readonly IReadOnlyDictionary<string, string> EmptyNames = new Dictionary<string, string>();

    /// <summary>Ist <paramref name="command"/> (Trigger oder Effect) im Scope des Kontexts erlaubt?</summary>
    public (ScopeCheck Result, IReadOnlyCollection<string> Supported) Check(string command, ScopeContext context) =>
        CheckIn(command, context.CurrentScope);

    private (ScopeCheck Result, IReadOnlyCollection<string> Supported) CheckIn(string command, string? scope)
    {
        if (scope is null || _db.GetCommand(command) is not { } known)
        {
            return (ScopeCheck.Unknown, Array.Empty<string>());
        }

        return known.AnyScope || known.Scopes.Contains(scope)
            ? (ScopeCheck.Allowed, known.Scopes)
            : (ScopeCheck.NotAllowed, known.Scopes);
    }

    private (string? Scope, double Support) RuleScope(string path, IReadOnlyDictionary<string, string> scalars)
    {
        if (_rules!.Discriminators.TryGetValue(path, out var discriminator))
        {
            // Der Scope hängt vom Geschwisterfeld ab: ein Wert, den die Vanilla-Dateien nicht eindeutig einem Scope zuordnen, bleibt unbekannt.
            return scalars.TryGetValue(discriminator.Key, out var value) && discriminator.Map.TryGetValue(value, out var mapped)
                ? (mapped, 1.0)
                : (null, _rules.Fields.GetValueOrDefault(path)?.Support ?? 0);
        }

        return _rules.Fields.TryGetValue(path, out var rule) && rule.Scope is not null && rule.Support >= ScopeRules.ConfidentSupport
            ? (rule.Scope, rule.Support)
            : (null, rule?.Support ?? 0);
    }

    /// <summary>Beginnt die Kette bei einem Event-Target (<c>capital.owner</c>) statt bei <c>root</c>, <c>prev</c>, <c>this</c> oder <c>scope:x</c>?</summary>
    private static bool StartsAtEventTarget(string chain)
    {
        var first = chain.Split('.')[0];
        return !first.Contains(':')
            && !first.Equals("root", StringComparison.OrdinalIgnoreCase)
            && !first.Equals("this", StringComparison.OrdinalIgnoreCase)
            && !first.Equals("prev", StringComparison.OrdinalIgnoreCase)
            && !first.Equals("from", StringComparison.OrdinalIgnoreCase)
            && !first.Equals("fromfrom", StringComparison.OrdinalIgnoreCase);
    }

    private string UnresolvedReason(string path) =>
        _rules!.Fields.ContainsKey(path) ? $"Start-Scope von {path} nicht eindeutig" : $"keine Regel für {path}";

    /// <summary>Scope und Grund beim Betreten eines Blocks.</summary>
    private (string? Scope, string? Reason) Enter(
        KeyKind kind,
        string key,
        string? current,
        string? fieldScope,
        List<string?> previous,
        Dictionary<string, string?> saved,
        string? reason,
        string path)
    {
        switch (kind)
        {
            case KeyKind.Transparent:
                return (current, reason);
            case KeyKind.This:
                return (current, reason);
            case KeyKind.Root:
                return fieldScope is null ? (null, "root unbekannt") : (fieldScope, null);
            case KeyKind.Prev:
                return previous.Count > 0 && previous[^1] is { } p ? (p, null) : (null, "prev unbekannt");
            case KeyKind.From:
                return (null, $"{key} ist nicht bestimmbar");
            case KeyKind.Chain:
                return ResolveChain(key, current, fieldScope, previous, saved, path);
            case KeyKind.Iterator:
            {
                var targets = _db.GetCommand(key)!.Targets;
                return targets.Count == 1 ? (targets.First(), null) : (null, $"{key} hat mehrere Ziele");
            }

            case KeyKind.EventTarget:
                return Output(key, current);
            case KeyKind.Parameter:
                return (null, $"Parameterblock von {key}");
            default:
                return (null, $"unbekannter Block {key}");
        }
    }

    private (string? Scope, string? Reason) ResolveChain(
        string key,
        string? current,
        string? fieldScope,
        List<string?> previous,
        Dictionary<string, string?> saved,
        string path)
    {
        var parts = key.Split('.');
        string? scope;
        string? reason = null;
        var first = parts[0];

        if (first.StartsWith("scope:", StringComparison.Ordinal))
        {
            var name = first["scope:".Length..];
            if (saved.TryGetValue(name, out var savedScope) && savedScope is not null)
            {
                scope = savedScope;
            }
            else if (LearnedSaved(path, name) is { } learned)
            {
                scope = learned;
            }
            else
            {
                scope = null;
                reason = $"Typ von scope:{name} unbekannt";
            }
        }
        else if (first.Equals("root", StringComparison.OrdinalIgnoreCase))
        {
            scope = fieldScope;
            reason = scope is null ? "root unbekannt" : null;
        }
        else if (first.Equals("this", StringComparison.OrdinalIgnoreCase))
        {
            scope = current;
        }
        else if (first.Equals("prev", StringComparison.OrdinalIgnoreCase))
        {
            scope = previous.Count > 0 ? previous[^1] : null;
            reason = scope is null ? "prev unbekannt" : null;
        }
        else if (first.Contains(':') || first.Equals("from", StringComparison.OrdinalIgnoreCase) || first.Equals("fromfrom", StringComparison.OrdinalIgnoreCase))
        {
            scope = null;
            reason = $"{first} ist nicht bestimmbar";
        }
        else
        {
            (scope, reason) = Output(first, current);
        }

        for (var i = 1; i < parts.Length && scope is not null; i++)
        {
            (scope, reason) = Output(parts[i], scope);
        }

        return (scope, scope is null ? reason ?? "Kette nicht auflösbar" : null);
    }

    private (string? Scope, string? Reason) Output(string name, string? from)
    {
        if (_db.GetTarget(name) is not { } target)
        {
            return (null, $"unbekanntes Ziel {name}");
        }

        return target.Outputs.Count == 1 ? (target.Outputs.First(), null) : (null, $"{name} hat mehrere Ergebnis-Scopes");
    }

    /// <summary>Merkt <c>save_scope_as = name</c> aus den Anweisungen von <paramref name="siblings"/> vor <paramref name="until"/> (bzw. vor dem Cursor).</summary>
    private static void CollectSaved(IEnumerable<ScriptNode> siblings, ScriptNode? until, string? scopeAtLevel, Dictionary<string, string?> saved, int caret = int.MaxValue)
    {
        foreach (var sibling in siblings)
        {
            if (until is not null && ReferenceEquals(sibling, until))
            {
                return;
            }

            if (until is null && sibling.Start >= caret)
            {
                return;
            }

            if (sibling.Key is "save_scope_as" or "save_temporary_scope_as" && sibling.Scalar is { Length: > 0 } name)
            {
                saved[name] = scopeAtLevel;
            }
        }
    }

    /// <summary>
    /// Der Skriptordner einer Datei: der Ordner unter <c>common</c>, oder <c>events</c>. <see langword="null"/>, wenn der Pfad in keinem von beiden liegt.
    /// Maßgeblich ist der letzte <c>in_game</c>-Ordner im Pfad; ohne ihn der Ordner <c>common</c> oder <c>events</c>, der der Datei am nächsten liegt
    /// (ein Ordner "common" weiter oben im Pfad, etwa <c>steamapps\common</c>, zählt so nicht).
    /// </summary>
    public static string? FolderOf(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return null;
        }

        var segments = filePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        var inGame = Array.FindLastIndex(segments, s => s.Equals("in_game", StringComparison.OrdinalIgnoreCase));
        if (inGame >= 0)
        {
            var kind = inGame + 1 < segments.Length ? segments[inGame + 1] : string.Empty;
            if (kind.Equals("common", StringComparison.OrdinalIgnoreCase) && inGame + 3 < segments.Length)
            {
                return segments[inGame + 2];
            }

            return kind.Equals("events", StringComparison.OrdinalIgnoreCase) && inGame + 2 < segments.Length ? "events" : null;
        }

        var common = Array.FindLastIndex(segments, s => s.Equals("common", StringComparison.OrdinalIgnoreCase));
        var events = Array.FindLastIndex(segments, s => s.Equals("events", StringComparison.OrdinalIgnoreCase));
        if (events > common && events + 1 < segments.Length)
        {
            return "events";
        }

        return common >= 0 && common + 2 < segments.Length ? segments[common + 1] : null;
    }
}
