using System.Security.Cryptography;
using System.Text;
using ParadoxModTool.ErrorLog;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.ModCompat;
using ParadoxModTool.Mods;
using ParadoxModTool.SearchReplace;

namespace ParadoxModTool.FlagTracker;

/// <summary>
/// Eine Wurzel der Quellen. Rank: Vanilla 0, Playset-Mods <c>LoadOrder + 1</c>, die aktive Mod außerhalb des Playsets über allen anderen;
/// im Bereich „Alle installierten“ ist jeder Rank 0. Name ist der Anzeigename der Mod oder „Vanilla“.
/// </summary>
public sealed record FlagRoot(string Directory, FlagOrigin Origin, string Name, int Rank);

/// <summary>
/// Die Quellen, die der Flag-Tracker liest: Wurzeln mit Rang, die gezählten Dateien (nach Überschreibung und <c>replace_path</c>)
/// und die wirksame Definition je Scripted Effect/Trigger.
/// </summary>
public sealed class FlagSourceSet
{
    private static readonly string[] Extensions = { ".txt", ".gui", ".yml" };

    private readonly GameProfile _profile;
    private readonly Dictionary<string, FlagRoot> _byKey;
    private OverrideRules? _rules;

    /// <summary>Überschreibungsregeln des Profils, einmal je Quellenmenge gebaut.</summary>
    private OverrideRules Rules => _rules ??= OverrideRules.FromProfile(_profile);

    private FlagSourceSet(GameProfile profile, bool isPlayset, IReadOnlyList<FlagRoot> roots)
    {
        _profile = profile;
        IsPlayset = isPlayset;
        Roots = roots;
        _byKey = new Dictionary<string, FlagRoot>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            _byKey.TryAdd(ErrorLogSources.Key(root.Directory), root);
        }
    }

    public IReadOnlyList<FlagRoot> Roots { get; }

    public bool IsPlayset { get; }

    /// <summary>Ladereihenfolge der relativen Pfade über Vanilla und Mods nach dem Profil (<c>OverrideRules.FromProfile(profile).LoadOrder</c>; Stellaris mit Groß-/Kleinschreibung).</summary>
    public IComparer<string> LoadOrder => Rules.LoadOrder;

    /// <summary>Rank der Wurzel mit diesem Ordner (<see cref="FlagRoot.Rank"/>); 0 für einen unbekannten Ordner.</summary>
    public int RankOf(string rootDirectory) => _byKey.TryGetValue(ErrorLogSources.Key(rootDirectory), out var root) ? root.Rank : 0;

    /// <summary>Stabiler Schlüssel für die Indexdatei: Spiel-Id, Art des Bereichs und die sortierten Wurzel-Schlüssel (<see cref="ErrorLogSources.Key"/>), als SHA-256 gehasht (erste 16 Hex-Zeichen).</summary>
    public string ScopeKey
    {
        get
        {
            var keys = Roots.Select(r => ErrorLogSources.Key(r.Directory).ToLowerInvariant()).Distinct().OrderBy(k => k, StringComparer.Ordinal);
            var text = _profile.Id + "|" + (IsPlayset ? "playset" : "all") + "|" + string.Join("|", keys);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant()[..16];
        }
    }

    /// <summary>Bereich „Playset“: Vanilla, die Mods des Playsets (und eigene Mods) nach Ladereihenfolge und die aktive Mod, auch wenn sie nicht im Playset ist.</summary>
    public static FlagSourceSet ForPlayset(GameProfile profile, IReadOnlyList<ModSource> mods, string activeModDirectory, IReadOnlyList<string> vanillaRoots)
    {
        var activeKey = KeyOrNull(activeModDirectory);
        var roots = vanillaRoots.Select(v => new FlagRoot(v, FlagOrigin.Vanilla, "Vanilla", 0)).ToList();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in mods.Where(m => m.InPlayset || m.IsOwn).OrderBy(m => m.LoadOrder))
        {
            var key = ErrorLogSources.Key(mod.Directory);
            // Die aktive Mod außerhalb des Playsets ordnet sich unten über allen anderen ein.
            if (!mod.InPlayset && string.Equals(key, activeKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (seen.Add(key))
            {
                roots.Add(new FlagRoot(mod.Directory, OriginOf(mod.Directory, activeKey), mod.Name, mod.LoadOrder + 1));
            }
        }

        if (activeKey is not null && !seen.Contains(activeKey))
        {
            roots.Add(new FlagRoot(activeModDirectory, FlagOrigin.ActiveMod, NameOfActive(mods, activeKey, activeModDirectory),
                roots.Count == 0 ? 0 : roots.Max(r => r.Rank) + 1));
        }

        return new FlagSourceSet(profile, true, roots);
    }

    /// <summary>Bereich „Alle installierten“: Vanilla, jede Mod und die aktive Mod, alle mit Rank 0; nichts wird ausgeblendet.</summary>
    public static FlagSourceSet ForAllInstalled(GameProfile profile, IReadOnlyList<ModSource> mods, string activeModDirectory, IReadOnlyList<string> vanillaRoots)
    {
        var activeKey = KeyOrNull(activeModDirectory);
        var roots = vanillaRoots.Select(v => new FlagRoot(v, FlagOrigin.Vanilla, "Vanilla", 0)).ToList();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in mods)
        {
            if (seen.Add(ErrorLogSources.Key(mod.Directory)))
            {
                roots.Add(new FlagRoot(mod.Directory, OriginOf(mod.Directory, activeKey), mod.Name, 0));
            }
        }

        if (activeKey is not null && !seen.Contains(activeKey))
        {
            roots.Add(new FlagRoot(activeModDirectory, FlagOrigin.ActiveMod, NameOfActive(mods, activeKey, activeModDirectory), 0));
        }

        return new FlagSourceSet(profile, false, roots);
    }

    private static string? KeyOrNull(string? directory) => string.IsNullOrWhiteSpace(directory) ? null : ErrorLogSources.Key(directory);

    private static FlagOrigin OriginOf(string directory, string? activeKey) =>
        activeKey is not null && string.Equals(ErrorLogSources.Key(directory), activeKey, StringComparison.OrdinalIgnoreCase) ? FlagOrigin.ActiveMod : FlagOrigin.OtherMod;

    private static string NameOfActive(IReadOnlyList<ModSource> mods, string activeKey, string directory) =>
        mods.FirstOrDefault(m => string.Equals(ErrorLogSources.Key(m.Directory), activeKey, StringComparison.OrdinalIgnoreCase))?.Name
        ?? Path.GetFileName(activeKey);

    /// <summary>
    /// Dateien, die zählen: je Wurzel die gelesenen Endungen (<c>*.txt</c>, <c>*.gui</c> überall, <c>*.yml</c> nur Lokalisierung unter einem
    /// <c>localization</c>- oder <c>localisation</c>-Segment, und davon nur die englische: danach ein Ordner <c>english</c> oder ein Dateiname auf
    /// <c>_l_english.yml</c>; hat eine Wurzel keine einzige englische Lokalisierungsdatei, zählen ihre Lokalisierungsdateien aller Sprachen),
    /// ohne Dateien, die eine höher gerankte Wurzel mit gleichem relativem Pfad ersetzt oder die <c>replace_path</c> ausblendet
    /// (<see cref="ReplacePaths.Of"/>/<see cref="ReplacePaths.Hides"/> mit <c>ReplacePathRoots</c>; frühere Mods nur bei <c>ReplacePathHidesEarlierMods</c>).
    /// Im Bereich „Alle installierten“ wird nichts ausgeblendet. <paramref name="extensions"/> ersetzt die gelesenen Endungen
    /// (null = <c>.txt</c>, <c>.gui</c>, <c>.yml</c>); die Lokalisierungsauswahl gilt nur für <c>.yml</c>.
    /// </summary>
    public IReadOnlyList<(FlagRoot Root, string RelativePath)> EnumerateFiles(IReadOnlyList<string>? extensions = null)
    {
        var wanted = extensions ?? Extensions;
        var all = new List<(FlagRoot Root, string RelativePath)>();
        foreach (var root in Roots)
        {
            // FileSearchService liefert nur nach Endung; die Einschränkung von .yml auf (englische) Lokalisierung folgt hier, je Wurzel.
            var files = FileSearchService.EnumerateSearchableFiles(root.Directory, wanted)
                .Select(file => Path.GetRelativePath(root.Directory, file).Replace('\\', '/'))
                .Select(relative => (Relative: relative, Loc: LocOf(relative)))
                .ToList();
            var englishOnly = files.Any(f => f.Loc == LocFile.English);
            foreach (var (relative, loc) in files)
            {
                if (loc is LocFile.None or LocFile.English || (loc == LocFile.OtherLanguage && !englishOnly))
                {
                    all.Add((root, relative));
                }
            }
        }

        if (!IsPlayset)
        {
            return all;
        }

        // Höchster Rank je relativem Pfad (Groß-/Kleinschreibung zählt nicht).
        var best = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (root, relative) in all)
        {
            best[relative] = best.TryGetValue(relative, out var rank) ? Math.Max(rank, root.Rank) : root.Rank;
        }

        // Ausgeblendete Ordner je Mod mit Rank > 0; Rank 0 sind Vanilla und eigene Mods außerhalb des Playsets, die nichts ausblenden.
        var hiders = Roots
            .Where(r => r.Origin != FlagOrigin.Vanilla && r.Rank > 0)
            .Select(r => (r.Rank, Folders: ReplacePaths.Of(new[] { r.Directory }, _profile.MetadataFile, _profile.ReplacePathRoots)))
            .Where(h => h.Folders.Count > 0)
            .ToList();

        return all.Where(f =>
            best[f.RelativePath] <= f.Root.Rank
            && !hiders.Any(h => h.Rank > f.Root.Rank
                && (f.Root.Origin == FlagOrigin.Vanilla || _profile.ReplacePathHidesEarlierMods)
                && ReplacePaths.Hides(h.Folders, f.RelativePath, _profile.ReplacePathRoots)))
            .ToList();
    }

    /// <summary>Wie eine Datei zur Lokalisierung steht (siehe <see cref="LocOf"/>).</summary>
    private enum LocFile
    {
        /// <summary>Keine <c>.yml</c>: zählt immer.</summary>
        None,

        /// <summary>Englische Lokalisierung: zählt immer.</summary>
        English,

        /// <summary>Lokalisierung einer anderen Sprache: zählt nur, wenn die Wurzel keine englische hat.</summary>
        OtherLanguage,

        /// <summary><c>.yml</c> außerhalb der Lokalisierung: zählt nie.</summary>
        NotLocalisation,
    }

    private static LocFile LocOf(string relative)
    {
        if (!relative.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
        {
            return LocFile.None;
        }

        // Lokalisierung: unter localization/localisation; englisch, wenn danach ein Ordner "english" kommt oder der Name auf _l_english.yml endet.
        var segments = relative.Split('/');
        var folders = segments[..^1];
        var loc = Array.FindIndex(folders, s =>
            string.Equals(s, "localization", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "localisation", StringComparison.OrdinalIgnoreCase));
        if (loc < 0)
        {
            return LocFile.NotLocalisation;
        }

        return folders.Skip(loc + 1).Any(s => string.Equals(s, "english", StringComparison.OrdinalIgnoreCase))
            || segments[^1].EndsWith("_l_english.yml", StringComparison.OrdinalIgnoreCase)
            ? LocFile.English
            : LocFile.OtherLanguage;
    }

    /// <summary>
    /// Wirksame Definition je Name und Art (Effect oder Trigger, <see cref="DefinitionKey"/>) eines Scripted Effects/Triggers aus <c>OverrideRules.FromProfile(profile).Match(Pfad).Behavior</c>:
    /// FirstWins = niedrigster Rank, sonst höchster. Mit <c>FirstWinsInLoadOrder</c> (bei FirstWins) bzw. <c>LastWinsInLoadOrder</c> (sonst)
    /// kommt die Pfad-Ladereihenfolge zuerst und der Rank entscheidet nur bei gleichem Pfad. Jede Definition der Dateien hat einen Eintrag;
    /// ein Effect und ein Trigger mit gleichem Namen haben je einen eigenen. Stellaris-Inline-Skripte (<c>inline_script:pfad</c>) haben den Schlüssel
    /// (Name, false) und als wirksame Datei die eine, die nach <see cref="EnumerateFiles"/> mit diesem relativen Pfad übrig ist.
    /// Im Bereich „Alle installierten“ zählt jede Definition: null.
    /// </summary>
    public IReadOnlyDictionary<DefinitionKey, FlagScannedFile>? EffectiveDefinitions(IReadOnlyList<FlagScannedFile> files)
    {
        if (!IsPlayset)
        {
            return null;
        }

        var result = new Dictionary<DefinitionKey, FlagScannedFile>(CalleeNameComparer.DefinitionKeys);
        foreach (var file in files)
        {
            foreach (var definition in file.Scan.Definitions)
            {
                var key = new DefinitionKey(definition.Name, definition.IsTrigger);
                if (definition.Name.StartsWith(ScriptReader.InlineScriptPrefix, StringComparison.Ordinal))
                {
                    // Inline-Skript = ganze Datei: EnumerateFiles hat je relativem Pfad schon nur die höchstrangige übrig gelassen.
                    result[key] = file;
                    continue;
                }

                if (result.TryGetValue(key, out var current) && !Wins(file.File, current.File))
                {
                    continue;
                }

                result[key] = file;
            }
        }

        return result;
    }

    /// <summary>
    /// Ob <paramref name="candidate"/> die Stelle <paramref name="current"/> nach denselben Regeln wie <see cref="EffectiveDefinitions"/> schlägt:
    /// FirstWins (niedrigster Rank) bzw. LastWins (höchster Rank) aus <c>OverrideRules.FromProfile(profile).Match(Pfad)</c>; mit
    /// <c>FirstWinsInLoadOrder</c>/<c>LastWinsInLoadOrder</c> kommt die Pfad-Ladereihenfolge zuerst. Bei Gleichstand: false (die aktuelle bleibt).
    /// Dieselbe Datei (gleiche Wurzel und gleicher relativer Pfad, Groß-/Kleinschreibung egal): die kleinere Zeile gewinnt.
    /// </summary>
    public bool Wins(SiteLocation candidate, SiteLocation current)
    {
        if (string.Equals(ErrorLogSources.Key(candidate.RootDirectory), ErrorLogSources.Key(current.RootDirectory), StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.RelativePath, current.RelativePath, StringComparison.OrdinalIgnoreCase))
        {
            return candidate.Line < current.Line;
        }

        var rules = Rules;
        var rank = _byKey.TryGetValue(ErrorLogSources.Key(candidate.RootDirectory), out var root) ? root.Rank : 0;
        var currentRank = _byKey.TryGetValue(ErrorLogSources.Key(current.RootDirectory), out var currentRoot) ? currentRoot.Rank : 0;
        var first = rules.Match(candidate.RelativePath).Behavior == OverrideBehavior.FirstWins;
        var byPath = first ? rules.FirstWinsInLoadOrder : rules.LastWinsInLoadOrder;
        var pathCmp = rules.LoadOrder.Compare(candidate.RelativePath, current.RelativePath);
        var cmp = byPath ? pathCmp : rank.CompareTo(currentRank);
        if (cmp == 0)
        {
            cmp = byPath ? rank.CompareTo(currentRank) : pathCmp;
        }

        return first ? cmp < 0 : cmp > 0;
    }
}
