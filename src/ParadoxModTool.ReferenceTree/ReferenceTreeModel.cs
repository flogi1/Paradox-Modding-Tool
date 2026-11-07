using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.ScriptCheck;

namespace ParadoxModTool.ReferenceTree;

/// <summary>
/// Macht aus einem Namen die Knoten des Verweisbaums: Wurzeln (Flag und/oder Baustein), Äste, Herkunftsgruppen und Stellen; Kinder werden
/// erst beim Aufklappen berechnet. Aufrufer-Knoten (Up) klappen weiter zu ihren Aufrufern auf, Gerufene (Down) zu ihren Gerufenen und Flag-Stellen.
/// </summary>
/// <remarks>
/// Pfad-Schlüssel: <c>F:</c> + Flag-Name für Flag-Wurzeln und Flag-Stellen, <c>E:</c> + <see cref="EventIds.Key"/> für Events, sonst <c>N:</c> + Name
/// (ordinal). Äste und Gruppen erben den Pfad des Elternknotens; Äste finden ihren Namen über den letzten Schlüssel, Gruppen ihre Stellen über
/// eine Tabelle je Knoten-Instanz (eine Kopie über <c>with</c> hat keine Kinder).
/// </remarks>
public sealed class ReferenceTreeModel
{
    private const string FlagPrefix = "F:";
    private const string EventPrefix = "E:";
    private const string NamePrefix = "N:";

    private static readonly IReadOnlyList<RefNode> None = Array.Empty<RefNode>();

    private readonly ReferenceSources _sources;
    private readonly ConditionalWeakTable<RefNode, IReadOnlyList<RefNode>> _groupItems = new();
    private readonly ConcurrentDictionary<string, bool> _definedInMod = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, IReadOnlyList<FlagSite>> _flagSites = new(StringComparer.Ordinal);

    public ReferenceTreeModel(ReferenceSources sources)
    {
        _sources = sources;
    }

    /// <summary>
    /// 0–2 Wurzeln: Flag-Wurzel, wenn der Flag-Index einen genauen Treffer hat (kein <c>*</c>); Baustein-Wurzel, wenn der Name Definitionen
    /// oder Aufrufer hat. Reihenfolge: Flag, dann Baustein. Leer = nicht gefunden.
    /// </summary>
    public IReadOnlyList<RefNode> Roots(string name)
    {
        name = (name ?? string.Empty).Trim();
        var result = new List<RefNode>();
        if (name.Length == 0)
        {
            return result;
        }

        var flagSites = FlagSitesOf(name);
        if (flagSites.Count > 0)
        {
            result.Add(FlagRoot(name, flagSites));
        }

        if (_sources.Checks is { } checks && !name.Contains('*'))
        {
            var definitions = checks.DefinitionsOf(name);
            var callers = checks.CallersOf(name);
            if (definitions.Count > 0 || callers.Count > 0)
            {
                result.Add(BlockRoot(name, definitions, callers.Count > 0));
            }
        }

        return result;
    }

    /// <summary>Kinder eines Knotens; leer bei nicht aufklappbaren Knoten.</summary>
    public IReadOnlyList<RefNode> Children(RefNode node)
    {
        if (!node.Expandable)
        {
            return None;
        }

        switch (node.NodeKind)
        {
            case RefNodeKind.Root:
                return node.FlagKind is not null ? FlagBranches(node) : BlockBranches(node);
            case RefNodeKind.Group:
                return _groupItems.TryGetValue(node, out var items) ? items : None;
            case RefNodeKind.Branch:
                return BranchChildren(node);
            case RefNodeKind.Site:
                return node.Direction == RefDirection.Up
                    ? Grouped(node, Callers(node.Name).Select(row => UpSite(node, row)))
                    : DownChildren(node, node.Name);
            default:
                return None;
        }
    }

    private RefNode FlagRoot(string name, IReadOnlyList<FlagSite> sites)
    {
        var first = sites.FirstOrDefault(s => s.Origin == FlagOrigin.ActiveMod) ?? sites[0];
        return new RefNode(RefNodeKind.Root, name, null, first.Kind, null, null, first.Origin, first.OriginName, RefDirection.Up,
            null, null, sites.Count, true, false, false, new[] { FlagPrefix + name });
    }

    private RefNode BlockRoot(string name, IReadOnlyList<(CheckFile File, CheckDefinition Definition)> definitions, bool hasCallers)
    {
        var (file, definition) = definitions.FirstOrDefault(d => d.File.Origin == FlagOrigin.ActiveMod);
        if (definition is null && definitions.Count > 0)
        {
            (file, definition) = definitions[0];
        }

        // Ohne Definition: Art aus der Form der Id (ns.123 = Event), sonst unbekannt.
        var kind = definition?.Kind ?? (LooksLikeEventId(name) ? CheckKind.Event : null);
        var origin = file is not null ? (file.Origin, file.OriginName) : (FlagOrigin.Vanilla, "Vanilla");
        var expandable = hasCallers || Callees(name).Count > 0 || _sources.FlagSitesIn(name).Count > 0;
        var partial = kind != CheckKind.Event && !DefinedInMod(name);
        return new RefNode(RefNodeKind.Root, name, kind, null, null, null, origin.Item1, origin.Item2, RefDirection.Up,
            null, null, 0, expandable, partial, false, new[] { BlockKey(name, kind) });
    }

    private IReadOnlyList<RefNode> FlagBranches(RefNode root)
    {
        var sites = FlagSitesOf(NameOf(root));
        var result = new List<RefNode>();
        foreach (var (role, branch) in new[] { (FlagRole.Set, RefBranch.Set), (FlagRole.Read, RefBranch.Read), (FlagRole.Remove, RefBranch.Remove) })
        {
            var count = sites.Count(s => s.Role == role);
            if (count > 0)
            {
                result.Add(BranchNode(root, branch, RefDirection.Up, count));
            }
        }

        return result;
    }

    private IReadOnlyList<RefNode> BlockBranches(RefNode root)
    {
        var name = NameOf(root);
        var result = new List<RefNode>();
        var callers = Callers(name).Count;
        if (callers > 0)
        {
            result.Add(BranchNode(root, RefBranch.UsedBy, RefDirection.Up, callers));
        }

        var uses = Callees(name).Count + _sources.FlagSitesIn(name).Count;
        if (uses > 0)
        {
            result.Add(BranchNode(root, RefBranch.Uses, RefDirection.Down, uses));
        }

        return result;
    }

    private IReadOnlyList<RefNode> BranchChildren(RefNode branch)
    {
        var name = NameOf(branch);
        switch (branch.Branch)
        {
            case RefBranch.Set:
            case RefBranch.Read:
            case RefBranch.Remove:
                var role = branch.Branch == RefBranch.Set ? FlagRole.Set : branch.Branch == RefBranch.Read ? FlagRole.Read : FlagRole.Remove;
                return Grouped(branch, FlagSitesOf(name).Where(s => s.Role == role).Select(s => FlagOwnerSite(branch, s)));
            case RefBranch.UsedBy:
                return Grouped(branch, Callers(name).Select(row => UpSite(branch, row)));
            case RefBranch.Uses:
                return DownChildren(branch, name);
            case RefBranch.Sets:
                return Grouped(branch, _sources.FlagSitesIn(name).Where(s => s.Role != FlagRole.Read).Select(s => FlagLeaf(branch, s)));
            case RefBranch.Reads:
                return Grouped(branch, _sources.FlagSitesIn(name).Where(s => s.Role == FlagRole.Read).Select(s => FlagLeaf(branch, s)));
            default:
                return None;
        }
    }

    /// <summary>Gerufene von <paramref name="name"/> (gruppiert), danach die Unteräste <c>Sets</c> und <c>Reads</c> aus den Flag-Stellen darin.</summary>
    private IReadOnlyList<RefNode> DownChildren(RefNode parent, string name)
    {
        var result = new List<RefNode>(Grouped(parent, Callees(name).Select(row => DownSite(parent, row))));
        var flags = _sources.FlagSitesIn(name);
        var sets = flags.Count(s => s.Role != FlagRole.Read);
        var reads = flags.Count - sets;
        if (sets > 0)
        {
            result.Add(BranchNode(parent, RefBranch.Sets, RefDirection.Down, sets));
        }

        if (reads > 0)
        {
            result.Add(BranchNode(parent, RefBranch.Reads, RefDirection.Down, reads));
        }

        return result;
    }

    /// <summary>Erst die Stellen der gewählten Mod (Pfad, Zeile), dann je eine zugeklappte Gruppe für andere Mods und Vanilla (Herkunft, Pfad, Zeile).</summary>
    private IReadOnlyList<RefNode> Grouped(RefNode parent, IEnumerable<RefNode> sites)
    {
        var all = sites.ToList();
        var result = all.Where(s => s.Origin == FlagOrigin.ActiveMod)
            .OrderBy(s => s.Location?.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Location?.Line ?? 0)
            .ToList();

        foreach (var (origin, group, originName) in new[] { (FlagOrigin.OtherMod, RefGroup.OtherMods, string.Empty), (FlagOrigin.Vanilla, RefGroup.Vanilla, "Vanilla") })
        {
            var items = all.Where(s => s.Origin == origin)
                .OrderBy(s => s.OriginName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Location?.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Location?.Line ?? 0)
                .ToList();
            if (items.Count == 0)
            {
                continue;
            }

            var node = new RefNode(RefNodeKind.Group, string.Empty, null, null, null, null, origin, originName, parent.Direction,
                null, group, items.Count, true, false, false, parent.Path);
            _groupItems.AddOrUpdate(node, items);
            result.Add(node);
        }

        return result;
    }

    /// <summary>Flag-Stelle unter einem Flag-Ast: Name und Art des Besitzers; ohne Besitzer nicht aufklappbar.</summary>
    private RefNode FlagOwnerSite(RefNode parent, FlagSite site)
    {
        var owner = _sources.OwnerAt(site.Location);
        return BlockSite(parent, owner?.Name ?? string.Empty, owner?.Kind, site.Location, site.Origin, site.OriginName, RefDirection.Up, site.Kind, site.Role);
    }

    /// <summary>Aufrufer: Name = Besitzer; aufklappbar mit Namen und Art ungleich <see cref="CheckKind.Other"/>.</summary>
    private RefNode UpSite(RefNode parent, CallRow row)
    {
        var (origin, originName) = _sources.OriginOf(row.Location);
        return BlockSite(parent, row.Owner ?? string.Empty, row.Kind, row.Location, origin, originName, RefDirection.Up, null, null);
    }

    /// <summary>Gerufener: Name = Ziel; aufklappbar mit Namen.</summary>
    private RefNode DownSite(RefNode parent, CallRow row)
    {
        var (origin, originName) = _sources.OriginOf(row.Location);
        return BlockSite(parent, row.Name, row.Kind, row.Location, origin, originName, RefDirection.Down, null, null);
    }

    /// <summary>
    /// Stelle eines Bausteins. Zirkel: Schlüssel schon im Pfad des Elternknotens. Partial nur bei Namen mit bekannter Art (nicht
    /// <see cref="CheckKind.Other"/>): Up ohne Event, Down immer, jeweils wenn die gewählte Mod den Namen nicht definiert.
    /// </summary>
    private RefNode BlockSite(RefNode parent, string name, CheckKind? kind, SiteLocation location, FlagOrigin origin, string originName,
        RefDirection direction, FlagKind? flagKind, FlagRole? role)
    {
        var named = name.Length > 0 && kind is not null;
        var key = BlockKey(name, kind);
        var cycle = named && parent.Path.Contains(key, StringComparer.Ordinal);
        var expandable = named && kind != CheckKind.Other && !cycle;
        var partial = named && kind != CheckKind.Other && (direction == RefDirection.Down || kind != CheckKind.Event) && !DefinedInMod(name);
        var path = named ? Append(parent.Path, key) : parent.Path;
        return new RefNode(RefNodeKind.Site, name, kind, flagKind, role, location, origin, originName, direction,
            null, null, 0, expandable, partial, cycle, path);
    }

    /// <summary>Flag-Stelle unter Sets/Reads: Blatt.</summary>
    private static RefNode FlagLeaf(RefNode parent, FlagSite site) =>
        new(RefNodeKind.Site, site.Name, null, site.Kind, site.Role, site.Location, site.Origin, site.OriginName, RefDirection.Down,
            null, null, 0, false, false, false, Append(parent.Path, FlagPrefix + site.Name));

    private static RefNode BranchNode(RefNode parent, RefBranch branch, RefDirection direction, int count) =>
        new(RefNodeKind.Branch, string.Empty, null, null, null, null, parent.Origin, parent.OriginName, direction,
            branch, null, count, true, false, false, parent.Path);

    /// <summary>
    /// Konkrete Fundstellen eines genauen Treffers im Flag-Index (groß/klein nach den Regeln des Index); leer bei <c>*</c>. Je Name nur einmal
    /// gesucht (die Suche durchläuft den ganzen Index), wie <see cref="DefinedInMod"/>.
    /// </summary>
    private IReadOnlyList<FlagSite> FlagSitesOf(string name)
    {
        if (_sources.Flags is not { } flags || name.Contains('*'))
        {
            return Array.Empty<FlagSite>();
        }

        return _flagSites.GetOrAdd(name, key => flags.Search(key).SitesByName.TryGetValue(key, out var sites)
            ? sites.Where(s => !s.IsPattern).ToList()
            : Array.Empty<FlagSite>());
    }

    private IReadOnlyList<CallRow> Callers(string name) => _sources.Checks?.CallersOf(name) ?? (IReadOnlyList<CallRow>)Array.Empty<CallRow>();

    /// <summary>
    /// Gerufene für Down-Knoten: nur Events und Namen mit mindestens einer Definition. Andere Aufrufe (eingebaute Befehle wie <c>is_ai = yes</c>,
    /// kaputte Verweise, die die Skript-Prüfung schon meldet) entfallen.
    /// </summary>
    private IReadOnlyList<CallRow> Callees(string name)
    {
        if (_sources.Checks is not { } checks)
        {
            return Array.Empty<CallRow>();
        }

        return checks.CalleesOf(name).Where(row => row.Kind == CheckKind.Event || checks.DefinitionsOf(row.Name).Count > 0).ToList();
    }

    private bool DefinedInMod(string name) => _definedInMod.GetOrAdd(name, _sources.DefinedInMod);

    /// <summary>Name aus dem letzten Pfad-Schlüssel (bei Events der Schlüssel nach <see cref="EventIds.Key"/>, der für Abfragen genügt).</summary>
    private static string NameOf(RefNode node) => node.Path[^1][2..];

    private static string BlockKey(string name, CheckKind? kind) => kind == CheckKind.Event ? EventPrefix + EventIds.Key(name) : NamePrefix + name;

    private static bool LooksLikeEventId(string name)
    {
        var dot = name.LastIndexOf('.');
        return EventIds.Namespace(name) is not null && dot < name.Length - 1 && name[(dot + 1)..].All(char.IsAsciiDigit);
    }

    private static IReadOnlyList<string> Append(IReadOnlyList<string> path, string key)
    {
        var result = new string[path.Count + 1];
        for (var i = 0; i < path.Count; i++)
        {
            result[i] = path[i];
        }

        result[^1] = key;
        return result;
    }
}
