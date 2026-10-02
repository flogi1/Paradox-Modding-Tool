namespace ParadoxModTool.Compatch;

/// <summary>Pfadreihenfolge des Baums: Teil für Teil, Ordner vor Dateien, ohne Groß-/Kleinschreibung.</summary>
public static class TreePath
{
    public static readonly IComparer<string> Comparer = Comparer<string>.Create(Compare);

    /// <summary>Datei einer Aktion; ohne <see cref="MergeAction.File"/> der Bereich der Adresse.</summary>
    public static string FileOf(MergeAction action) => action.File.Length > 0 ? action.File : action.Address.Scope;

    private static int Compare(string? x, string? y)
    {
        var a = (x ?? string.Empty).Split('/');
        var b = (y ?? string.Empty).Split('/');
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var aIsFile = i == a.Length - 1;
            var bIsFile = i == b.Length - 1;
            if (aIsFile != bIsFile)
            {
                return aIsFile ? 1 : -1;
            }

            var byName = StringComparer.OrdinalIgnoreCase.Compare(a[i], b[i]);
            if (byName != 0)
            {
                return byName;
            }
        }

        return a.Length.CompareTo(b.Length);
    }
}

public enum TreeNodeKind
{
    Folder,
    File,

    /// <summary>Alle Aktionen einer Adresse (ein Gebäude, ein Event ...), sobald es mindestens zwei sind.</summary>
    Block,
    Action,
}

/// <summary>Ein Knoten des Ordnerbaums: Ordner, Datei oder Aktion, mit Zählungen der Aktionen darunter.</summary>
public sealed class ActionTreeNode
{
    public required TreeNodeKind Kind { get; init; }

    /// <summary>Anzeigename: Ordner- oder Dateiname bzw. Anzeigepfad der Aktion.</summary>
    public required string Name { get; init; }

    /// <summary>Voller Pfad (Ordner, Datei) bzw. Aktions-ID.</summary>
    public required string Path { get; init; }

    public MergeAction? Action { get; init; }

    public List<ActionTreeNode> Children { get; } = new();

    public int Count { get; set; }

    public int OpenConflicts { get; set; }

    public int Open { get; set; }

    /// <summary>Erledigte Stellen darunter; gleich <see cref="Count"/> = alles erledigt (grüner Haken bis zum obersten Ordner).</summary>
    public int Done { get; set; }
}

/// <summary>Baut aus Aktionen den Ordnerbaum (Spec Nachtrag 2): Ordner → Unterordner → Datei → Aktionen, strikt nach Pfadteilen.</summary>
public static class ActionTree
{
    /// <remarks>
    /// Die Aktionen werden in <see cref="ActionOrder.Sort"/>-Reihenfolge durchlaufen; Knoten entstehen dadurch schon in Baumreihenfolge.
    /// Hat eine Adresse mindestens zwei Aktionen, stehen sie unter einem Blockknoten (ein Eintrag je Gebäude, Event ..., Nutzerwunsch).
    /// </remarks>
    /// <param name="presorted"><see langword="true"/>: <paramref name="actions"/> stehen schon in <see cref="ActionOrder.Sort"/>-Reihenfolge (das Merge-Fenster sortiert je Lauf einmal; Sortieren von zehntausenden Stellen kostet spürbar Zeit).</param>
    public static IReadOnlyList<ActionTreeNode> Build(IEnumerable<MergeAction> actions, Func<MergeAction, bool> isOpenConflict, Func<MergeAction, bool> isOpen, bool presorted = false, Func<MergeAction, bool>? isDone = null)
    {
        var root = new ActionTreeNode { Kind = TreeNodeKind.Folder, Name = string.Empty, Path = string.Empty };
        var nodes = new Dictionary<string, ActionTreeNode>(StringComparer.OrdinalIgnoreCase);
        var sorted = presorted ? actions as IReadOnlyList<MergeAction> ?? actions.ToList() : ActionOrder.Sort(actions);
        var perAddress = sorted.GroupBy(a => a.Address).ToDictionary(g => g.Key, g => g.Count());
        foreach (var action in sorted)
        {
            var parts = TreePath.FileOf(action).Split('/');
            var parent = root;
            var path = string.Empty;
            for (var i = 0; i < parts.Length; i++)
            {
                path = path.Length == 0 ? parts[i] : path + "/" + parts[i];
                var isFile = i == parts.Length - 1;
                var key = (isFile ? "f:" : "d:") + path;
                if (!nodes.TryGetValue(key, out var node))
                {
                    node = new ActionTreeNode { Kind = isFile ? TreeNodeKind.File : TreeNodeKind.Folder, Name = parts[i], Path = path };
                    nodes[key] = node;
                    parent.Children.Add(node);
                }

                Count(node, action, isOpenConflict, isOpen, isDone);
                parent = node;
            }

            if (action.Address.Kind != AddressKind.WholeFile && perAddress[action.Address] >= 2)
            {
                var key = "b:" + parent.Path + "|" + action.Address.Id;
                if (!nodes.TryGetValue(key, out var block))
                {
                    var name = action.Address.Kind == AddressKind.FileRest ? CompatchTexts.FileRestBlock : action.Address.Key;
                    block = new ActionTreeNode { Kind = TreeNodeKind.Block, Name = name, Path = action.Address.Id };
                    nodes[key] = block;
                    parent.Children.Add(block);
                }

                Count(block, action, isOpenConflict, isOpen, isDone);
                parent = block;
            }

            parent.Children.Add(new ActionTreeNode { Kind = TreeNodeKind.Action, Name = action.DisplayPath, Path = action.Id, Action = action, Count = 1, Done = isDone?.Invoke(action) == true ? 1 : 0 });
        }

        return root.Children;
    }

    private static void Count(ActionTreeNode node, MergeAction action, Func<MergeAction, bool> isOpenConflict, Func<MergeAction, bool> isOpen, Func<MergeAction, bool>? isDone)
    {
        node.Count++;
        if (isOpenConflict(action))
        {
            node.OpenConflicts++;
        }

        if (isDone?.Invoke(action) == true)
        {
            node.Done++;
        }

        if (isOpen(action))
        {
            node.Open++;
        }
    }
}
