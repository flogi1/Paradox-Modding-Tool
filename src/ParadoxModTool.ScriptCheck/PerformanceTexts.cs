using System.Text;

namespace ParadoxModTool.ScriptCheck;

/// <summary>Texte zu Funden <see cref="FindingKind.ExpensiveIterator"/>: Stufe, Weg, Vorschläge und der Bericht als Text.</summary>
public static class PerformanceTexts
{
    /// <summary>"Verschachtelt", wenn ein weiterer globaler Iterator darin steht, sonst die Schwere.</summary>
    public static string Level(Finding finding) =>
        finding.Performance?.Inner is not null ? ScriptCheckTexts.PerfNested : ScriptCheckTexts.Severity(finding.Severity);

    /// <summary>Der Weg vom Start bis zum Besitzer, mit Pfeilen verbunden.</summary>
    public static string Path(PerformanceInfo info) => string.Join(" \u2192 ", info.Path);

    /// <summary>Vorschläge in fester Reihenfolge; leer ohne Leistungsangaben.</summary>
    public static IReadOnlyList<string> Suggestions(Finding finding)
    {
        var list = new List<string>();
        if (finding.Performance is not { } info)
        {
            return list;
        }

        if (info.Narrower.Count > 0)
        {
            list.Add(ScriptCheckTexts.PerfSuggestNarrower(string.Join(", ", info.Narrower)));
        }

        list.Add(ScriptCheckTexts.PerfSuggestCheapCheck);
        list.Add(ScriptCheckTexts.PerfSuggestCache);
        if (info.Rate != PerformanceRate.Checked)
        {
            list.Add(ScriptCheckTexts.PerfSuggestRarerPulse);
        }

        return list;
    }

    /// <summary>Takt als Text.</summary>
    public static string RateText(PerformanceRate rate) => rate switch
    {
        PerformanceRate.Daily => ScriptCheckTexts.PerfRateDaily,
        PerformanceRate.Monthly => ScriptCheckTexts.PerfRateMonthly,
        PerformanceRate.Checked => ScriptCheckTexts.PerfRateChecked,
        PerformanceRate.Yearly => ScriptCheckTexts.PerfRateYearly,
        _ => ScriptCheckTexts.PerfRateRare,
    };

    /// <summary>Bericht als reiner Text: ein Block je Fund der Art <see cref="FindingKind.ExpensiveIterator"/>, in der Reihenfolge der Liste.</summary>
    public static string Report(IReadOnlyList<Finding> findings, string targetName, string gameName)
    {
        var shown = findings.Where(f => f.Kind == FindingKind.ExpensiveIterator && f.Performance is not null).ToList();
        var text = new StringBuilder();
        text.AppendLine(ScriptCheckTexts.PerfReportHeader(targetName, gameName, shown.Count));
        foreach (var finding in shown)
        {
            var info = finding.Performance!;
            text.AppendLine(Level(finding) + " | " + finding.Name + " | " + finding.Location.RelativePath + ":" + finding.Location.Line);
            text.AppendLine("  " + ScriptCheckTexts.PerfReportPath + ": " + Path(info));
            if (info.Inner is not null)
            {
                text.AppendLine("  " + ScriptCheckTexts.PerfReportInner + ": " + info.Inner);
            }

            text.AppendLine("  " + ScriptCheckTexts.PerfReportRate + ": " + RateText(info.Rate));
            foreach (var suggestion in Suggestions(finding))
            {
                text.AppendLine("  " + ScriptCheckTexts.PerfReportSuggestion + ": " + suggestion);
            }

            text.AppendLine();
        }

        return text.ToString();
    }
}
