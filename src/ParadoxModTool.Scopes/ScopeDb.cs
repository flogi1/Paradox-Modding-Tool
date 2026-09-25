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
    public sealed record Command(HashSet<string> Scopes, bool AnyScope, HashSet<string> Targets);

    public sealed record EventTarget(HashSet<string> Inputs, HashSet<string> Outputs)
    {
        /// <summary>Der Name braucht einen Schlüssel dahinter (<c>location:paris</c>): ein globaler Verweis, kein Ziel, das man allein hinschreibt.</summary>
        public bool RequiresData { get; set; }
    }

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
        var any = scopes.Count == 0 || scopes.Contains("none");
        scopes.Remove("none");
        var targets = entry.Targets.Select(ScopeName.Normalize).ToHashSet();

        if (_commands.TryGetValue(entry.Name, out var known))
        {
            known.Scopes.UnionWith(scopes);
            known.Targets.UnionWith(targets);
            _commands[entry.Name] = known with { AnyScope = known.AnyScope || any };
        }
        else
        {
            _commands[entry.Name] = new Command(scopes, any, targets);
        }
    }

    private void AddTarget(ScriptDocEntry entry)
    {
        var inputs = entry.Scopes.Select(ScopeName.Normalize).ToHashSet();
        var outputs = entry.Targets.Select(ScopeName.Normalize).ToHashSet();
        var requiresData = entry.Extra.TryGetValue("Requires Data", out var flag) && flag.Equals("yes", StringComparison.OrdinalIgnoreCase);
        if (_targets.TryGetValue(entry.Name, out var known))
        {
            known.Inputs.UnionWith(inputs);
            known.Outputs.UnionWith(outputs);
            known.RequiresData |= requiresData;
        }
        else
        {
            _targets[entry.Name] = new EventTarget(inputs, outputs) { RequiresData = requiresData };
        }
    }

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

    /// <summary>Ein bekannter Trigger, Effect, Modifier oder Event-Target: ein Name, der kein strukturelles Feld einer Datei ist.</summary>
    public bool IsKnownName(string key) => _commands.ContainsKey(key) || _targets.ContainsKey(key) || _modifiers.ContainsKey(key);
}
