using System.Text.RegularExpressions;
using ParadoxModTool.Scopes;

namespace ParadoxModTool.Performance;

/// <summary>
/// Variablen, die ein Block liest, ohne vorher zu prüfen, ob es sie gibt: Fehlt die Variable, schreibt jeder Aufruf einen Fehler ins error.log.
/// Geprüft werden Script Values und Trigger des Dumps sowie (auch ohne Dump) jede Stelle des error.log, deren häufigste Meldung eine Variable nennt.
/// Als Guard zählt, was im obersten Block textlich vor dem Zugriff steht: <c>has_variable = NAME</c>, <c>exists = var:NAME</c> oder
/// <c>var:NAME ?=</c> (auch <c>var:NAME.x ?=</c> und ohne Leerzeichen <c>var:NAME?=</c>); innerhalb von <c>NOT</c>/<c>NOR</c> zählt ein Guard nicht. Welcher Zweig ihn trägt, prüft die Regel nicht.
/// </summary>
public sealed class UnguardedVariableRule : IPerformanceRule
{
    private const string VarPrefix = "var:";

    private static readonly Regex TrailingLocation = new(@"\s+at\s+\S+:\d+\s*$", RegexOptions.CultureInvariant);

    public string Id => "unguarded-var";

    /// <summary>
    /// Höchstens ein Hinweis je oberstem Block (<see cref="BlockDedupe"/>); das Detail nennt die Einträge des error.log im ganzen Block und,
    /// wenn die Stelle aus dem Dump kommt, die Gesamtzeit ihrer Zeile.
    /// </summary>
    public IEnumerable<PerformanceHint> Check(RuleContext context)
    {
        var hints = new List<(ScriptBlockInfo Block, PerformanceHint Hint)>();
        foreach (var place in Candidates(context))
        {
            if (context.BlockAt(place.Path, place.Line) is not { } block)
            {
                continue;
            }

            var names = UnguardedNames(block.BlockText);
            if (names.Count == 0)
            {
                continue;
            }

            var detail = PerformanceTexts.HintUnguardedDetail(string.Join(", ", names), ErrorsInBlock(context, place.Path, block));
            if (place.RowTotal is { } total)
            {
                detail += " " + PerformanceTexts.HintUnguardedRowTotal(total);
            }

            hints.Add((block, new PerformanceHint(Id, HintSeverity.Warning, place.Path, place.Line, PerformanceTexts.HintUnguardedTitle, detail, place.Value)));
        }

        return BlockDedupe.Keep(hints);
    }

    /// <summary>Einträge des error.log im ganzen Block; <c>null</c> ohne error.log oder wenn der Block keine hat.</summary>
    private static int? ErrorsInBlock(RuleContext context, string path, ScriptBlockInfo block)
    {
        if (context.Errors is not { } errors)
        {
            return null;
        }

        var count = 0;
        for (var line = block.StartLine; line <= block.EndLine; line++)
        {
            count += errors.CountAt(path, line);
        }

        return count == 0 ? null : count;
    }

    /// <summary>Die Namen der Variablen, die ohne vorherigen Guard gelesen werden, ordinal sortiert und ohne Doppelte.</summary>
    private static IReadOnlyList<string> UnguardedNames(string blockText)
    {
        var tokens = ScopeLexer.Tokenize(blockText);
        var guarded = new HashSet<string>(StringComparer.Ordinal);
        var unguarded = new SortedSet<string>(StringComparer.Ordinal);
        var keys = new List<string>();

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            switch (token.Kind)
            {
                case ScopeTokenKind.Open:
                    // Jeder Block kommt auf den Stapel, auch Listen- und anonyme Blöcke (leerer Schlüssel), damit Close immer passt.
                    keys.Add(IsWord(tokens, i - 2) && IsOperator(tokens, i - 1, "=") ? tokens[i - 2].Text : string.Empty);
                    continue;
                case ScopeTokenKind.Close:
                    if (keys.Count > 0)
                    {
                        keys.RemoveAt(keys.Count - 1);
                    }

                    continue;
                case not ScopeTokenKind.Word:
                    continue;
            }

            var negated = keys.Exists(k => k.Equals("NOT", StringComparison.OrdinalIgnoreCase) || k.Equals("NOR", StringComparison.OrdinalIgnoreCase));
            if (token.Text == "has_variable" && IsOperator(tokens, i + 1, "=") && IsWord(tokens, i + 2))
            {
                if (!negated)
                {
                    guarded.Add(tokens[i + 2].Text);
                }

                i += 2;
                continue;
            }

            if (VariableName(token.Text) is not { } name)
            {
                continue;
            }

            // Der Lexer trennt '?' und '!' nicht ab: "var:X?=" kommt als Wort "var:X?" und Operator "=". Das ist ?= bzw. != wie mit Leerzeichen.
            var glued = token.Text[^1] is '?' or '!' && IsOperator(tokens, i + 1, "=");

            // exists = var:NAME und var:NAME ?= lesen sicher; sie sind selbst kein ungeschützter Zugriff und schützen alles danach.
            var safeRead = IsOperator(tokens, i + 1, "?=")
                || (glued && token.Text[^1] == '?')
                || (IsOperator(tokens, i - 1, "=") && IsWord(tokens, i - 2) && tokens[i - 2].Text == "exists");
            if (safeRead)
            {
                if (!negated)
                {
                    guarded.Add(name);
                }
            }
            else if (!guarded.Contains(name))
            {
                unguarded.Add(name);
            }
        }

        return unguarded.ToList();
    }

    /// <summary>Name aus <c>var:NAME</c> oder <c>var:NAME.feld</c>; <c>null</c> für andere Wörter (auch <c>global_var:</c> und <c>local_var:</c>).</summary>
    private static string? VariableName(string word)
    {
        if (!word.StartsWith(VarPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var name = word[VarPrefix.Length..].TrimEnd('?', '!');
        var dot = name.IndexOf('.');
        if (dot >= 0)
        {
            name = name[..dot];
        }

        return name.Length == 0 ? null : name;
    }

    private static bool IsWord(List<ScopeToken> tokens, int index) =>
        index >= 0 && index < tokens.Count && tokens[index].Kind == ScopeTokenKind.Word;

    private static bool IsOperator(List<ScopeToken> tokens, int index, string text) =>
        index >= 0 && index < tokens.Count && tokens[index].Kind == ScopeTokenKind.Operator && tokens[index].Text == text;

    /// <summary>
    /// Script Values und Trigger des Dumps, dazu die Stellen des error.log mit einer Variablen-Meldung; je Stelle (Pfad ohne Groß-/Kleinschreibung) einmal.
    /// Sortiermaß ist die höchste Gesamtzeit der Zeilen an der Stelle, ohne Zeile die Anzahl der Fehler. <c>RowTotal</c> ist diese Gesamtzeit
    /// bei einer Stelle aus dem Dump (auch wenn das error.log sie ebenfalls nennt), sonst <c>null</c>.
    /// </summary>
    private static IEnumerable<(string Path, int Line, double Value, double? RowTotal)> Candidates(RuleContext context)
    {
        var places = new Dictionary<(string Path, int Line), (string Path, int Line, double Value, double? RowTotal)>();
        foreach (var row in context.Rows.Select(r => r.Row).Where(r => IsCandidateKind(r.Kind)))
        {
            var key = (row.Path.ToUpperInvariant(), row.Line);
            places[key] = places.TryGetValue(key, out var known)
                ? (known.Path, known.Line, Math.Max(known.Value, row.Total), Math.Max(known.Value, row.Total))
                : (row.Path, row.Line, row.Total, row.Total);
        }

        if (context.Errors is { } errors)
        {
            foreach (var place in context.ErrorPlaces)
            {
                var message = WithoutLocation(errors.MostCommonMessageAt(place.Path, place.Line) ?? string.Empty, place.Path);
                var mentionsVariable = message.Contains(VarPrefix, StringComparison.OrdinalIgnoreCase)
                    || message.Contains("variable", StringComparison.OrdinalIgnoreCase);
                if (mentionsVariable)
                {
                    places.TryAdd((place.Path.ToUpperInvariant(), place.Line), (place.Path, place.Line, place.Count, null));
                }
            }
        }

        return places.Values;
    }

    /// <summary>Die Meldung ohne die Fundstelle: ohne ein abschließendes <c>at Pfad:Zeile</c> und ohne den Pfad der Stelle, damit etwa <c>x_variables.txt</c> nicht als Variablen-Meldung zählt.</summary>
    private static string WithoutLocation(string message, string path)
    {
        var text = TrailingLocation.Replace(message, string.Empty);
        return path.Length == 0 ? text : text.Replace(path, string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    // "named_script_value" beginnt nicht mit "script_value", darum alle drei Präfixe.
    private static bool IsCandidateKind(string kind) =>
        kind.StartsWith("script_value", StringComparison.Ordinal)
        || kind.StartsWith("named_script_value", StringComparison.Ordinal)
        || kind.StartsWith("trigger", StringComparison.Ordinal);
}
