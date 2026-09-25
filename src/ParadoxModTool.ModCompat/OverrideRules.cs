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
public sealed record OverrideRule(string Folder, OverrideBehavior Behavior, bool Certain = true);

/// <summary>Ergebnis der Regelsuche für einen Dateipfad. <see cref="Scope"/> ist der Ordner, in dem Schlüssel einander überschreiben.</summary>
public readonly record struct RuleMatch(OverrideBehavior Behavior, bool Certain, bool Known, string Scope);

/// <summary>Die Regeltabelle eines Spiels: ausgelieferte Einträge, darüber die Einträge der Spielebene. Der längste passende Ordner gilt, bei gleicher Länge der spätere Eintrag.</summary>
public sealed class OverrideRules
{
    private readonly List<(string[] Segments, OverrideRule Rule)> _entries = new();

    public OverrideRules(IEnumerable<OverrideRule> shipped, IEnumerable<OverrideRule>? game = null)
    {
        foreach (var rule in shipped.Concat(game ?? Array.Empty<OverrideRule>()))
        {
            var segments = Split(rule.Folder);
            // Ein Eintrag desselben Ordners aus der höheren Ebene ersetzt den früheren.
            _entries.RemoveAll(e => e.Segments.SequenceEqual(segments, StringComparer.OrdinalIgnoreCase));
            _entries.Add((segments, rule));
        }
    }

    public IReadOnlyList<OverrideRule> Rules => _entries.Select(e => e.Rule).ToList();

    public static OverrideRules FromProfile(GameProfile profile, IEnumerable<OverrideRule>? game = null)
    {
        var shipped = new List<OverrideRule>();
        foreach (var entry in profile.OverrideRules)
        {
            if (Enum.TryParse<OverrideBehavior>(entry.Behavior, ignoreCase: true, out var behavior) && Split(entry.Folder).Length > 0)
            {
                shipped.Add(new OverrideRule(entry.Folder, behavior, entry.Certain));
            }
        }

        return new OverrideRules(shipped, game);
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

        return new RuleMatch(found.Rule.Behavior, found.Rule.Certain, true, string.Join('/', directory[..depth]));
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
