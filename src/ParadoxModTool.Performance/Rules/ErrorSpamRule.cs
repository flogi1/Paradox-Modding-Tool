namespace ParadoxModTool.Performance;

/// <summary>
/// Stellen, an denen das error.log sehr oft dieselbe Stelle meldet: Jeder Eintrag kostet das Spiel Zeit beim Schreiben des Logs.
/// Arbeitet auf <see cref="RuleContext.ErrorPlaces"/> und darum auch ohne Dump; mit Dump hängt sie die Gesamtzeit der Hotspot-Zeile an.
/// </summary>
public sealed class ErrorSpamRule : IPerformanceRule
{
    public string Id => "error-spam";

    public IEnumerable<PerformanceHint> Check(RuleContext context)
    {
        if (context.Errors is not { } errors)
        {
            yield break;
        }

        foreach (var place in context.ErrorPlaces.Where(p => p.Count >= context.Thresholds.ErrorSpamMin))
        {
            var detail = PerformanceTexts.HintErrorSpamDetail(place.Count, place.PerMinute, errors.MostCommonMessageAt(place.Path, place.Line) ?? string.Empty);
            if (HighestTotalAt(context, place) is { } total)
            {
                detail += " " + PerformanceTexts.HintErrorSpamRowTotal(total);
            }

            yield return new PerformanceHint(Id, HintSeverity.Warning, place.Path, place.Line, PerformanceTexts.HintErrorSpamTitle, detail, place.Count);
        }
    }

    // Mehrere Zeilen (Arten) an derselben Stelle: das Maximum, weil Total inklusiv ist und sich nicht addieren lässt.
    private static double? HighestTotalAt(RuleContext context, ErrorPlace place)
    {
        var totals = context.Rows
            .Where(r => r.Row.Line == place.Line && string.Equals(r.Row.Path, place.Path, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Row.Total)
            .ToList();
        return totals.Count == 0 ? null : totals.Max();
    }
}
