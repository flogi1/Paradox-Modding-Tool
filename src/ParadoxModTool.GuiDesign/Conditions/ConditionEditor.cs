namespace ParadoxModTool.GuiDesign.Conditions;

/// <summary>
/// Bearbeitet einen Bedingungsbaum über Pfade (<see cref="CondPath"/>), mit Rückgängig/Wiederholen. Knoten sind unveränderlich: jede Änderung baut den
/// Pfad zur Wurzel neu und setzt dort <see cref="CondNode.Source"/> auf <c>null</c>; unveränderte Geschwister behalten ihren Originaltext.
/// </summary>
public sealed class ConditionEditor
{
    private readonly Stack<CondNode?> _undo = new();
    private readonly Stack<CondNode?> _redo = new();

    public ConditionEditor(CondNode? root) => Root = root;

    public event EventHandler? Changed;

    public CondNode? Root { get; private set; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public CondNode? NodeAt(CondPath path)
    {
        var node = Root;
        foreach (var index in path.Indices)
        {
            node = node switch
            {
                LogicNode logic when index >= 0 && index < logic.Items.Count => logic.Items[index],
                NotNode not when index == 0 => not.Inner,
                _ => null,
            };
        }

        return node;
    }

    /// <summary>
    /// Leerer Baum: neue Wurzel. Markiert ist eine Gruppe: ans Ende. Markiert ist ein Glied einer Gruppe: dahinter. Sonst werden markierter und neuer
    /// Knoten zu einer neuen UND-Gruppe. Gibt den Pfad des neuen Knotens zurück.
    /// </summary>
    public CondPath Add(CondPath? at, CondNode node)
    {
        if (Root is null)
        {
            Commit(node);
            return CondPath.Root;
        }

        at ??= CondPath.Root;
        var target = NodeAt(at) ?? Root;
        if (target is LogicNode group)
        {
            Commit(Set(at, _ => group with { Items = group.Items.Append(node).ToArray(), Source = null }));
            return at.Child(group.Items.Count);
        }

        if (at.Parent is { } parentPath && NodeAt(parentPath) is LogicNode parent)
        {
            var index = at.Last + 1;
            var items = parent.Items.ToList();
            items.Insert(index, node);
            Commit(Set(parentPath, _ => parent with { Items = items, Source = null }));
            return parentPath.Child(index);
        }

        Commit(Set(at, current => new LogicNode(LogicOp.And, new[] { current!, node }, null)));
        return at.Child(1);
    }

    public CondPath Wrap(CondPath at, LogicOp op)
    {
        Commit(Set(at, current => new LogicNode(op, new[] { current! }, null)));
        return at;
    }

    public CondPath ToggleNot(CondPath at)
    {
        Commit(Set(at, current => current is NotNode not ? not.Inner : new NotNode(current!, null)));
        return at;
    }

    /// <summary>Verschiebt ein Glied innerhalb seiner Gruppe; außerhalb (oder ohne Gruppe) <c>null</c> ohne Änderung.</summary>
    public CondPath? Move(CondPath at, int delta)
    {
        if (at.Parent is not { } parentPath || NodeAt(parentPath) is not LogicNode parent)
        {
            return null;
        }

        var target = at.Last + delta;
        if (target < 0 || target >= parent.Items.Count)
        {
            return null;
        }

        var items = parent.Items.ToList();
        var node = items[at.Last];
        items.RemoveAt(at.Last);
        items.Insert(target, node);
        Commit(Set(parentPath, _ => parent with { Items = items, Source = null }));
        return parentPath.Child(target);
    }

    /// <summary>Entfernt den Knoten; ein NICHT ohne Inhalt fällt mit weg. Rückgabe: sinnvolle Auswahl danach, <c>null</c> bei leerem Baum.</summary>
    public CondPath? Remove(CondPath at)
    {
        while (at.Parent is { } up && NodeAt(up) is NotNode)
        {
            at = up;
        }

        if (at.Parent is not { } parentPath)
        {
            Commit(null);
            return null;
        }

        var parent = (LogicNode)NodeAt(parentPath)!;
        var items = parent.Items.Where((_, i) => i != at.Last).ToArray();
        Commit(Set(parentPath, _ => parent with { Items = items, Source = null }));
        return items.Length == 0 ? parentPath : parentPath.Child(Math.Min(at.Last, items.Length - 1));
    }

    public CondPath Merge(CondPath at)
    {
        if (NodeAt(at) is { } node && !ReferenceEquals(ConditionWriter.Merge(node), node))
        {
            Commit(Set(at, current => ConditionWriter.Merge(current!)));
        }

        return at;
    }

    public void Replace(CondPath at, CondNode node) => Commit(Set(at, _ => node));

    public void Undo()
    {
        if (_undo.Count == 0)
        {
            return;
        }

        _redo.Push(Root);
        Root = _undo.Pop();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }

        _undo.Push(Root);
        Root = _redo.Pop();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Commit(CondNode? root)
    {
        _undo.Push(Root);
        _redo.Clear();
        Root = root;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Neue Wurzel, in der der Knoten bei <paramref name="path"/> durch <paramref name="change"/> ersetzt ist; alle Vorfahren verlieren ihre Quelle.</summary>
    private CondNode? Set(CondPath path, Func<CondNode?, CondNode?> change) => Rebuild(Root, path.Indices, 0, change);

    private static CondNode? Rebuild(CondNode? node, IReadOnlyList<int> path, int depth, Func<CondNode?, CondNode?> change)
    {
        if (depth == path.Count)
        {
            return change(node);
        }

        var index = path[depth];
        switch (node)
        {
            case LogicNode logic:
                var child = Rebuild(logic.Items[index], path, depth + 1, change);
                var items = logic.Items.ToList();
                if (child is null)
                {
                    items.RemoveAt(index);
                }
                else
                {
                    items[index] = child;
                }

                return logic with { Items = items, Source = null };
            case NotNode not:
                var inner = Rebuild(not.Inner, path, depth + 1, change);
                return inner is null ? null : not with { Inner = inner, Source = null };
            default:
                return node;
        }
    }
}
