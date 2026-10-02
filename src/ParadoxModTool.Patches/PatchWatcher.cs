using System.Text.RegularExpressions;

namespace ParadoxModTool.Patches;

/// <summary>Ein Suchmuster des Wächters: ein eingetragenes Muster oder (<see cref="RowKey"/>) der Schlüssel einer Tabellenzeile.</summary>
public sealed record WatchPattern(string Text, bool RowKey);

/// <summary>Eine Fundstelle: Zeilennummer (ab 1), getroffenes Muster und der Code-Teil der Zeile ohne Kommentar.</summary>
public sealed record WatchLine(int Line, string Pattern, bool RowKey, string Text);

/// <summary>
/// Wächter der Patch-Rezepte (Spec 2026-09-30-patch-recipes, Teil 3): sucht die Muster eines Rezepts in Vanilla- und Mod-Dateien
/// und stuft jede Trefferdatei ein.
/// </summary>
public static partial class PatchWatcher
{
    private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase)
    {
        "gfx", "fonts", "licenses", "tests", "content_source", "sound", "music", "dlc",
    };

    /// <summary>Ordner, die nie durchsucht werden, egal auf welcher Ebene (EU V: <c>in_game/gfx/…</c>).</summary>
    public static IReadOnlyList<string> ExcludedFolders { get; } = Excluded.Order(StringComparer.Ordinal).ToList();

    /// <summary>Erst die eingetragenen Muster, dann die Schlüssel der Tabellenzeilen; leere und doppelte fallen weg.</summary>
    public static IReadOnlyList<WatchPattern> Patterns(PatchRecipe recipe)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<WatchPattern>();
        foreach (var (text, rowKey) in recipe.Watch.Patterns.Select(p => (p.Trim(), false)).Concat(recipe.Rows.Select(r => (r.Key.Trim(), true))))
        {
            if (text.Length > 0 && seen.Add(text))
            {
                result.Add(new WatchPattern(text, rowKey));
            }
        }

        return result;
    }

    /// <summary>Nur <c>.txt</c>-Dateien außerhalb der ausgeschlossenen Ordner.</summary>
    public static bool IsScannedFile(string relativePath)
    {
        var segments = relativePath.Replace('\\', '/').Split('/');
        return segments[^1].EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            && !segments[..^1].Any(Excluded.Contains);
    }

    /// <summary>
    /// Fundstellen in einem Text. Es zählt nur der Code-Teil einer Zeile (ohne Kommentar). Mit <paramref name="recipeId"/> werden die erzeugten Bereiche
    /// dieses Rezepts übersprungen; eine Zeile, die genau ein eigener Listeneintrag des Rezepts ist, zählt nicht.
    /// </summary>
    public static IReadOnlyList<WatchLine> FindLines(string text, IReadOnlyList<WatchPattern> patterns, string? recipeId, IReadOnlyCollection<string> ownEntries) =>
        FindLines(text, Matcher.For(patterns), recipeId, ownEntries);

    /// <summary>Die Ausdrücke eines Scans, einmal gebaut für alle Dateien (sonst kostet der Bau mehr als die Suche).</summary>
    private sealed class Matcher
    {
        private Matcher(Regex? any, IReadOnlyList<(WatchPattern Pattern, Regex Regex)> each)
        {
            Any = any;
            Each = each;
        }

        public Regex? Any { get; }

        public IReadOnlyList<(WatchPattern Pattern, Regex Regex)> Each { get; }

        public static Matcher For(IReadOnlyList<WatchPattern> patterns) => patterns.Count == 0
            ? new Matcher(null, Array.Empty<(WatchPattern, Regex)>())
            : new Matcher(
                new Regex(LinePattern.BuildAny(patterns.Select(p => p.Text).ToList()).ToString(), RegexOptions.CultureInvariant | RegexOptions.Compiled),
                patterns.Select(p => (p, LinePattern.Build(p.Text, MatchKind.Text))).ToList());
    }

    private static IReadOnlyList<WatchLine> FindLines(string text, Matcher matcher, string? recipeId, IReadOnlyCollection<string> ownEntries)
    {
        if (matcher.Any is null || !matcher.Any.IsMatch(text))
        {
            return Array.Empty<WatchLine>();
        }

        var regexes = matcher.Each;
        var regions = recipeId is null ? new List<(int Start, int End)>() : LineRuleApplier.OwnRegions(text, recipeId);
        var entries = ownEntries.Select(Normalize).Where(e => e.Length > 0).ToList();
        var result = new List<WatchLine>();
        var position = 0;
        var number = 0;
        foreach (var raw in text.Split('\n'))
        {
            number++;
            var start = position;
            position += raw.Length + 1;
            if (regions.Any(r => start >= r.Start && start < r.End))
            {
                continue;
            }

            var line = raw.TrimEnd('\r');
            var code = line[..ScriptSpans.CodeLength(line)];
            if (code.Trim().Length == 0 || !matcher.Any.IsMatch(code))
            {
                continue;
            }

            // Eigene Listeneinträge schreibt der Motor immer auf eine eigene Zeile (ScriptSpans.InsertBeforeBrace).
            if (entries.Count > 0 && entries.Contains(Normalize(code)))
            {
                continue;
            }

            foreach (var (pattern, regex) in regexes)
            {
                if (!regex.IsMatch(code))
                {
                    continue;
                }

                result.Add(new WatchLine(number, pattern.Text, pattern.RowKey, code.Trim()));
            }
        }

        return result;
    }

    private static string Normalize(string text) => Regex.Replace(text.Trim(), @"\s+", " ");
}
