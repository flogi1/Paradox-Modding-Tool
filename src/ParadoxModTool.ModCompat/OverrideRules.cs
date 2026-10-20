using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.ModCompat;

/// <summary>Wie ein Ordner Überschreibungen behandelt (Quelle: EU-V-Wiki "Mod compatibility").</summary>
public enum OverrideBehavior
{
    /// <summary>Nur der Dateipfad zählt (Bilder, Modelle, Sonstiges).</summary>
    WholeFile,

    /// <summary>Einträge der obersten Ebene, Präfixe erlaubt.</summary>
    PerKey,

    /// <summary>Erster gewinnt je Schlüssel.</summary>
    FirstWins,

    /// <summary>Letzter gewinnt je Schlüssel.</summary>
    LastWins,

    /// <summary>Einzelne Schlüssel unterhalb der obersten Ebene sind ersetzbar (defines).</summary>
    PartialKeys,

    /// <summary>Nur anhängen (on_actions).</summary>
    AppendOnly,

    /// <summary>Schlüssel je Sprache, Unterordner replace/ überschreibt einzeln.</summary>
    LocalizationKeys,
}

/// <summary>Ein Eintrag der Tabelle. <see cref="Folder"/> ist relativ zum Wurzelordner der Spielinhalte, <c>*</c> steht für genau ein Segment.</summary>
/// <remarks>
/// <see cref="Unwrap"/> und <see cref="KeyField"/> bestimmen, was der Schlüssel eines Eintrags ist (siehe <see cref="RuleKeys"/>): Einträge in einer
/// Hülle wie <c>country_decisions = { … }</c> zählen einzeln, und bei <see cref="KeyField"/> liefert das Feld im Block den Namen (<c>id</c> bei EU-IV-Events).
/// <see cref="Operators"/> sind die Präfixe, die in diesem Ordner wirken; null = die des Spiels, leer = keine (Victoria 3).
/// </remarks>
public sealed record OverrideRule(string Folder, OverrideBehavior Behavior, bool Certain = true, IReadOnlyList<string>? Unwrap = null, string? KeyField = null,
    IReadOnlyList<string>? Operators = null);

/// <summary>Ergebnis der Regelsuche für einen Dateipfad. <see cref="Scope"/> ist der Ordner, in dem Schlüssel einander überschreiben.</summary>
public readonly record struct RuleMatch(OverrideBehavior Behavior, bool Certain, bool Known, string Scope, string? KeyField = null, IReadOnlyList<string>? Wrappers = null,
    IReadOnlyList<string>? Operators = null)
{
    /// <summary>Hüllen-Blöcke, deren Einträge einzeln zählen; leer, wenn die Regel keine hat.</summary>
    public IReadOnlyList<string> Unwrap => Wrappers ?? Array.Empty<string>();

    /// <summary>Ob das Präfix (ohne Doppelpunkt) hier wirkt: die Präfixe der Regel, ohne eigene die des Spiels (<paramref name="gameOperators"/>).</summary>
    public bool Allows(string op, IReadOnlyCollection<string> gameOperators) =>
        (Operators ?? gameOperators).Contains(op, StringComparer.OrdinalIgnoreCase);
}

/// <summary>Die Regeltabelle eines Spiels: ausgelieferte Einträge, darüber die Einträge der Spielebene. Der längste passende Ordner gilt, bei gleicher Länge der spätere Eintrag.</summary>
public sealed class OverrideRules
{
    private readonly List<(string[] Segments, OverrideRule Rule)> _entries = new();

    public OverrideRules(IEnumerable<OverrideRule> shipped, IEnumerable<OverrideRule>? game = null)
    {
        var gameList = (game ?? Array.Empty<OverrideRule>()).ToList();
        var gameRules = gameList.ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var entry in shipped.Concat(gameList))
        {
            var rule = entry;
            var segments = Split(rule.Folder);
            // Ein Eintrag desselben Ordners aus der höheren Ebene ersetzt den früheren. Die Spielebene (Regelfenster) kennt keine
            // Schlüsselangaben; ohne eigene behält sie die des früheren Eintrags, sonst kämen die Fehlalarme über gemeinsame Hüllen zurück.
            var earlier = _entries.FindLast(e => e.Segments.SequenceEqual(segments, StringComparer.OrdinalIgnoreCase));
            if (earlier.Rule is { } previous && rule.Unwrap is null && rule.KeyField is null)
            {
                rule = rule with { Unwrap = previous.Unwrap, KeyField = previous.KeyField };
            }

            // Die Präfixe eines Ordners kommen aus dem Profil; das Regelfenster kennt sie nicht und behält die des früheren Eintrags,
            // für einen neuen Ordner die der Regel, die bisher für ihn galt (Victoria 3: common/x ohne Präfixe).
            if (rule.Operators is null && gameRules.Contains(entry))
            {
                var parent = earlier.Rule is not null ? earlier : _entries
                    .Where(e => e.Segments.Length <= segments.Length && Matches(e.Segments, segments))
                    .OrderBy(e => e.Segments.Length)
                    .LastOrDefault();
                if (parent.Rule is { } inherited)
                {
                    rule = rule with { Operators = inherited.Operators };
                }
            }

            _entries.RemoveAll(e => e.Segments.SequenceEqual(segments, StringComparer.OrdinalIgnoreCase));
            _entries.Add((segments, rule));
        }
    }

    public IReadOnlyList<OverrideRule> Rules => _entries.Select(e => e.Rule).ToList();

    /// <summary>INJECT hängt an, statt Teilschlüssel zu überschreiben (siehe <see cref="GameProfile.InjectAppends"/>).</summary>
    public bool InjectAppends { get; init; }

    /// <summary>Zuerst geladen gewinnt nach Ladereihenfolge über Vanilla und Mods (siehe <see cref="GameProfile.FirstWinsInLoadOrder"/>).</summary>
    public bool FirstWinsInLoadOrder { get; init; }

    /// <summary>Zuletzt geladen gewinnt nach Ladereihenfolge der Dateien über Vanilla und Mods (siehe <see cref="GameProfile.LastWinsInLoadOrder"/>).</summary>
    public bool LastWinsInLoadOrder { get; init; }

    /// <summary>Dateinamen mit Groß-/Kleinschreibung vergleichen (siehe <see cref="GameProfile.LoadOrderCaseSensitive"/>).</summary>
    public bool LoadOrderCaseSensitive { get; init; }

    /// <summary>Ladereihenfolge der Pfade über Vanilla und Mods für <see cref="FirstWinsInLoadOrder"/> und <see cref="LastWinsInLoadOrder"/>.</summary>
    public GameLoadOrder LoadOrder => GameLoadOrder.For(LoadOrderCaseSensitive);

    public static OverrideRules FromProfile(GameProfile profile, IEnumerable<OverrideRule>? game = null)
    {
        var shipped = new List<OverrideRule>();
        foreach (var entry in profile.OverrideRules)
        {
            if (Enum.TryParse<OverrideBehavior>(entry.Behavior, ignoreCase: true, out var behavior) && Split(entry.Folder).Length > 0)
            {
                shipped.Add(new OverrideRule(entry.Folder, behavior, entry.Certain, entry.Unwrap is { Count: > 0 } unwrap ? unwrap.ToList() : null,
                    string.IsNullOrWhiteSpace(entry.KeyField) ? null : entry.KeyField.Trim(), entry.Operators?.ToList()));
            }
        }

        return new OverrideRules(shipped, game) { InjectAppends = profile.InjectAppends, FirstWinsInLoadOrder = profile.FirstWinsInLoadOrder, LastWinsInLoadOrder = profile.LastWinsInLoadOrder, LoadOrderCaseSensitive = profile.LoadOrderCaseSensitive };
    }

    public RuleMatch Match(string relativePath)
    {
        var all = Split(relativePath);
        var directory = all.Length > 0 ? all[..^1] : all;
        (string[] Segments, OverrideRule Rule)? best = null;
        foreach (var entry in _entries)
        {
            if (entry.Segments.Length <= directory.Length && Matches(entry.Segments, directory) && (best is null || entry.Segments.Length >= best.Value.Segments.Length))
            {
                best = entry;
            }
        }

        if (best is not { } found)
        {
            return new RuleMatch(OverrideBehavior.PerKey, false, false, string.Join('/', directory));
        }

        var depth = found.Segments.Length;
        if (found.Rule.Behavior == OverrideBehavior.PerKey && directory.Length > depth)
        {
            depth++;
        }

        return new RuleMatch(found.Rule.Behavior, found.Rule.Certain, true, string.Join('/', directory[..depth]), found.Rule.KeyField, found.Rule.Unwrap, found.Rule.Operators);
    }

    private static bool Matches(string[] pattern, string[] directory)
    {
        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] != "*" && !string.Equals(pattern[i], directory[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string[] Split(string path) =>
        path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
