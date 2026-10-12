using System.Windows.Controls;
using ParadoxModTool.Editor;
using ParadoxModTool.SaveGames;
using ParadoxModTool.Settings;
using MenuItem = System.Windows.Controls.MenuItem;

namespace ParadoxModTool.App;

/// <summary>Texte der Menüleiste (<see cref="MenuTexts"/>) und "Alle speichern"; die kleine Werkzeugleiste beschriftet MainWindow.Layout.cs.</summary>
public partial class MainWindow
{
    /// <summary>Einmal im Konstruktor. Tooltips, die sich zur Laufzeit ändern (Rückgängig, Hilfe), setzen ihre eigenen Methoden auch aus <see cref="MenuTexts"/>.</summary>
    private void InitializeMenuTexts()
    {
        WindowMenuItem.Header = MenuTexts.Games;
        SetMenuText(ModsMenuItem, MenuTexts.Mods, MenuTexts.ModsTip);
        // Interface-Rework 2: in beiden Layouts "Mod-Übersicht"; das Fenster selbst heißt weiter "Meine Mods".
        SetMenuText(MyModsMenuItem, MenuTexts.Overview, MenuTexts.OverviewTip);
        SetMenuText(ProjectMenuItem, MenuTexts.Project, MenuTexts.ProjectTip);
        ProjectCopyMenuItem.Header = MenuTexts.ProjectCopy;
        ProjectMoveMenuItem.Header = MenuTexts.ProjectMove;
        ProjectCompareMenuItem.Header = MenuTexts.ProjectCompare;
        SetMenuText(ProjectSyncMenuItem, MenuTexts.ProjectSync, MenuTexts.ProjectSyncTip);
        SetMenuText(SettingsMenuItem, SettingsTexts.MenuSettings, SettingsTexts.MenuSettingsTip);
        SetMenuText(CuToolMenuItem, MenuTexts.CuTool, MenuTexts.CuToolTip);
        SetMenuText(PatchRecipesMenuItem, MenuTexts.PatchRecipes, MenuTexts.PatchRecipesTip);
        SetMenuText(GeneratorPackagesMenuItem, MenuTexts.GeneratorPackages, MenuTexts.GeneratorPackagesTip);
        SetMenuText(PackageEditorMenuItem, MenuTexts.PackageEditor, MenuTexts.PackageEditorTip);
        SetMenuText(ModCompatMenuItem, MenuTexts.ModCompat, MenuTexts.ModCompatTip);
        SetMenuText(ErrorLogMenuItem, ParadoxModTool.ErrorLog.ErrorLogTexts.Menu, ParadoxModTool.ErrorLog.ErrorLogTexts.MenuTip);
        SetMenuText(DesyncMenuItem, ParadoxModTool.Desync.DesyncTexts.Menu, ParadoxModTool.Desync.DesyncTexts.MenuTip);
        SetMenuText(SaveExplorerMenuItem, SaveExplorerTexts.Menu, SaveExplorerSettings is null ? SaveExplorerTexts.NotAvailable : SaveExplorerTexts.MenuTip);
        SetMenuText(CompatchMenuItem, MenuTexts.Compatch, MenuTexts.CompatchTip);
        SetMenuText(ScriptDocsMenuItem, MenuTexts.ScriptDocs, MenuTexts.ScriptDocsTip);
        SetMenuText(TemplatesMenuItem, MenuTexts.Templates, MenuTexts.TemplatesTip);
        HelpMenuItem.Header = MenuTexts.Help;
        CheckUpdatesMenuItem.Header = MenuTexts.CheckUpdates;
        ReportBugMenuItem.Header = MenuTexts.ReportBug;
        ChangelogMenuItem.Header = MenuTexts.Changelog;
        AboutMenuItem.Header = MenuTexts.About;
        SetMenuText(SupportMenuItem, MenuTexts.Support, MenuTexts.SupportTip);
        SetMenuText(SponsorMenuItem, MenuTexts.SupportGitHub, MenuTexts.SupportGitHubTip);
        SetMenuText(KofiMenuItem, MenuTexts.SupportKofi, MenuTexts.SupportKofiTip);
        HelpSupportHeader.Header = MenuTexts.Support;
        HelpSupportHeader.ToolTip = MenuTexts.SupportTip;
        SetMenuText(HelpSponsorMenuItem, MenuTexts.SupportGitHub, MenuTexts.SupportGitHubTip);
        SetMenuText(HelpKofiMenuItem, MenuTexts.SupportKofi, MenuTexts.SupportKofiTip);
        ApplyRepositoryAvailability();
        InitializeUpdateBarTexts();

        UpdateBugTrackerButton();
        ApplyWindowLayout();
    }

    private static void SetMenuText(MenuItem item, string header, string tip)
    {
        item.Header = header;
        item.ToolTip = tip;
    }

    /// <summary>
    /// "Alle speichern" (Knopf im Arbeitsbereich, Strg+Umschalt+S): speichert jedes geänderte Dokument beider Arbeitsbereiche über denselben
    /// Weg wie "Speichern" (Rückgängig-Schritt, Recovery, GUI-Index). Bricht der Nutzer "Speichern unter" ab, bleibt nur dieses Dokument
    /// ungespeichert. Danach ist in jedem Arbeitsbereich wieder der vorher aktive Tab ausgewählt.
    /// </summary>
    private void SaveAllDocuments()
    {
        var previouslyActive = AllPanes.Select(pane => (Pane: pane, Document: pane.ActiveDocument)).ToList();
        foreach (var (pane, document) in WorkspaceSave.DocumentsToSave(AllPanes))
        {
            pane.ActivateDocument(document);
            SaveActiveDocumentOf(pane);
        }

        foreach (var (pane, document) in previouslyActive)
        {
            if (document is not null && pane.Documents.Contains(document))
            {
                pane.ActivateDocument(document);
            }
        }
    }
}
