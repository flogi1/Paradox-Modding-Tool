using System.IO;
using System.Windows;
using ParadoxModTool.Editor;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Custom GUI im klassischen GUI-Designer (EU IV, Plan 3 Task 7): "Custom GUI anlegen" an einem Element und "In Vanilla-Fenster einbauen…" an einem
/// obersten Mod-Fenster. Beide schreiben ihre Dateien (.gui, <c>common/custom_gui</c>, Lokalisierung) als einen Rückgängig-Schritt
/// (<see cref="ApplyScriptFiles"/>) und öffnen danach die Skriptdatei am ersten neuen Eintrag.
/// </summary>
public partial class MainWindow
{
    /// <summary>Verbindet das Eigenschaftenfeld mit den Custom-GUI-Knöpfen; nur für Spiele mit Custom GUI (EU IV).</summary>
    private void WireCustomGuiTools(bool classic)
    {
        var custom = classic && GuiSettings.Layout.ScriptWiring == GuiScriptWiring.CustomGui;
        GuiSettings.CustomGuiCreator = custom ? CreateCustomGui : null;
        GuiSettings.CustomGuiBuilder = custom ? BuildCustomGuiInto : null;
    }

    /// <summary>"Custom GUI anlegen": plant für das Element (wie es auf der Platte steht), schreibt und öffnet die Skriptdatei.</summary>
    private bool CreateCustomGui(GuiNode element, string guiPath)
    {
        var title = GuiTexts.CustomGuiCreate;
        if (CustomTarget(title) is not { } target || OnDisk(element, guiPath, title) is not { } found)
        {
            return false;
        }

        var (index, modRoot, _) = target;
        CustomGuiPlan plan;
        try
        {
            plan = CustomGuiDraft.ForElement(found.Node, found.Document, guiPath, modRoot, index, ReadScriptFile, GameLanguage());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, GuiTexts.TakeOverFailed(ex.Message), title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!plan.CanApply)
        {
            MessageBox.Show(this, string.Join("\n", plan.Problems), title, MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        return ApplyScriptFiles(
            plan.Files, plan.FocusPath, plan.FocusLine, GuiTexts.CustomGuiAction(ClassicWindows.NameOf(found.Node)), modRoot, title, GuiTexts.CustomGuiSnapshotNote);
    }

    /// <summary>
    /// "In Vanilla-Fenster einbauen…": Gastfenster und "Original entfernen" im Dialog, dann der Plan (Übernahme des Gastfensters wie "In Mod übernehmen");
    /// zieht dabei ein Fenster aus einer verlierenden Mod-Datei um, wird vorher gefragt.
    /// </summary>
    private bool BuildCustomGuiInto(GuiNode window, string guiPath)
    {
        var title = GuiTexts.CustomGuiHostTitle;
        if (CustomTarget(title) is not { } target)
        {
            return false;
        }

        var (index, modRoot, workspace) = target;
        var name = ClassicWindows.NameOf(window);
        var dialog = new CustomGuiHostWindow(name, index.Supported) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.SelectedHost is not { } host || OnDisk(window, guiPath, title) is not { } found)
        {
            return false;
        }

        CustomGuiPlan plan;
        try
        {
            var mod = RegisteredGuiMod(modRoot);
            var layout = CurrentGuiLayout;
            var targets = GuiModTargets.For(modRoot, mod?.GuiOverrideFile, mod?.GuiTypesFile, layout.FirstWins, classic: true);
            plan = CustomGuiDraft.BuildInto(
                found.Node, host, found.Document, guiPath, dialog.RemoveOriginal, workspace.Index, targets, index, ReadScriptFile, GameLanguage(),
                path => File.Exists(path) ? GuiTextReader.Read(path).EncodingName : null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, GuiTexts.TakeOverFailed(ex.Message), title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!plan.CanApply)
        {
            MessageBox.Show(this, string.Join("\n", plan.Problems), title, MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        if (plan.Moves.Count > 0 && MessageBox.Show(
                this, GuiTexts.ClassicMoveConfirm(plan.Moves, CurrentGuiLayout.FirstWins), title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return false;
        }

        // Hinweise der Übernahme (z. B. eine Abhängigkeit gewinnt, dann erscheint das eingebaute Fenster nicht): wie in der Übersicht von "In Mod übernehmen".
        if (plan.Warnings.Count > 0 && MessageBox.Show(
                this, string.Join("\n", plan.Warnings) + "\n\n" + GuiTexts.CustomGuiWarningsConfirm, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return false;
        }

        return ApplyScriptFiles(
            plan.Files, plan.FocusPath, plan.FocusLine, GuiTexts.CustomGuiBuildAction(name, host.WindowName), modRoot, title, GuiTexts.CustomGuiSnapshotNote);
    }

    /// <summary>Fertiger Custom-GUI-Index, Mod-Ordner und Arbeitsbereich, passend zueinander; sonst Hinweis und <see langword="null"/>.</summary>
    private (CustomGuiIndex Index, string ModRoot, GuiWorkspace Workspace)? CustomTarget(string title)
    {
        if (GuiSettings.Workspace is not { Classic.CustomGuis: { IsCompletedSuccessfully: true } task } workspace)
        {
            MessageBox.Show(this, GuiTexts.DraftNoIndex, title, MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        if (CurrentGuiModRoots().ModRoot is not { } modRoot)
        {
            MessageBox.Show(this, GuiTexts.NewFileNoMod, title, MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        if (!GuiIndexFitsMod(workspace, modRoot))
        {
            MessageBox.Show(this, GuiTexts.DraftNoIndex, title, MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        return (task.Result, modRoot, workspace);
    }

    /// <summary>
    /// Das Element so, wie es auf der Platte steht: Die Pläne ändern den Text der Datei, nicht den des Designers. Ein Tab mit ungespeicherten Änderungen
    /// oder ein Element, das nicht mehr an derselben Stelle steht, ergibt einen Hinweis und <see langword="null"/>.
    /// </summary>
    private (GuiNode Node, GuiDocument Document)? OnDisk(GuiNode element, string path, string title)
    {
        var file = Path.GetFileName(path);
        if (AllPanes.SelectMany(p => p.Documents).Any(d => d.IsModified && EditorPane.PathsEqual(d.FilePath, path)))
        {
            MessageBox.Show(this, GuiTexts.TakeOverOpenModified(file), title, MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        try
        {
            var document = GuiParser.Parse(GuiTextReader.Read(path).Text);
            // Zuerst dieselbe Stelle; weichen die Texte nur in Zeilenenden oder BOM ab, derselbe Pfad im Baum (wie GuiDraft.LineInCopy), mit demselben Namen.
            bool Same(GuiNode n) =>
                n.Kind == element.Kind && string.Equals(n.Name, element.Name, StringComparison.Ordinal) && ClassicWindows.NameOf(n) == ClassicWindows.NameOf(element);
            var node = document.Root.Descendants().FirstOrDefault(n => n.Span == element.Span && Same(n))
                ?? (GuiNodePath.Of(element).FindExact(document) is { } byPath && Same(byPath) ? byPath : null);
            if (node is not null)
            {
                return (node, document);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, GuiTexts.TakeOverFailed(ex.Message), title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        MessageBox.Show(this, GuiTexts.CustomGuiElementMoved(ClassicWindows.NameOf(element), file), title, MessageBoxButton.OK, MessageBoxImage.Information);
        return null;
    }

    /// <summary>Text einer vorhandenen Datei (erkannte Kodierung, ohne BOM) oder <see langword="null"/>.</summary>
    private static string? ReadScriptFile(string path) => File.Exists(path) ? GuiTextReader.Read(path).Text : null;

    /// <summary>Spielsprache aus <c>settings.txt</c> (<c>l_german</c>) oder <see langword="null"/>.</summary>
    private static string? GameLanguage() => GuiSettings.GameSettingsPath is { } settings ? ClassicScreenSettings.TryReadLanguage(settings) : null;
}
