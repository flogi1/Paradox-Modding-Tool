using System.Windows;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.SaveGames;
using ParadoxModTool.Settings;
using ParadoxModTool.Shell;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Separator = System.Windows.Controls.Separator;

namespace ParadoxModTool.App;

/// <summary>
/// Kachelleiste (Interface-Rework 2): verbindet <see cref="TileBar"/> mit Texten, Handlern und Zählern des Hauptfensters. Jede Kachel ruft
/// denselben Handler wie der klassische Menüpunkt; "Projekt verwalten" und "GUI" öffnen ein Menü mit den Einträgen der alten Untermenüs.
/// </summary>
public partial class MainWindow
{
    /// <summary>Einmal im Konstruktor nach den Menütexten.</summary>
    private void InitializeTileBar()
    {
        TileBarControl.Bind(_settings, new TileSource(
            id => MenuTexts.TileLabel(id, ExternalTileLabel),
            TileTipWithHint,
            TileOffered,
            id => ToolStateOf(id).Available,
            id => ToolStateOf(id).Reason,
            BuildTileMenu));
        TileBarControl.TileClicked += (_, id) => OpenTile(id);
    }

    /// <summary>Beschriftungen aus anderen Projekten (Error-Log, Desync, Save Explorer, Performance-Checker); die übrigen kennt <see cref="MenuTexts.TileLabel"/>.</summary>
    private static string? ExternalTileLabel(string id) => id switch
    {
        TileIds.ErrorLog => ParadoxModTool.ErrorLog.ErrorLogTexts.Menu,
        TileIds.Desync => ParadoxModTool.Desync.DesyncTexts.Menu,
        TileIds.SaveExplorer => SaveExplorerTexts.Menu,
        TileIds.Performance => ParadoxModTool.Performance.PerformanceTexts.Menu,
        TileIds.FlagTracker => FlagTrackerTexts.Menu,
        TileIds.ScriptCheck => ParadoxModTool.ScriptCheck.ScriptCheckTexts.Menu,
        TileIds.GfxAudit => ParadoxModTool.GfxAudit.GfxAuditTexts.Menu,
        _ => null,
    };

    private static string? ExternalTileTip(string id) => id switch
    {
        TileIds.ErrorLog => ParadoxModTool.ErrorLog.ErrorLogTexts.MenuTip,
        TileIds.Desync => ParadoxModTool.Desync.DesyncTexts.MenuTip,
        TileIds.SaveExplorer => SaveExplorerTexts.MenuTip,
        TileIds.Performance => ParadoxModTool.Performance.PerformanceTexts.MenuTip,
        TileIds.FlagTracker => FlagTrackerTexts.MenuTip,
        TileIds.ScriptCheck => ParadoxModTool.ScriptCheck.ScriptCheckTexts.MenuTip,
        TileIds.GfxAudit => ParadoxModTool.GfxAudit.GfxAuditTexts.MenuTip,
        _ => null,
    };

    /// <summary>GUI-Kacheln nur mit eingeschalteten GUI-Werkzeugen; der Daten-Helfer außerdem nur im Jomini-Format.</summary>
    private static bool TileOffered(string id) =>
        (TileCatalog.Find(id)?.RequiresGuiTools != true || GuiSettings.ToolsEnabled) && (id != TileIds.GuiDataHelper || GuiSettings.DataHelperOffered);

    /// <summary>Klick auf eine Kachel ohne Menü: derselbe Handler wie der klassische Menüpunkt.</summary>
    private void OpenTile(string id)
    {
        var args = new RoutedEventArgs();
        switch (id)
        {
            case TileIds.Project: OpenBranchSync_Click(this, args); break;
            case TileIds.Mods: OpenModsWindow_Click(this, args); break;
            case TileIds.CuTool: OpenVanillaDiffWindow_Click(this, args); break;
            case TileIds.Compatch: OpenCompatch_Click(this, args); break;
            case TileIds.ModCompat: OpenModCompat_Click(this, args); break;
            case TileIds.Playsets: OpenPlaysets_Click(this, args); break;
            case TileIds.PatchRecipes: OpenPatchRecipes_Click(this, args); break;
            case TileIds.SaveExplorer: OpenSaveExplorer_Click(this, args); break;
            case TileIds.ErrorLog: OpenErrorLog_Click(this, args); break;
            case TileIds.BugTracker: BugTracker_Click(this, args); break;
            case TileIds.ScriptDocs: OpenScriptDocsWindow_Click(this, args); break;
            case TileIds.ScopeFinder: ScopePaths_Click(this, args); break;
            case TileIds.GuiDataHelper: GuiDataHelper_Click(this, args); break;
            case TileIds.Templates: OpenTemplateGenerator_Click(this, args); break;
            case TileIds.GeneratorPackages: OpenGeneratorPackages_Click(this, args); break;
            case TileIds.PackageEditor: OpenPackageEditor_Click(this, args); break;
            case TileIds.Desync: OpenDesync_Click(this, args); break;
            case TileIds.Performance: OpenPerformance_Click(this, args); break;
            case TileIds.FlagTracker: OpenFlagTracker_Click(this, args); break;
            case TileIds.ScriptCheck: OpenScriptCheck_Click(this, args); break;
            case TileIds.GfxAudit: OpenGfxAudit_Click(this, args); break;
        }
    }

    /// <summary>Menü der Kachel "GUI" (die Einträge des alten GUI-Menüs). "Projekt verwalten" öffnet ohne Menü direkt den Abgleich.</summary>
    private ContextMenu? BuildTileMenu(string id)
    {
        switch (id)
        {
            case TileIds.Gui:
            {
                var menu = new ContextMenu();
                menu.Items.Add(GuiTileItem(GuiTexts.MenuInspect, GuiTexts.MenuInspectTip, GuiTexts.KeyInspect, GuiInspectMenuItem_Click));
                if (GuiSettings.DataHelperOffered)
                {
                    menu.Items.Add(GuiTileItem(GuiDataTexts.Title, GuiDataTexts.MenuTip, GuiDataTexts.Key, GuiDataHelper_Click));
                }

                // Bausteine mit GUI-Werkzeugen in beiden Formaten (klassisch eigene für HOI IV und EU IV, Plan 3 Task 8); "Neue GUI-Datei" immer
                // (klassisch nach <Mod>/interface, Plan 3 Task 9).
                if (GuiSettings.BlocksOffered)
                {
                    menu.Items.Add(GuiTileItem(GuiTexts.MenuBlocks, GuiTexts.MenuBlocksTip, GuiTexts.KeyBlocks, GuiBlocks_Click));
                }

                menu.Items.Add(GuiTileItem(GuiTexts.MenuNewFile, GuiTexts.MenuNewFileTip, null, NewGuiFile_Click));

                menu.Items.Add(GuiTileItem(GuiTexts.MenuModSettings, GuiTexts.MenuModSettingsTip, null, GuiModSettings_Click));
                return menu;
            }

            default:
                return null;
        }
    }

    private static MenuItem GuiTileItem(string header, string tip, string? gesture, RoutedEventHandler onClick)
    {
        var item = new MenuItem { Header = header, ToolTip = tip, InputGestureText = gesture };
        item.Click += onClick;
        return item;
    }
}
