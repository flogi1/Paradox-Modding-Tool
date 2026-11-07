using ParadoxModTool.FlagTracker;

namespace ParadoxModTool.ScriptCheck;

/// <summary>Eine gelesene Datei mit Ort (Zeile 0), Herkunft, Anzeigename der Wurzel und ihren Definitionen und Event-Aufrufen.</summary>
public sealed record CheckFile(SiteLocation File, FlagOrigin Origin, string OriginName, CheckScan Scan);

/// <summary>Eine Zeile einer „Wer ruft wen“-Abfrage: bei Aufrufern der Besitzer, bei Aufgerufenen das Ziel.</summary>
/// <param name="Name">Aufrufer: Name des Besitzers (leer bei anonymen Blöcken); Aufgerufene: Name des Ziels.</param>
/// <param name="Kind">Art des Namens (Besitzerart bzw. Zielart).</param>
/// <param name="Location">Ort des Verweises (Wurzel, relativer Pfad, Zeile).</param>
/// <param name="Owner">Name der umschließenden Definition; null bei anonymen Blöcken.</param>
/// <param name="Conditional">Der Verweis steht unter einer Bedingung.</param>
public sealed record CallRow(string Name, CheckKind Kind, SiteLocation Location, string? Owner, bool Conditional);

/// <summary>Ein Verweis mit seinem Ort (Wurzel, relativer Pfad, Zeile des Verweises).</summary>
public sealed record LocatedRef(SiteLocation Location, CheckRef Ref);

/// <summary>
/// Ergebnis eines Aufbaus der Skriptprüfung: alle gelesenen Dateien, alle Event-Aufrufe, die Erwähnungen der Namen der gewählten Mod
/// in allen Dateien und die Aufrufformen in den <c>.txt</c>-Dateien der gewählten Mod. Listen in Reihenfolge der Dateien, darin nach Zeile.
/// </summary>
public sealed class ScriptCheckIndex
{
    public ScriptCheckIndex(
        FlagSourceSet sources,
        FlagRoot? modRoot,
        IReadOnlyList<CheckFile> files,
        IReadOnlyList<LocatedRef> eventCalls,
        IReadOnlyList<LocatedRef> mentions,
        IReadOnlyList<LocatedRef> modBlockCalls,
        IReadOnlyList<string> unreadFiles,
        DateTime builtUtc,
        IReadOnlyList<CheckDefinition>? replacedDefinitions = null)
    {
        ReplacedDefinitions = replacedDefinitions ?? Array.Empty<CheckDefinition>();
        Sources = sources;
        ModRoot = modRoot;
        Files = files;
        EventCalls = eventCalls;
        Mentions = mentions;
        ModBlockCalls = modBlockCalls;
        UnreadFiles = unreadFiles;
        BuiltUtc = builtUtc;
        _byTarget = new Lazy<Dictionary<string, List<LocatedRef>>>(TargetMap);
        _byOwner = new Lazy<Dictionary<string, List<LocatedRef>>>(OwnerMap);
        _definitions = new Lazy<Dictionary<string, List<(CheckFile File, CheckDefinition Definition)>>>(DefinitionMap);
        _candidates = new Lazy<string[]>(BuildCandidates);
    }

    /// <summary>
    /// Definitionen der Dateien außerhalb der Mod, die eine Mod-Datei mit gleichem Pfad ersetzt. Diese Dateien stehen nicht in <see cref="Files"/> (sie gelten
    /// im Spiel nicht), ihre Namen zeigen aber: Die Mod hat sie kopiert, nicht erfunden; sie zählen nur für tot/unbenutzt.
    /// </summary>
    public IReadOnlyList<CheckDefinition> ReplacedDefinitions { get; }

    /// <summary>Die Quellen, aus denen gebaut wurde.</summary>
    public FlagSourceSet Sources { get; }

    /// <summary>Die Wurzel der gewählten Mod (<see cref="FlagOrigin.ActiveMod"/>); null, wenn keine gewählt ist.</summary>
    public FlagRoot? ModRoot { get; }

    public IReadOnlyList<CheckFile> Files { get; }

    /// <summary>Event-Aufrufe aus allen Dateien.</summary>
    public IReadOnlyList<LocatedRef> EventCalls { get; }

    /// <summary>Erwähnungen der Namen, die die gewählte Mod definiert, aus allen Dateien.</summary>
    public IReadOnlyList<LocatedRef> Mentions { get; }

    /// <summary>Aufrufformen (<see cref="ScriptCheckReader.ReadBlockCalls"/>) in den <c>.txt</c>-Dateien der gewählten Mod.</summary>
    public IReadOnlyList<LocatedRef> ModBlockCalls { get; }

    /// <summary>Volle Pfade der Dateien, die nicht gelesen werden konnten (sortiert).</summary>
    public IReadOnlyList<string> UnreadFiles { get; }

    public DateTime BuiltUtc { get; }

    public int FileCount => Files.Count;

    private Lazy<Dictionary<string, List<LocatedRef>>> _byTarget = null!;
    private Lazy<Dictionary<string, List<LocatedRef>>> _byOwner = null!;
    private Lazy<Dictionary<string, List<(CheckFile File, CheckDefinition Definition)>>> _definitions = null!;
    private Lazy<string[]> _candidates = null!;

    private static string NameKey(string name, CheckKind kind) => (kind == CheckKind.Event ? "E:" + EventIds.Key(name) : "N:" + name);

    private IEnumerable<LocatedRef> AllRefs() => EventCalls.Concat(ModBlockCalls).Concat(Mentions);

    private Dictionary<string, List<(CheckFile File, CheckDefinition Definition)>> DefinitionMap()
    {
        var map = new Dictionary<string, List<(CheckFile File, CheckDefinition Definition)>>(StringComparer.Ordinal);
        foreach (var file in Files)
        {
            foreach (var d in file.Scan.Definitions)
            {
                Add(map, NameKey(d.Name, d.Kind), (file, d));
            }
        }

        return map;
    }

    private string[] BuildCandidates()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Files)
        {
            foreach (var d in file.Scan.Definitions)
            {
                names.Add(d.Name);
            }
        }

        foreach (var r in AllRefs())
        {
            names.Add(r.Ref.Target);
        }

        var array = names.ToArray();
        Array.Sort(array, StringComparer.OrdinalIgnoreCase);
        return array;
    }

    private static void Add<T>(Dictionary<string, List<T>> map, string key, T value)
    {
        if (!map.TryGetValue(key, out var list))
        {
            map[key] = list = new List<T>();
        }

        list.Add(value);
    }

    /// <summary>Beide Schlüsselformen eines abgefragten Namens: als Event (Id-Vergleich) und ordinal.</summary>
    private static string[] QueryKeys(string name) => new[] { "E:" + EventIds.Key(name), "N:" + name };

    private Dictionary<string, List<LocatedRef>> TargetMap()
    {
        var map = new Dictionary<string, List<LocatedRef>>(StringComparer.Ordinal);
        foreach (var r in AllRefs())
        {
            Add(map, NameKey(r.Ref.Target, r.Ref.TargetKind), r);
        }

        return map;
    }

    private Dictionary<string, List<LocatedRef>> OwnerMap()
    {
        var map = new Dictionary<string, List<LocatedRef>>(StringComparer.Ordinal);
        foreach (var r in AllRefs())
        {
            if (r.Ref.Owner is { } owner)
            {
                Add(map, NameKey(owner, r.Ref.OwnerKind), r);
            }
        }

        return map;
    }

    private IEnumerable<LocatedRef> Lookup(Dictionary<string, List<LocatedRef>> map, string name)
    {
        // Reihenfolge der Quellen (Event-Aufrufe, Mod-Aufrufformen, Erwähnungen) bleibt je Schlüssel erhalten
        foreach (var key in QueryKeys(name))
        {
            if (map.TryGetValue(key, out var list))
            {
                foreach (var r in list)
                {
                    yield return r;
                }
            }
        }
    }

    private static IReadOnlyList<CallRow> Rows(IEnumerable<LocatedRef> refs, bool callers)
    {
        var rows = new List<CallRow>();
        var seen = new HashSet<(string Root, string Path, int Line, string? Owner, string Target)>();
        foreach (var r in refs)
        {
            var c = r.Ref;
            var target = NameKey(c.Target, c.TargetKind);
            if (!seen.Add((r.Location.RootDirectory, r.Location.RelativePath, r.Location.Line, c.Owner, target)))
            {
                continue;
            }

            rows.Add(callers
                ? new CallRow(c.Owner ?? string.Empty, c.OwnerKind, r.Location, c.Owner, c.Conditional)
                : new CallRow(c.Target, c.TargetKind, r.Location, c.Owner, c.Conditional));
        }

        return rows;
    }

    /// <summary>
    /// Alle Stellen, die <paramref name="name"/> aufrufen oder erwähnen: Event-Aufrufe, Aufrufformen der Mod und Erwähnungen
    /// (Events über <see cref="EventIds.Key"/>, sonst ordinal). Zeile = Besitzer und Ort des Verweises; gleiche Zeilen werden zusammengefasst.
    /// Aufrufer eines Namens, den nicht die gewählte Mod definiert, kommen nur aus Event-Aufrufen, weil Erwähnungen nur für die Namen der Mod gesammelt werden.
    /// </summary>
    public IReadOnlyList<CallRow> CallersOf(string name) => Rows(Lookup(_byTarget.Value, name), true);

    /// <summary>Alle Verweise, deren Besitzer <paramref name="name"/> ist (Besitzerart beliebig). Zeile = Ziel und Ort des Verweises.</summary>
    public IReadOnlyList<CallRow> CalleesOf(string name) => Rows(Lookup(_byOwner.Value, name), false);

    /// <summary>Alle Definitionen mit dem Namen <paramref name="name"/> (Events über <see cref="EventIds.Key"/>, sonst ordinal), mit ihrer Datei.</summary>
    public IReadOnlyList<(CheckFile File, CheckDefinition Definition)> DefinitionsOf(string name)
    {
        var map = _definitions.Value;
        var result = new List<(CheckFile, CheckDefinition)>();
        foreach (var key in QueryKeys(name))
        {
            if (map.TryGetValue(key, out var list))
            {
                result.AddRange(list);
            }
        }

        return result;
    }

    /// <summary>
    /// Namen zur Suche: alle Definitionsnamen und Ziele von Verweisen. Genauer Treffer zuerst, dann Teiltreffer ohne Groß-/Kleinschreibung
    /// (sortiert), höchstens 500.
    /// </summary>
    public IReadOnlyList<string> SearchNames(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<string>();
        }

        var all = _candidates.Value;
        var result = new List<string>();
        if (all.Contains(query, StringComparer.Ordinal))
        {
            result.Add(query);
        }

        foreach (var n in all)
        {
            if (result.Count >= 500)
            {
                break;
            }

            if (!string.Equals(n, query, StringComparison.Ordinal) && n.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(n);
            }
        }

        return result;
    }
}
