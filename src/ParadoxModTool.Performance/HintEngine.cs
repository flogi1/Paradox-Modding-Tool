namespace ParadoxModTool.Performance;

/// <summary>Lässt die Regeln über einen <see cref="RuleContext"/> laufen und ordnet ihre Hinweise.</summary>
public static class HintEngine
{
    /// <summary>Die Regeln, die PMT standardmäßig prüft.</summary>
    public static IReadOnlyList<IPerformanceRule> DefaultRules { get; } = new IPerformanceRule[]
    {
        new ErrorSpamRule(),
        new UnguardedVariableRule(),
        new FrequentTickRule(),
        new OverriddenVanillaFileRule(),
        new ManyCallsRule(),
    };

    /// <summary>
    /// Alle Hinweise der Regeln: Warnungen vor Hinweisen, dann nach der Stelle der Regel in <paramref name="rules"/> (die Werte verschiedener
    /// Regeln haben verschiedene Einheiten, etwa Fehleranzahl und Sekunden), dann <see cref="PerformanceHint.Value"/> absteigend, dann Pfad (ordinal) und Zeile.
    /// Je (Regel, Pfad, Zeile) bleibt nur der erste Hinweis dieser Ordnung, also der mit dem größten Wert, unabhängig von der Reihenfolge innerhalb der Regel.
    /// </summary>
    public static IReadOnlyList<PerformanceHint> Run(RuleContext context, IEnumerable<IPerformanceRule> rules) =>
        rules.SelectMany((rule, position) => rule.Check(context).Select(hint => (Hint: hint, Position: position)))
            .OrderBy(h => h.Hint.Severity == HintSeverity.Warning ? 0 : 1)
            .ThenBy(h => h.Position)
            .ThenByDescending(h => h.Hint.Value)
            .ThenBy(h => h.Hint.Path, StringComparer.Ordinal)
            .ThenBy(h => h.Hint.Line)
            .Select(h => h.Hint)
            .DistinctBy(h => (h.RuleId, h.Path, h.Line))
            .ToList();
}
