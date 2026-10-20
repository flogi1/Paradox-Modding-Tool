using ParadoxModTool.ModCompat;

namespace ParadoxModTool.Compatch;

/// <summary>
/// Ordnerregeln für den Update-Modus: Anhängen (<c>on_action</c>) wirkt über Dateien hinweg, eine Mod-Datei am Vanilla-Pfad ersetzt die
/// ganze Datei. Darum werden solche Dateien je Schlüssel gelesen, damit jeder Block eine eigene Stelle ist (Spec, Abschnitt 6).
/// </summary>
public static class UpdateModRules
{
    public static OverrideRules From(OverrideRules rules) =>
        new(rules.Rules.Select(r => r.Behavior == OverrideBehavior.AppendOnly ? r with { Behavior = OverrideBehavior.PerKey } : r)) { InjectAppends = rules.InjectAppends, FirstWinsInLoadOrder = rules.FirstWinsInLoadOrder, LastWinsInLoadOrder = rules.LastWinsInLoadOrder, LoadOrderCaseSensitive = rules.LoadOrderCaseSensitive };
}
