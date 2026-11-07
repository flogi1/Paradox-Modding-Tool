namespace ParadoxModTool.Performance;

/// <summary>Wie dringend ein Hinweis ist; Warnungen stehen vor Hinweisen.</summary>
public enum HintSeverity
{
    Info,
    Warning,
}

/// <summary>Ein Hinweis einer Regel zu einer Stelle im Skript.</summary>
/// <param name="RuleId">Kennung der Regel, z. B. <c>error-spam</c>.</param>
/// <param name="Path">Pfad der Stelle mit <c>/</c>, wie im Dump oder im error.log.</param>
/// <param name="Line">Zeile der Stelle (1-basiert).</param>
/// <param name="Title">Kurzer Titel, bereits übersetzt.</param>
/// <param name="Detail">Erklärung mit Zahlen, bereits übersetzt und formatiert.</param>
/// <param name="Value">Sortiermaß innerhalb einer Regel (größer = wichtiger): Fehleranzahl oder Gesamtzeit der Zeile.</param>
public sealed record PerformanceHint(string RuleId, HintSeverity Severity, string Path, int Line, string Title, string Detail, double Value);
