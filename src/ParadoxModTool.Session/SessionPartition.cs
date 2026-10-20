using System.Text.Json.Nodes;

namespace ParadoxModTool.Session;

/// <summary>
/// Teilt <see cref="AppSessionState"/> in zwei Ablageorte: Einstellungen, die für alle Spiele gelten (Theme, Oberflächen-Vorlieben,
/// Update-Prüfung) und Zustand, der zu genau einem Spiel gehört (offene Dateien, Pfade, Explorer-Wurzeln, Kategorien, Fensterposition).
/// Jede Eigenschaft von <see cref="AppSessionState"/> steht in genau einer der beiden Listen; ein Test stellt das sicher,
/// damit eine neue Eigenschaft nicht unbemerkt verloren geht.
/// </summary>
public static class SessionPartition
{
    /// <summary>Eigenschaften, die in <c>settings.json</c> liegen und für alle Spiele gelten.</summary>
    public static readonly IReadOnlyList<string> GlobalNames = new[]
    {
        nameof(AppSessionState.ColorizeWholeTab),
        nameof(AppSessionState.NoteIconColorHex),
        nameof(AppSessionState.IconColorsByTheme),
        nameof(AppSessionState.MultiRowTabs),
        nameof(AppSessionState.PathBarVisible),
        nameof(AppSessionState.ReplaceWarningsInWindow),
        nameof(AppSessionState.NotesPanelVisible),
        nameof(AppSessionState.NotesPanelAlwaysVisible),
        nameof(AppSessionState.HiddenTabIcons),
        nameof(AppSessionState.HiddenFilterEntries),
        nameof(AppSessionState.ShowDoneBugs),
        nameof(AppSessionState.BugsPanelWholeProject),
        nameof(AppSessionState.DocumentBarVisible),
        nameof(AppSessionState.CheckForUpdatesOnStartup),
        nameof(AppSessionState.LastUpdateCheckUtc),
        nameof(AppSessionState.SkippedUpdateVersion),
        nameof(AppSessionState.ThemeId),
        nameof(AppSessionState.LastGameProfileId),
        nameof(AppSessionState.ScriptHoverEnabled),
        nameof(AppSessionState.ScriptAutoCompleteEnabled),
        nameof(AppSessionState.ScriptCompleteCustomLocalizations),
        nameof(AppSessionState.ScriptHintButtonsVisible),
        nameof(AppSessionState.HiddenToolbarItems),
        nameof(AppSessionState.ToolbarItemsHiddenByMigration),
        nameof(AppSessionState.ScopeStatusBarEnabled),
        nameof(AppSessionState.ScopeStatusBarTopEnabled),
        nameof(AppSessionState.ScopeWarningsEnabled),
        nameof(AppSessionState.ScopePanelEnabled),
        nameof(AppSessionState.BracketHighlightEnabled),
        nameof(AppSessionState.ScopeHoverEnabled),
        nameof(AppSessionState.ModifierHoverEnabled),
        nameof(AppSessionState.ModifierNamesEnabled),
        nameof(AppSessionState.DiffFoldingEnabled),
        nameof(AppSessionState.CuToolLastTab),
        nameof(AppSessionState.ExplorerButtonsOnLeft),
        nameof(AppSessionState.ExplorerShowErrorMarks),
        nameof(AppSessionState.ExplorerSearchFoldersOnly),
        nameof(AppSessionState.ExplorerHideUgcReferences),
        nameof(AppSessionState.GuiDesignerEnabled),
        nameof(AppSessionState.GuiDesignerMode),
        nameof(AppSessionState.GuiDesignerLayout),
        nameof(AppSessionState.UiLanguage),
        nameof(AppSessionState.GuiBreadcrumbEnabled),
        nameof(AppSessionState.GuiShownInEnabled),
        nameof(AppSessionState.GuiToolsEnabled),
        nameof(AppSessionState.SettingsWindowCategory),
        nameof(AppSessionState.SettingsWindowBounds),
        nameof(AppSessionState.ErrorLogStandalone),
        nameof(AppSessionState.ErrorLogWindowBounds),
        nameof(AppSessionState.SaveExplorerStandalone),
        nameof(AppSessionState.SaveExplorerWindowBounds),
        nameof(AppSessionState.MyModsView),
        nameof(AppSessionState.MyModsGrouping),
        nameof(AppSessionState.MyModsSort),
        nameof(AppSessionState.MyModsSortDescending),
        nameof(AppSessionState.MyModsOnlyPublished),
        nameof(AppSessionState.MyModsOnlyOpenBugs),
        nameof(AppSessionState.MyModsOnlyWarnings),
        nameof(AppSessionState.MyModsGameOrder),
        nameof(AppSessionState.MyModsHiddenGames),
        nameof(AppSessionState.MyModsAlertsCollapsed),
        nameof(AppSessionState.MyModsWindowBounds),
        nameof(AppSessionState.MyModsHiddenAlertKinds),
        nameof(AppSessionState.MyModsStartMaximized),
        nameof(AppSessionState.WorkspaceFilterButtonVisible),
        nameof(AppSessionState.WorkspaceIconButtonVisible),
        nameof(AppSessionState.WindowLayout),
        nameof(AppSessionState.ToolbarPosition),
        nameof(AppSessionState.ToolbarLabelsVisible),
        nameof(AppSessionState.ToolbarOrder),
        nameof(AppSessionState.TileBarVisible),
        nameof(AppSessionState.TileSize),
        nameof(AppSessionState.TileLabelsVisible),
        nameof(AppSessionState.TileOrder),
        nameof(AppSessionState.HiddenTiles),
        nameof(AppSessionState.IconOnlyTiles),
        nameof(AppSessionState.WorkspaceCategoryButtonVisible),
        nameof(AppSessionState.WorkspaceSortButtonVisible),
        nameof(AppSessionState.WorkspaceMarkButtonsVisible),
        nameof(AppSessionState.CompatchCautiousWrite),
        nameof(AppSessionState.CompatchSkipLocked),
        nameof(AppSessionState.CompatchResultRight),
        nameof(AppSessionState.CompatchMainAsBase),
        nameof(AppSessionState.CompatchFillMissing),
        nameof(AppSessionState.CompatchHideFilled),
        nameof(AppSessionState.CompatchHideReplaced),
        nameof(AppSessionState.CompatchHideDecided),
        nameof(AppSessionState.CompatchUncertainOnly),
        nameof(AppSessionState.CompatchKindsSideBySide),
        nameof(AppSessionState.CompatchDefaultView),
        nameof(AppSessionState.CompatchCardsFolderPane),
        nameof(AppSessionState.CompatchHideComments),
    };

    /// <summary>Eigenschaften, die in <c>games\&lt;id&gt;\session.json</c> liegen und zu einem Spiel gehören.</summary>
    public static readonly IReadOnlyList<string> GameNames = new[]
    {
        nameof(AppSessionState.SplitViewEnabled),
        nameof(AppSessionState.ActiveGameProfileId),
        nameof(AppSessionState.LeftPane),
        nameof(AppSessionState.RightPane),
        nameof(AppSessionState.ResultsPanelOnTop),
        nameof(AppSessionState.CategoryColorOverrides),
        nameof(AppSessionState.GameExecutablePath),
        nameof(AppSessionState.PlaysetsFilePath),
        nameof(AppSessionState.LaunchOptionArgs),
        nameof(AppSessionState.GeneratorCopyModifiers),
        nameof(AppSessionState.GeneratorScaleModifiers),
        nameof(AppSessionState.GeneratorRemoveAcrossLevels),
        nameof(AppSessionState.GeneratorLookupSearch),
        nameof(AppSessionState.GeneratorLookupSource),
        nameof(AppSessionState.GeneratorLookupTag),
        nameof(AppSessionState.GeneratorImageGridBounds),
        nameof(AppSessionState.ErrorLogFolder),
        nameof(AppSessionState.ExplorerRoots),
        nameof(AppSessionState.ExplorerExpandedPaths),
        nameof(AppSessionState.VanillaDiffModRootOverride),
        nameof(AppSessionState.ActiveModChoice),
        nameof(AppSessionState.SearchLastFolder),
        nameof(AppSessionState.StorageParentFolder),
        nameof(AppSessionState.StoreInsideModFolder),
        nameof(AppSessionState.Categories),
        nameof(AppSessionState.CategoryColorOverridesByTheme),
        nameof(AppSessionState.Window),
        nameof(AppSessionState.ScriptDocsFolder),
        nameof(AppSessionState.CompatchHiddenKinds),
        nameof(AppSessionState.CompatchLastStart),
        nameof(AppSessionState.FolderFilters),
        nameof(AppSessionState.GuiHiddenConditionals),
    };

    /// <summary>Kopiert die genannten Eigenschaften (tiefe Kopie); fehlende bleiben weg.</summary>
    public static JsonObject Pick(JsonObject source, IEnumerable<string> names)
    {
        var result = new JsonObject();
        foreach (var name in names)
        {
            if (source.TryGetPropertyValue(name, out var value))
            {
                result[name] = value?.DeepClone();
            }
        }

        return result;
    }

    /// <summary>
    /// Drei-Wege-Zusammenführung der globalen Einstellungen beim Speichern: Ausgangspunkt ist der Stand auf der Platte (den ein
    /// anderer Prozess inzwischen geändert haben kann). Nur Eigenschaften, die dieser Prozess seit dem Laden
    /// (<paramref name="baseline"/>) selbst geändert hat (<paramref name="current"/>), überschreiben den Plattenstand.
    /// So gehen Änderungen eines anderen gleichzeitig laufenden Fensters nicht verloren.
    /// </summary>
    public static JsonObject MergeGlobal(JsonObject disk, JsonObject baseline, JsonObject current)
    {
        var result = Pick(disk, GlobalNames);
        foreach (var name in GlobalNames)
        {
            current.TryGetPropertyValue(name, out var now);
            baseline.TryGetPropertyValue(name, out var before);
            if (!JsonNode.DeepEquals(now, before))
            {
                result[name] = now?.DeepClone();
            }
        }

        return result;
    }
}
