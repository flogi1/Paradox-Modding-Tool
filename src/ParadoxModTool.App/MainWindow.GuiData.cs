using System.IO;
using System.Windows;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.Conditions;
using ParadoxModTool.GuiDesign.DataTypes;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.App;

/// <summary>
/// GUI-Daten-Helfer (siehe docs/superpowers/specs/2026-09-26-gui-data-helper-design.md und ...-gui-condition-helper-design.md): lädt den Datentyp-Dump des Spiels (mit Zwischenspeicher und
/// Veraltet-Prüfung), bestimmt den Start aus dem Datenkontext und fügt den gebauten Ausdruck im Code-Editor oder im Eigenschaftenfeld des Designers ein.
/// </summary>
public partial class MainWindow
{
    private void GuiDataHelper_Click(object sender, RoutedEventArgs e) => _ = OpenGuiDataHelperAsync();

    /// <summary>Ordner, Installationsordner und Zwischenspeicher des aktiven Spiels; ohne Spielprofil ein leeres Ergebnis ("fehlt").</summary>
    private DataTypeLoad LoadDataTypes()
    {
        if (_activeProfile is not { } profile)
        {
            return DataTypeSource.Load(null, null, string.Empty);
        }

        var cache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ParadoxModTool",
            "games",
            profile.Id,
            "gui",
            "data-types.json");
        var install = GuiSettings.InstallDirectory ?? ComputeGameFilesGuess(_gameExecutablePath);
        return DataTypeSource.Load(DataTypeSource.FolderFor(profile.DefaultModRootTemplate), install, cache);
    }

    /// <summary>Menü, Symbolleiste, Strg+Umschalt+D: Start aus dem Datenkontext an der Cursorstelle einer .gui-Datei, Einfügen an der Cursorstelle.</summary>
    private async Task OpenGuiDataHelperAsync()
    {
        if (!GuiSettings.ToolsEnabled)
        {
            return;
        }

        var pane = ActivePane;
        var load = await Task.Run(LoadDataTypes);
        var catalog = GuiSettings.Workspace?.Catalog ?? new GuiPropertyCatalog();
        string? start = null;
        ConditionTarget? target = null;
        if (pane.CurrentFilePath is { } path && path.EndsWith(".gui", StringComparison.OrdinalIgnoreCase))
        {
            var document = GuiParser.Parse(pane.CurrentText);
            var caret = pane.CurrentCaretOffset;
            if (document.FindDeepestAt(caret) is { } node)
            {
                start = DataContextResolver.StartAt(node, load.Database, catalog);
            }

            target = ConditionTarget.Find(document, caret);
        }

        var window = new GuiDataHelperWindow(load, catalog, start, target?.ValueText) { Owner = this };
        if (window.ShowDialog() != true || window.Result is not { } expression)
        {
            return;
        }

        // Ergebnis aus dem Tab "Bedingung" ersetzt genau den Wert an der Cursorstelle; eines aus "Kette"/"Nachschlagen" wird wie bisher eingefügt.
        if (ConditionTarget.ReplaceRange(target, window.ConditionTabActive) is { } range)
        {
            pane.SelectRange(range.Start, range.Length);
        }

        pane.InsertAtCaret(expression);
    }

    /// <summary>Knopf "Daten-Helfer" an visible, enabled, down und checked im Designer: Tab "Bedingung" mit dem Feldwert.</summary>
    private GuiHelperResult? OpenGuiConditionHelperFor(GuiNode element, string value)
    {
        var load = LoadDataTypes();
        var catalog = GuiSettings.Workspace?.Catalog ?? new GuiPropertyCatalog();
        var window = new GuiDataHelperWindow(load, catalog, DataContextResolver.StartAt(element, load.Database, catalog), value) { Owner = this };
        return window.ShowDialog() == true && window.Result is { } result ? new GuiHelperResult(result, window.ConditionTabActive) : null;
    }

    /// <summary>Knopf "Daten-Helfer" im Eigenschaftenfeld des Designers: Start aus dem Datenkontext des Elements, Ergebnis zurück ins Feld.</summary>
    private string? OpenGuiDataHelperFor(GuiNode element)
    {
        var load = LoadDataTypes();
        var catalog = GuiSettings.Workspace?.Catalog ?? new GuiPropertyCatalog();
        var window = new GuiDataHelperWindow(load, catalog, DataContextResolver.StartAt(element, load.Database, catalog)) { Owner = this };
        return window.ShowDialog() == true ? window.Result : null;
    }
}
