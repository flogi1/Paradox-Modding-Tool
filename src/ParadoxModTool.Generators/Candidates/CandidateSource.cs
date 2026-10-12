namespace ParadoxModTool.Generators.Candidates;

/// <summary>Ein Modifier, der als Kandidat in Frage kommt: <see cref="IsNew"/> aus der Skript-Datenbank (neu im jüngsten Import), <see cref="Boolean"/> aus der Definition (ja/nein).</summary>
public sealed record CandidateSource(string Name, bool IsNew, bool Boolean);

/// <summary>Die Quellen der Kandidatenliste: Definitionen der Spieldateien und Einträge der Skript-Datenbank.</summary>
public static class CandidateSources
{
    /// <summary>
    /// Vereinigung nach Name (ohne Groß-/Kleinschreibung). Eine Definition zählt, wenn ihre Kategorie <paramref name="category"/> ist (ohne
    /// Groß-/Kleinschreibung; leer und <c>all</c> passen nie), ein Eintrag der Skript-Datenbank, wenn einer seiner Scopes <paramref name="category"/> ist.
    /// <see cref="CandidateSource.IsNew"/> kommt nur aus der Skript-Datenbank, <see cref="CandidateSource.Boolean"/> nur aus den Definitionen.
    /// </summary>
    public static IReadOnlyList<CandidateSource> Merge(
        string category,
        IEnumerable<(string Name, string Category, bool Boolean)> definitions,
        IEnumerable<(string Name, IReadOnlyList<string> Scopes, bool IsNew)> scriptDocs)
    {
        var found = new Dictionary<string, CandidateSource>(StringComparer.OrdinalIgnoreCase);
        if (category.Length == 0 || category.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<CandidateSource>();
        }

        foreach (var (name, defCategory, boolean) in definitions)
        {
            if (name.Length > 0 && string.Equals(defCategory, category, StringComparison.OrdinalIgnoreCase))
            {
                found[name] = found.TryGetValue(name, out var old) ? old with { Boolean = old.Boolean || boolean } : new CandidateSource(name, false, boolean);
            }
        }

        foreach (var (name, scopes, isNew) in scriptDocs)
        {
            if (name.Length > 0 && scopes.Any(s => string.Equals(s, category, StringComparison.OrdinalIgnoreCase)))
            {
                found[name] = found.TryGetValue(name, out var old) ? old with { IsNew = old.IsNew || isNew } : new CandidateSource(name, isNew, false);
            }
        }

        return found.Values.ToList();
    }
}
