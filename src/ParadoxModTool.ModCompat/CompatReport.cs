using System.Text;

namespace ParadoxModTool.ModCompat;

public sealed record CompatReportOptions(CompatSeverity MinSeverity = CompatSeverity.Info, string Title = "Kompatibilitätsbericht", string? Subtitle = null);

/// <summary>Schreibt die Funde als Markdown, als Planungshilfe für einen Kompatibilitätspatch.</summary>
public static class CompatReport
{
    public static string Build(IReadOnlyList<CompatFinding> findings, CompatReportOptions? options = null)
    {
        options ??= new CompatReportOptions();
        var shown = findings.Where(f => f.Severity >= options.MinSeverity).ToList();
        var text = new StringBuilder();
        text.AppendLine("# " + options.Title);
        if (!string.IsNullOrWhiteSpace(options.Subtitle))
        {
            text.AppendLine();
            text.AppendLine(options.Subtitle);
        }

        text.AppendLine();
        text.AppendLine($"Funde: {Count(shown, CompatSeverity.Conflict)} Konflikte, {Count(shown, CompatSeverity.Hint)} Hinweise, {Count(shown, CompatSeverity.Info)} Infos");
        if (shown.Count == 0)
        {
            text.AppendLine();
            text.AppendLine("Keine Funde.");
            return text.ToString();
        }

        foreach (var (severity, heading) in new[] { (CompatSeverity.Conflict, "Konflikte"), (CompatSeverity.Hint, "Hinweise"), (CompatSeverity.Info, "Infos") })
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
            title += " (unsicher)";
        }

        text.AppendLine(title);
        text.AppendLine();
        text.AppendLine(finding.Message);
        if (finding.Winner is not null)
        {
            text.AppendLine();
            text.AppendLine("- Gewinner: " + finding.Winner.Name);
        }

        if (CompatText.Suggestion(finding.Suggestion) is { Length: > 0 } suggestion)
        {
            text.AppendLine("- Empfehlung: " + suggestion);
        }

        if (finding.Note is not null)
        {
            text.AppendLine("- Hinweis: " + finding.Note);
        }

        if (finding.Contributions.Count > 0)
        {
            text.AppendLine("- Beiträge:");
            foreach (var c in finding.Contributions)
            {
                var place = c.Line > 0 ? $"{c.RelativePath}:{c.Line}" : c.RelativePath;
                text.AppendLine($"  - {c.Participant.Name}: {CompatText.Operator(c.Operator)} — {place}");
            }
        }
    }
}
