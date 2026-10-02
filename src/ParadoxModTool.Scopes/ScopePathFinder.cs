using System.Text.RegularExpressions;

namespace ParadoxModTool.Scopes;

public enum PathStepKind
{
    /// <summary>Ein Event-Target wie <c>capital</c> oder <c>owner</c>: führt von einem Scope zu genau einem anderen.</summary>
    Target,

    /// <summary>Ein Iterator wie <c>every_neighbor_country</c>: ein Block, der über mehrere Ziele läuft.</summary>
    Iterator,

    /// <summary>Ein globaler Verweis mit Schlüssel (<c>location:KEY</c>): von überall aus benutzbar, nur als erster Schritt.</summary>
    GlobalLink,

    /// <summary>Ein Verweis mit Schlüssel aus einem bestimmten Scope (<c>holding:KEY</c>): an jeder Stelle einer Kette benutzbar.</summary>
    DataLink,
}

/// <summary>Ein Schritt eines Weges zwischen zwei Scopes.</summary>
/// <param name="Kind">Art des Schritts.</param>
/// <param name="Name">Name des Event-Targets bzw. Basisname des Iterators (ohne <c>any_</c>/<c>every_</c>/<c>random_</c>/<c>ordered_</c>).</param>
/// <param name="To">Der Scope nach dem Schritt.</param>
/// <param name="AnyInput">Das Spiel nennt keine Eingabe-Scopes: der Schritt gilt scheinbar überall (solche Ziele sind oft allgemein und stehen weiter hinten).</param>
/// <param name="IteratorForms">Bei Iteratoren die Vorsilben, die das Spiel kennt (<c>any</c> für Trigger, <c>every</c>/<c>random</c>/<c>ordered</c> für Effects).</param>
public sealed record PathStep(PathStepKind Kind, string Name, string To, bool AnyInput, IReadOnlySet<string> IteratorForms)
{
    /// <summary>Wie der Schritt im Skript geschrieben wird; bei Iteratoren mit der gewünschten Vorsilbe (falls das Spiel sie kennt, sonst der nächstliegenden).</summary>
    public string Text(string preferredForm) => Kind switch
    {
        PathStepKind.Iterator => $"{ResolveForm(preferredForm)}_{Name}",
        PathStepKind.GlobalLink or PathStepKind.DataLink => $"{Name}:KEY",
        _ => Name,
    };

    private string ResolveForm(string preferred)
    {
        var order = preferred switch
        {
            "any" => new[] { "any", "every", "random", "ordered" },
            "random" => new[] { "random", "every", "ordered", "any" },
            "ordered" => new[] { "ordered", "every", "random", "any" },
            _ => new[] { "every", "random", "ordered", "any" },
        };
        return order.FirstOrDefault(IteratorForms.Contains) ?? order[0];
    }
}

/// <summary>Ein Weg von einem Scope zu einem anderen als Folge von Schritten.</summary>
/// <param name="StartScope">Der Scope, in dem der Weg beginnt.</param>
/// <param name="Goal">Der Ziel-Scope.</param>
/// <param name="StartPrefix">Womit der Weg im Skript beginnt, wenn er nicht im aktuellen Scope startet: <c>root</c> oder <c>scope:actor</c>; sonst leer.</param>
/// <param name="Steps">Die Schritte.</param>
public sealed record ScopePath(string StartScope, string Goal, string StartPrefix, IReadOnlyList<PathStep> Steps)
{
    /// <summary>Besteht der Weg nur aus Event-Targets und Verweisen (keine Iteratoren)? Dann lässt er sich als Kette <c>owner.capital</c> schreiben.</summary>
    public bool IsChain => Steps.All(s => s.Kind != PathStepKind.Iterator);

    /// <summary>
    /// Die Blockköpfe des Weges von außen nach innen: aufeinanderfolgende Targets bilden eine Kette (<c>owner.capital</c>),
    /// jeder Iterator ist ein eigener Block.
    /// </summary>
    public IReadOnlyList<string> Headers(string preferredForm)
    {
        var headers = new List<string>();
        var chain = new List<string>();
        if (StartPrefix.Length > 0)
        {
            chain.Add(StartPrefix);
        }

        foreach (var step in Steps)
        {
            if (step.Kind == PathStepKind.Iterator)
            {
                if (chain.Count > 0)
                {
                    headers.Add(string.Join('.', chain));
                    chain.Clear();
                }

                headers.Add(step.Text(preferredForm));
            }
            else
            {
                chain.Add(step.Text(preferredForm));
            }
        }

        if (chain.Count > 0)
        {
            headers.Add(string.Join('.', chain));
        }

        return headers;
    }

    /// <summary>Kurzform für die Liste: <c>owner.capital</c> oder <c>every_neighbor_country › capital</c>.</summary>
    public string Describe(string preferredForm) => string.Join(" › ", Headers(preferredForm));

    /// <summary>Der Ausdruck einer reinen Kette (<c>owner.capital</c>); <see langword="null"/>, wenn der Weg Iteratoren enthält.</summary>
    public string? ChainText() => IsChain ? Headers("every")[0] : null;
}

/// <summary>Ergebnis einer Wegsuche.</summary>
/// <param name="Paths">Die Wege nach Filter und Obergrenze, kürzeste zuerst.</param>
/// <param name="Total">Anzahl der Wege nach dem Filter, vor der Obergrenze.</param>
/// <param name="Truncated">Die Suche hat bei einer Länge die Obergrenze erreicht und längere Wege nicht mehr untersucht.</param>
public sealed record ScopePathResult(IReadOnlyList<ScopePath> Paths, int Total, bool Truncated);

/// <summary>
/// Findet Wege zwischen Scopes: von einem Scope zum Ziel über Event-Targets (<c>capital</c>, <c>owner</c>) und Iteratoren (<c>every_neighbor_country</c>).
/// Der Graph entsteht aus der Script-Datenbank; nur Schritte mit genau einem Ergebnis-Scope zählen (das kann auch die Scope-Verfolgung auflösen).
/// </summary>
public sealed partial class ScopePathFinder
{
    /// <summary>So viele Suchergebnisse bleiben höchstens im Zwischenspeicher, danach wird er geleert.</summary>
    private const int MaxCachedSearches = 16;

    private static readonly Regex IteratorName = MakeIteratorName();

    private readonly Dictionary<(string Start, string Goal, int MaxSteps, bool GlobalLinks, string Prefix), (List<ScopePath> Paths, bool Truncated)> _searches = new();

    /// <summary>
    /// Obergrenze der Wege je Länge, damit die Suche bei vier Schritten nicht ausufert. Kürzere Längen sind immer vollständig, weil die Suche nach Länge
    /// aufsteigend läuft und nach der ersten gekürzten Länge aufhört.
    /// </summary>
    internal int MaxCandidatesPerLength { get; init; } = 20000;

    private readonly Dictionary<string, List<PathStep>> _fromScope = new(StringComparer.Ordinal);
    private readonly List<PathStep> _fromAnyScope = new();
    private readonly List<PathStep> _globalLinks = new();

    public ScopePathFinder(ScopeDb db)
    {
        var scopes = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var (name, target) in db.Targets)
        {
            // Ziele, die einen Wert liefern (modifier:KEY, estate_power), sind keine Schritte, sondern Werte am Kettenende.
            if (target.Outputs.Count != 1 || target.Outputs.Contains("value") || target.Outputs.Contains("boolean")
                || name is "root" or "prev" or "this" or "from" or "fromfrom")
            {
                continue;
            }

            var to = target.Outputs.First();
            scopes.Add(to);
            if (target.PlainForm)
            {
                AddForm(target.PlainInputs, PathStepKind.Target, _fromAnyScope);
            }

            if (target.DataForm)
            {
                AddForm(target.DataInputs, PathStepKind.DataLink, _globalLinks);
            }

            void AddForm(IReadOnlySet<string> inputs, PathStepKind kind, List<PathStep> withoutInputs)
            {
                if (inputs.Count == 0)
                {
                    withoutInputs.Add(new PathStep(kind == PathStepKind.DataLink ? PathStepKind.GlobalLink : kind, name, to, AnyInput: true, EmptySet));
                    return;
                }

                foreach (var input in inputs)
                {
                    scopes.Add(input);
                    Add(input, new PathStep(kind, name, to, AnyInput: false, EmptySet));
                }
            }
        }

        // Iteratoren: any_x (Trigger) und every_x/random_x/ordered_x (Effects) bilden eine Familie mit demselben Basisnamen und Ziel.
        var families = new Dictionary<(string Name, string To), Family>();
        foreach (var (name, command) in db.Commands)
        {
            if (IteratorName.Match(name) is not { Success: true } match || command.Targets.Count != 1)
            {
                continue;
            }

            var key = (match.Groups[2].Value, command.Targets.First());
            if (!families.TryGetValue(key, out var family))
            {
                families[key] = family = new Family();
            }

            family.Forms.Add(match.Groups[1].Value);
            family.Inputs.UnionWith(command.Scopes);
            family.Any |= command.AnyScope;
        }

        foreach (var ((name, to), family) in families)
        {
            scopes.Add(to);
            if (family.Any || family.Inputs.Count == 0)
            {
                _fromAnyScope.Add(new PathStep(PathStepKind.Iterator, name, to, AnyInput: true, family.Forms));
                continue;
            }

            foreach (var input in family.Inputs)
            {
                scopes.Add(input);
                Add(input, new PathStep(PathStepKind.Iterator, name, to, AnyInput: false, family.Forms));
            }
        }

        Scopes = scopes.ToList();

        // Werte am Kettenende: Trigger, die einen Wert vergleichen (country_tax_base, opinion(ARG)), und Ziele mit Wert-Ausgabe (estate_power, modifier:KEY).
        foreach (var (name, command) in db.Commands)
        {
            if (command.IsComparison)
            {
                AddValue(command.AnyScope ? EmptySet : command.Scopes, command.TakesArgument ? name + "(ARG)" : name);
            }
        }

        foreach (var (name, target) in db.Targets)
        {
            if (!target.Outputs.Contains("value"))
            {
                continue;
            }

            if (target.PlainForm)
            {
                AddValue(target.PlainInputs, name);
            }

            if (target.DataForm)
            {
                AddValue(target.DataInputs, name + ":KEY");
            }
        }
    }

    private readonly Dictionary<string, HashSet<string>> _valuesByScope = new(StringComparer.Ordinal);
    private readonly HashSet<string> _valuesAnywhere = new(StringComparer.Ordinal) { "var:KEY" };

    private void AddValue(IReadOnlySet<string> scopes, string text)
    {
        if (scopes.Count == 0)
        {
            _valuesAnywhere.Add(text);
            return;
        }

        foreach (var scope in scopes)
        {
            if (!_valuesByScope.TryGetValue(scope, out var values))
            {
                _valuesByScope[scope] = values = new HashSet<string>(StringComparer.Ordinal);
            }

            values.Add(text);
        }
    }

    /// <summary>
    /// Was eine Kette im Scope <paramref name="scope"/> als Wert beenden kann, fertig geschrieben und sortiert: vergleichbare Trigger (<c>country_tax_base</c>,
    /// mit Argument <c>opinion(ARG)</c>), Ziele mit Wert-Ausgabe (<c>estate_power</c>, <c>modifier:KEY</c>) und immer <c>var:KEY</c>.
    /// </summary>
    public IReadOnlyList<string> Values(string scope) =>
        _valuesAnywhere
            .Concat(_valuesByScope.TryGetValue(scope, out var values) ? values : Enumerable.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Die Vorschläge für "Wert am Ende": zuerst die eingebauten Werte (<see cref="Values"/>), dann die Script-Values der Mod, dann die aus Vanilla; jede Gruppe
    /// sortiert. Ein Name steht nur an seiner ersten Stelle (eine Mod, die einen Vanilla-Wert überschreibt, steht bei der Mod).
    /// </summary>
    public IReadOnlyList<string> ValueSuggestions(string scope, IReadOnlyList<string> modValues, IReadOnlyList<string> vanillaValues)
    {
        var builtIn = Values(scope);
        var seen = new HashSet<string>(builtIn, StringComparer.Ordinal);
        var result = new List<string>(builtIn);
        foreach (var group in new[] { modValues, vanillaValues })
        {
            result.AddRange(group.Order(StringComparer.Ordinal).Where(seen.Add));
        }

        return result;
    }

    private sealed class Family
    {
        public readonly HashSet<string> Forms = new();
        public readonly HashSet<string> Inputs = new();
        public bool Any;
    }

    private static readonly IReadOnlySet<string> EmptySet = new HashSet<string>();

    /// <summary>Alle Scopes, die im Graph vorkommen (Start oder Ziel eines Schritts), sortiert.</summary>
    public IReadOnlyList<string> Scopes { get; }

    private void Add(string scope, PathStep step)
    {
        if (!_fromScope.TryGetValue(scope, out var steps))
        {
            _fromScope[scope] = steps = new List<PathStep>();
        }

        steps.Add(step);
    }

    /// <summary>
    /// Alle Wege von <paramref name="start"/> nach <paramref name="goal"/> mit höchstens <paramref name="maxSteps"/> Schritten, kürzeste zuerst; bei gleicher Länge
    /// stehen Event-Targets vor Iteratoren und Schritte mit festen Eingabe-Scopes vor allgemeinen. Ohne Weg oder bei Start gleich Ziel ist die Liste leer.
    /// </summary>
    /// <param name="startPrefix">Womit der Weg im Skript beginnt (<c>root</c>, <c>scope:actor</c>), wenn nicht im aktuellen Scope.</param>
    /// <param name="includeGlobalLinks">Auch globale Verweise mit Schlüssel (<c>location:KEY</c>) als erster Schritt anbieten.</param>
    public IReadOnlyList<ScopePath> Find(string start, string goal, int maxSteps = 2, bool includeGlobalLinks = false, string startPrefix = "", int limit = 60) =>
        Search(start, goal, maxSteps, includeGlobalLinks, startPrefix, string.Empty, "every", limit).Paths;

    /// <summary>
    /// Wie <see cref="Find"/>, aber mit Filter vor der Obergrenze <paramref name="limit"/>: <paramref name="filter"/> (Teiltext, ohne Groß/Klein) wird gegen
    /// <see cref="ScopePath.Describe"/> in der Form <paramref name="preferredForm"/> geprüft. Die Wege je Start, Ziel, Schrittzahl, Verweis-Einstellung und Präfix
    /// werden zwischengespeichert, Filtern sucht also nicht neu.
    /// </summary>
    public ScopePathResult Search(string start, string goal, int maxSteps, bool includeGlobalLinks, string startPrefix, string filter, string preferredForm, int limit = 60)
    {
        var key = (start, goal, maxSteps, includeGlobalLinks, startPrefix);
        if (!_searches.TryGetValue(key, out var all))
        {
            if (_searches.Count >= MaxCachedSearches)
            {
                _searches.Clear();
            }

            _searches[key] = all = Collect(start, goal, maxSteps, includeGlobalLinks, startPrefix);
        }

        var matching = filter.Length == 0
            ? all.Paths
            : all.Paths.Where(p => p.Describe(preferredForm).Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        return new ScopePathResult(matching.Take(limit).ToList(), matching.Count, all.Truncated);
    }

    /// <summary>Sucht die Wege Länge für Länge; jede Länge wird für sich sortiert. Erreicht eine Länge die Obergrenze, endet die Suche dort (gekürzt).</summary>
    private (List<ScopePath> Paths, bool Truncated) Collect(string start, string goal, int maxSteps, bool includeGlobalLinks, string startPrefix)
    {
        var all = new List<ScopePath>();
        if (start == goal || maxSteps < 1)
        {
            return (all, false);
        }

        for (var length = 1; length <= maxSteps; length++)
        {
            var found = new List<ScopePath>();
            var steps = new List<PathStep>();
            var visited = new HashSet<string>(StringComparer.Ordinal) { start };
            var usedSelfHop = false;

            void Walk(string scope)
            {
                foreach (var step in Candidates(scope, first: steps.Count == 0, includeGlobalLinks))
                {
                    if (found.Count >= MaxCandidatesPerLength)
                    {
                        return;
                    }

                    // Kein Scope kommt zweimal vor; nur ein einziger Schritt im selben Scope ist erlaubt (every_neighbor_country, Länder eines Landes ...).
                    var selfHop = step.To == scope;
                    if (visited.Contains(step.To) && (!selfHop || usedSelfHop))
                    {
                        continue;
                    }

                    // Ein Weg endet, sobald er das Ziel erreicht: kürzere Treffer gehören zu ihrer eigenen Länge.
                    var last = steps.Count + 1 == length;
                    if (last != (step.To == goal))
                    {
                        continue;
                    }

                    steps.Add(step);
                    if (last)
                    {
                        found.Add(new ScopePath(start, goal, startPrefix, steps.ToArray()));
                    }
                    else
                    {
                        var added = visited.Add(step.To);
                        usedSelfHop |= selfHop;
                        Walk(step.To);
                        usedSelfHop &= !selfHop;
                        if (added)
                        {
                            visited.Remove(step.To);
                        }
                    }

                    steps.RemoveAt(steps.Count - 1);
                }
            }

            Walk(start);
            all.AddRange(found
                .OrderBy(p => p.Steps.Count(s => s.Kind == PathStepKind.Iterator))
                .ThenBy(p => p.Steps.Count(s => s.Kind is PathStepKind.GlobalLink or PathStepKind.DataLink))
                .ThenBy(p => p.Steps.Count(s => s.AnyInput))
                .ThenBy(p => p.Describe("every"), StringComparer.Ordinal));

            if (found.Count >= MaxCandidatesPerLength)
            {
                return (all, true);
            }
        }

        return (all, false);
    }

    /// <summary>
    /// Die möglichen nächsten Glieder aus <paramref name="scope"/>, wie sie der Kettenbaukasten anbietet: feste Eingabe-Scopes vor allgemeinen, dann Event-Targets,
    /// Verweise mit Schlüssel, Iteratoren, globale Verweise (nur als erstes Glied und auf Wunsch), dann nach Name.
    /// </summary>
    public IReadOnlyList<PathStep> NextSteps(string scope, bool first, bool includeGlobalLinks) =>
        Candidates(scope, first, includeGlobalLinks)
            .OrderBy(s => s.AnyInput)
            .ThenBy(s => s.Kind switch
            {
                PathStepKind.Target => 0,
                PathStepKind.DataLink => 1,
                PathStepKind.Iterator => 2,
                _ => 3,
            })
            .ThenBy(s => s.Name, StringComparer.Ordinal)
            .ToList();

    private IEnumerable<PathStep> Candidates(string scope, bool first, bool includeGlobalLinks)
    {
        if (_fromScope.TryGetValue(scope, out var specific))
        {
            foreach (var step in specific)
            {
                yield return step;
            }
        }

        foreach (var step in _fromAnyScope)
        {
            yield return step;
        }

        if (first && includeGlobalLinks)
        {
            foreach (var step in _globalLinks)
            {
                yield return step;
            }
        }
    }

    private static readonly HashSet<string> TriggerLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "limit", "trigger", "potential", "allow", "visible", "enabled", "is_valid", "is_shown", "possible", "can_be_picked", "select_trigger",
    };

    /// <summary>
    /// Schreibt der Nutzer an dieser Stelle voraussichtlich einen Trigger (dann <c>any_</c>-Iteratoren) oder einen Effect (<c>every_</c>)? Der innerste Block entscheidet:
    /// ein <c>any_…</c>-Block oder ein Feld wie <c>limit</c>/<c>potential</c>/<c>trigger</c> heißt Trigger, ein <c>every_…</c>-Block Effect.
    /// </summary>
    public static bool LooksLikeTriggerContext(ScopeContext context)
    {
        foreach (var label in context.Frames.Select(f => f.Label).Reverse().Concat(new[] { context.FieldPath?.Split('/').Last() ?? string.Empty }))
        {
            if (label.StartsWith("any_", StringComparison.Ordinal) || label.StartsWith("trigger_", StringComparison.Ordinal) || TriggerLabels.Contains(label) || label.EndsWith("_trigger", StringComparison.Ordinal))
            {
                return true;
            }

            if (label.StartsWith("every_", StringComparison.Ordinal) || label.StartsWith("random_", StringComparison.Ordinal) || label.StartsWith("ordered_", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return false;
    }

    [GeneratedRegex("^(any|every|random|ordered)_(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex MakeIteratorName();
}
