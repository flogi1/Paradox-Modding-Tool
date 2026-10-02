using System.Text;

namespace ParadoxModTool.ModCompat;

public sealed record CompatReportOptions(CompatSeverity MinSeverity = CompatSeverity.Info, string? Title = null, string? Subtitle = null);

/// <summary>Schreibt die Funde als Markdown, als Planungshilfe für einen Kompatibilitätspatch.</summary>
public static class CompatReport
{
    public static string Build(IReadOnlyList<CompatFinding> findings, CompatReportOptions? options = null)
    {
        options ??= new CompatReportOptions();
        var shown = findings.Where(f => f.Severity >= options.MinSeverity).ToList();
        var text = new StringBuilder();
        text.AppendLine("# " + (options.Title ?? ModCompatTexts.ReportTitle));
        if (!string.IsNullOrWhiteSpace(options.Subtitle))
        {
            text.AppendLine();
            text.AppendLine(options.Subtitle);
        }

        text.AppendLine();
        text.AppendLine(ModCompatTexts.ReportCounts(Count(shown, CompatSeverity.Conflict), Count(shown, CompatSeverity.Hint), Count(shown, CompatSeverity.Info)));
        if (shown.Count == 0)
        {
            text.AppendLine();
            text.AppendLine(ModCompatTexts.NoFindings);
            return text.ToString();
        }

        foreach (var (severity, heading) in new[] { (CompatSeverity.Conflict, ModCompatTexts.Conflicts), (CompatSeverity.Hint, ModCompatTexts.Hints), (CompatSeverity.Info, ModCompatTexts.Infos) })
        {
            var section = shown.Where(f => f.Severity == severity).ToList();
            if (section.Count == 0)
            {
                continue;
            }

            text.AppendLine();
            text.AppendLine("## " + heading);
            foreach (var finding in section)
            {
                AppendFinding(text, finding);
            }
        }

        return text.ToString();
    }

    private static int Count(IEnumerable<CompatFinding> findings, CompatSeverity severity) => findings.Count(f => f.Severity == severity);

    private static void AppendFinding(StringBuilder text, CompatFinding finding)
    {
        text.AppendLine();
        var title = $"### {CompatText.Kind(finding.Kind)} — {finding.RelativePath}";
        if (finding.Key.Length > 0)
        {
            title += $" `{finding.Key}`";
        }

        if (finding.Uncertain)
        {
            title += " (" + ModCompatTexts.Uncertain + ")";
        }

        text.AppendLine(title);
        text.AppendLine();
        text.AppendLine(finding.Message);
        if (finding.Winner is not null)
        {
            text.AppendLine();
            text.AppendLine("- " + ModCompatTexts.WinnerLine(finding.Winner.Name));
        }

        if (CompatText.Suggestion(finding.Suggestion) is { Length: > 0 } suggestion)
        {
            text.AppendLine("- " + ModCompatTexts.SuggestionLine(suggestion));
        }

        if (finding.Note is not null)
        {
            text.AppendLine("- " + ModCompatTexts.NoteLine(finding.Note));
        }

        if (finding.Contributions.Count > 0)
        {
            text.AppendLine("- " + ModCompatTexts.ReportContributions);
            foreach (var c in finding.Contributions)
            {
                var place = c.Line > 0 ? $"{c.RelativePath}:{c.Line}" : c.RelativePath;
                text.AppendLine($"  - {c.Participant.Name}: {CompatText.Operator(c.Operator)} — {place}");
            }
        }
    }
}
