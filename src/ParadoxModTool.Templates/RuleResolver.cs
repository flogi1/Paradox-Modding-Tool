namespace ParadoxModTool.Templates;

/// <summary>Die Regeldatei einer Ebene.</summary>
public sealed record RuleLayerFile(RuleLayer Layer, RuleFile File);

/// <summary>Eine Zeile der Regelliste: die Regel mit ihrer Ebene und ob sie wirksam ist.</summary>
public sealed record RuleListRow(EffectiveRule Effective, bool Active);

/// <summary>Bringt die Regeln aller Ebenen in ihre Rangfolge und entscheidet, welche wirksam sind.</summary>
public static class RuleResolver
{
    /// <summary>
    /// Alle Regeln, spezifischste Ebene zuerst und innerhalb einer Ebene in Listenreihenfolge, die mitgelieferten zuletzt. Wirksam ist eine Regel, wenn sie
    /// eingeschaltet ist, ihre Kennung in keiner <see cref="RuleFile.DisabledRuleIds"/> einer gleichen oder spezifischeren Ebene steht, sie (auf "Alle Spiele")
    /// für das Spiel gilt und keine spezifischere Regel dieselbe Kennung hat.
    /// </summary>
    public static IReadOnlyList<RuleListRow> List(IEnumerable<RuleLayerFile> layers, string gameId)
    {
        var ordered = layers.Where(l => l.Layer != RuleLayer.Shipped).OrderByDescending(l => l.Layer).ToList();
        ordered.Add(new RuleLayerFile(RuleLayer.Shipped, new RuleFile { Rules = ShippedRules.All.ToList() }));

        var rows = new List<RuleListRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var layerFile in ordered)
        {
            foreach (var rule in layerFile.File.Rules)
            {
                var active = rule.Enabled
                    && (layerFile.Layer != RuleLayer.AllGames || AppliesToGame(rule, gameId))
                    && !ordered.Any(o => o.Layer >= layerFile.Layer && o.File.DisabledRuleIds.Contains(rule.Id))
                    && seen.Add(rule.Id);
                rows.Add(new RuleListRow(new EffectiveRule(rule, layerFile.Layer), active));
            }
        }

        return rows;
    }

    /// <summary>Die wirksamen Regeln in Rangfolge.</summary>
    public static IReadOnlyList<EffectiveRule> Resolve(IEnumerable<RuleLayerFile> layers, string gameId) =>
        List(layers, gameId).Where(r => r.Active).Select(r => r.Effective).ToList();

    private static bool AppliesToGame(CountingRule rule, string gameId) =>
        rule.Games.Count == 0
        || rule.Games.Contains("*")
        || rule.Games.Any(g => string.Equals(g, gameId, StringComparison.OrdinalIgnoreCase));
}
