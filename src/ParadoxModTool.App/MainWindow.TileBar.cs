using System.Windows;
using ParadoxModTool.Editor.Gui;
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
            id => MenuTexts.TileTip(id, ExternalTileTip),
            TileOffered,
            TileEnabled,
            id => id == TileIds.SaveExplorer ? SaveExplorerTexts.NotAvailable : null,
            BuildTileMenu));
        TileBarControl.TileClicked += (_, id) => OpenTile(id);
    }

    /// <summary>Beschriftungen aus anderen Projekten (Error-Log, Desync, Save Explorer); die übrigen kennt <see cref="MenuTexts.TileLabel"/>.</summary>
    private static string? ExternalTileLabel(string id) => id switch
    {
        TileIds.ErrorLog => ParadoxModTool.ErrorLog.ErrorLogTexts.Menu,
        TileIds.Desync => ParadoxModTool.Desync.DesyncTexts.Menu,
        TileIds.SaveExplorer => SaveExplorerTexts.Menu,
        _ => null,
    };

    private static string? ExternalTileTip(string id) => id switch
    {
        TileIds.ErrorLog => ParadoxModTool.ErrorLog.ErrorLogTexts.MenuTip,
        TileIds.Desync => ParadoxModTool.Desync.DesyncTexts.MenuTip,
        TileIds.SaveExplorer => SaveExplorerTexts.MenuTip,
        _ => null,
    };

    /// <summary>GUI-Kacheln nur mit eingeschalteten GUI-Werkzeugen.</summary>
    private static bool TileOffered(string id) => TileCatalog.Find(id)?.RequiresGuiTools != true || GuiSettings.ToolsEnabled;

    /// <summary>Der Save Explorer braucht Profil-Unterstützung (wie der Menüpunkt).</summary>
    private bool TileEnabled(string id) => id != TileIds.SaveExplorer || SaveExplorerSettings is not null;

    /// <summary>Klick auf eine Kachel ohne Menü: derselbe Handler wie der klassische Menüpunkt.</summary>
    private void OpenTile(string id)
    {
        var args = new RoutedEventArgs();
        switch (id)
        {
            case TileIds.CuTool: OpenVanillaDiffWindow_Click(this, args); break;
            case TileIds.Compatch: OpenCompatch_Click(this, args); break;
            case TileIds.ModCompat: OpenModCompat_Click(this, args); break;
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
        }
    }

    /// <summary>Menü der Kacheln "Projekt verwalten" (Kopieren, Verschieben, Vergleichen, Abgleich) und "GUI" (die Einträge des alten GUI-Menüs).</summary>
    private ContextMenu? BuildTileMenu(string id)
    {
        switch (id)
        {
            case TileIds.Project:
            {
                var menu = new ContextMenu();
                var path = (_modSourcePane ?? PanelPane).CurrentFilePath;
                var targets = path is null ? null : BuildBranchTargets(path);
                menu.Items.Add(ProjectTileItem(MenuTexts.ProjectCopy, path, targets?.Copy, Explorer.ExplorerBranchAction.Copy));
                menu.Items.Add(ProjectTileItem(MenuTexts.ProjectMove, path, targets?.Move, Explorer.ExplorerBranchAction.Move));
                menu.Items.Add(ProjectTileItem(MenuTexts.ProjectCompare, path, targets?.Compare, Explorer.ExplorerBranchAction.Compare));
                menu.Items.Add(new Separator());
                var sync = new MenuItem { Header = MenuTexts.ProjectSync, ToolTip = MenuTexts.ProjectSyncTip };
                sync.Click += OpenBranchSync_Click;
                menu.Items.Add(sync);
                return menu;
            }

            case TileIds.Gui:
            {
                var menu = new ContextMenu();
                menu.Items.Add(GuiTileItem(GuiTexts.MenuInspect, GuiTexts.MenuInspectTip, GuiTexts.KeyInspect, GuiInspectMenuItem_Click));
                menu.Items.Add(GuiTileItem(GuiDataTexts.Title, GuiDataTexts.MenuTip, GuiDataTexts.Key, GuiDataHelper_Click));
                menu.Items.Add(GuiTileItem(GuiTexts.MenuBlocks, GuiTexts.MenuBlocksTip, GuiTexts.KeyBlocks, GuiBlocks_Click));
                menu.Items.Add(GuiTileItem(GuiTexts.MenuNewFile, GuiTexts.MenuNewFileTip, null, NewGuiFile_Click));
                menu.Items.Add(GuiTileItem(GuiTexts.MenuModSettings, GuiTexts.MenuModSettingsTip, null, GuiModSettings_Click));
                return menu;
            }

            default:
                return null;
        }
    }

    private MenuItem ProjectTileItem(string header, string? path, IReadOnlyList<Explorer.ExplorerBranchTarget>? targets, Explorer.ExplorerBranchAction action)
    {
        var item = new MenuItem { Header = header };
        FillProjectSubmenu(item, path, targets, action);
        return item;
    }

    private static MenuItem GuiTileItem(string header, string tip, string? gesture, RoutedEventHandler onClick)
    {
        var item = new MenuItem { Header = header, ToolTip = tip, InputGestureText = gesture };
        item.Click += onClick;
        return item;
    }
}
