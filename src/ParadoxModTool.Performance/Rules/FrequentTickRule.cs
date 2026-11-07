using System.Globalization;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.Scopes;

namespace ParadoxModTool.Performance;

/// <summary>
/// Teure Blöcke, die häufig laufen: Zeilen ab <see cref="RuleThresholds.FrequentTickMinTotal"/> Gesamtzeit, deren oberster Block direkt eine
/// Kennzeichnung aus <see cref="PerformanceSettings.FrequentTickMarkers"/> trägt (z. B. <c>ai_tick = daily</c>, verschachtelte zählen nicht) oder
/// dessen Schlüssel in <see cref="PerformanceSettings.FrequentPulseNames"/> steht. Ohne Profiler-Einstellungen bleibt die Regel stumm.
/// Bei einer Kennzeichnung zählt der wirksame Abstand in Tagen: Tage je Tick (<c>daily</c> 1, <c>monthly</c> 30, <c>yearly</c> 365) mal
/// <c>ai_tick_frequency</c> aus demselben obersten Block (ohne Angabe 1). Gewarnt wird nur unter <see cref="RuleThresholds.FrequentTickMaxPeriodDays"/>;
/// ist der Abstand nicht bestimmbar (Frequenz keine feste Zahl, etwa ein Script Value, oder unbekannter Tick), gibt es einen einfachen Hinweis.
/// </summary>
public sealed class FrequentTickRule : IPerformanceRule
{
    private const string FrequencyKey = "ai_tick_frequency";

    public string Id => "frequent-tick";

    /// <summary>Höchstens ein Hinweis je oberstem Block (<see cref="BlockDedupe"/>): die Zeile mit der höchsten Gesamtzeit.</summary>
    public IEnumerable<PerformanceHint> Check(RuleContext context)
    {
        if (context.Settings is not { } settings)
        {
            return Array.Empty<PerformanceHint>();
        }

        var markers = settings.FrequentTickMarkers
            .Select(m => (Text: m, Tokens: ScopeLexer.Tokenize(m)))
            .Where(m => m.Tokens.Count == 3 && m.Tokens[0].Kind == ScopeTokenKind.Word && m.Tokens[1].Kind == ScopeTokenKind.Operator)
            .ToList();
        var maxPeriod = context.Thresholds.FrequentTickMaxPeriodDays;
        var hints = new List<(ScriptBlockInfo Block, PerformanceHint Hint)>();
        foreach (var row in context.Rows.Select(r => r.Row).Where(r => r.Total >= context.Thresholds.FrequentTickMinTotal))
        {
            if (context.BlockAt(row.Path, row.Line) is not { } block)
            {
                continue;
            }

            if (settings.FrequentPulseNames.Find(n => string.Equals(n, block.TopLevelKey, StringComparison.OrdinalIgnoreCase)) is { } pulse)
            {
                hints.Add((block, Hint(HintSeverity.Warning, row, PerformanceTexts.HintFrequentTickPulseDetail(pulse, block.TopLevelKey, row.Total))));
                continue;
            }

            if (TickIn(block.BlockText, markers) is not { } tick)
            {
                continue;
            }

            if (tick.PeriodDays is not { } period)
            {
                var detail = PerformanceTexts.HintFrequentTickUnknownDetail(tick.Marker, block.TopLevelKey, tick.FrequencyText, row.Total, maxPeriod);
                hints.Add((block, Hint(HintSeverity.Info, row, detail)));
            }
            else if (period < maxPeriod)
            {
                var detail = PerformanceTexts.HintFrequentTickDetail(tick.Marker, block.TopLevelKey, tick.Frequency!.Value, period, row.Total);
                hints.Add((block, Hint(HintSeverity.Warning, row, detail)));
            }
        }

        return BlockDedupe.Keep(hints);
    }

    private PerformanceHint Hint(HintSeverity severity, ProfilingRow row, string detail) =>
        new(Id, severity, row.Path, row.Line, PerformanceTexts.HintFrequentTickTitle, detail, row.Total);

    /// <summary>Gefundene Kennzeichnung mit Frequenz; <see cref="PeriodDays"/> ist <c>null</c>, wenn der Abstand nicht bestimmbar ist.</summary>
    /// <param name="FrequencyText">Wert von <c>ai_tick_frequency</c> wie im Skript (ohne Angabe <c>1</c>).</param>
    /// <param name="Frequency">Der Wert als Zahl; <c>null</c>, wenn er keine feste ganze Zahl ist.</param>
    private sealed record Tick(string Marker, string FrequencyText, int? Frequency, long? PeriodDays);

    /// <summary>
    /// Die erste Kennzeichnung und das erste <c>ai_tick_frequency</c>, die als Anweisung direkt im obersten Block stehen (Reihenfolge egal);
    /// Leerraum und Groß-/Kleinschreibung zählen nicht. <c>null</c> ohne Kennzeichnung.
    /// </summary>
    private static Tick? TickIn(string blockText, List<(string Text, List<ScopeToken> Tokens)> markers)
    {
        if (markers.Count == 0)
        {
            return null;
        }

        var tokens = ScopeLexer.Tokenize(blockText);
        (string Text, List<ScopeToken> Tokens)? found = null;
        string? frequencyText = null;
        var depth = 0;
        for (var i = 0; i < tokens.Count; i++)
        {
            switch (tokens[i].Kind)
            {
                case ScopeTokenKind.Open:
                    depth++;
                    continue;
                case ScopeTokenKind.Close:
                    depth--;
                    continue;
            }

            if (depth != 1 || i + 2 >= tokens.Count || tokens[i].Kind != ScopeTokenKind.Word || tokens[i + 1].Kind != ScopeTokenKind.Operator)
            {
                continue;
            }

            var value = tokens[i + 2];
            if (frequencyText is null && string.Equals(tokens[i].Text, FrequencyKey, StringComparison.OrdinalIgnoreCase) && tokens[i + 1].Text == "=")
            {
                // Ein Block als Wert (Inline-Script-Value) ist keine feste Zahl; die Schleife läuft weiter, damit die Tiefe stimmt.
                frequencyText = value.Kind == ScopeTokenKind.Open ? "{ … }" : value.Text;
            }

            if (value.Kind is not (ScopeTokenKind.Word or ScopeTokenKind.String))
            {
                continue;
            }

            if (found is null)
            {
                foreach (var marker in markers)
                {
                    if (Same(tokens[i], marker.Tokens[0]) && Same(tokens[i + 1], marker.Tokens[1]) && Same(value, marker.Tokens[2]))
                    {
                        found = marker;
                        break;
                    }
                }
            }

            i += 2;
        }

        if (found is not { } hit)
        {
            return null;
        }

        frequencyText ??= "1";
        int? frequency = int.TryParse(frequencyText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        long? period = frequency is { } f && UnitDays(hit.Tokens[2].Text) is { } unit ? unit * f : null;
        return new Tick(hit.Text, frequencyText, frequency, period);
    }

    /// <summary>Tage je Tick; <c>null</c> für Werte, die das Spiel nicht kennt (EU V kennt <c>never</c>, <c>daily</c> und <c>monthly</c>).</summary>
    private static long? UnitDays(string tick) => tick.ToLowerInvariant() switch
    {
        "daily" => 1,
        "monthly" => 30,
        "yearly" => 365,
        _ => null,
    };

    private static bool Same(ScopeToken a, ScopeToken b) => string.Equals(a.Text, b.Text, StringComparison.OrdinalIgnoreCase);
}
