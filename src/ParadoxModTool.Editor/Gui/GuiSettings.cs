using System.IO;
using ParadoxModTool.Categories;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;

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

    /// <summary>
    /// GUI-Daten-Helfer im Hauptfenster (Menü, Leiste, Kachel, Strg+Umschalt+D): nur mit GUI-Werkzeugen und im Jomini-Format; klassische .gui-Dateien
    /// (HOI IV, EU IV) haben keine Datenausdrücke.
    /// </summary>
    public static bool DataHelperOffered => ToolsEnabled && !Layout.IsClassic;

    /// <summary>
    /// Vanilla-Entwürfe ("Für Mod bearbeiten", "In Mod übernehmen"): in beiden Formaten. Klassisch (HOI IV, EU IV) übernimmt der klassische Planer die
    /// geänderten Fenster (<see cref="GuiDesign.Designer.ClassicOverridePlanner"/>). Bleibt als Schalter, damit die Aufrufer eine Stelle haben.
    /// </summary>
    public static bool DraftsOffered => true;

    /// <summary>Inspektor "Als neuen Typ in Mod übernehmen": nur im Jomini-Format; HOI IV und EU IV kennen keine Typen (<see cref="GuiTexts.ClassicNoTypes"/>).</summary>
    public static bool TypeCopyOffered => !Layout.IsClassic;

    /// <summary>
    /// Bausteine (Fenster, Palette, Knopf, Kontextmenü, Strg+Umschalt+B): nur mit GUI-Werkzeugen und im Jomini-Format. Die Bausteine sind Jomini-Code;
    /// klassische Bausteine kommen mit Plan 3, bis dahin <see cref="GuiTexts.ClassicBlocksLater"/>.
    /// </summary>
    public static bool BlocksOffered => ToolsEnabled && !Layout.IsClassic;

    /// <summary>"Neue GUI-Datei": nur im Jomini-Format; klassisch käme die Datei nach &lt;Mod&gt;/gui, das HOI IV und EU IV nicht lesen (Plan 3).</summary>
    public static bool NewFileOffered => !Layout.IsClassic;

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
    public static string? DesignerLayout { get; set; }

    /// <summary>Format und Überschreibregeln der .gui-Dateien des aktiven Spiels (Profil, Abschnitt <c>gui</c>). Das Hauptfenster setzt es vor dem Index.</summary>
    public static GuiLayout Layout { get; set; } = GuiLayout.Default;

    /// <summary>Profil-Id des aktiven Spiels (z.B. <c>hoi4</c>); das Hauptfenster setzt sie mit <see cref="Layout"/>. Null ohne Profil.</summary>
    public static string? GameId { get; set; }

    /// <summary>
    /// Erklärtexte des klassischen Designers für das aktive Spiel (<c>profiles/guiexplain/classic.json</c>). Das Hauptfenster setzt sie mit
    /// <see cref="Layout"/>; für Jomini-Spiele und ohne Datei bleibt es bei <see cref="ClassicExplanations.Empty"/>.
    /// </summary>
    public static ClassicExplanations Explanations { get; set; } = ClassicExplanations.Empty;

    /// <summary>
    /// Das Spielprofil hat einen Abschnitt <c>gui</c>. Ohne ihn (Stellaris) gibt es keinen Designer, .gui-Dateien bleiben reiner Code. Standard: an, bis das
    /// Hauptfenster das Profil kennt.
    /// </summary>
    public static bool ProfileHasGui { get; set; } = true;

    /// <summary>
    /// <c>settings.txt</c> des Spiels im Benutzerordner (Elternordner des Mod-Ordners laut Spielprofil): klassisch die Voreinstellung für Auflösung und
    /// UI-Skalierung der Vorschau. Das Hauptfenster setzt ihn mit dem Profil; null ohne Mod-Ordner im Profil.
    /// </summary>
    public static string? GameSettingsPath { get; set; }

    /// <summary>
    /// Benutzerordner des Spiels (Elternordner des Mod-Ordners laut Spielprofil, <c>Documents\Paradox Interactive\&lt;Spiel&gt;</c>): Jomini liest dort
    /// <c>pdx_settings.txt</c> bzw. <c>pdx_settings.json</c> für Auflösung und UI-Skalierung der Vorschau. Das Hauptfenster setzt ihn mit dem Profil; null
    /// ohne Mod-Ordner im Profil.
    /// </summary>
    public static string? GameUserFolder { get; set; }

    /// <summary>
    /// "Überstand markieren" in Designer und Inspektor (beide Formate): an = jedes überstehende Element rot umranden, aus (Standard) = nur das gewählte. Gilt
    /// für die Sitzung; neue Vorschaufenster übernehmen den zuletzt gewählten Wert.
    /// </summary>
    public static bool MarkAllOverflow { get; set; }

    private static string? _previewLanguage;

    /// <summary>
    /// Sprache der Jomini-Vorschautexte, kanonisch (<c>l_german</c>); null = die Sprache des Spiels (<see cref="GuiPreviewScreen.GameLanguage"/>). Gilt für die
    /// Sitzung und wird nicht gespeichert; Designer und Inspektoren folgen über <see cref="PreviewLanguageChanged"/>.
    /// </summary>
    public static string? PreviewLanguage
    {
        get => _previewLanguage;
        set
        {
            var language = string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
            if (!string.Equals(_previewLanguage, language, StringComparison.Ordinal))
            {
                _previewLanguage = language;
                PreviewLanguageChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    /// <summary>Die Vorschausprache hat sich geändert: offene Inspektoren rechnen ihre Vorschau neu, Designer gleichen ihren Umschalter an.</summary>
    public static event EventHandler? PreviewLanguageChanged;

    private static IReadOnlyList<string> _previewLanguages = Array.Empty<string>();
    private static string? _previewLanguagesInstall;
    private static int _scanGeneration;

    /// <summary>
    /// Sprachen der Installation (kanonisch, <c>l_english</c> zuerst) für den Umschalter der Vorschau. Gefüllt wird sie im Hintergrund
    /// (<see cref="SetWorkspace"/>), weil der Textdienst die Ordner beim ersten Zugriff durchsucht; bis dahin ist sie leer. Der UI-Thread liest nur diese Liste.
    /// </summary>
    public static IReadOnlyList<string> PreviewLanguages => _previewLanguages;

    /// <summary>Die Sprachliste ist fertig (aus einem Hintergrundthread gemeldet): Designer füllen ihren Umschalter neu.</summary>
    public static event EventHandler? PreviewLanguagesChanged;

    /// <summary>Hintergrundlauf der letzten Sprachsuche (für Tests).</summary>
    internal static Task? LanguageScanTask { get; private set; }

    /// <summary>Sprachsuche des Textdienstes; ein Prüfstand kann sie ersetzen (für Tests).</summary>
    internal static Func<GuiDesign.Text.JominiTextService, IReadOnlyList<string>> LanguageScan { get; set; } = text => text.Languages;

    /// <summary>
    /// Sucht die Sprachen des Arbeitsbereichs im Hintergrund und meldet <see cref="PreviewLanguagesChanged"/>. Eine gewählte Vorschausprache, die der
    /// neue Arbeitsbereich nicht hat, fällt auf "Spiel" zurück, damit Umschalter und Optionen übereinstimmen. Ein anderes Spiel leert die Liste sofort;
    /// beim selben Spiel (gespeicherte .gui-Datei) bleibt die alte stehen, bis die neue da ist.
    /// </summary>
    private static void StartLanguageScan(GuiWorkspace? workspace)
    {
        var generation = Interlocked.Increment(ref _scanGeneration);
        var install = workspace?.Options.InstallDirectory;
        if (workspace?.Text is not { } text)
        {
            _previewLanguages = Array.Empty<string>();
            _previewLanguagesInstall = null;
            LanguageScanTask = null;
            return;
        }

        if (!string.Equals(install, _previewLanguagesInstall, StringComparison.OrdinalIgnoreCase))
        {
            _previewLanguages = Array.Empty<string>();
            _previewLanguagesInstall = install;
        }

        var scan = LanguageScan;
        LanguageScanTask = Task.Run(() =>
        {
            IReadOnlyList<string> found;
            try
            {
                found = scan(text);
            }
            catch (Exception)
            {
                found = Array.Empty<string>();
            }

            if (generation != Volatile.Read(ref _scanGeneration))
            {
                return;
            }

            _previewLanguages = found;
            if (_previewLanguage is { } chosen && !found.Contains(chosen))
            {
                PreviewLanguage = null;
            }

            PreviewLanguagesChanged?.Invoke(null, EventArgs.Empty);
        });
    }

    /// <summary>
    /// Lädt die Texte dieser Sprache (und Englisch) im Hintergrund vor, damit die Vorschau nicht darauf wartet. Ohne Arbeitsbereich oder Textdienst
    /// geschieht nichts; <see cref="GuiDesign.Text.JominiTextService.Warm"/> wirft nie.
    /// </summary>
    public static void WarmText(GuiWorkspace? workspace, string language)
    {
        if (workspace?.Text is { } text)
        {
            _ = Task.Run(() => text.Warm(language));
        }
    }

    /// <summary>Umschalter Code | Designer | Geteilt für diese Datei anbieten: .gui-Datei, Spiel mit gui-Abschnitt, Designer eingeschaltet.</summary>
    public static bool OffersDesigner(string path) =>
        DesignerOffered && ProfileHasGui && path.EndsWith(".gui", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Farbdialog für das Eigenschaftenfeld: bekommt die aktuelle Farbe, liefert die gewählte oder null (abgebrochen). Das Hauptfenster setzt ihn
    /// (WinForms-Farbdialog; das Editor-Projekt hat kein WinForms). Null = kein Knopf "Farbe wählen".
    /// </summary>
    public static Func<System.Windows.Media.Color, System.Windows.Media.Color?>? ColorPicker { get; set; }

    /// <summary>
    /// Sprite-Auswahl für das Eigenschaftenfeld (klassisch): bekommt den aktuellen Namen und liefert den gewählten oder null (abgebrochen). Die App
    /// setzt ihn (Fenster mit Vorschau). Null = kein Knopf "Auswählen…".
    /// </summary>
    public static Func<string?, string?>? SpritePicker { get; set; }

    /// <summary>
    /// "Sprite anlegen" für das Eigenschaftenfeld (klassisch): bekommt einen Namensvorschlag und liefert den Namen des angelegten Sprites oder null
    /// (abgebrochen). Die App setzt ihn. Null = kein Knopf "Sprite anlegen…".
    /// </summary>
    public static Func<string?, string?>? SpriteCreator { get; set; }

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
            || ((InstallDirectory ?? Workspace?.Options.InstallDirectory) is { Length: > 0 } install && GuiContexts.IsVanillaPath(install, path, Layout)));

    /// <summary>
    /// Engine-Fabrik für eine Vorschau: aus dem Arbeitsbereich, sonst (Index lädt, kein Spielpfad) eine Engine ohne Spieldaten im Format des Spiels,
    /// damit klassische Fenster wenigstens als schematische Kästen erscheinen. Jeder Aufruf der Fabrik liefert eine neue Engine.
    /// </summary>
    public static Func<IGuiLayoutEngine> LayoutEngineFactory(GuiWorkspace? workspace)
    {
        if (workspace is not null)
        {
            return workspace.CreateLayoutEngine;
        }

        // Das Format beim Anlegen der Fabrik festhalten: die Engine läuft später im Hintergrund, Layout kann sich bis dahin ändern.
        var layout = Layout;
        if (layout.IsClassic)
        {
            return () => new ClassicLayoutEngine(ClassicAssets.Build(Array.Empty<GuiSourceRoot>(), Array.Empty<GuiSourceFile>(), layout.SpriteFirstWins, layout.CaseSensitive));
        }

        return () => new GuiLayoutEngine(GuiRules.Empty, _ => null);
    }

    /// <summary>
    /// Statuszeile nach einer Vorschau: ohne Arbeitsbereich der Indexzustand; klassisch ohne <c>interface</c> im Installationsordner der Grund für die nur
    /// schematische Vorschau; sonst leer.
    /// </summary>
    public static string PreviewStatus =>
        Workspace is not { } workspace ? Status
        : ClassicInterfaceMissing(workspace.Options.InstallDirectory) ? GuiTexts.ClassicNoInterface(workspace.Options.InstallDirectory)
        : string.Empty;

    /// <summary>Klassisches Spiel, dessen Installationsordner kein <c>interface</c> enthält (falscher Spielpfad): keine Sprites, keine Schriften.</summary>
    public static bool ClassicInterfaceMissing(string? installDirectory) =>
        Layout.IsClassic && !(installDirectory is { Length: > 0 } install && Directory.Exists(Path.Combine(install, "interface")));

    public static void SetWorkspace(GuiWorkspace? workspace, string status)
    {
        Workspace = workspace;
        _status = status;
        StartLanguageScan(workspace);
        // Neuer Arbeitsbereich: die Sprache der Vorschau (gewählt, sonst die des Spiels) im Hintergrund vorladen; Englisch lädt der Arbeitsbereich selbst.
        if (workspace?.Text is not null)
        {
            WarmText(workspace, _previewLanguage ?? GuiPreviewScreen.GameLanguage());
        }

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

    /// <summary>
    /// Ausgeblendete bedingte Elemente (Schlüssel aus <see cref="GuiConditionalKeys"/>) des aktiven Spiels, für Designer und Inspektor. Nur über
    /// <see cref="SetConditionalHidden"/>, <see cref="ShowAllConditionals"/> und <see cref="LoadHiddenConditionals"/> ändern; die Vorschau rechnet im
    /// Hintergrund, daher bekommt sie eine Kopie in <see cref="LayoutOptions.HiddenConditionals"/>.
    /// </summary>
    public static HashSet<string> HiddenConditionals { get; } = new(StringComparer.Ordinal);

    /// <summary>Ein Schlüssel kam hinzu oder fiel weg: offene Designer und Inspektoren rechnen ihre Vorschau neu.</summary>
    public static event EventHandler? HiddenConditionalsChanged;

    /// <summary>Blendet ein bedingtes Element aus (<paramref name="hidden"/>) oder wieder ein; meldet nur bei einer Änderung.</summary>
    public static void SetConditionalHidden(string key, bool hidden)
    {
        if (hidden ? HiddenConditionals.Add(key) : HiddenConditionals.Remove(key))
        {
            HiddenConditionalsChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>"Alle einblenden": ohne Datei (null) alle Schlüssel, sonst nur die der angesehenen Datei; meldet nur bei einer Änderung.</summary>
    public static void ShowAllConditionals(string? viewedFile)
    {
        var prefix = viewedFile is null ? null : GuiConditionalKeys.FilePrefix(viewedFile);
        if (HiddenConditionals.RemoveWhere(key => prefix is null || key.StartsWith(prefix, StringComparison.Ordinal)) > 0)
        {
            HiddenConditionalsChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>Anzahl ausgeblendeter Schlüssel der angesehenen Datei; ohne Datei (null) alle.</summary>
    public static int HiddenCount(string? viewedFile)
    {
        if (viewedFile is null)
        {
            return HiddenConditionals.Count;
        }

        var prefix = GuiConditionalKeys.FilePrefix(viewedFile);
        return HiddenConditionals.Count(key => key.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>Gespeicherte Schlüssel des Spiels beim Start übernehmen (ersetzt die bisherigen); meldet einmal <see cref="HiddenConditionalsChanged"/>.</summary>
    public static void LoadHiddenConditionals(IEnumerable<string>? keys)
    {
        HiddenConditionals.Clear();
        HiddenConditionals.UnionWith(keys ?? Enumerable.Empty<string>());
        HiddenConditionalsChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Gespeicherte Einstellungen beim Start übernehmen; meldet einmal <see cref="Changed"/>, damit offene Tabs Ansicht und Aufteilung anwenden.</summary>
    public static void Load(bool toolsEnabled, bool designerEnabled, GuiViewMode defaultMode, string? layout, bool breadcrumbEnabled, bool shownInEnabled)
    {
        ToolsEnabled = toolsEnabled;
        DesignerEnabled = designerEnabled;
        DefaultMode = defaultMode;
        DesignerLayout = layout;
        BreadcrumbEnabled = breadcrumbEnabled;
        ShownInEnabled = shownInEnabled;
        Changed?.Invoke(null, EventArgs.Empty);
    }
}
