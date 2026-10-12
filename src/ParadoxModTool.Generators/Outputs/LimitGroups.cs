using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Generators;

/// <summary>
/// Ein Kind <c>if = { limit = { … } … }</c> eines Bereichs: Spanne der Anweisung, Lage seiner Klammern, die normalisierte Bedingung
/// und seine inneren Einheiten (alle Kinder außer <c>limit</c>, dazu eigene Kommentarzeilen außerhalb von <c>limit</c>).
/// </summary>
internal sealed record LimitGroup(int Start, int End, BlockSpan Span, string Limit, List<(int Start, int End)> Units);

/// <summary>
/// Gruppen nach Bedingung für "Abgleich nach Treffer" mit <c>ifLimit</c> (Spec 2026-10-10-generator-packages-iv2-dynamic-ideas, "Conditional ideas
/// in the setup effect"): Ideen mit Bedingung stehen in gemeinsamen <c>if</c>-Blöcken, deren <c>limit</c> die Bedingung ist.
/// </summary>
internal static class LimitGroups
{
    /// <summary>
    /// Bedingung vergleichbar machen wie "Nur Format" der Paketprüfung (<see cref="GeneratorCheck.Tokens"/>): ohne Kommentare und Leerraum, Token mit einem
    /// Leerzeichen verbunden; so sind <c>{value=x}</c> und <c>{ value = x }</c> gleich, ein <c>#</c> in Anführungszeichen bleibt Text.
    /// </summary>
    internal static string Normalize(string condition) => string.Join(" ", GeneratorCheck.Tokens(condition));

    /// <summary>Die Kinder <c>if</c> mit einem Kind <c>limit</c> (Block) unter den Einträgen eines Bereichs, in Quellreihenfolge.</summary>
    public static List<LimitGroup> Find(string text, IReadOnlyList<ScriptEntry> entries)
    {
        var groups = new List<LimitGroup>();
        foreach (var statement in entries.OfType<ScriptStatement>())
        {
            if (statement.Key != "if" || statement.Value is not ScriptBlock block || statement.SourceStart is not { } start || statement.SourceEnd is not { } end
                || block.Entries.OfType<ScriptStatement>().FirstOrDefault(e => e.Key == "limit" && e.Value is ScriptBlock) is not { } limit
                || limit.SourceStart is not { } limitStart || limit.SourceEnd is not { } limitEnd)
            {
                continue;
            }

            var open = text.IndexOf('{', start);
            var close = text.LastIndexOf('}', end - 1);
            var limitOpen = text.IndexOf('{', limitStart);
            var limitClose = text.LastIndexOf('}', limitEnd - 1);
            if (open < 0 || close < open || limitOpen < 0 || limitClose < limitOpen)
            {
                continue;
            }

            var units = MatchSyncer.Units(text, block.Entries, open + 1, close, e => ReferenceEquals(e, limit));
            groups.Add(new LimitGroup(start, end, new BlockSpan(start, open, close), Normalize(text[(limitOpen + 1)..limitClose]), units));
        }

        return groups;
    }
}
