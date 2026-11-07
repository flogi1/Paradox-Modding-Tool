namespace ParadoxModTool.ScriptCheck;

/// <summary>Ein Zyklus: Mitglieder ordinal sortiert; <paramref name="Unconditional"/> = er enthält einen Kreis nur aus unbedingten Kanten.</summary>
public sealed record CycleGroup(IReadOnlyList<string> Members, bool Unconditional);

/// <summary>Findet Zyklen in einem gerichteten Graphen (starke Zusammenhangskomponenten nach Tarjan, ohne Rekursion).</summary>
public static class CycleFinder
{
    /// <summary>
    /// Starke Zusammenhangskomponenten mit mindestens zwei Mitgliedern oder einer Schleife auf sich selbst, nach dem ersten Mitglied sortiert.
    /// <see cref="CycleGroup.Unconditional"/>: die Komponente enthält eine solche Komponente des Teilgraphen aus den unbedingten Kanten
    /// (jede Komponente des Teilgraphen liegt ganz in einer Komponente des ganzen Graphen).
    /// </summary>
    public static IReadOnlyList<CycleGroup> Find(IReadOnlyCollection<(string From, string To, bool Conditional)> edges)
    {
        var unconditional = Components(edges.Where(e => !e.Conditional).Select(e => (e.From, e.To)).ToList())
            .SelectMany(c => c)
            .ToHashSet(StringComparer.Ordinal);
        return Components(edges.Select(e => (e.From, e.To)).ToList())
            .Select(c => new CycleGroup(c, c.Any(unconditional.Contains)))
            .OrderBy(g => g.Members[0], StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Komponenten mit mindestens zwei Mitgliedern oder einer Selbstschleife, Mitglieder ordinal sortiert.</summary>
    private static List<List<string>> Components(IReadOnlyList<(string From, string To)> edges)
    {
        var successors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var seen = new HashSet<(string, string)>();
        var selfLoops = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (from, to) in edges)
        {
            if (!successors.TryGetValue(from, out var list))
            {
                successors[from] = list = new List<string>();
            }

            successors.TryAdd(to, new List<string>());
            if (from == to)
            {
                selfLoops.Add(from);
            }
            else if (seen.Add((from, to)))
            {
                list.Add(to);
            }
        }

        var result = new List<List<string>>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        var low = new Dictionary<string, int>(StringComparer.Ordinal);
        var onStack = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var work = new Stack<(string Node, int Next)>();
        var counter = 0;

        void Enter(string node)
        {
            index[node] = low[node] = counter++;
            stack.Push(node);
            onStack.Add(node);
            work.Push((node, 0));
        }

        foreach (var start in successors.Keys)
        {
            if (index.ContainsKey(start))
            {
                continue;
            }

            Enter(start);
            while (work.Count > 0)
            {
                var (node, next) = work.Pop();
                var targets = successors[node];
                if (next < targets.Count)
                {
                    work.Push((node, next + 1));
                    var target = targets[next];
                    if (!index.ContainsKey(target))
                    {
                        Enter(target);
                    }
                    else if (onStack.Contains(target))
                    {
                        low[node] = Math.Min(low[node], index[target]);
                    }

                    continue;
                }

                if (low[node] == index[node])
                {
                    var component = new List<string>();
                    string member;
                    do
                    {
                        member = stack.Pop();
                        onStack.Remove(member);
                        component.Add(member);
                    }
                    while (member != node);

                    if (component.Count > 1 || selfLoops.Contains(node))
                    {
                        component.Sort(StringComparer.Ordinal);
                        result.Add(component);
                    }
                }

                if (work.Count > 0)
                {
                    var parent = work.Peek().Node;
                    low[parent] = Math.Min(low[parent], low[node]);
                }
            }
        }

        return result;
    }
}
