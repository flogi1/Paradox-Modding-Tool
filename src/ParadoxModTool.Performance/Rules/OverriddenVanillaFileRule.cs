namespace ParadoxModTool.Performance;

/// <summary>
/// Hotspots in Dateien, die die Mod aus Vanilla übernimmt und überschreibt: Oft steckt die Zeit im Vanilla-Teil der Kopie.
/// Ein Hinweis je Datei, an der Zeile mit der höchsten Gesamtzeit.
/// </summary>
public sealed class OverriddenVanillaFileRule : IPerformanceRule
{
    public string Id => "vanilla-copy";

    public IEnumerable<PerformanceHint> Check(RuleContext context) =>
        context.Rows
            .Where(r => r.Origin == RowOrigin.Both)
            .GroupBy(r => r.Row.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.MaxBy(r => r.Row.Total)!.Row)
            .Select(row => new PerformanceHint(
                Id, HintSeverity.Info, row.Path, row.Line, PerformanceTexts.HintVanillaCopyTitle, PerformanceTexts.HintVanillaCopyDetail(row.Total), row.Total));
}
