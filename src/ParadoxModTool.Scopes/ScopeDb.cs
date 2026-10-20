using System.Text.RegularExpressions;
using ParadoxModTool.ScriptDocs;

namespace ParadoxModTool.Scopes;

/// <summary>Einheitliche Schreibweise von Scope-Namen: Trigger nennen "international_organization", Modifier-Kategorien "InternationalOrganization".</summary>
public static class ScopeName
{
    public static string Normalize(string name) =>
        Regex.Replace(name.Trim(), "(?<=[a-z0-9])(?=[A-Z])", "_").Replace(' ', '_').ToLowerInvariant();
}

/// <summary>
/// Nachschlagetabellen über der Script-Datenbank, wie sie die Scope-Verfolgung braucht: in welchen Scopes ein Trigger oder Effect gilt, welche Ziele ein
/// Iterator hat und wohin ein Event-Target führt.
/// </summary>
public sealed class ScopeDb
{
    /// <summary>Trigger oder Effect. <see cref="AnyScope"/> heißt: keine Einschränkung bekannt oder ausdrücklich "none".</summary>
    public sealed record Command(HashSet<string> Scopes, bool AnyScope, HashSet<string> Targets)
    {
        /// <summary>Ein Trigger, der einen Wert vergleicht (<c>Traits: &lt;, &lt;=, =, !=, &gt;, &gt;=</c>) und darum auch als Wert am Kettenende taugt.</summary>
        public bool IsComparison { get; init; }

        /// <summary>Die Beschreibung zeigt eine Form mit Argument (<c>opinion(&lt;target&gt;)</c>).</summary>
        public bool TakesArgument { get; init; }
    }

    public sealed record EventTarget(HashSet<string> Inputs, HashSet<string> Outputs)
    {
        /// <summary>Der Name braucht einen Schlüssel dahinter (<c>location:paris</c>): ein globaler Verweis, kein Ziel, das man allein hinschreibt.</summary>
        public bool RequiresData { get; set; }

        /// <summary>Es gibt eine Form ohne Schlüssel (<c>culture</c>), benutzbar aus <see cref="PlainInputs"/> (leer: aus jedem Scope).</summary>
        public bool PlainForm { get; set; }

        public HashSet<string> PlainInputs { get; } = new(StringComparer.Ordinal);

        /// <summary>Es gibt eine Form mit Schlüssel (<c>culture:KEY</c>, <c>modifier:KEY</c>), benutzbar aus <see cref="DataInputs"/> (leer: globaler Verweis).</summary>
        public bool DataForm { get; set; }

        public HashSet<string> DataInputs { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Die Doku-Logs nennen als Ergebnis nur "various" (Stellaris: <c>target</c>, <c>prevprev</c>, <c>fromfromfrom</c>): der Scope dahinter hängt vom
        /// Kontext ab. "various" steht darum nicht in <see cref="Outputs"/>.
        /// </summary>
        public bool VariousOutputs { get; set; }
    }

    /// <summary>So schreibt Stellaris einen Ergebnis-Scope, der vom Kontext abhängt.</summary>
    private const string Various = "various";

    private static readonly Regex IteratorName = new("^(any|every|random|ordered)_", RegexOptions.Compiled);

    private readonly Dictionary<string, Command> _commands = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EventTarget> _targets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _modifiers = new(StringComparer.Ordinal);

    public static ScopeDb Empty { get; } = new();

    public static ScopeDb FromDatabase(ScriptDocDatabase database)
    {
        var db = new ScopeDb();
        foreach (var entry in database.Entries)
        {
            switch (entry.Kind)
            {
                case ScriptDocKind.Trigger:
                case ScriptDocKind.Effect:
                    db.AddCommand(entry);
                    break;
                case ScriptDocKind.EventTarget:
                    db.AddTarget(entry);
                    break;
                case ScriptDocKind.Modifier:
                    db._modifiers[entry.Name] = entry.Scopes.Select(ScopeName.Normalize).ToHashSet();
                    break;
            }
        }

        return db;
    }

    private void AddCommand(ScriptDocEntry entry)
    {
        var scopes = entry.Scopes.Select(ScopeName.Normalize).ToHashSet();
        // Ältere Spiel-Builds schreiben "none", neuere "none/any"; die Datenbank kann beides enthalten.
        var any = scopes.Count == 0 || scopes.Contains("none") || scopes.Contains("none/any");
        scopes.Remove("none");
        scopes.Remove("none/any");
        var targets = entry.Targets.Select(ScopeName.Normalize).ToHashSet();
        var comparison = entry.Kind == ScriptDocKind.Trigger && entry.Description.Contains("Traits: <, <=, =, !=, >, >=", StringComparison.Ordinal);
        var argument = entry.Description.Contains(entry.Name + "(", StringComparison.Ordinal);

        if (_commands.TryGetValue(entry.Name, out var known))
        {
            known.Scopes.UnionWith(scopes);
            known.Targets.UnionWith(targets);
            _commands[entry.Name] = known with
            {
                AnyScope = known.AnyScope || any,
                IsComparison = known.IsComparison || comparison,
                TakesArgument = known.TakesArgument || argument,
            };
        }
        else
        {
            _commands[entry.Name] = new Command(scopes, any, targets) { IsComparison = comparison, TakesArgument = argument };
        }
    }

    private void AddTarget(ScriptDocEntry entry)
    {
        var inputs = entry.Scopes.Select(ScopeName.Normalize).ToHashSet();
        var outputs = entry.Targets.Select(ScopeName.Normalize).ToHashSet();
        var requiresData = entry.Extra.TryGetValue("Requires Data", out var flag) && flag.Equals("yes", StringComparison.OrdinalIgnoreCase);
        if (!_targets.TryGetValue(entry.Name, out var target))
        {
            _targets[entry.Name] = target = new EventTarget(new HashSet<string>(), new HashSet<string>());
        }

        target.Inputs.UnionWith(inputs);
        target.VariousOutputs |= outputs.Remove(Various);
        target.Outputs.UnionWith(outputs);
        target.RequiresData |= requiresData;

        // Neue Datenbanken nennen beide Formen getrennt (ScriptDocImporter.MergeDuplicates); ältere nur "Requires Data" für alles zusammen.
        var knowsForms = entry.Extra.ContainsKey("Plain Inputs") || entry.Extra.ContainsKey("Data Inputs");
        if (knowsForms)
        {
            if (entry.Extra.TryGetValue("Plain Inputs", out var plain))
            {
                target.PlainForm = true;
                target.PlainInputs.UnionWith(SplitScopes(plain));
            }

            if (entry.Extra.TryGetValue("Data Inputs", out var data))
            {
                target.DataForm = true;
                target.DataInputs.UnionWith(SplitScopes(data));
            }
        }
        else if (requiresData)
        {
            target.DataForm = true;
            target.DataInputs.UnionWith(inputs);
        }
        else
        {
            target.PlainForm = true;
            target.PlainInputs.UnionWith(inputs);
        }
    }

    private static IEnumerable<string> SplitScopes(string list) =>
        list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(ScopeName.Normalize);

    /// <summary>Alle Event-Targets mit ihren Ein- und Ausgabe-Scopes.</summary>
    public IEnumerable<KeyValuePair<string, EventTarget>> Targets => _targets;

    /// <summary>Alle Trigger und Effects.</summary>
    public IEnumerable<KeyValuePair<string, Command>> Commands => _commands;

    public Command? GetCommand(string key) => _commands.GetValueOrDefault(key);

    public EventTarget? GetTarget(string key) => _targets.GetValueOrDefault(key);

    public bool IsModifier(string key) => _modifiers.ContainsKey(key);

    public static bool IsIteratorName(string key) => IteratorName.IsMatch(key);

    /// <summary>
    /// Die Scopes, für die ein Statement "abstimmt": bei Trigger/Effect ihre Scopes, bei einem Modifier seine Kategorien. <see langword="null"/>, wenn der Name
    /// unbekannt oder in jedem Scope gültig ist.
    /// </summary>
    public IReadOnlySet<string>? VoteScopes(string key)
    {
        if (_commands.TryGetValue(key, out var command))
        {
            return command.AnyScope || command.Scopes.Count == 0 ? null : command.Scopes;
        }

        return _modifiers.TryGetValue(key, out var categories) && categories.Count > 0 ? categories : null;
    }

    /// <summary>Ein Scope-Typ, den die Doku-Logs nennen (Scopes und Ziele der Trigger/Effects, Ein- und Ausgaben der Event-Targets); "none" zählt nicht.</summary>
    public bool IsScopeType(string name)
    {
        _scopeTypes ??= _commands.Values.SelectMany(c => c.Scopes.Concat(c.Targets))
            .Concat(_targets.Values.SelectMany(t => t.Inputs.Concat(t.Outputs)))
            .Where(s => s != "none")
            .ToHashSet(StringComparer.Ordinal);
        return _scopeTypes.Contains(ScopeName.Normalize(name));
    }

    private HashSet<string>? _scopeTypes;

    /// <summary>Ein bekannter Trigger, Effect, Modifier oder Event-Target: ein Name, der kein strukturelles Feld einer Datei ist.</summary>
    public bool IsKnownName(string key) => _commands.ContainsKey(key) || _targets.ContainsKey(key) || _modifiers.ContainsKey(key);
}
