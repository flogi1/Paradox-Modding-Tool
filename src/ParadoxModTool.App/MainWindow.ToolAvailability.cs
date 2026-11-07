using System.IO;
using System.Windows.Controls;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.Generators;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.Settings;

namespace ParadoxModTool.App;

/// <summary>
/// Werkzeug-Sperre je Spiel (Spec 2026-10-31-tool-gating-perform-types): Kacheln, klassische Menüpunkte und Tastenkürzel fragen dieselbe Tabelle
/// <see cref="ToolAvailability"/>. Ein PMT-Fenster hat ein festes Spiel; die Fakten entstehen beim Start und noch einmal, sobald die Script-Doku geladen ist
/// und sobald die Zählung der Generator-Pakete (im Hintergrund) eine neue Zahl bringt.
/// </summary>
public partial class MainWindow
{
    private ToolFacts? _toolFacts;

    /// <summary>Script-Doku einmal geladen (vorher gibt es keinen Hinweis "keine Script-Doku", sonst stünde er bei jedem Start kurz da).</summary>
    private bool _scriptDocsChecked;

    private ToolState ToolStateOf(string tileId) => ToolAvailability.For(tileId, _toolFacts ??= BuildToolFacts());

    private ToolFacts BuildToolFacts()
    {
        var profile = GameContext.Profile ?? _activeProfile;
        return new ToolFacts(
            GameName: string.IsNullOrWhiteSpace(profile?.DisplayName) ? GameContext.GameId : profile.DisplayName,
            HasSaveExplorer: SaveExplorerSettings is not null,
            PerformanceBlockedReason: PerformanceDisabledTip,
            HasFlagTable: FlagTrackerAvailable,
            HasScriptCheckRules: ScriptCheckAvailable,
            HasGfxAuditRules: GfxAuditAvailable,
            HasDesyncRules: DesyncAvailable,
            HasGuiProfile: profile is null || profile.Gui is not null,
            GuiToolsEnabled: GuiSettings.ToolsEnabled,
            GuiClassic: GuiSettings.Layout.IsClassic,
            GeneratorPackageCount: _generatorPackageCount,
            HasRecipePresets: GameContext.GameId == "ck3",
            HasScriptDocsData: !_scriptDocsChecked || ScriptDocs.Database.Entries.Count > 0);
    }

    /// <summary>Zuletzt gezählte Pakete des Spiels; bis die erste Zählung im Hintergrund fertig ist 0 (Hinweis "keine Pakete" höchstens kurz).</summary>
    private int _generatorPackageCount;

    /// <summary>Lauf-Nummer der Paketzählung: ein langsamer älterer Lauf überschreibt nicht das Ergebnis eines neueren.</summary>
    private int _generatorCountRun;

    /// <summary>
    /// Zählt die Pakete des Spiels im Hintergrund (beim Start und wenn der Paket-Editor Pakete geändert hat) und setzt danach die Werkzeug-Sperre neu,
    /// falls sich die Zahl geändert hat. Spiel und Datenordner werden im UI-Thread gelesen.
    /// </summary>
    private async void RefreshGeneratorPackageCount()
    {
        var run = ++_generatorCountRun;
        var gameId = GameContext.GameId;
        string root;
        try
        {
            root = GeneratorPackageLoader.PackagesRoot(GameContext.Repository.GameDirectory(gameId));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        var count = await Task.Run(() => CountGeneratorPackages(root, gameId));
        if (run != _generatorCountRun || count == _generatorPackageCount)
        {
            return;
        }

        _generatorPackageCount = count;
        ApplyToolAvailability();
    }

    /// <summary>Pakete des Spiels (mitgelieferte und eigene); Lesefehler zählen als "keine", der Hinweis ist dann höchstens zu vorsichtig.</summary>
    private static int CountGeneratorPackages(string root, string gameId)
    {
        try
        {
            return GeneratorPackageLoader.ListAll(GeneratorPackageLoader.ShippedRoot, root, gameId).Count(p => p.Package is not null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    /// <summary>Klassische Menüpunkte je Kachel; der Tooltip folgt der Tabelle (Sperrgrund oder Normaltext plus Hinweis).</summary>
    private IEnumerable<(string Id, MenuItem Item)> ToolMenuItems => new (string, MenuItem)[]
    {
        (TileIds.SaveExplorer, SaveExplorerMenuItem),
        (TileIds.Performance, PerformanceMenuItem),
        (TileIds.FlagTracker, FlagTrackerMenuItem),
        (TileIds.ScriptCheck, ScriptCheckMenuItem),
        (TileIds.GfxAudit, GfxAuditMenuItem),
        (TileIds.Desync, DesyncMenuItem),
        (TileIds.Gui, GuiMenuItem),
        (TileIds.GuiDataHelper, GuiDataHelperMenuItem),
        (TileIds.GeneratorPackages, GeneratorPackagesMenuItem),
        (TileIds.PackageEditor, PackageEditorMenuItem),
        (TileIds.PatchRecipes, PatchRecipesMenuItem),
        (TileIds.ScriptDocs, ScriptDocsMenuItem),
    };

    /// <summary>Nach dem Start-Block und nach dem Laden der Script-Doku: Fakten neu lesen, Menüpunkte setzen, Kacheln neu bauen.</summary>
    private void ApplyToolAvailability()
    {
        _toolFacts = BuildToolFacts();
        foreach (var (id, item) in ToolMenuItems)
        {
            var state = ToolStateOf(id);
            item.IsEnabled = state.Available;
            item.ToolTip = state.Available ? WithHint(BaseMenuTip(id, item), state.Hint) : state.Reason;
            ToolTipService.SetShowOnDisabled(item, true);
        }

        ToolbarScopeFinderButton.ToolTip = ToolbarTipFor(ToolbarItemIds.ScopeFinder);
        ApplyReferenceTreeAvailability();
        TileBarControl.Rebuild();
    }

    /// <summary>Normaltext des Menüpunkts: das GUI-Menü hat keinen eigenen, der Daten-Helfer seinen eigenen, die übrigen den der Kachel.</summary>
    private static string? BaseMenuTip(string id, MenuItem item) => id switch
    {
        TileIds.Gui => null,
        TileIds.GuiDataHelper => GuiDataTexts.MenuTip,
        _ => MenuTexts.TileTip(id, ExternalTileTip),
    };

    /// <summary>Kachel-Tooltip: Normaltext plus Hinweis bei teilweise nutzbaren Werkzeugen.</summary>
    private string TileTipWithHint(string id) => WithHint(MenuTexts.TileTip(id, ExternalTileTip), ToolStateOf(id).Hint) ?? string.Empty;

    private static string? WithHint(string? tip, string? hint) => hint is null ? tip : string.IsNullOrEmpty(tip) ? hint : tip + Environment.NewLine + hint;
}
