using System.Globalization;
using System.Text;

namespace ParadoxModTool.Performance;

/// <summary>Grenzen für den Auftragsentwurf an eine KI: Zeilen der Rangliste, Anzahl der Codeblöcke und Zeilen je Codeblock.</summary>
public sealed record AiTaskLimits(int TopRows = 30, int Snippets = 12, int SnippetLines = 120);

/// <summary>Ein Codeblock um eine Fundstelle: Pfad wie im Dump, Zeilenbereich (1-basiert, einschließlich), Code und Erläuterung ("Hotspot Nr. 3, Total 1,2 s", "Hinweis: …").</summary>
public sealed record AiTaskDraftSnippet(string Path, int StartLine, int EndLine, string Code, string Note);

/// <summary>Der Entwurf des Auftrags: Markdown-Text, Pfade der betroffenen Dateien (Dump-Pfade, der Modder hakt sie im KI-Fenster an) und die Codeblöcke.</summary>
public sealed record AiTaskDraft(string Markdown, IReadOnlyList<string> ModFiles, IReadOnlyList<AiTaskDraftSnippet> Snippets);

/// <summary>
/// Baut aus einem Messlauf den Entwurf des KI-Auftrags: Messlauf, Zeiten je Herkunft, Hotspots der Mod-Seite, Hinweise der Regeln, betroffene Dateien
/// und die Lieferung, die die KI abgeben soll. Dazu Codeblöcke um die Hotspots und Hinweise. Liest nur über <c>readFile</c>; schreibt nichts.
/// </summary>
public static class PerformanceAiTask
{
    private static readonly string[] NewLines = { "\r\n", "\n" };

    /// <param name="currentModHash">Hash der Moddateien jetzt; <c>null</c>, wenn unbekannt.</param>
    /// <param name="readFile">Liest die Datei zu einem Dump-Pfad (die Zuordnung zur Moddatei ist Sache des Aufrufers); <c>null</c>, wenn es sie nicht gibt.</param>
    public static AiTaskDraft Build(
        PerformanceRun run,
        ProfilingSummary summary,
        RunDuration duration,
        IReadOnlyList<PerformanceHint> hints,
        string? hintsNote,
        OriginClassifier classifier,
        string? currentModHash,
        AiTaskLimits limits,
        Func<string, string?> readFile)
    {
        var rows = summary.Rank(RankBy.Total, OriginClassifier.ModSide, limits.TopRows);
        var files = rows.Select(r => r.Row.Path).Concat(hints.Select(h => h.Path)).Distinct(StringComparer.Ordinal).ToList();
        var markdown = new StringBuilder();
        AppendRun(markdown, run, duration, currentModHash);
        AppendOrigins(markdown, summary);
        AppendHotspots(markdown, rows);
        AppendHints(markdown, hints, hintsNote);
        AppendFiles(markdown, files);
        AppendDeliverables(markdown);
        var snippets = rows.Count == 0 ? new List<AiTaskDraftSnippet>() : Snippets(rows, hints, limits, readFile);
        return new AiTaskDraft(markdown.ToString(), files, snippets);
    }

    private static void AppendRun(StringBuilder text, PerformanceRun run, RunDuration duration, string? currentModHash)
    {
        text.Append("## ").Append(PerformanceTexts.AiHeadingRun).Append("\n\n");
        text.Append(PerformanceTexts.AiRunName(run.Name)).Append('\n');
        text.Append(PerformanceTexts.AiRunGame(run.GameId)).Append('\n');
        text.Append(PerformanceTexts.AiRunMod(ModName(run))).Append('\n');
        text.Append(PerformanceTexts.AiRunDate(run.CreatedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture))).Append('\n');
        text.Append(PerformanceTexts.AiRunMonths(run.PlannedMonths, run.CountedMonths ?? duration.Months)).Append('\n');
        text.Append(PerformanceTexts.AiRunSave(run.SaveName)).Append('\n');
        text.Append(PerformanceTexts.AiRunPlayset(run.PlaysetName)).Append('\n');
        text.Append(HashLine(run.ModFilesHash, currentModHash)).Append('\n');
        text.Append(PerformanceTexts.AiTimeUnitNote).Append('\n');
        text.Append(PerformanceTexts.NoiseNote(new NoiseSettings())).Append("\n\n");
    }

    private static string ModName(PerformanceRun run)
    {
        var folder = Path.GetFileName(run.ModRoot.TrimEnd('\\', '/'));
        return folder.Length > 0 ? folder : run.ModKey;
    }

    private static string HashLine(string? atRun, string? now)
    {
        if (string.IsNullOrEmpty(atRun) || string.IsNullOrEmpty(now))
        {
            return PerformanceTexts.AiHashUnknown;
        }

        return string.Equals(atRun, now, StringComparison.Ordinal)
            ? PerformanceTexts.AiHashSame
            : PerformanceTexts.AiHashChanged(Short(atRun), Short(now)) + '\n' + PerformanceTexts.AiHashNow;
    }

    private static string Short(string hash) => hash.Length > 12 ? hash[..12] : hash;

    private static void AppendOrigins(StringBuilder text, ProfilingSummary summary)
    {
        text.Append("## ").Append(PerformanceTexts.AiHeadingOrigins).Append("\n\n");
        foreach (var origin in Enum.GetValues<RowOrigin>())
        {
            text.Append(PerformanceTexts.AiOriginLine(origin, Time(summary.SelfByOrigin[origin]))).Append('\n');
        }

        text.Append(PerformanceTexts.ReportModSelf(Time(summary.ModSelf))).Append("\n\n");
    }

    private static void AppendHotspots(StringBuilder text, IReadOnlyList<RankedRow> rows)
    {
        text.Append("## ").Append(PerformanceTexts.AiHeadingHotspots).Append("\n\n");
        if (rows.Count == 0)
        {
            text.Append(PerformanceTexts.AiNoModRows).Append("\n\n");
            return;
        }

        text.Append(PerformanceTexts.AiHotspotHeader).Append('\n');
        foreach (var ranked in rows)
        {
            var row = ranked.Row;
            text.Append(PerformanceTexts.AiHotspotRow(ranked.Rank, $"{row.Path}:{row.Line.ToString(CultureInfo.InvariantCulture)}", row.Kind,
                row.Calls.ToString("N0", CultureInfo.CurrentCulture), Time(row.Self), Time(row.Total))).Append('\n');
        }

        text.Append('\n');
    }

    private static void AppendHints(StringBuilder text, IReadOnlyList<PerformanceHint> hints, string? note)
    {
        if (hints.Count == 0 && note is null)
        {
            return;
        }

        text.Append("## ").Append(PerformanceTexts.AiHeadingHints).Append("\n\n");
        foreach (var hint in hints)
        {
            text.Append(PerformanceTexts.ReportHintLine(hint.Severity, hint.Path, hint.Line, hint.Title)).Append('\n');
            text.Append("  ").Append(hint.Detail).Append('\n');
        }

        if (note is not null)
        {
            text.Append(note).Append('\n');
        }

        text.Append('\n');
    }

    private static void AppendFiles(StringBuilder text, IReadOnlyList<string> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        text.Append("## ").Append(PerformanceTexts.AiHeadingFiles).Append("\n\n");
        foreach (var file in files)
        {
            text.Append("- ").Append(file).Append('\n');
        }

        text.Append('\n');
    }

    private static void AppendDeliverables(StringBuilder text)
    {
        text.Append("## ").Append(PerformanceTexts.AiHeadingDeliverables).Append("\n\n");
        text.Append("- ").Append(PerformanceTexts.AiDeliverableFindings).Append('\n');
        text.Append("- ").Append(PerformanceTexts.AiDeliverableProposals).Append('\n');
        text.Append("- ").Append(PerformanceTexts.AiDeliverableTechniques).Append('\n');
        text.Append("- ").Append(PerformanceTexts.AiDeliverableRemeasure).Append('\n');
    }

    /// <summary>Je Hotspot-Zeile, dann je Hinweis der umgebende Block; gleiche Blöcke (Pfad plus Startzeile) nur einmal, höchstens <see cref="AiTaskLimits.Snippets"/>.</summary>
    private static List<AiTaskDraftSnippet> Snippets(IReadOnlyList<RankedRow> rows, IReadOnlyList<PerformanceHint> hints, AiTaskLimits limits, Func<string, string?> readFile)
    {
        var snippets = new List<AiTaskDraftSnippet>();
        var seen = new HashSet<(string Path, int Start)>();
        var places = rows.Select(r => (r.Row.Path, r.Row.Line, Note: PerformanceTexts.AiSnippetNoteHotspot(r.Rank, r.Row.Total)))
            .Concat(hints.Select(h => (h.Path, h.Line, Note: PerformanceTexts.AiSnippetNoteHint(h.Title))));
        foreach (var (path, line, note) in places)
        {
            if (snippets.Count >= limits.Snippets)
            {
                break;
            }

            var text = readFile(path);
            var block = text is null ? null : ScriptBlockLocator.Locate(text, line);
            if (block is null || !seen.Add((path, block.StartLine)))
            {
                continue;
            }

            snippets.Add(Cut(path, block, note, limits.SnippetLines));
        }

        return snippets;
    }

    /// <summary>Höchstgröße eines Codeblocks in Byte (UTF-8); hält den Auftrag samt Anleitung unter 100 000 Byte.</summary>
    public const int SnippetBytes = 5000;

    /// <summary>Kürzt auf höchstens <paramref name="maxLines"/> Zeilen und <see cref="SnippetBytes"/> Byte, immer an einer Zeilengrenze.</summary>
    private static AiTaskDraftSnippet Cut(string path, ScriptBlockInfo block, string note, int maxLines)
    {
        var lines = block.BlockText.Split(NewLines, StringSplitOptions.None);
        var code = block.BlockText.Replace("\r\n", "\n").TrimEnd('\n');
        if (lines.Length <= maxLines && Encoding.UTF8.GetByteCount(code) <= SnippetBytes)
        {
            return new AiTaskDraftSnippet(path, block.StartLine, block.EndLine, code, note);
        }

        var shown = 0;
        var bytes = 0;
        foreach (var line in lines.Take(Math.Max(maxLines, 1)))
        {
            var lineBytes = Encoding.UTF8.GetByteCount(line.TrimEnd('\r')) + 1;
            if (shown > 0 && bytes + lineBytes > SnippetBytes)
            {
                break;
            }

            bytes += lineBytes;
            shown++;
        }

        return new AiTaskDraftSnippet(path, block.StartLine, block.StartLine + shown - 1, string.Join('\n', lines.Take(shown)).TrimEnd('\r'),
            note + "; " + PerformanceTexts.AiSnippetCut(shown, lines.Length));
    }

    private static string Time(double value) => value.ToString("N3", CultureInfo.CurrentCulture);
}
