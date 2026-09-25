using System.Text;
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
        PathStepKind.GlobalLink => $"{Name}:KEY",
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

    /// <summary>
    /// Der Weg als verschachtelte Blöcke zum Einfügen an der Cursorstelle (die Einrückung der ersten Zeile steht dort schon). Der Cursor gehört in die
    /// leere Zeile im innersten Block; <paramref name="caretOffset"/> ist seine Position im Text.
    /// </summary>
    public string ToBlock(string preferredForm, string indent, string newline, out int caretOffset, string tab = "\t")
    {
        var headers = Headers(preferredForm);
        var text = new StringBuilder();

        for (var level = 0; level < headers.Count; level++)
        {
            text.Append(level == 0 ? string.Empty : indent + Repeat(tab, level)).Append(headers[level]).Append(" = {").Append(newline);
        }

        text.Append(indent).Append(Repeat(tab, headers.Count));
        caretOffset = text.Length;

        for (var level = headers.Count - 1; level >= 0; level--)
        {
            text.Append(newline).Append(indent).Append(Repeat(tab, level)).Append('}');
        }

        return text.ToString();
    }

    private static string Repeat(string tab, int count) => string.Concat(Enumerable.Repeat(tab, count));
}

/// <summary>
/// Findet Wege zwischen Scopes: von einem Scope zum Ziel über Event-Targets (<c>capital</c>, <c>owner</c>) und Iteratoren (<c>every_neighbor_country</c>).
/// Der Graph entsteht aus der Script-Datenbank; nur Schritte mit genau einem Ergebnis-Scope zählen (das kann auch die Scope-Verfolgung auflösen).
/// </summary>
public sealed partial class ScopePathFinder
{
    /// <summary>Obergrenze der untersuchten Wege, damit die Suche bei drei Schritten nicht ausufert.</summary>
    private const int MaxCandidates = 20000;

    private static readonly Regex IteratorName = MakeIteratorName();

    private readonly Dictionary<string, List<PathStep>> _fromScope = new(StringComparer.Ordinal);
    private readonly List<PathStep> _fromAnyScope = new();
    private readonly List<PathStep> _globalLinks = new();

    public ScopePathFinder(ScopeDb db)
    {
        var scopes = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var (name, target) in db.Targets)
        {
            if (target.Outputs.Count != 1 || name is "root" or "prev" or "this" or "from" or "fromfrom")
            {
                continue;
            }

            var to = target.Outputs.First();
            scopes.Add(to);
            if (target.RequiresData)
            {
                _globalLinks.Add(new PathStep(PathStepKind.GlobalLink, name, to, AnyInput: true, EmptySet));
            }
            else if (target.Inputs.Count == 0)
            {
                _fromAnyScope.Add(new PathStep(PathStepKind.Target, name, to, AnyInput: true, EmptySet));
            }
            else
            {
                foreach (var input in target.Inputs)
                {
                    scopes.Add(input);
                    Add(input, new PathStep(PathStepKind.Target, name, to, AnyInput: false, EmptySet));
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
    public IReadOnlyList<ScopePath> Find(string start, string goal, int maxSteps = 2, bool includeGlobalLinks = false, string startPrefix = "", int limit = 60)
    {
        var results = new List<ScopePath>();
        if (start == goal || maxSteps < 1)
        {
            return results;
        }

        var steps = new List<PathStep>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { start };
        var usedSelfHop = false;

        void Walk(string scope)
        {
            if (steps.Count >= maxSteps || results.Count >= MaxCandidates)
            {
                return;
            }

            foreach (var step in NextSteps(scope, first: steps.Count == 0, includeGlobalLinks))
            {
                // Kein Scope kommt zweimal vor; nur ein einziger Schritt im selben Scope ist erlaubt (every_neighbor_country, Länder eines Landes ...).
                var selfHop = step.To == scope;
                if (visited.Contains(step.To) && (!selfHop || usedSelfHop))
                {
                    continue;
                }

                steps.Add(step);
                if (step.To == goal)
                {
                    results.Add(new ScopePath(start, goal, startPrefix, steps.ToArray()));
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

        return results
            .OrderBy(p => p.Steps.Count)
            .ThenBy(p => p.Steps.Count(s => s.Kind == PathStepKind.Iterator))
            .ThenBy(p => p.Steps.Count(s => s.Kind == PathStepKind.GlobalLink))
            .ThenBy(p => p.Steps.Count(s => s.AnyInput))
            .ThenBy(p => p.Describe("every"), StringComparer.Ordinal)
            .Take(limit)
            .ToList();
    }

    private IEnumerable<PathStep> NextSteps(string scope, bool first, bool includeGlobalLinks)
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
