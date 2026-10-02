using System.Text;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Patches;

/// <summary>Ein Baustein im erzeugten Bereich; <see cref="Text"/> ohne Markenzeile, ohne Einrückung des Bereichs, Zeilen mit <c>\n</c>.</summary>
public sealed record RegionUnit(string UnitKey, string Text);

public sealed class RegionReadResult
{
    public bool Found { get; init; }

    public bool Broken { get; init; }

    public string? Problem { get; init; }

    public IReadOnlyList<RegionUnit> Units { get; init; } = Array.Empty<RegionUnit>();

    internal int Start { get; init; }

    internal int End { get; init; }

    internal string Indent { get; init; } = string.Empty;
}

/// <summary>Der Bereich passt nicht in die Datei (z. B. fehlender Block für <c>inside</c>).</summary>
public sealed class PatchRegionException : Exception
{
    public PatchRegionException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Liest und schreibt den erzeugten Bereich einer Regel zwischen <c># PMT-Patch &lt;rezept&gt;/&lt;regel&gt; begin</c> und <c>… end</c>;
/// jeder Baustein beginnt mit <c># PMT-Unit &lt;schlüssel&gt;</c>. Bausteine werden mit der Einrückung der Marken geschrieben.
/// </summary>
public static class GeneratedRegion
{
    public const string UnitPrefix = "# PMT-Unit ";

    public static string BeginMarker(string recipeId, string ruleId) => $"# PMT-Patch {recipeId}/{ruleId} begin";

    public static string EndMarker(string recipeId, string ruleId) => $"# PMT-Patch {recipeId}/{ruleId} end";

    /// <summary>Ersetzt den Inhalt aller erzeugten Bereiche (jedes Rezepts) durch Leerzeichen; Zeilenumbrüche und damit alle Positionen bleiben.</summary>
    public static string MaskAll(string text)
    {
        var masked = new System.Text.StringBuilder(text.Length);
        var inside = false;
        var lineStart = 0;
        while (lineStart < text.Length)
        {
            var newline = text.IndexOf('\n', lineStart);
            var lineEnd = newline < 0 ? text.Length : newline;
            var trimmed = text.AsSpan(lineStart, lineEnd - lineStart).Trim();
            var marker = trimmed.StartsWith("# PMT-Patch ", StringComparison.Ordinal);
            if (marker && trimmed.EndsWith(" begin", StringComparison.Ordinal))
            {
                inside = true;
            }

            for (var i = lineStart; i < lineEnd; i++)
            {
                masked.Append(inside && text[i] != '\r' ? ' ' : text[i]);
            }

            if (marker && trimmed.EndsWith(" end", StringComparison.Ordinal))
            {
                inside = false;
            }

            if (newline >= 0)
            {
                masked.Append('\n');
            }

            lineStart = newline < 0 ? text.Length : newline + 1;
        }

        return masked.ToString();
    }

    public static RegionReadResult Read(string text, string recipeId, string ruleId)
    {
        var begin = BeginMarker(recipeId, ruleId);
        var end = EndMarker(recipeId, ruleId);
        var lines = Lines(text);
        var begins = lines.Where(l => l.Body.Trim() == begin).ToList();
        var ends = lines.Where(l => l.Body.Trim() == end).ToList();
        if (begins.Count == 0 && ends.Count == 0)
        {
            return new RegionReadResult();
        }

        if (begins.Count != 1 || ends.Count != 1 || ends[0].Start < begins[0].Start)
        {
            return Broken(text, recipeId, ruleId);
        }

        var indent = ScriptSpans.Indent(begins[0].Body);
        var units = new List<RegionUnit>();
        string? key = null;
        var body = new List<string>();
        foreach (var line in lines.Where(l => l.Start > begins[0].Start && l.Start < ends[0].Start))
        {
            var trimmed = line.Body.Trim();
            if (trimmed.StartsWith(UnitPrefix, StringComparison.Ordinal))
            {
                Flush(key, body, units);
                key = trimmed[UnitPrefix.Length..].Trim();
                if (units.Any(u => u.UnitKey == key))
                {
                    // doppelte Marke (z. B. kopierter Baustein): nichts schreiben, sonst ginge die Kopie verloren
                    return Broken(text, recipeId, ruleId);
                }

                body.Clear();
            }
            else if (key is null)
            {
                if (trimmed.Length > 0)
                {
                    return Broken(text, recipeId, ruleId);
                }
            }
            else
            {
                body.Add(line.Body.StartsWith(indent, StringComparison.Ordinal) ? line.Body[indent.Length..] : line.Body.TrimStart(' ', '\t'));
            }
        }

        Flush(key, body, units);
        return new RegionReadResult { Found = true, Units = units, Start = begins[0].Start, End = ends[0].End, Indent = indent };
    }

    public static string Write(string text, string recipeId, string ruleId, string? inside, IReadOnlyList<RegionUnit> units, string newLine)
    {
        // Eine Markenzeile im Baustein-Text würde den Baustein beim nächsten Lesen teilen oder den Bereich beschädigen.
        if (units.FirstOrDefault(u => u.Text.Split('\n').Any(l => l.Trim().StartsWith("# PMT-Unit ", StringComparison.Ordinal) || l.Trim().StartsWith("# PMT-Patch ", StringComparison.Ordinal))) is { } marked)
        {
            throw new PatchRegionException(PatchTexts.MarkerInUnit(marked.UnitKey));
        }

        var existing = Read(text, recipeId, ruleId);
        if (existing.Broken)
        {
            throw new PatchRegionException(PatchTexts.RegionBroken(existing.Problem ?? string.Empty));
        }

        if (existing.Found)
        {
            return text[..existing.Start] + Region(recipeId, ruleId, units, existing.Indent, newLine) + text[existing.End..];
        }

        if (inside is null)
        {
            var prefix = text.Length > 0 && !text.EndsWith('\n') ? text + newLine : text;
            return prefix + Region(recipeId, ruleId, units, string.Empty, newLine);
        }

        var (insertAt, indent, needsBreak) = InsidePoint(text, inside);
        var region = Region(recipeId, ruleId, units, indent, newLine);

        // Steht die Klammer hinter anderem Inhalt: Leerraum davor weg (kein Leerzeichen am Zeilenende), Klammer eingerückt auf eigene Zeile.
        return needsBreak
            ? text[..insertAt].TrimEnd(' ', '\t') + newLine + region + indent[..^1] + text[insertAt..]
            : text[..insertAt] + region + text[insertAt..];
    }

    private static (int InsertAt, string Indent, bool NeedsBreak) InsidePoint(string text, string inside)
    {
        ScriptBlock root;
        try
        {
            root = ScriptDocument.Parse(text);
        }
        catch (ScriptParseException ex)
        {
            throw new PatchRegionException(ex.Message);
        }

        ScriptStatement? statement = null;
        var block = root;
        foreach (var segment in inside.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            statement = block.Entries.OfType<ScriptStatement>().FirstOrDefault(s => s.Key == segment && s.Value is ScriptBlock);
            if (statement is null)
            {
                throw new PatchRegionException(PatchTexts.InsideMissing(inside));
            }

            block = (ScriptBlock)statement.Value;
        }

        if (statement is not { SourceStart: { } start, SourceEnd: { } end })
        {
            throw new PatchRegionException(PatchTexts.InsideMissing(inside));
        }

        var keyLineStart = text.LastIndexOf('\n', Math.Max(0, start - 1)) + 1;
        var indent = ScriptSpans.Indent(text[keyLineStart..start]) + "\t";
        var brace = end - 1;
        var braceLineStart = text.LastIndexOf('\n', Math.Max(0, brace - 1)) + 1;
        var onOwnLine = text[braceLineStart..brace].Trim().Length == 0;
        return onOwnLine ? (braceLineStart, indent, false) : (brace, indent, true);
    }

    private static string Region(string recipeId, string ruleId, IReadOnlyList<RegionUnit> units, string indent, string newLine)
    {
        var builder = new StringBuilder();
        builder.Append(indent).Append(BeginMarker(recipeId, ruleId)).Append(newLine);
        foreach (var unit in units)
        {
            builder.Append(indent).Append(UnitPrefix).Append(unit.UnitKey).Append(newLine);
            foreach (var line in unit.Text.Replace("\r\n", "\n").Split('\n'))
            {
                builder.Append(line.Length > 0 ? indent + line : line).Append(newLine);
            }
        }

        builder.Append(indent).Append(EndMarker(recipeId, ruleId)).Append(newLine);
        return builder.ToString();
    }

    private static void Flush(string? key, List<string> body, List<RegionUnit> units)
    {
        if (key is null)
        {
            return;
        }

        var lines = body.ToList();
        while (lines.Count > 0 && lines[^1].Trim().Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        units.Add(new RegionUnit(key, string.Join("\n", lines)));
    }

    private static RegionReadResult Broken(string text, string recipeId, string ruleId) =>
        new() { Found = true, Broken = true, Problem = recipeId + "/" + ruleId };

    private static List<(int Start, int End, string Body)> Lines(string text)
    {
        var result = new List<(int, int, string)>();
        var start = 0;
        while (start < text.Length)
        {
            var newLine = text.IndexOf('\n', start);
            var end = newLine < 0 ? text.Length : newLine + 1;
            var body = text[start..end].TrimEnd('\n').TrimEnd('\r');
            result.Add((start, end, body));
            start = end;
        }

        return result;
    }
}
