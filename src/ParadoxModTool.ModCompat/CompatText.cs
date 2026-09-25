namespace ParadoxModTool.ModCompat;

/// <summary>Deutsche Anzeigetexte für Bericht und Fenster.</summary>
public static class CompatText
{
    public static string Severity(CompatSeverity severity) => severity switch
    {
        CompatSeverity.Conflict => "Konflikt",
        CompatSeverity.Hint => "Hinweis",
        _ => "Info",
    };

    public static string Kind(CompatKind kind) => kind switch
    {
        CompatKind.FileOverride => "Datei ersetzt",
        CompatKind.FileIdentical => "Datei identisch",
        CompatKind.KeyIdentical => "Schlüssel identisch",
        CompatKind.KeyCombinable => "Kombinierbar",
        CompatKind.KeyLoss => "Beitrag geht verloren",
        CompatKind.KeyConflict => "Schlüsselkonflikt",
        CompatKind.RedundantOverride => "Unnötige Kopie",
        CompatKind.TooCoarse => "Zu grob überschrieben",
        CompatKind.AdditiveOnly => "Nur Ergänzung",
        CompatKind.ReplaceNeeded => "Ersetzung nötig",
        CompatKind.Unreadable => "Nicht lesbar",
        CompatKind.MissingFolder => "Ordner fehlt",
        _ => kind.ToString(),
    };

    public static string Suggestion(CompatSuggestion suggestion) => suggestion switch
    {
        CompatSuggestion.NeedsCompatch => "Kompatibilitätspatch nötig",
        CompatSuggestion.ReviewBothSides => "Beide Seiten prüfen",
        CompatSuggestion.UseInject => "INJECT: genügt",
        CompatSuggestion.UseReplace => "REPLACE: nötig",
        CompatSuggestion.ProvideOnlyChanged => "Nur Geändertes bereitstellen",
        CompatSuggestion.RemoveRedundant => "Kopie entfernen",
        _ => string.Empty,
    };

    public static string Operator(OverrideOperator op) => op switch
    {
        OverrideOperator.InjectOrCreate => "INJECT_OR_CREATE:",
        OverrideOperator.ReplaceOrCreate => "REPLACE_OR_CREATE:",
        OverrideOperator.TryInject => "TRY_INJECT:",
        OverrideOperator.TryReplace => "TRY_REPLACE:",
        OverrideOperator.Inject => "INJECT:",
        OverrideOperator.Replace => "REPLACE:",
        _ => "Definition",
    };

    public static string Behavior(OverrideBehavior behavior) => behavior switch
    {
        OverrideBehavior.WholeFile => "Nur Datei",
        OverrideBehavior.PerKey => "Je Schlüssel (INJECT/REPLACE)",
        OverrideBehavior.FirstWins => "Erster gewinnt",
        OverrideBehavior.LastWins => "Letzter gewinnt",
        OverrideBehavior.PartialKeys => "Einzelne Werte",
        OverrideBehavior.AppendOnly => "Nur anhängen",
        OverrideBehavior.LocalizationKeys => "Lokalisierung",
        _ => behavior.ToString(),
    };
}
