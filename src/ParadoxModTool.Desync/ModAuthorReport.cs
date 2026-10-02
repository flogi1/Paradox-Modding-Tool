using System.Globalization;
using System.Text;
using static ParadoxModTool.Desync.ReportFormat;

namespace ParadoxModTool.Desync;

/// <summary>
/// Baut den Bericht für Mod-Autoren als Markdown aus den Tatsachen: Fundstellen, Zustandsunterschiede, Hinweise und Umgebung.
/// Alle Texte sind Englisch und werden nie übersetzt. Werte stehen einzeilig (Backticks werden zu <c>'</c>), lange Werte werden gekürzt.
/// Außerhalb von Code-Spannen werden Markdown-Zeichen (<c>\ * _ [ ] &lt; &gt; #</c>) mit einem Backslash geschützt.
/// </summary>
public static class ModAuthorReport
{
    /// <summary>Längster Wert (Meldung, Feldwert); längere werden auf 119 Zeichen gekürzt und mit einer Ellipse beendet.</summary>
    public const int MaxValueLength = 120;

    private const string NoAdvice = "No specific advice; compare the leads above.";

    /// <summary>Zeichen, die Markdown außerhalb einer Code-Spanne als Auszeichnung lesen könnte.</summary>
    private const string MarkdownCharacters = "\\*_[]<>#";

    /// <summary>Der Titel ohne Markdown-Auszeichnung: "Desync (OOS) in " und die verschiedenen Mods der Hinweise, sonst "unknown mod".</summary>
    public static string Title(ReportFacts facts)
    {
        var mods = facts.Leads
            .Select(l => l.ModName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => Plain(name!))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return "Desync (OOS) in " + (mods.Count > 0 ? string.Join(", ", mods) : "unknown mod");
    }

    /// <summary>Der ganze Bericht; die Abschnitte stehen in fester Reihenfolge, Zeilen mit "\n" getrennt, ohne Zeilenumbruch am Ende.</summary>
    public static string Build(ReportFacts facts, IReadOnlyList<AdviceRule> advice)
    {
        var lines = new List<string> { "# " + Markdown(Title(facts)), "", "## Where it comes from" };

        lines.AddRange(facts.Leads.Count > 0
            ? facts.Leads.Select(LeadLine)
            : ["- No leads in the 7 game days before the OOS."]);

        lines.Add("");
        if (!facts.DiffAvailable)
            lines.Add("State comparison not run.");
        else if (facts.Causes.Count == 0)
            lines.Add("State comparison found no cause candidates.");
        else
        {
            lines.Add("**State differences (top cause candidates):**");
            lines.AddRange(facts.Causes.Select(CauseLine));
        }

        lines.Add("");
        lines.Add("## What to do");
        var hints = ReportAdvice.For(facts, advice);
        lines.AddRange(hints.Count > 0 ? hints.Select(hint => "- " + Text(hint)) : [$"- {NoAdvice}"]);

        lines.Add("");
        lines.Add("## Environment");
        lines.AddRange(EnvironmentLines(facts));

        lines.Add("");
        lines.Add("The full OOS dumps of all machines are available on request.");
        return string.Join("\n", lines);
    }

    /// <summary>Ein Text auf einer Zeile ohne Backticks (sie würden die Code-Spanne beenden).</summary>
    private static string Plain(string text) => OneLine(text.Replace('`', '\''));

    /// <summary>Schützt Markdown-Zeichen mit einem Backslash; nur für Text außerhalb von Code-Spannen.</summary>
    private static string Markdown(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (MarkdownCharacters.Contains(c))
                builder.Append('\\');
            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>Ein Text außerhalb einer Code-Spanne: einzeilig, ohne Backticks, Markdown-Zeichen geschützt.</summary>
    private static string Text(string text) => Markdown(Plain(text));

    /// <summary>Ein Wert für eine Code-Spanne: einzeilig, ohne Backticks, höchstens <see cref="MaxValueLength"/> Zeichen.</summary>
    private static string Code(string text) => Truncate(Plain(text), MaxValueLength);

    /// <summary>Eine Meldung außerhalb einer Code-Spanne: wie <see cref="Code"/> gekürzt, danach Markdown-Zeichen geschützt.</summary>
    private static string Message(string text) => Markdown(Code(text));

    private static string LeadLine(ReportLead lead)
    {
        var location = lead.File is null
            ? ""
            : $"`{Plain(lead.File)}{(lead.Line is { } line ? ":" + line.ToString(CultureInfo.InvariantCulture) : "")}` ";
        var mod = string.IsNullOrEmpty(lead.ModName) ? "vanilla/unknown" : Text(lead.ModName);
        return $"- {location}({mod}) {Dash} {Message(lead.Message)} {Dash} {Machines(lead.MachineIds)}, {GameDate(lead.GameDate) ?? Unknown}";
    }

    private static string CauseLine(ReportCause cause)
    {
        var path = Text(CausePath(cause.DisplayPath));
        if (cause.Fields.Count == 0)
            return $"- {path} {Dash} {ChangeWords(cause.Change)}";

        var fields = cause.Fields.Select(f =>
            $"{Text(f.Path.Length > 0 ? f.Path : "(unit)")}: `{Code(f.ReferenceValue ?? "(absent)")}` vs `{Code(f.DesyncedValue ?? "(absent)")}`");
        return $"- {path} {Dash} {string.Join("; ", fields)}";
    }

    private static List<string> EnvironmentLines(ReportFacts facts)
    {
        var summary = facts.Summary;
        var reference = ReferenceFirst(summary).FirstOrDefault();
        var version = string.IsNullOrEmpty(reference?.Save.Version) ? Unknown : Text(reference.Save.Version);
        var dlcs = summary.Warnings.FirstOrDefault(w => w.Kind == SummaryWarningKind.DifferentDlcs) is { } warning
            ? $"differ: {Text(warning.Detail)}"
            : "identical";
        var mods = summary.Machines.OrderBy(m => m.MachineId)
            .Select(m => $"machine {m.MachineId}: {(m.Mods.Count > 0 ? string.Join(", ", m.Mods.Select(mod => Text(ModText(mod)))) : "none")}")
            .ToList();

        return
        [
            $"- Game {version} (game {Commit(reference?.GameCommit)}, engine {Commit(reference?.EngineCommit)})",
            $"- DLCs: {dlcs}",
            $"- Mods: {(mods.Count > 0 ? string.Join("; ", mods) : Unknown)}",
        ];
    }
}
