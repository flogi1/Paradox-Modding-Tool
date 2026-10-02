using System.Globalization;
using System.Text.RegularExpressions;

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

    /// <summary>
    /// Höchstens <paramref name="max"/> Einträge, deren Name mit <paramref name="prefix"/> beginnt (Groß-/Kleinschreibung egal); aktuelle vor veralteten, dann alphabetisch.
    /// Ohne <paramref name="includeCustomLocalizations"/> fehlen die Custom Localizations (sie zählen dann auch nicht für <paramref name="max"/>).
    /// </summary>
    IReadOnlyList<ScriptHint> Complete(string prefix, int max, bool includeCustomLocalizations = true);
}

/// <summary>
/// Der <see cref="IScriptHintProvider"/> über einer <see cref="ScriptDocDatabase"/>. Die Datenbank lässt sich im Betrieb austauschen
/// (nach einem Einlesen der Logs); Abfragen aus anderen Threads sehen immer einen vollständigen Stand.
/// </summary>
public sealed class ScriptHintIndex : IScriptHintProvider
{
    private sealed record Snapshot(Dictionary<string, List<ScriptHint>> ByName, ScriptHint[] Sorted, (Regex Pattern, List<ScriptHint> Hints)[] Templates);

    private static readonly Snapshot Empty = new(new Dictionary<string, List<ScriptHint>>(), Array.Empty<ScriptHint>(), Array.Empty<(Regex, List<ScriptHint>)>());

    /// <summary>Platzhalter in Vorlagennamen wie <c>$CULTURE$_opinion</c> (Crusader Kings III).</summary>
    private static readonly Regex Placeholder = new(@"\$[A-Z0-9_]+\$", RegexOptions.CultureInvariant);

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

        // Jeder Platzhalter steht für mindestens ein Zeichen eines Namens; die festen Teile müssen genau passen.
        var templates = byName
            .Where(pair => Placeholder.IsMatch(pair.Key))
            .Select(pair => (new Regex("^" + string.Join("[A-Za-z0-9_]+", Placeholder.Split(pair.Key).Select(Regex.Escape)) + "$", RegexOptions.CultureInvariant), pair.Value))
            .ToArray();

        _snapshot = new Snapshot(byName, sorted, templates);
    }

    /// <summary>Einträge mit genau diesem Namen; gibt es keine, die Vorlagen, deren Muster den Namen trifft (z. B. <c>$CULTURE$_opinion</c> für <c>french_opinion</c>).</summary>
    public IReadOnlyList<ScriptHint> Lookup(string word)
    {
        var snapshot = _snapshot;
        var hits = snapshot.ByName.TryGetValue(word, out var list)
            ? list
            : snapshot.Templates.Where(t => t.Pattern.IsMatch(word)).SelectMany(t => t.Hints);
        return hits.OrderBy(h => h.IsOutdated).ThenBy(h => h.Kind).ToList();
    }

    public IReadOnlyList<ScriptHint> Complete(string prefix, int max, bool includeCustomLocalizations = true)
    {
        if (prefix.Length == 0 || max <= 0)
        {
            return Array.Empty<ScriptHint>();
        }

        return _snapshot.Sorted
            .Where(h => includeCustomLocalizations || h.Kind != ScriptDocKind.CustomLocalization)
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

    /// <summary>
    /// Die Zahl an <paramref name="column"/> (0-basiert) samt Schlüssel links davon, z. B. <c>global_tax_modifier = 0.1</c>; <see langword="null"/>,
    /// wenn dort keine Zahl steht, die Zahl zu einem Namen gehört, der Operator kein einfaches <c>=</c> ist (Vergleiche wie <c>&gt;=</c>,
    /// <c>?=</c>) oder die Stelle im Kommentar oder in einer Zeichenkette liegt. Gelesen wird immer mit Dezimalpunkt.
    /// </summary>
    public static (int Start, int Length, string Key, decimal Value)? NumberAt(string line, int column)
    {
        if (column < 0 || column >= line.Length || !IsNumberChar(line[column]))
        {
            return null;
        }

        var start = column;
        while (start > 0 && IsNumberChar(line[start - 1]))
        {
            start--;
        }

        var end = column + 1;
        while (end < line.Length && IsNumberChar(line[end]))
        {
            end++;
        }

        if ((start > 0 && IsWordChar(line[start - 1])) || (end < line.Length && IsWordChar(line[end])))
        {
            return null;
        }

        var token = line[start..end];
        if (token.IndexOf('-', 1) >= 0
            || !decimal.TryParse(token, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return KeyBeforeEquals(line, start) is { } key ? (start, end - start, key, value) : null;
    }

    /// <summary>
    /// Der Verweis auf eine Konstante oder einen Script-Value an <paramref name="column"/> (0-basiert) samt Schlüssel links vom <c>=</c>, z. B.
    /// <c>county_fertility_growth_mult = small_county_fertility_growth_gain</c> oder <c>x = @konstante</c>; <see langword="null"/>, wenn dort ein anderes Wort steht
    /// (Teil von <c>scope:x</c>, Zahl, Zeichenkette), der Operator kein einfaches <c>=</c> ist oder die Stelle im Kommentar liegt. Ob der Name existiert, prüft der Aufrufer.
    /// </summary>
    public static (int Start, int Length, string Key, string Name)? ValueRefAt(string line, int column)
    {
        if (column < 0 || column >= line.Length || !(IsWordChar(line[column]) || line[column] == '@'))
        {
            return null;
        }

        var start = column;
        while (start > 0 && IsWordChar(line[start - 1]))
        {
            start--;
        }

        if (line[start] != '@' && start > 0 && line[start - 1] == '@')
        {
            start--;
        }

        var end = Math.Max(column, start) + 1;
        while (end < line.Length && IsWordChar(line[end]))
        {
            end++;
        }

        var name = line[start..end];
        if (!name.Any(char.IsLetter) || (end < line.Length && !(char.IsWhiteSpace(line[end]) || line[end] is '}' or '#')))
        {
            return null;
        }

        return KeyBeforeEquals(line, start) is { } key ? (start, end - start, key, name) : null;
    }

    /// <summary>Der Schlüssel vor <c>=</c> links von <paramref name="valueStart"/> (nur ein einfaches <c>=</c>, im Code, mit mindestens einem Buchstaben); sonst <see langword="null"/>.</summary>
    private static string? KeyBeforeEquals(string line, int valueStart)
    {
        var i = valueStart - 1;
        while (i >= 0 && char.IsWhiteSpace(line[i]))
        {
            i--;
        }

        if (i < 1 || line[i] != '=' || line[i - 1] is '<' or '>' or '!' or '?' or '=')
        {
            return null;
        }

        i--;
        while (i >= 0 && char.IsWhiteSpace(line[i]))
        {
            i--;
        }

        var keyEnd = i + 1;
        while (i >= 0 && IsWordChar(line[i]))
        {
            i--;
        }

        var keyStart = i + 1;
        var key = line[keyStart..keyEnd];
        return key.Any(char.IsLetter) && IsCodeContext(line[..keyStart]) ? key : null;
    }

    private static bool IsNumberChar(char c) => char.IsAsciiDigit(c) || c is '.' or '-';

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
