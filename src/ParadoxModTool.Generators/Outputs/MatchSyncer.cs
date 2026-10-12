using System.Text;
using System.Text.RegularExpressions;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Generators;

/// <summary>Ergebnis des Abgleichs nach Treffer: neuer Text, ersetzte Läufe, ersatzlos entfernte Läufe (je erste Zeile) und ob angehängt wurde.</summary>
public sealed record MatchSyncResult(string Text, int Replaced, IReadOnlyList<string> Removed, bool Appended);

/// <summary>
/// Ausgabe "Abgleich nach Treffer" (Spec 2026-10-06-generator-packages-idea-groups, Abschnitt 1): die Kinder eines Blocks (oder der Datei) sind Einheiten,
/// also Anweisungen, GUI-Kopfzeilen mit Rumpf, Listenwerte, unbenannte Blöcke und eigene Kommentarzeilen. Einheiten, in deren Text der Regex trifft,
/// ersetzt der gerenderte Text an Ort und Stelle; ohne Treffer kommt er ans Blockende bzw. Dateiende. Alles andere bleibt zeichengleich.
/// Mit <c>ifLimit</c> (Spec 2026-10-10-generator-packages-iv2-dynamic-ideas, "Conditional ideas in the setup effect") ist ein Kind
/// <c>if = { limit = { … } … }</c> keine Einheit: seine übrigen Kinder bilden die Gruppe seiner Bedingung, alle anderen Kinder die oberste Ebene.
/// </summary>
public static class MatchSyncer
{
    /// <summary>Der abgeglichene Bereich: Block (oder <see langword="null"/> für die Datei), seine Grenzen und seine Einträge.</summary>
    private sealed record Region(BlockSpan? Block, int Start, int End, IReadOnlyList<ScriptEntry> Entries);

    /// <param name="blockPath">Blockpfad wie bei "Abschnitt einfügen"; leer = oberste Ebene der Datei.</param>
    /// <param name="rendered">Gerenderter Text (normalisiert, mit der Einrückung der Vorlage).</param>
    /// <param name="insertAfter">Nur für neue Einheiten: Der Text kommt hinter die letzte Einheit, auf die der Regex trifft (im Block bzw. in der Datei); ohne solche Einheit ans Ende wie sonst.</param>
    /// <param name="ifLimit">
    /// <see langword="null"/> = ohne Gruppen; leer = Einheit auf oberster Ebene; sonst Einheit im <c>if</c>, dessen <c>limit</c> diese Bedingung ist
    /// (verglichen ohne Kommentare und Leerraumunterschiede); mit <paramref name="insertAfter"/> nur ein <c>if</c> derselben Kategorie, d. h. mit einer Einheit,
    /// auf die <paramref name="insertAfter"/> trifft. Steht die Einheit in einer anderen Gruppe, zieht sie um; ein <c>if</c>, das dabei nur
    /// sein <c>limit</c> behält, fällt weg, ein fehlendes entsteht hinter dem Anker bzw. vor dem <c>}</c> des Bereichs.
    /// </param>
    public static MatchSyncResult Apply(string existing, string? blockPath, Regex match, string rendered, string newLine, Regex? insertAfter = null, string? ifLimit = null)
    {
        var region = Read(existing, blockPath);
        if (ifLimit is not null)
        {
            return ApplyGrouped(existing, blockPath, region, match, rendered, newLine, insertAfter, ifLimit);
        }

        var units = Units(existing, region.Entries, region.Start, region.End);
        var hits = units.Where(u => match.IsMatch(existing[u.Start..u.End])).ToList();
        if (hits.Count == 0)
        {
            return rendered.Length == 0
                ? new MatchSyncResult(existing, 0, Array.Empty<string>(), false)
                : new MatchSyncResult(InsertNew(existing, region, units, rendered, newLine, insertAfter), 0, Array.Empty<string>(), true);
        }

        var result = LineRuns.Apply(existing, hits, rendered, newLine);
        return new MatchSyncResult(result.Text, result.Replaced, result.Removed, false);
    }

    /// <summary>
    /// Abgleich mit Gruppen nach Bedingung: Stehen alle Treffer in der Zielgruppe, ersetzt der Text sie an Ort und Stelle. Sonst fallen alle Treffer
    /// weg, ebenso jedes <c>if</c> mit Treffer, das nur sein <c>limit</c> behält, und der Text kommt in die Zielgruppe. Ein Umzug zählt als ersetzt.
    /// </summary>
    private static MatchSyncResult ApplyGrouped(string existing, string? blockPath, Region region, Regex match, string rendered, string newLine, Regex? insertAfter, string ifLimit)
    {
        var target = LimitGroups.Normalize(ifLimit);
        var (top, groups) = Grouped(existing, region);
        // Gruppe -1 = oberste Ebene, sonst Index des if in Quellreihenfolge (bleibt beim Entfernen von Einheiten gleich)
        var hits = top.Where(u => match.IsMatch(existing[u.Start..u.End])).Select(u => (Unit: u, Group: -1))
            .Concat(groups.SelectMany((g, i) => g.Units.Where(u => match.IsMatch(existing[u.Start..u.End])).Select(u => (Unit: u, Group: i))))
            .ToList();
        if (hits.Count == 0 && rendered.Length == 0)
        {
            return new MatchSyncResult(existing, 0, Array.Empty<string>(), false);
        }

        var inPlace = rendered.Length > 0 && hits.Count > 0 && hits.All(h => (h.Group < 0 ? string.Empty : groups[h.Group].Limit) == target);
        var runs = hits.Count > 0 ? LineRuns.Apply(existing, hits.Select(h => h.Unit), inPlace ? rendered : string.Empty, newLine) : null;
        // Das if, in das der Text gleich kommt, bleibt stehen, auch wenn es kurz leer ist; Treffer zählen nicht, sie fallen gleich weg
        var hitStarts = hits.Select(h => h.Unit.Start).ToHashSet();
        var receiver = rendered.Length > 0 && !inPlace && target.Length > 0
            ? groups.FindIndex(g => Receives(existing, g, target, insertAfter, g.Units.Where(u => !hitStarts.Contains(u.Start)).ToList()))
            : -1;
        var text = RemoveEmptyIfs(runs?.Text ?? existing, blockPath, hits.Select(h => h.Group).Where(i => i >= 0 && i != receiver).ToHashSet());
        if (runs is not null && (rendered.Length == 0 || inPlace))
        {
            return new MatchSyncResult(text, runs.Replaced, runs.Removed, false);
        }

        text = InsertGrouped(text, blockPath, rendered, newLine, insertAfter, target, ifLimit, ChildIndent(existing, region));
        return new MatchSyncResult(text, hits.Count > 0 ? 1 : 0, Array.Empty<string>(), hits.Count == 0);
    }

    /// <summary>Einheiten der obersten Ebene (ohne die if/limit-Kinder) und die if/limit-Gruppen eines Bereichs.</summary>
    private static (List<(int Start, int End)> Top, List<LimitGroup> Groups) Grouped(string text, Region region)
    {
        var groups = LimitGroups.Find(text, region.Entries);
        var starts = groups.Select(g => g.Start).ToHashSet();
        return (Units(text, region.Entries, region.Start, region.End, e => e.SourceStart is { } start && starts.Contains(start)), groups);
    }

    /// <summary>Die if/limit-Kinder mit den Indizes <paramref name="candidates"/> entfernen, wenn sie keine Einheit mehr haben.</summary>
    private static string RemoveEmptyIfs(string text, string? blockPath, IReadOnlySet<int> candidates)
    {
        if (candidates.Count == 0)
        {
            return text;
        }

        var groups = LimitGroups.Find(text, Read(text, blockPath).Entries);
        foreach (var group in candidates.Where(i => i < groups.Count && groups[i].Units.Count == 0).Select(i => groups[i]).OrderByDescending(g => g.Start))
        {
            text = RemoveStatement(text, group.Start, group.End);
        }

        return text;
    }

    /// <summary>
    /// Eine Anweisung entfernen: auf eigenen Zeilen ganze Zeilen, sonst nur ihre Spanne. Steht darüber eine Leerzeile und darunter eine Leerzeile,
    /// ein <c>}</c> oder das Textende, fällt die Leerzeile darüber mit weg (keine doppelte Leerzeile, keine Leerzeile vor <c>}</c>).
    /// </summary>
    private static string RemoveStatement(string text, int start, int end)
    {
        var lineStart = BlockText.LineStart(text, start);
        var lineEnd = BlockText.LineEnd(text, end);
        var rest = text[end..lineEnd].Trim();
        if (text[lineStart..start].Trim().Length > 0 || (rest.Length > 0 && !rest.StartsWith('#')))
        {
            return text[..start] + text[end..];
        }

        if (lineStart > 0)
        {
            var above = BlockText.LineStart(text, lineStart - 1);
            var below = lineEnd < text.Length ? text[lineEnd..BlockText.LineEnd(text, lineEnd)].Trim() : string.Empty;
            if (text[above..lineStart].Trim().Length == 0 && (below.Length == 0 || below.StartsWith('}')))
            {
                lineStart = above;
            }
        }

        return text[..lineStart] + text[lineEnd..];
    }

    /// <summary>
    /// Text in die Zielgruppe setzen: oberste Ebene wie ohne Gruppen (Anker nur unter Einheiten der obersten Ebene), vorhandenes if derselben Kategorie vor sein <c>}</c>,
    /// sonst ein neues if hinter den Anker (Anker in einem if = hinter das ganze if) bzw. vor das <c>}</c> des Bereichs.
    /// </summary>
    private static string InsertGrouped(string text, string? blockPath, string rendered, string newLine, Regex? insertAfter, string target, string ifLimit, string indent)
    {
        var region = Read(text, blockPath);
        var (top, groups) = Grouped(text, region);
        if (target.Length == 0)
        {
            return InsertNew(text, region, top, rendered, newLine, insertAfter);
        }

        var lines = rendered.EndsWith('\n') ? rendered : rendered + newLine;
        if (groups.FirstOrDefault(g => Receives(text, g, target, insertAfter, g.Units)) is { } group)
        {
            return BlockText.InsertBeforeClose(text, group.Span, lines, newLine);
        }

        var anchors = top.Select(u => (u.Start, u.End, After: u.End)).Concat(groups.SelectMany(g => g.Units.Select(u => (u.Start, u.End, After: g.End))));
        var anchor = insertAfter is null ? -1 : anchors.Where(a => insertAfter.IsMatch(text[a.Start..a.End])).Select(a => BlockText.LineEnd(text, a.After)).DefaultIfEmpty(-1).Max();
        if (anchor > region.End)
        {
            anchor = -1;
        }

        var step = indent.Contains('\t') ? "\t" : "    ";
        var block = new StringBuilder();
        block.Append(indent).Append("if = {").Append(newLine);
        block.Append(indent).Append(step).Append("limit = {").Append(newLine);
        foreach (var line in ifLimit.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))
        {
            block.Append(indent).Append(step).Append(step).Append(line).Append(newLine);
        }

        block.Append(indent).Append(step).Append('}').Append(newLine);
        block.Append(lines);
        block.Append(indent).Append('}').Append(newLine);
        return InsertAt(text, region, anchor, block.ToString(), newLine);
    }

    /// <summary>
    /// Ein if nimmt den Text der Zielgruppe auf, wenn sein <c>limit</c> die Bedingung ist und es zur selben Kategorie gehört: mit <paramref name="insertAfter"/>
    /// trifft der Regex auf eine seiner Einheiten <paramref name="units"/> (vor dem Entfernen der Treffer: die Einheiten, die bleiben).
    /// </summary>
    private static bool Receives(string text, LimitGroup group, string target, Regex? insertAfter, IReadOnlyList<(int Start, int End)> units) =>
        group.Limit == target && (insertAfter is null || units.Any(u => insertAfter.IsMatch(text[u.Start..u.End])));

    /// <summary>Einrückung der ersten Kindzeile eines Bereichs; ohne Kindzeile ein Tab.</summary>
    private static string ChildIndent(string text, Region region)
    {
        foreach (var (lineStart, lineEnd, trimmed) in BlockText.Lines(text, region.Start, region.End))
        {
            if (lineStart >= region.Start && trimmed.Length > 0)
            {
                return BlockText.LeadingWhitespace(text[lineStart..lineEnd]);
            }
        }

        return "\t";
    }

    /// <summary>
    /// Einheiten unter den Einträgen eines Bereichs als Spannen, dazu eigene Kommentarzeilen außerhalb der Einheiten. Einträge, für die
    /// <paramref name="skip"/> gilt, sind keine Einheiten; Kommentarzeilen in ihnen auch nicht.
    /// </summary>
    internal static List<(int Start, int End)> Units(string text, IReadOnlyList<ScriptEntry> entries, int regionStart, int regionEnd, Func<ScriptEntry, bool>? skip = null)
    {
        var units = new List<(int Start, int End)>();
        var skipped = new List<(int Start, int End)>();
        foreach (var unit in EntryUnits.Build(entries))
        {
            if ((unit.Header.FirstOrDefault()?.SourceStart ?? unit.Entry.SourceStart) is { } start && unit.Entry.SourceEnd is { } end)
            {
                (skip?.Invoke(unit.Entry) == true ? skipped : units).Add((start, end));
            }
        }

        // Kommentarzeilen auf eigener Zeile außerhalb der Einheiten sind eigene Einheiten
        var covered = units.Concat(skipped).ToList();
        foreach (var (lineStart, lineEnd, trimmed) in BlockText.Lines(text, regionStart, regionEnd))
        {
            if (lineStart >= regionStart && trimmed.StartsWith('#') && !covered.Any(u => u.Start <= lineStart && lineStart < u.End))
            {
                var start = lineStart + (text[lineStart..lineEnd].Length - text[lineStart..lineEnd].TrimStart(' ', '\t').Length);
                units.Add((start, start + trimmed.Length));
            }
        }

        return units;
    }

    private static Region Read(string text, string? blockPath)
    {
        if (!string.IsNullOrWhiteSpace(blockPath))
        {
            var (span, entries) = BlockText.FindWithEntries(text, blockPath.Trim());
            return new Region(span, span.Open + 1, span.Close, entries);
        }

        try
        {
            return new Region(null, 0, text.Length, ScriptDocument.Parse(text).Entries);
        }
        catch (Exception ex) when (ex is not GeneratorOutputException)
        {
            throw new GeneratorOutputException(GeneratorsTexts.ParseFailed(ex.Message));
        }
    }

    /// <summary>Neuen Text hinter die letzte Einheit setzen, auf die <paramref name="insertAfter"/> trifft; sonst vor das <c>}</c> des Blocks bzw. ans Dateiende.</summary>
    private static string InsertNew(string text, Region region, IEnumerable<(int Start, int End)> anchors, string rendered, string newLine, Regex? insertAfter)
    {
        var lines = rendered.EndsWith('\n') ? rendered : rendered + newLine;
        // Hinter der letzten Einheit ihrer Kategorie einfügen; schließt der Block in derselben Zeile, gilt der Weg ans Ende
        var hits = insertAfter is null ? new List<(int Start, int End)>() : anchors.Where(u => insertAfter.IsMatch(text[u.Start..u.End])).ToList();
        (int Start, int End)? anchorUnit = hits.Count == 0 ? null : hits.MaxBy(u => BlockText.LineEnd(text, u.End));
        var anchor = anchorUnit is { } a ? BlockText.LineEnd(text, a.End) : -1;
        if (anchor > region.End)
        {
            anchor = -1;
        }

        // Hinter einem Anker kommt der Abstand mit, der zwischen dem Anker und der Einheit davor steht (so viele Leerzeilen wie dort)
        var gap = anchor >= 0 ? GapAbove(text, region, anchorUnit!.Value.Start) : 0;
        // Ein neuer Abschnitt mit Kommentarkopf bekommt eine Leerzeile davor, sonst zählten seine Zierzeilen beim nächsten Abgleich zum Abschnitt davor
        var before = anchor >= 0 ? text[..anchor] : region.Block is { } span ? text[..BlockText.LineStart(text, span.Close)] : text;
        if (gap > 0)
        {
            lines = string.Concat(Enumerable.Repeat(newLine, gap)) + lines;
        }
        else if (lines.TrimStart().StartsWith('#') && LastLine(before).Trim().Length > 0)
        {
            lines = newLine + lines;
        }

        return InsertAt(text, region, anchor, lines, newLine);
    }

    /// <summary>
    /// Leerzeilen direkt über der Zeile von <paramref name="unitStart"/> bis zur vorigen nicht leeren Zeile im Bereich; steht davor keine Zeile des Bereichs
    /// (erste Einheit), ist der Abstand 0.
    /// </summary>
    private static int GapAbove(string text, Region region, int unitStart)
    {
        var count = 0;
        var line = BlockText.LineStart(text, unitStart);
        while (line > 0)
        {
            var previous = BlockText.LineStart(text, line - 1);
            if (previous < region.Start && region.Block is not null)
            {
                return 0;
            }

            if (text[previous..line].Trim().Length > 0)
            {
                return count;
            }

            count++;
            line = previous;
        }

        return 0;
    }

    /// <summary>Ganze Zeilen an <paramref name="anchor"/> einfügen (fehlt davor das Zeilenende, kommt es dazu); ohne Anker vor das <c>}</c> des Blocks bzw. ans Dateiende.</summary>
    private static string InsertAt(string text, Region region, int anchor, string lines, string newLine)
    {
        if (anchor >= 0)
        {
            var gap = text[anchor - 1] != '\n' ? newLine : string.Empty;
            return text[..anchor] + gap + lines + text[anchor..];
        }

        return region.Block is { } target
            ? BlockText.InsertBeforeClose(text, target, lines, newLine)
            : text + (text.Length > 0 && !text.EndsWith('\n') ? newLine : string.Empty) + lines;
    }

    /// <summary>Letzte Zeile eines Texts ohne Zeilenende; endet der Text mit einem Zeilenende, die Zeile davor (leer bei einer Leerzeile).</summary>
    private static string LastLine(string text)
    {
        var line = (text.EndsWith('\n') ? text[..^1] : text).TrimEnd('\r');
        return line[(line.LastIndexOf('\n') + 1)..];
    }
}
