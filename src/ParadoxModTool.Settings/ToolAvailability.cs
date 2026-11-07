namespace ParadoxModTool.Settings;

/// <summary>
/// Zustand eines Werkzeugs im Spiel dieses Fensters: <paramref name="Reason"/> nur, wenn es gesperrt ist (Tooltip der ausgegrauten Kachel bzw.
/// des Menüpunkts); <paramref name="Hint"/> nur, wenn es läuft, aber im Spiel nur teilweise nutzbar ist (Zusatzzeile im Tooltip).
/// </summary>
public sealed record ToolState(bool Available, string? Reason, string? Hint);

/// <summary>
/// Was das Spiel dieses Fensters kann, einmal beim Start aus Profil und geladenen Daten gelesen (ein PMT-Fenster hat ein festes Spiel).
/// <paramref name="PerformanceBlockedReason"/>: <see langword="null"/> = Profiler nutzbar, sonst der Sperrgrund.
/// </summary>
public sealed record ToolFacts(
    string GameName,
    bool HasSaveExplorer,
    string? PerformanceBlockedReason,
    bool HasFlagTable,
    bool HasScriptCheckRules,
    bool HasGfxAuditRules,
    bool HasDesyncRules,
    bool HasGuiProfile,
    bool GuiToolsEnabled,
    bool GuiClassic,
    int GeneratorPackageCount,
    bool HasRecipePresets,
    bool HasScriptDocsData);

/// <summary>
/// Zentrale Tabelle "Werkzeug im Spiel verfügbar?" für Kacheln, Menüpunkte und Tastenkürzel (Spec 2026-10-31-tool-gating-perform-types).
/// Gesperrt wird nur, was im Spiel gar nicht läuft; teilweise nutzbare Werkzeuge bekommen einen Hinweis. Ob eine Kachel überhaupt angeboten
/// wird (GUI-Werkzeuge aus, Daten-Helfer im klassischen Format), entscheidet weiter die Kachelleiste.
/// </summary>
public static class ToolAvailability
{
    public static ToolState For(string tileId, ToolFacts facts) => tileId switch
    {
        TileIds.SaveExplorer => Gate(facts.HasSaveExplorer, facts),
        TileIds.Performance => facts.PerformanceBlockedReason is { } reason ? new ToolState(false, reason, null) : Ok,
        TileIds.FlagTracker => Gate(facts.HasFlagTable, facts),
        TileIds.ScriptCheck => !facts.HasScriptCheckRules ? Gate(false, facts) : facts.HasScriptDocsData ? Ok : Hint(MenuTexts.HintNoScriptDocs),
        TileIds.GfxAudit => Gate(facts.HasGfxAuditRules, facts),
        TileIds.Desync => Gate(facts.HasDesyncRules, facts),
        TileIds.Gui or TileIds.GuiDataHelper => Gate(facts.HasGuiProfile, facts),
        TileIds.GeneratorPackages or TileIds.PackageEditor => facts.GeneratorPackageCount == 0 ? Hint(MenuTexts.HintNoPackages) : Ok,
        TileIds.PatchRecipes => facts.HasRecipePresets ? Ok : Hint(MenuTexts.HintNoRecipePresets),
        TileIds.ScriptDocs or TileIds.ScopeFinder => facts.HasScriptDocsData ? Ok : Hint(MenuTexts.HintNoScriptDocs),
        _ => Ok,
    };

    private static ToolState Ok => new(true, null, null);

    private static ToolState Hint(string hint) => new(true, null, hint);

    private static ToolState Gate(bool supported, ToolFacts facts) => supported ? Ok : new ToolState(false, MenuTexts.NotSupported(facts.GameName), null);
}
