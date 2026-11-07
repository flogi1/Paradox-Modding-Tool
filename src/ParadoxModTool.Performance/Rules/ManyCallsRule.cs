namespace ParadoxModTool.Performance;

/// <summary>
/// Script Values, die sehr oft berechnet werden: Hochgerechnet auf ein Spieljahr mehr Aufrufe als <see cref="RuleThresholds.ManyCallsPerYear"/>.
/// Ohne bekannte Monatszahl (oder bei 0 Monaten) zählen die Aufrufe des Dumps unverändert. Sortiermaß ist die Gesamtzeit der Zeile, die Aufrufe pro Jahr stehen nur im Detail.
/// </summary>
public sealed class ManyCallsRule : IPerformanceRule
{
    public string Id => "many-calls";

    public IEnumerable<PerformanceHint> Check(RuleContext context)
    {
        foreach (var row in context.Rows.Select(r => r.Row).Where(r => IsScriptValue(r.Kind)))
        {
            var callsPerYear = context.Months is > 0 and var months ? row.Calls * 12.0 / months : row.Calls;
            if (callsPerYear > context.Thresholds.ManyCallsPerYear)
            {
                yield return new PerformanceHint(
                    Id, HintSeverity.Info, row.Path, row.Line, PerformanceTexts.HintManyCallsTitle, PerformanceTexts.HintManyCallsDetail(callsPerYear), row.Total);
            }
        }
    }

    // "named_script_value" beginnt nicht mit "script_value", darum beide Präfixe.
    private static bool IsScriptValue(string kind) =>
        kind.StartsWith("script_value", StringComparison.Ordinal) || kind.StartsWith("named_script_value", StringComparison.Ordinal);
}
