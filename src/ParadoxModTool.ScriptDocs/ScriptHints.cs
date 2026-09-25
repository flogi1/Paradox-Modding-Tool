namespace ParadoxModTool.ScriptDocs;

/// <summary>Angaben zu einem Eintrag der Script-Datenbank für Hover-Tipps und Autovervollständigung im Editor.</summary>
public sealed record ScriptHint(
    string Name,
    ScriptDocKind Kind,
    string Description,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> Targets,
    bool IsOutdated,
    bool IsNew);

/// <summary>Liefert dem Editor Hinweise zu Wörtern im Skript.</summary>
public interface IScriptHintProvider
{
    /// <summary>Alle Einträge mit genau diesem Namen (derselbe Name kann z. B. Trigger und Modifier zugleich sein); leer, wenn unbekannt.</summary>
    IReadOnlyList<ScriptHint> Lookup(string word);

    /// <summary>Höchstens <paramref name="max"/> Einträge, deren Name mit <paramref name="prefix"/> beginnt (Groß-/Kleinschreibung egal); aktuelle vor veralteten, dann alphabetisch.</summary>
    IReadOnlyList<ScriptHint> Complete(string prefix, int max);
}

/// <summary>
/// Der <see cref="IScriptHintProvider"/> über einer <see cref="ScriptDocDatabase"/>. Die Datenbank lässt sich im Betrieb austauschen
/// (nach einem Einlesen der Logs); Abfragen aus anderen Threads sehen immer einen vollständigen Stand.
/// </summary>
public sealed class ScriptHintIndex : IScriptHintProvider
{
    private sealed record Snapshot(Dictionary<string, List<ScriptHint>> ByName, ScriptHint[] Sorted);

    private static readonly Snapshot Empty = new(new Dictionary<string, List<ScriptHint>>(), Array.Empty<ScriptHint>());

    private volatile Snapshot _snapshot = Empty;

    public ScriptHintIndex(ScriptDocDatabase? database = null)
    {
        if (database is not null)
        {
            SetDatabase(database);
        }
    }

    /// <summary>Baut den Index aus dem aktuellen Stand der Datenbank neu auf.</summary>
    public void SetDatabase(ScriptDocDatabase database)
    {
        var hints = database.Entries
            .Select(e => new ScriptHint(e.Name, e.Kind, e.Description, e.Scopes.ToArray(), e.Targets.ToArray(), database.IsOutdated(e), database.IsNew(e)))
            .ToList();

        var byName = new Dictionary<string, List<ScriptHint>>(StringComparer.Ordinal);
        foreach (var hint in hints)
        {
            if (!byName.TryGetValue(hint.Name, out var list))
            {
                byName[hint.Name] = list = new List<ScriptHint>();
            }

            list.Add(hint);
        }

        var sorted = hints
            .OrderBy(h => h.IsOutdated)
            .ThenBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(h => h.Kind)
            .ToArray();

        _snapshot = new Snapshot(byName, sorted);
    }

    public IReadOnlyList<ScriptHint> Lookup(string word) =>
        _snapshot.ByName.TryGetValue(word, out var list)
            ? list.OrderBy(h => h.IsOutdated).ThenBy(h => h.Kind).ToList()
            : Array.Empty<ScriptHint>();

    public IReadOnlyList<ScriptHint> Complete(string prefix, int max)
    {
        if (prefix.Length == 0 || max <= 0)
        {
            return Array.Empty<ScriptHint>();
        }

        return _snapshot.Sorted
            .Where(h => h.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Take(max)
            .ToList();
    }
}

/// <summary>Hilfen, um in einer Textzeile das Wort unter dem Zeiger oder vor dem Cursor zu finden.</summary>
public static class ScriptWords
{
    /// <summary>Wortzeichen in Skriptnamen: Buchstaben, Ziffern und "_" ("scope:owner" und "root.owner" zerfallen so in einzelne Wörter).</summary>
    public static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>
    /// Das Wort, das das Zeichen an <paramref name="column"/> (0-basiert) enthält; <see langword="null"/>, wenn dort kein Wortzeichen steht
    /// oder das Wort keinen Buchstaben enthält (reine Zahlen sind keine Namen).
    /// </summary>
    public static (int Start, int Length)? WordAt(string line, int column)
    {
        if (column < 0 || column >= line.Length || !IsWordChar(line[column]))
        {
            return null;
        }

        var start = column;
        while (start > 0 && IsWordChar(line[start - 1]))
        {
            start--;
        }

        var end = column;
        while (end < line.Length && IsWordChar(line[end]))
        {
            end++;
        }

        var word = line.AsSpan(start, end - start);
        return word.ToString().Any(char.IsLetter) ? (start, end - start) : null;
    }

    /// <summary>Beginn des Wortes, das direkt vor <paramref name="caretColumn"/> endet (gleich <paramref name="caretColumn"/>, wenn dort kein Wort steht).</summary>
    public static int PrefixStart(string line, int caretColumn)
    {
        var start = Math.Min(caretColumn, line.Length);
        while (start > 0 && IsWordChar(line[start - 1]))
        {
            start--;
        }

        return start;
    }

    /// <summary>
    /// Steht der Text am Ende von <paramref name="lineBeforeWord"/> im Code? Nein, wenn davor in der Zeile ein Kommentar (<c>#</c>) beginnt
    /// oder ein Anführungszeichen offen ist (Zeichenkette, z. B. ein Lokalisierungsschlüssel).
    /// </summary>
    public static bool IsCodeContext(string lineBeforeWord)
    {
        var inString = false;
        foreach (var c in lineBeforeWord)
        {
            if (c == '"')
            {
                inString = !inString;
            }
            else if (c == '#' && !inString)
            {
                return false;
            }
        }

        return !inString;
    }
}
