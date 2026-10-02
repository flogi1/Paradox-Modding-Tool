using System.IO;
using System.Windows;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Blocks;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// GUI-Bausteine (docs/superpowers/specs/2026-09-27-gui-building-blocks-design.md): Bibliothek des aktiven Spiels laden, das Fenster "Bausteine" aus
/// GUI-Menü, Strg+Umschalt+B, Kontextmenü und GUI-Leiste öffnen und das Ergebnis am Cursor einfügen; "Neue GUI-Datei" erzeugt den Inhalt aus einem Baustein.
/// </summary>
public partial class MainWindow
{
    private void GuiBlocks_Click(object sender, RoutedEventArgs e) => OpenGuiBlocks();

    /// <summary>Spiel-ID und PMT-Version für Export (Manifest) und Import (Warnung bei fremdem Spiel).</summary>
    private static GuiBlockWindowOptions WithGame(GuiBlockWindowOptions options) =>
        options with { GameId = GameContext.Profile?.Id, PmtVersion = AppInfo.Version.ToString() };

    /// <summary>
    /// Die drei Ebenen des aktiven Spiels: mitgeliefert im Programmordner, eigene für das Spiel, eigene für die aktive Mod (Plan-Ruling 11: je Spiel
    /// getrennt; ohne Spielprofil keine mitgelieferten, ohne aktive Mod keine Mod-Ebene).
    /// </summary>
    private GuiBlockLibrary LoadGuiBlockLibrary()
    {
        var shipped = GameContext.Profile is { } profile
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles", "guiblocks", profile.Id)
            : null;
        var game = Path.Combine(GameContext.Repository.GameDirectory(GameContext.GameId), "guiblocks");
        var mod = _mods?.ActiveMod is { } active ? Path.Combine(_mods.Registry.DataDirectory(active.Id), "guiblocks") : null;
        return new GuiBlockLibrary(shipped, game, mod);
    }

    /// <summary>Fenster "Bausteine" für den aktiven Arbeitsbereich; nur mit eingeschalteten GUI-Werkzeugen (Plan-Ruling 10).</summary>
    private void OpenGuiBlocks()
    {
        if (!GuiSettings.ToolsEnabled)
        {
            return;
        }

        var pane = ActivePane;
        var window = new GuiBlockWindow(
            LoadGuiBlockLibrary(),
            GuiSettings.Workspace?.Index,
            _activeHighlighting,
            WithGame(new GuiBlockWindowOptions(null, null, GuiTexts.BlocksInsert, pane.HasActiveDocument)))
        {
            Owner = this,
        };
        if (window.ShowDialog() == true && window.Result is { } text)
        {
            pane.InsertGuiBlock(text);
        }
        else if (window.FileToOpen is { } path)
        {
            var document = pane.LoadFile(path, _activeHighlighting);
            UpdateHasNote(document);
        }
    }

    /// <summary>
    /// "Als Baustein speichern": Dialog mit dem Code, den Gruppen der Bibliothek und der Mod-Ebene nur mit aktiver Mod; vor dem Überschreiben eines
    /// eigenen Bausteins gleichen Namens wird gefragt. Danach liest die Palette ihre Bausteingruppe neu.
    /// </summary>
    private void SaveGuiBlock(string code)
    {
        var library = LoadGuiBlockLibrary();
        var groups = GuiBlockLibrary.StandardGroups
            .Concat(library.Effective.Select(b => b.Group))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var dialog = new GuiSaveBlockWindow(code, groups, library.DirectoryOf(GuiBlockLayer.Mod) is not null, _activeHighlighting) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (!GuiSaveBlockWindow.ConfirmOverwrite(library, dialog.BlockName, dialog.Layer, name =>
            MessageBox.Show(this, GuiTexts.BlocksSaveOverwrite(name), GuiTexts.BlocksSaveTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes))
        {
            return;
        }

        try
        {
            library.Save(dialog.ComposedText, dialog.BlockName, dialog.Layer);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show(this, GuiTexts.BlocksSaveFailed(ex.Message), GuiTexts.BlocksSaveTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        GuiSettings.NotifyBlocksChanged();
    }

    /// <summary>
    /// Baustein aus der Designer-Palette (Teil-2-Ruling 2): Fenster "Bausteine" mit dem Eintrag vorgewählt; liefert den erzeugten Code. "Datei öffnen"
    /// öffnet die Datei im aktiven Arbeitsbereich und fügt nichts ein.
    /// </summary>
    private string? PickGuiBlock(string name)
    {
        var window = new GuiBlockWindow(
            LoadGuiBlockLibrary(),
            GuiSettings.Workspace?.Index,
            _activeHighlighting,
            WithGame(new GuiBlockWindowOptions(name, null, GuiTexts.BlocksInsert, CanAct: true)))
        {
            Owner = this,
        };
        if (window.ShowDialog() == true)
        {
            return window.Result;
        }

        if (window.FileToOpen is { } path)
        {
            var document = ActivePane.LoadFile(path, _activeHighlighting);
            UpdateHasNote(document);
        }

        return null;
    }

    /// <summary>
    /// Inhalt einer neuen GUI-Datei aus einem Baustein (Plan-Ruling 7): ohne Parameter und Wiederholungen direkt, sonst über das Fenster "Bausteine" mit
    /// dem Widget-Namen aus dem Dateinamen als Vorbelegung für <c>name</c>. Abbrechen gibt <c>null</c>.
    /// </summary>
    private string? GuiBlockFileText(GuiBlockLibrary library, GuiBlock block, string fileName)
    {
        if (block.Parameters.Count == 0 && block.Repeats.Count == 0)
        {
            return GuiBlockRenderer.Render(block, GuiBlockValues.Defaults(block));
        }

        var presets = new Dictionary<string, string> { ["name"] = GuiBlockNames.WidgetNameFor(fileName) };
        var window = new GuiBlockWindow(
            library,
            GuiSettings.Workspace?.Index,
            _activeHighlighting,
            WithGame(new GuiBlockWindowOptions(block.Name, presets, GuiTexts.NewFileCreate, CanAct: true)))
        {
            Owner = this,
        };
        return window.ShowDialog() == true ? window.Result : null;
    }
}
