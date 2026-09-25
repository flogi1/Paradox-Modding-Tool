using System.Text.RegularExpressions;

namespace ParadoxModTool.ScriptDocs;

/// <summary>Suchanfrage an die Datenbank: Text, Arten, Scope und ob veraltete Einträge ausgeblendet werden.</summary>
public sealed class ScriptDocQuery
{
    /// <summary>Suchtext; ohne Platzhalter ein Teilstring, mit <c>*</c> (beliebig viele Zeichen) oder <c>?</c> (ein Zeichen) ein Muster über den ganzen Namen.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Nur diese Arten; <see langword="null"/> oder leer = alle.</summary>
    public ISet<ScriptDocKind>? Kinds { get; set; }

    /// <summary>Nur Einträge, die in diesem Scope benutzbar sind; <see langword="null"/> oder leer = alle. Schreibweise (Groß-/Kleinschreibung, "_") ist egal.</summary>
    public string? Scope { get; set; }

    public bool HideOutdated { get; set; } = true;

    /// <summary>
    /// Gefilterte Einträge, sortiert nach Trefferqualität: Name beginnt mit dem Text, Name enthält den Text, nur die Beschreibung enthält ihn;
    /// innerhalb davon alphabetisch. Bei einem Muster mit Platzhaltern zählt nur der Name.
    /// </summary>
    public IReadOnlyList<ScriptDocEntry> Run(ScriptDocDatabase database)
    {
        var text = Text.Trim();
        var isPattern = text.Contains('*') || text.Contains('?');
        var pattern = isPattern ? PatternRegex(text) : null;
        var scopeKey = string.IsNullOrWhiteSpace(Scope) ? null : ScopeKey(Scope);

        var ranked = new List<(int Rank, ScriptDocEntry Entry)>();
        foreach (var entry in database.Entries)
        {
            if (Kinds is { Count: > 0 } && !Kinds.Contains(entry.Kind))
            {
                continue;
            }

            if (HideOutdated && database.IsOutdated(entry))
            {
                continue;
            }

            if (scopeKey is not null && !entry.Scopes.Any(s => ScopeKey(s) == scopeKey))
            {
                continue;
            }

            var rank = text.Length == 0 ? 0 : RankOf(entry, text, pattern);
            if (rank >= 0)
            {
                ranked.Add((rank, entry));
            }
        }

        return ranked
            .OrderBy(r => r.Rank)
            .ThenBy(r => r.Entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r => r.Entry)
            .ToList();
    }

    /// <summary>Vergleichsschlüssel für Scopes: "InternationalOrganization" und "international_organization" sind derselbe Scope.</summary>
    public static string ScopeKey(string scope) => scope.Replace("_", string.Empty).ToLowerInvariant();

    /// <summary>Alle vorkommenden Scopes (in der Schreibweise des ersten Vorkommens), alphabetisch.</summary>
    public static IReadOnlyList<string> AllScopes(ScriptDocDatabase database) =>
        database.Entries
            .SelectMany(e => e.Scopes)
            .GroupBy(ScopeKey)
            .Select(g => g.First())
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static int RankOf(ScriptDocEntry entry, string text, Regex? pattern)
    {
        if (pattern is not null)
        {
            return pattern.IsMatch(entry.Name) ? 0 : -1;
        }

        if (entry.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (entry.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return entry.Description.Contains(text, StringComparison.OrdinalIgnoreCase) ? 2 : -1;
    }

    private static Regex PatternRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".");
        return new Regex("^" + escaped + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    }
}
