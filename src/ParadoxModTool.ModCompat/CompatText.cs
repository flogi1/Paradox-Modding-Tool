namespace ParadoxModTool.ModCompat;

/// <summary>Anzeigetexte für Bericht und Fenster (über <see cref="ModCompatTexts"/> lokalisiert).</summary>
public static class CompatText
{
    public static string Severity(CompatSeverity severity) => severity switch
    {
        CompatSeverity.Conflict => ModCompatTexts.SeverityConflict,
        CompatSeverity.Hint => ModCompatTexts.SeverityHint,
        _ => "Info",
    };

    public static string Kind(CompatKind kind) => kind switch
    {
        CompatKind.FileOverride => ModCompatTexts.KindFileOverride,
        CompatKind.FileIdentical => ModCompatTexts.KindFileIdentical,
        CompatKind.KeyIdentical => ModCompatTexts.KindKeyIdentical,
        CompatKind.KeyCombinable => ModCompatTexts.KindKeyCombinable,
        CompatKind.KeyLoss => ModCompatTexts.KindKeyLoss,
        CompatKind.KeyConflict => ModCompatTexts.KindKeyConflict,
        CompatKind.RedundantOverride => ModCompatTexts.KindRedundantOverride,
        CompatKind.TooCoarse => ModCompatTexts.KindTooCoarse,
        CompatKind.AdditiveOnly => ModCompatTexts.KindAdditiveOnly,
        CompatKind.ValueChangeOnly => ModCompatTexts.KindValueChangeOnly,
        CompatKind.ReplaceNeeded => ModCompatTexts.KindReplaceNeeded,
        CompatKind.Unreadable => ModCompatTexts.KindUnreadable,
        CompatKind.MissingFolder => ModCompatTexts.KindMissingFolder,
        CompatKind.ReplacePathNoEffect => ModCompatTexts.KindReplacePathNoEffect,
        CompatKind.ReplacePathGuiCrash => ModCompatTexts.KindReplacePathGuiCrash,
        CompatKind.PrefixNoEffect => ModCompatTexts.KindPrefixNoEffect,
        CompatKind.PrefixActsAsReplace => ModCompatTexts.KindPrefixActsAsReplace,
        _ => kind.ToString(),
    };

    public static string Suggestion(CompatSuggestion suggestion) => suggestion switch
    {
        CompatSuggestion.NeedsCompatch => ModCompatTexts.SuggestionNeedsCompatch,
        CompatSuggestion.ReviewBothSides => ModCompatTexts.SuggestionReviewBothSides,
        CompatSuggestion.UseInject => ModCompatTexts.SuggestionUseInject,
        CompatSuggestion.UseReplace => ModCompatTexts.SuggestionUseReplace,
        CompatSuggestion.ProvideOnlyChanged => ModCompatTexts.SuggestionProvideOnlyChanged,
        CompatSuggestion.RemoveRedundant => ModCompatTexts.SuggestionRemoveRedundant,
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
        OverrideBehavior.WholeFile => ModCompatTexts.BehaviorWholeFile,
        OverrideBehavior.PerKey => ModCompatTexts.BehaviorPerKey,
        OverrideBehavior.FirstWins => ModCompatTexts.BehaviorFirstWins,
        OverrideBehavior.LastWins => ModCompatTexts.BehaviorLastWins,
        OverrideBehavior.PartialKeys => ModCompatTexts.BehaviorPartialKeys,
        OverrideBehavior.AppendOnly => ModCompatTexts.BehaviorAppendOnly,
        OverrideBehavior.LocalizationKeys => ModCompatTexts.BehaviorLocalizationKeys,
        _ => behavior.ToString(),
    };
}
