using ParadoxModTool.FlagTracker;
using ParadoxModTool.ScriptCheck;

namespace ParadoxModTool.ReferenceTree;

/// <summary>Besitzer einer Stelle: Name und Art der umschließenden Definition.</summary>
public sealed record RefOwner(string Name, CheckKind Kind);

/// <summary>
/// Brücke zwischen Flag-Index und Skript-Prüfungs-Index: umschließende Definition einer Flag-Stelle, Flag-Stellen innerhalb einer
/// Definition und Herkunft einer Datei. Beide Indizes dürfen fehlen.
/// </summary>
public sealed class ReferenceSources
{
    private readonly Lazy<Dictionary<string, List<CheckDefinition>>> _definitionsByFile;
    private readonly Lazy<Dictionary<string, List<FlagSite>>> _sitesByFile;
    private readonly Lazy<Dictionary<string, CheckFile>> _filesByKey;

    public ReferenceSources(FlagIndex? flags, ScriptCheckIndex? checks)
    {
        Flags = flags;
        Checks = checks;
        _definitionsByFile = new Lazy<Dictionary<string, List<CheckDefinition>>>(BuildDefinitions);
        _sitesByFile = new Lazy<Dictionary<string, List<FlagSite>>>(BuildSites);
        _filesByKey = new Lazy<Dictionary<string, CheckFile>>(BuildFiles);
    }

    public FlagIndex? Flags { get; }

    public ScriptCheckIndex? Checks { get; }

    /// <summary>Umschließende Definition (nicht weich, EndLine &gt; 0) der Stelle: gleiche Wurzel und gleicher relativer Pfad, Line &lt;= Zeile &lt;= EndLine; sonst null.</summary>
    public RefOwner? OwnerAt(SiteLocation location)
    {
        if (!_definitionsByFile.Value.TryGetValue(FileKey(location), out var list))
        {
            return null;
        }

        foreach (var definition in list)
        {
            if (definition.Line <= location.Line && location.Line <= definition.EndLine)
            {
                return new RefOwner(definition.Name, definition.Kind);
            }
        }

        return null;
    }

    /// <summary>Konkrete Flag-Stellen (ohne Muster) innerhalb aller nicht-weichen Definitionen von <paramref name="name"/> (Events über <see cref="EventIds.Key"/>).</summary>
    public IReadOnlyList<FlagSite> FlagSitesIn(string name)
    {
        var result = new List<FlagSite>();
        if (Checks is null || Flags is null)
        {
            return result;
        }

        var seen = new HashSet<FlagSite>();
        foreach (var (file, definition) in Checks.DefinitionsOf(name))
        {
            if (definition.Soft || definition.EndLine <= 0 || !_sitesByFile.Value.TryGetValue(FileKey(file.File), out var sites))
            {
                continue;
            }

            foreach (var site in sites)
            {
                if (!site.IsPattern && definition.Line <= site.Location.Line && site.Location.Line <= definition.EndLine && seen.Add(site))
                {
                    result.Add(site);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Herkunft der Datei aus dem Skript-Prüfungs-Index; fehlt sie dort, aus den Flag-Stellen derselben Datei; sonst Vanilla, außer die Wurzel
    /// ist eine Mod-Wurzel der Quellen (dann andere Mod mit dem Ordnernamen).
    /// </summary>
    public (FlagOrigin Origin, string OriginName) OriginOf(SiteLocation location)
    {
        var key = FileKey(location);
        if (_filesByKey.Value.TryGetValue(key, out var file))
        {
            return (file.Origin, file.OriginName);
        }

        if (_sitesByFile.Value.TryGetValue(key, out var sites) && sites.Count > 0)
        {
            return (sites[0].Origin, sites[0].OriginName);
        }

        if (Checks is not null)
        {
            foreach (var root in Checks.Sources.Roots)
            {
                if (root.Origin != FlagOrigin.Vanilla && SameRoot(root.Directory, location.RootDirectory))
                {
                    return (FlagOrigin.OtherMod, Path.GetFileName(NormalizeRoot(location.RootDirectory)));
                }
            }
        }

        return (FlagOrigin.Vanilla, "Vanilla");
    }

    /// <summary>Die Definition liegt in der gewählten Mod (<see cref="ScriptCheckIndex.ModRoot"/>).</summary>
    public bool DefinedInMod(string name)
    {
        var modRoot = Checks?.ModRoot;
        if (Checks is null || modRoot is null)
        {
            return false;
        }

        return Checks.DefinitionsOf(name).Any(d => SameRoot(d.File.File.RootDirectory, modRoot.Directory));
    }

    private Dictionary<string, List<CheckDefinition>> BuildDefinitions()
    {
        var map = new Dictionary<string, List<CheckDefinition>>(StringComparer.OrdinalIgnoreCase);
        if (Checks is null)
        {
            return map;
        }

        foreach (var file in Checks.Files)
        {
            foreach (var definition in file.Scan.Definitions)
            {
                if (definition.Soft || definition.EndLine <= 0)
                {
                    continue;
                }

                var key = FileKey(file.File);
                if (!map.TryGetValue(key, out var list))
                {
                    map[key] = list = new List<CheckDefinition>();
                }

                list.Add(definition);
            }
        }

        return map;
    }

    private Dictionary<string, List<FlagSite>> BuildSites()
    {
        var map = new Dictionary<string, List<FlagSite>>(StringComparer.OrdinalIgnoreCase);
        if (Flags is null)
        {
            return map;
        }

        foreach (var site in Flags.Sites)
        {
            var key = FileKey(site.Location);
            if (!map.TryGetValue(key, out var list))
            {
                map[key] = list = new List<FlagSite>();
            }

            list.Add(site);
        }

        return map;
    }

    private Dictionary<string, CheckFile> BuildFiles()
    {
        var map = new Dictionary<string, CheckFile>(StringComparer.OrdinalIgnoreCase);
        if (Checks is null)
        {
            return map;
        }

        foreach (var file in Checks.Files)
        {
            map.TryAdd(FileKey(file.File), file);
        }

        return map;
    }

    private static string FileKey(SiteLocation location) => NormalizeRoot(location.RootDirectory) + "|" + location.RelativePath;

    private static string NormalizeRoot(string root) => root.Replace('\\', '/').TrimEnd('/');

    private static bool SameRoot(string a, string b) => string.Equals(NormalizeRoot(a), NormalizeRoot(b), StringComparison.OrdinalIgnoreCase);
}
