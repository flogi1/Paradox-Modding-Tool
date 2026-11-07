using System.Text.RegularExpressions;

namespace ParadoxModTool.FlagTracker;

/// <summary>Phase des Index-Aufbaus, in dieser Reihenfolge.</summary>
public enum BuildPhase
{
    /// <summary>Dateien lesen (bei Refresh nur neue oder geänderte).</summary>
    Reading,

    /// <summary>Aufrufe der Scripted Effects/Trigger in den Skriptdateien sammeln.</summary>
    Calls,

    /// <summary><c>$PARAM$</c> auflösen und den Index bauen (ein Schritt).</summary>
    Resolving,
}

/// <summary>
/// Fortschritt des Index-Aufbaus: erledigt von insgesamt in der Phase. Total kann 0 sein (nichts zu tun in dieser Phase,
/// z. B. Reading bei einem Refresh ohne Änderung); Done geht je Phase nie zurück.
/// </summary>
public sealed record BuildProgress(BuildPhase Phase, int Done, int Total);

/// <summary>Eine Art mit Familie (Family nur bei ScopeFlag, sonst null).</summary>
public sealed record FlagKindFamily(FlagKind Kind, string? Family);

/// <summary>
/// Übersicht eines Namens. Kinds: jede vorkommende Art mit Familie (ein ScopeFlag ohne Familie nur, wenn der Name kein ScopeFlag mit Familie hat). InActiveMod: mindestens eine Fundstelle in der aktiven Mod.
/// NeverSet/NeverRead: nur bei InActiveMod und nie bei SymbolValue. PatternMayCover: ein Muster mit der fehlenden Rolle und einer Art des Namens (nicht ohne Buchstaben und Ziffern wie * oder *_*) passt auf den Namen
/// (die Oberfläche zeigt dann „evtl. über Muster“ statt der Warnung).
/// </summary>
public sealed record FlagNameSummary(string Name, IReadOnlyList<FlagKindFamily> Kinds, int SetCount, int ReadCount, int RemoveCount,
    bool InActiveMod, bool NeverSet, bool NeverRead, bool PatternMayCover);

/// <summary>
/// Suchergebnis: Namen (ordinal sortiert, höchstens <see cref="FlagIndex.MaxNames"/>), ihre Fundstellen und die passenden Muster.
/// SkippedCount: nur bei <see cref="FlagIndex.Warnings(Func{string, bool}?)"/> die Zahl aller übersprungenen Namen mit Warnung (auch hinter der Grenze).
/// </summary>
public sealed record FlagSearchResult(string Query, IReadOnlyList<FlagNameSummary> Names, IReadOnlyDictionary<string, IReadOnlyList<FlagSite>> SitesByName,
    IReadOnlyList<FlagSite> PatternSites, bool Truncated, int SkippedCount = 0);

/// <summary>Durchsuchbarer Index aller Fundstellen eines Bereichs.</summary>
public sealed class FlagIndex
{
    /// <summary>Höchstzahl der Namen in einem Suchergebnis.</summary>
    public const int MaxNames = 5000;

    private static readonly FlagSearchResult EmptyResult = new(
        string.Empty, Array.Empty<FlagNameSummary>(), new Dictionary<string, IReadOnlyList<FlagSite>>(), Array.Empty<FlagSite>(), false);

    /// <summary>Fundstellen je Name, schreibgeschützt (sie gehen unverändert in <see cref="FlagSearchResult.SitesByName"/>).</summary>
    /// <remarks>Schlüssel ist die angezeigte Schreibweise; verglichen wird mit <see cref="_nameComparer"/>.</remarks>
    private readonly Dictionary<string, IReadOnlyList<FlagSite>> _byName;
    private readonly string[] _names;

    /// <summary>Vergleich der Namen: ordinal, bei <c>namesIgnoreCase</c> ohne Rücksicht auf Groß/Klein.</summary>
    private readonly StringComparer _nameComparer;
    private readonly bool _ignoreCase;
    private readonly List<FlagSite> _patterns = new();

    /// <summary>Ergebnis von <see cref="Warnings"/>, einmal je Index berechnet (der Index ändert sich nicht; das Fenster fragt bei jeder Zustandsänderung).</summary>
    private readonly Lazy<FlagSearchResult> _warnings;

    /// <summary>Ausdruck je Mustername, erst bei Bedarf gebaut (Aufbau des Index zahlt keine Regex-Kosten).</summary>
    private readonly Dictionary<string, Lazy<Regex>> _patternRegexes = new(StringComparer.Ordinal);

    /// <summary>
    /// <paramref name="sites"/>: alle Fundstellen (konkrete und Muster); die Reihenfolge innerhalb eines Namens bleibt erhalten.
    /// <paramref name="unreadFiles"/>: volle Pfade der Dateien, die nicht gelesen werden konnten. <paramref name="fileCount"/>: Zahl der gelesenen Dateien.
    /// <paramref name="namesIgnoreCase"/>: Namen ohne Rücksicht auf Groß/Klein zusammenfassen (HOI IV, EU IV); angezeigt wird die ordinal
    /// kleinste Schreibweise der Gruppe, Suche und Muster gelten dann ebenfalls ohne Rücksicht auf Groß/Klein.
    /// </summary>
    public FlagIndex(IReadOnlyList<FlagSite> sites, IReadOnlyList<string> unreadFiles, DateTime builtUtc, int fileCount, bool namesIgnoreCase = false)
    {
        _warnings = new Lazy<FlagSearchResult>(ComputeWarnings);
        _ignoreCase = namesIgnoreCase;
        _nameComparer = namesIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        _byName = new Dictionary<string, IReadOnlyList<FlagSite>>(_nameComparer);
        Sites = sites;
        UnreadFiles = unreadFiles;
        BuiltUtc = builtUtc;
        FileCount = fileCount;
        var byName = new Dictionary<string, List<FlagSite>>(_nameComparer);
        var shown = new Dictionary<string, string>(_nameComparer);
        foreach (var site in sites)
        {
            if (site.IsPattern)
            {
                _patterns.Add(site);
                if (!_patternRegexes.ContainsKey(site.Name))
                {
                    var patternName = site.Name;
                    _patternRegexes[patternName] = new Lazy<Regex>(() => ToRegex(patternName, false, namesIgnoreCase));
                }

                continue;
            }

            if (!byName.TryGetValue(site.Name, out var list))
            {
                list = new List<FlagSite>();
                byName[site.Name] = list;
                shown[site.Name] = site.Name;
            }
            else if (string.CompareOrdinal(site.Name, shown[site.Name]) < 0)
            {
                shown[site.Name] = site.Name;
            }

            list.Add(site);
        }

        foreach (var (name, list) in byName)
        {
            _byName[shown[name]] = list.AsReadOnly();
        }

        _names = _byName.Keys.ToArray();
        Array.Sort(_names, StringComparer.Ordinal);
    }

    /// <summary>Alle Fundstellen in der übergebenen Reihenfolge.</summary>
    public IReadOnlyList<FlagSite> Sites { get; }

    public IReadOnlyList<string> UnreadFiles { get; }

    public DateTime BuiltUtc { get; }

    public int FileCount { get; }

    /// <summary>
    /// Sucht nach Namen. <c>*</c> steht für beliebig viele Zeichen, alles andere gilt wörtlich (ganzer Name; groß/klein-genau, außer bei <c>namesIgnoreCase</c>).
    /// Ein Muster kommt in PatternSites, wenn die Suche auf seinen Text passt (<c>*</c> darin wörtlich) oder, bei einer Suche ohne
    /// <c>*</c>, das Muster auf die Suche passt (nicht bei Mustern ohne Buchstaben und Ziffern wie <c>*_*</c>). Leere Suche ergibt ein leeres Ergebnis.
    /// </summary>
    public FlagSearchResult Search(string query)
    {
        query = (query ?? string.Empty).Trim();
        if (query.Length == 0)
        {
            return EmptyResult;
        }

        var regex = ToRegex(query, true, _ignoreCase);
        var hasWildcard = query.Contains('*');

        var names = new List<FlagNameSummary>();
        var sitesByName = new Dictionary<string, IReadOnlyList<FlagSite>>(_nameComparer);
        var truncated = false;
        foreach (var name in _names)
        {
            if (!regex.IsMatch(name))
            {
                continue;
            }

            if (names.Count == MaxNames)
            {
                truncated = true;
                break;
            }

            var sites = _byName[name];
            names.Add(Summarize(name, sites));
            sitesByName[name] = sites;
        }

        var patternSites = _patterns
            .Where(p => regex.IsMatch(p.Name) || (!hasWildcard && !IsAllWildcard(p.Name) && _patternRegexes[p.Name].Value.IsMatch(query)))
            .ToList();

        return new FlagSearchResult(query, names, sitesByName, patternSites, truncated);
    }

    /// <summary>
    /// Alle Namen mit Warnung („nie gesetzt“ oder „nie gelesen“, also nur Namen mit Fundstelle in der aktiven Mod), ordinal sortiert,
    /// höchstens <see cref="MaxNames"/>. Für das leere Suchfeld; Query ist leer, PatternSites bleibt leer.
    /// </summary>
    public FlagSearchResult Warnings() => _warnings.Value;

    /// <summary>
    /// Wie <see cref="Warnings()"/>, aber Namen, für die <paramref name="skip"/> true liefert (ausgeblendete), werden schon beim Sammeln
    /// übersprungen: Die Liste füllt sich bis <see cref="MaxNames"/> mit sichtbaren Namen, und Truncated gilt nur für diese.
    /// <see cref="FlagSearchResult.SkippedCount"/> zählt alle übersprungenen Namen mit Warnung, auch hinter der Grenze. Ohne
    /// <paramref name="skip"/> das zwischengespeicherte Ergebnis von <see cref="Warnings()"/>.
    /// </summary>
    public FlagSearchResult Warnings(Func<string, bool>? skip) => skip is null ? _warnings.Value : ComputeWarnings(skip);

    private FlagSearchResult ComputeWarnings() => ComputeWarnings(null);

    private FlagSearchResult ComputeWarnings(Func<string, bool>? skip)
    {
        var names = new List<FlagNameSummary>();
        var sitesByName = new Dictionary<string, IReadOnlyList<FlagSite>>(_nameComparer);
        var truncated = false;
        var skipped = 0;
        foreach (var name in _names)
        {
            // Ohne skip endet die Schleife an der Grenze; mit skip zählen danach nur noch übersprungene Namen (für SkippedCount).
            if (truncated && skip is null)
            {
                break;
            }

            var sites = _byName[name];
            if (!sites.Any(s => s.Origin == FlagOrigin.ActiveMod))
            {
                continue;
            }

            // skip erst nach dem Filter auf die aktive Mod: Namen nur aus Vanilla oder anderen Mods (die meisten) kosten keinen Vergleich.
            var skipThis = skip is not null && skip(name);
            if (truncated && !skipThis)
            {
                continue;
            }

            var summary = Summarize(name, sites);
            if (!summary.NeverSet && !summary.NeverRead)
            {
                continue;
            }

            if (skipThis)
            {
                skipped++;
                continue;
            }

            if (names.Count == MaxNames)
            {
                truncated = true;
                continue;
            }

            names.Add(summary);
            sitesByName[name] = sites;
        }

        return new FlagSearchResult(string.Empty, names, sitesByName, Array.Empty<FlagSite>(), truncated, skipped);
    }

    private FlagNameSummary Summarize(string name, IReadOnlyList<FlagSite> sites)
    {
        var kinds = sites.Select(s => new FlagKindFamily(s.Kind, s.Family)).Distinct()
            .OrderBy(k => k.Kind).ThenBy(k => k.Family, StringComparer.Ordinal).ToList();

        // Ein Flag ohne Familie (HOI IV [?f:days], Stellaris set_$X$_flag) ist dasselbe Flag wie eines mit Familie: nur die Familie zeigen.
        if (kinds.Any(k => k.Kind == FlagKind.ScopeFlag && k.Family is not null))
        {
            kinds.RemoveAll(k => k.Kind == FlagKind.ScopeFlag && k.Family is null);
        }
        var setCount = sites.Count(s => s.Role == FlagRole.Set);
        var readCount = sites.Count(s => s.Role == FlagRole.Read);
        var removeCount = sites.Count(s => s.Role == FlagRole.Remove);
        var inActiveMod = sites.Any(s => s.Origin == FlagOrigin.ActiveMod);
        var warnable = inActiveMod && !kinds.Any(k => k.Kind == FlagKind.SymbolValue);
        var neverSet = warnable && setCount == 0;
        var neverRead = warnable && readCount == 0;
        var mayCover = (neverSet && PatternCovers(name, kinds, FlagRole.Set)) || (neverRead && PatternCovers(name, kinds, FlagRole.Read));
        return new FlagNameSummary(name, kinds, setCount, readCount, removeCount, inActiveMod, neverSet, neverRead, mayCover);
    }

    /// <summary>
    /// Deckt ein Muster mit dieser Rolle und einer Art des Namens den Namen ab? Muster ohne Buchstaben und Ziffern (<c>*</c>, <c>*_*</c>; z. B. ein nie übergebenes <c>$P$</c>
    /// als ganzer Name) zählen nicht, denn sie passten auf jeden Namen und nähmen jeder Warnung die Schärfe.
    /// </summary>
    private bool PatternCovers(string name, IReadOnlyList<FlagKindFamily> kinds, FlagRole role) =>
        _patterns.Any(p => p.Role == role && !IsAllWildcard(p.Name) && kinds.Any(k => k.Kind == p.Kind) && _patternRegexes[p.Name].Value.IsMatch(name));

    /// <summary>
    /// Bleibt ohne die <c>*</c> kein Buchstabe und keine Ziffer übrig (<c>*</c>, <c>*_*</c>, <c>*.*</c>)? Solche Muster passen auf fast jeden Namen;
    /// sie erscheinen nur bei einer Suche, die ihren Text trifft (etwa <c>*</c>).
    /// </summary>
    private static bool IsAllWildcard(string name) => !name.Any(char.IsLetterOrDigit);

    /// <summary>
    /// Text mit <c>*</c> als Platzhalter zu einem verankerten regulären Ausdruck; alle anderen Zeichen wörtlich. Mehrere <c>*</c> hintereinander gelten als eins.
    /// <paramref name="nonBacktracking"/> nur für die Suchanfrage (Laufzeit linear in der Namenslänge); der Aufbau dieser Ausdrücke ist teuer,
    /// daher nutzen die vielen Muster-Fundstellen die Standardoptionen. <paramref name="ignoreCase"/>: ohne Rücksicht auf Groß/Klein.
    /// </summary>
    internal static Regex ToRegex(string text, bool nonBacktracking, bool ignoreCase) =>
        new("^" + Regex.Replace(Regex.Escape(text), @"(\\\*)+", ".*") + "$",
            RegexOptions.CultureInvariant | RegexOptions.Singleline
            | (nonBacktracking ? RegexOptions.NonBacktracking : RegexOptions.None)
            | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None));
}
