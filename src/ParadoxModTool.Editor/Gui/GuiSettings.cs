using ParadoxModTool.Categories;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Index;

namespace ParadoxModTool.Editor.Gui;

public enum GuiViewMode
{
    Code,
    Designer,
    Split,
}

/// <summary>
/// Datenquelle und Schalter für den GUI-Designer; gilt für alle Arbeitsbereiche eines Fensters (Muster wie <see cref="ScopeSettings"/>).
/// Das Hauptfenster setzt den Arbeitsbereich (Index, Katalog, Texturen) und die Schalter, die Editoren lesen sie bei Bedarf.
/// </summary>
public static class GuiSettings
{
    /// <summary>Index, Katalog und Texturen von Spiel und aktiver Mod; null, solange er lädt oder kein Spielpfad bekannt ist.</summary>
    public static GuiWorkspace? Workspace { get; private set; }

    private static string? _status;

    /// <summary>Kurzer Zustandstext zum GUI-Index (für die Statuszeile des Designers); vor dem ersten Laden "wird geladen".</summary>
    public static string Status => _status ?? GuiTexts.IndexLoading;

    /// <summary>Umschalter Code | Designer | Geteilt bei .gui-Dateien anbieten. Standard: an.</summary>
    public static bool DesignerEnabled { get; private set; } = true;

    /// <summary>
    /// Hauptschalter der GUI-Werkzeuge. Aus: kein Index, kein Designer, keine Brotkrumen, kein "Inspizieren" und kein "Wird angezeigt in".
    /// Die einzelnen Schalter behalten dabei ihren Wert; wirksam sind die <c>…Offered</c>-Eigenschaften.
    /// </summary>
    public static bool ToolsEnabled { get; private set; } = true;

    /// <summary>Umschalter Code | Designer | Geteilt wird angeboten (Hauptschalter und eigener Schalter an).</summary>
    public static bool DesignerOffered => ToolsEnabled && DesignerEnabled;

    /// <summary>Brotkrumen-Leiste wird gezeigt (Hauptschalter und eigener Schalter an).</summary>
    public static bool BreadcrumbOffered => ToolsEnabled && BreadcrumbEnabled;

    /// <summary>"Wird angezeigt in (GUI)" wird angeboten (Hauptschalter und eigener Schalter an).</summary>
    public static bool ShownInOffered => ToolsEnabled && ShownInEnabled;

    public static void SetToolsEnabled(bool enabled)
    {
        if (ToolsEnabled != enabled)
        {
            ToolsEnabled = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>Ansicht für neu aktivierte .gui-Tabs: die zuletzt gewählte.</summary>
    public static GuiViewMode DefaultMode { get; private set; } = GuiViewMode.Code;

    /// <summary>Aufteilung der Bereiche als Text: "Designeranteil;Baumbreite;Eigenschaftenhöhe" (invariante Zahlen).</summary>
    public static string? Layout { get; set; }

    /// <summary>
    /// Farbdialog für das Eigenschaftenfeld: bekommt die aktuelle Farbe, liefert die gewählte oder null (abgebrochen). Das Hauptfenster setzt ihn
    /// (WinForms-Farbdialog; das Editor-Projekt hat kein WinForms). Null = kein Knopf "Farbe wählen".
    /// </summary>
    public static Func<System.Windows.Media.Color, System.Windows.Media.Color?>? ColorPicker { get; set; }

    /// <summary>
    /// GUI-Daten-Helfer für das Eigenschaftenfeld: bekommt das gewählte Element (für den Datenkontext) und liefert den gebauten Ausdruck oder null
    /// (abgebrochen). Das Hauptfenster setzt ihn, weil es die Datentypen lädt. Null = kein Knopf "Daten-Helfer".
    /// </summary>
    public static Func<GuiDesign.Syntax.GuiNode, string?>? DataHelper { get; set; }

    /// <summary>
    /// Bedingungs-Helfer für <c>visible</c>, <c>enabled</c>, <c>down</c> und <c>checked</c>: bekommt Element und aktuellen Feldwert und liefert das Ergebnis
    /// oder null (abgebrochen). Null = diese Felder nutzen den Daten-Helfer wie andere.
    /// </summary>
    public static Func<GuiDesign.Syntax.GuiNode, string, GuiHelperResult?>? ConditionHelper { get; set; }

    /// <summary>
    /// Bausteine des aktiven Spiels für die Palette (alle Ebenen, wirksame Einträge). Das Hauptfenster setzt ihn, weil es aktive Mod und Spiel kennt;
    /// aufgerufen wird er auf dem UI-Thread. Null = keine Bausteingruppe.
    /// </summary>
    public static Func<IReadOnlyList<GuiDesign.Blocks.GuiBlock>>? BlockSource { get; set; }

    /// <summary>
    /// Fenster "Bausteine" für einen Palette-Eintrag: bekommt den Namen, liefert den erzeugten Code oder null (abgebrochen). Das Hauptfenster setzt ihn.
    /// </summary>
    public static Func<string, string?>? BlockPicker { get; set; }

    /// <summary>Eigene Bausteine wurden gespeichert, gelöscht oder kopiert: die Palette liest ihre Bausteingruppe neu.</summary>
    public static event EventHandler? BlocksChanged;

    public static void NotifyBlocksChanged() => BlocksChanged?.Invoke(null, EventArgs.Empty);

    public static event EventHandler? Changed;

    /// <summary>
    /// Installationsordner des Spiels (mit <c>game</c>, <c>jomini</c>, <c>clausewitz</c>). Das Hauptfenster setzt ihn vor jedem Aufbau des GUI-Index,
    /// damit der Schreibschutz für Vanilla-.gui schon greift, solange der Index noch lädt.
    /// </summary>
    public static string? InstallDirectory { get; set; }

    /// <summary>
    /// Vanilla-.gui-Datei (Spielordner laut Herkunft oder im Installationsordner, also auch jomini und clausewitz): im Code-Editor
    /// schreibgeschützt, außer als Entwurf; gespeichert wird sie nie (Plan-Ruling 3).
    /// </summary>
    public static bool IsVanillaGui(OpenDocument? document) =>
        document?.FilePath is { } path
        && path.EndsWith(".gui", StringComparison.OrdinalIgnoreCase)
        && (document.Origin == FileOrigin.Vanilla
            || ((InstallDirectory ?? Workspace?.Options.InstallDirectory) is { Length: > 0 } install && GuiContexts.IsVanillaPath(install, path)));

    public static void SetWorkspace(GuiWorkspace? workspace, string status)
    {
        Workspace = workspace;
        _status = status;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static void SetDesignerEnabled(bool enabled)
    {
        if (DesignerEnabled != enabled)
        {
            DesignerEnabled = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    public static void SetDefaultMode(GuiViewMode mode) => DefaultMode = mode;

    /// <summary>Brotkrumen-Leiste über dem Code von .gui-Dateien. Standard: an.</summary>
    public static bool BreadcrumbEnabled { get; private set; } = true;

    /// <summary>Kontextmenü "Wird angezeigt in (GUI)" in Skriptdateien. Standard: an. Wird beim Öffnen des Menüs gelesen.</summary>
    public static bool ShownInEnabled { get; private set; } = true;

    public static void SetBreadcrumbEnabled(bool enabled)
    {
        if (BreadcrumbEnabled != enabled)
        {
            BreadcrumbEnabled = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    public static void SetShownInEnabled(bool enabled) => ShownInEnabled = enabled;

    /// <summary>Gespeicherte Einstellungen beim Start übernehmen; meldet einmal <see cref="Changed"/>, damit offene Tabs Ansicht und Aufteilung anwenden.</summary>
    public static void Load(bool toolsEnabled, bool designerEnabled, GuiViewMode defaultMode, string? layout, bool breadcrumbEnabled, bool shownInEnabled)
    {
        ToolsEnabled = toolsEnabled;
        DesignerEnabled = designerEnabled;
        DefaultMode = defaultMode;
        Layout = layout;
        BreadcrumbEnabled = breadcrumbEnabled;
        ShownInEnabled = shownInEnabled;
        Changed?.Invoke(null, EventArgs.Empty);
    }
}
