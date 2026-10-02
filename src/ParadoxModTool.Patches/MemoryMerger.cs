using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Patches;

public enum UnitOutcomeKind
{
    Inserted,
    Replaced,
    Unchanged,
    Merged,
    Conflict,
    DeletedByHand,
    Adopted,
    Removed,
    KeptEditedRemoved,
}

/// <summary>Ergebnis für einen Baustein; <see cref="Text"/> null = der Baustein steht danach nicht in der Datei.</summary>
public sealed record UnitOutcome(UnitOutcomeKind Kind, string? Text, IReadOnlyList<string> Conflicts);

/// <summary>
/// Entscheidet je Baustein zwischen neu Erzeugtem (<c>fresh</c>), Dateistand (<c>mine</c>) und Gedächtnis (<c>remembered</c>):
/// unberührte Bausteine werden ersetzt, von Hand geänderte per 3-Wege-Merge mit dem Gedächtnis als Basis zusammengeführt.
/// </summary>
public static class MemoryMerger
{
    private static readonly IReadOnlyList<string> None = Array.Empty<string>();

    /// <param name="fresh">null, wenn es die Tabellenzeile nicht mehr gibt.</param>
    public static UnitOutcome Decide(string unitKey, string? fresh, string? mine, string? remembered, bool isLocalization)
    {
        if (fresh is null)
        {
            if (mine is null)
            {
                return new UnitOutcome(UnitOutcomeKind.Removed, null, None);
            }

            if (remembered is null)
            {
                return new UnitOutcome(UnitOutcomeKind.Unchanged, mine, None);
            }

            return Same(mine, remembered)
                ? new UnitOutcome(UnitOutcomeKind.Removed, null, None)
                : new UnitOutcome(UnitOutcomeKind.KeptEditedRemoved, mine, None);
        }

        if (mine is null)
        {
            return remembered is null
                ? new UnitOutcome(UnitOutcomeKind.Inserted, fresh, None)
                : new UnitOutcome(UnitOutcomeKind.DeletedByHand, null, None);
        }

        if (remembered is null)
        {
            return new UnitOutcome(UnitOutcomeKind.Adopted, mine, None);
        }

        if (Same(mine, remembered))
        {
            return Same(fresh, mine)
                ? new UnitOutcome(UnitOutcomeKind.Unchanged, mine, None)
                : new UnitOutcome(UnitOutcomeKind.Replaced, fresh, None);
        }

        if (Same(fresh, remembered) || Same(fresh, mine))
        {
            return new UnitOutcome(UnitOutcomeKind.Unchanged, mine, None);
        }

        return isLocalization ? MergeLocalization(unitKey, fresh, mine, remembered) : MergeScript(unitKey, fresh, mine, remembered);
    }

    /// <summary>Gleich bis auf Zeilenenden, Leerraum am Zeilenende und Leerzeilen am Ende.</summary>
    public static bool Same(string a, string b) => Normalize(a) == Normalize(b);

    private static string Normalize(string text) =>
        string.Join("\n", text.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd())).TrimEnd('\n');

    private static UnitOutcome MergeScript(string unitKey, string fresh, string mine, string remembered)
    {
        ScriptBlock baseRoot, freshRoot, mineRoot;
        try
        {
            baseRoot = ScriptDocument.Parse(remembered);
            freshRoot = ScriptDocument.Parse(fresh);
            mineRoot = ScriptDocument.Parse(mine);
        }
        catch (ScriptParseException)
        {
            return new UnitOutcome(UnitOutcomeKind.Conflict, mine, new[] { PatchTexts.UnitUnreadable(unitKey) });
        }

        var result = MergeEngine.Merge(baseRoot, freshRoot, mineRoot);
        var options = mine.Contains("\n\t", StringComparison.Ordinal) ? new ScriptWriteOptions { UseTabs = true, IndentSize = 1 } : null;
        var text = ScriptWriter.Write(result.MergedRoot, preferSourceVerbatim: true, options).TrimEnd('\r', '\n');
        return result.HasConflicts
            ? new UnitOutcome(UnitOutcomeKind.Conflict, text, result.Conflicts.Select(c => PatchTexts.UnitConflict(unitKey, c.Description)).ToList())
            : new UnitOutcome(UnitOutcomeKind.Merged, text, None);
    }

    private static UnitOutcome MergeLocalization(string unitKey, string fresh, string mine, string remembered)
    {
        var baseLines = LocLines(remembered).ToDictionary(l => l.Key, l => l.Line, StringComparer.Ordinal);
        var mineLines = LocLines(mine).ToDictionary(l => l.Key, l => l.Line, StringComparer.Ordinal);
        var freshLines = LocLines(fresh);
        var freshKeys = freshLines.Select(l => l.Key).ToHashSet(StringComparer.Ordinal);
        var result = new List<string>();
        var conflicts = new List<string>();

        foreach (var (key, freshLine) in freshLines)
        {
            var hasMine = mineLines.TryGetValue(key, out var mineLine);
            var hasBase = baseLines.TryGetValue(key, out var baseLine);
            if (!hasMine)
            {
                if (!hasBase)
                {
                    result.Add(freshLine);
                }
                else if (!Same(freshLine, baseLine!))
                {
                    conflicts.Add(PatchTexts.LocKeyConflict(key));
                    result.Add(freshLine);
                }

                continue;
            }

            if (hasBase && Same(mineLine!, baseLine!))
            {
                result.Add(freshLine);
            }
            else if ((hasBase && Same(freshLine, baseLine!)) || Same(mineLine!, freshLine))
            {
                result.Add(mineLine!);
            }
            else
            {
                conflicts.Add(PatchTexts.LocKeyConflict(key));
                result.Add(freshLine);
            }
        }

        foreach (var (key, mineLine) in LocLines(mine).Where(l => !freshKeys.Contains(l.Key)))
        {
            if (!baseLines.TryGetValue(key, out var baseLine) || !Same(mineLine, baseLine))
            {
                result.Add(mineLine);
            }
        }

        var text = string.Join("\n", result);
        return conflicts.Count > 0
            ? new UnitOutcome(UnitOutcomeKind.Conflict, text, conflicts)
            : new UnitOutcome(UnitOutcomeKind.Merged, text, None);
    }

    private static List<(string Key, string Line)> LocLines(string text) =>
        text.Replace("\r\n", "\n").Split('\n')
            .Where(l => l.Trim().Length > 0)
            .Select(l => (Key: l.Contains(':') ? l[..l.IndexOf(':')].Trim() : l.Trim(), Line: l))
            .GroupBy(l => l.Key, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();
}
