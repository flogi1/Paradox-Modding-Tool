namespace ParadoxModTool.GameProfiles;

/// <summary>
/// Eine Ordnerkategorie innerhalb der Mod-/Spielstruktur (z. B. "common", "gui"),
/// inklusive Anzeigefarbe für die Tab-Kategorie-Anzeige im Editor
/// (siehe Architektur-Spezifikation, Abschnitt 5).
/// </summary>
public sealed class FolderCategory
{
    public string Path { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DefaultColor { get; set; } = "#808080";
    public string? Description { get; set; }
}

/// <summary>Ordnet eine Dateiendung ihrem Parser/Highlighter-Bezeichner zu.</summary>
public sealed class FileTypeMapping
{
    public string Extension { get; set; } = string.Empty;
    public string Parser { get; set; } = string.Empty;
    public string? DefaultEncoding { get; set; }
}

/// <summary>Ein Eintrag im Spielstart-Untermenü der Werkzeugleiste.</summary>
public sealed class LaunchOption
{
    public string Label { get; set; } = string.Empty;
    public string Args { get; set; } = string.Empty;
}

/// <summary>Ein Eintrag der Überschreib-Regeltabelle eines Spielprofils (siehe <c>ParadoxModTool.ModCompat</c>).</summary>
public sealed class OverrideRuleEntry
{
    public string Folder { get; set; } = string.Empty;
    public string Behavior { get; set; } = "PerKey";
    public bool Certain { get; set; } = true;
}

/// <summary>
/// Datengetriebenes Profil eines Paradox-Spiels — der zentrale Baustein der
/// Mehrspiel-Fähigkeit des Tools (siehe Architektur-Spezifikation, Abschnitt 4).
/// Wird aus einer JSON-Datei unter profiles/ geladen, ohne dass dafür Code
/// geändert werden muss.
/// </summary>
public sealed class GameProfile
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string EngineFamily { get; set; } = string.Empty;
    public string DefaultModRootTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Pfad der Mod-Metadaten-Datei relativ zum Mod-Ordner (Launcher-v2-JSON-Format, z. B. <c>.metadata/metadata.json</c>),
    /// oder <c>ParadoxModTool.Mods.ModMetadata.ClassicSiblingMetadataFile</c> ("*.mod") für das klassische Format
    /// (Geschwisterdatei &lt;Ordnername&gt;.mod NEBEN dem Mod-Ordner, z. B. EU IV).
    /// </summary>
    public string MetadataFile { get; set; } = string.Empty;
    public string LoadOrderRule { get; set; } = string.Empty;
    public List<FolderCategory> FolderCategories { get; set; } = new();
    public List<FileTypeMapping> FileTypes { get; set; } = new();
    public List<LaunchOption> LaunchOptions { get; set; } = new();
    /// <summary>
    /// Ordnername der Vanilla-Historie im Speicherort für Snapshots (z. B. "EU V Vanilla History"). Ohne Angabe
    /// "&lt;Anzeigename&gt; Vanilla History". Je Spiel verschieden, damit zwei Spiele mit demselben Oberordner ihre Historien nicht mischen.
    /// </summary>
    public string? VanillaHistoryFolderName { get; set; }

    public string EffectiveVanillaHistoryFolderName =>
        string.IsNullOrWhiteSpace(VanillaHistoryFolderName) ? DisplayName + " Vanilla History" : VanillaHistoryFolderName;

    public string? ErrorLogRules { get; set; }
    public string? Notes { get; set; }

    /// <summary>Überschreib-Verhalten je Ordner (Mod-Kompatibilität): Ordnermuster mit <c>*</c> für ein Segment, Verhalten als Name von <c>OverrideBehavior</c>, "Certain" = laut Wiki gesichert.</summary>
    public List<OverrideRuleEntry> OverrideRules { get; set; } = new();

    /// <summary>
    /// Präfixe vor Einträgen der obersten Ebene, die das Spiel kennt (Namen ohne Doppelpunkt, z. B. "INJECT", "REPLACE"). Leer = keine
    /// (EU IV). Quelle ist das offizielle Wiki des Spiels; unbestätigt bleibt die Liste leer.
    /// </summary>
    public List<string> KeyOperators { get; set; } = new();

    /// <summary>Steam-AppID des Spiels (für den Workshop-Upload); ohne Angabe ist das Veröffentlichen auf Steam für dieses Spiel gesperrt.</summary>
    public string? SteamAppId { get; set; }

    /// <summary>Woher die Script-Datenbank dieses Spiels stammt: "log" (Standard, aus docs/*.log) oder "wiki" (siehe ScriptDocsWikiFile).</summary>
    public string ScriptDocsSourceKind { get; set; } = "log";

    /// <summary>Bei ScriptDocsSourceKind="wiki": Pfad relativ zum profiles-Ordner (z. B. "scriptdocs/eu4.json").</summary>
    public string? ScriptDocsWikiFile { get; set; }

    /// <summary>GUI-Einordnung für den GUI-Editor. Null bei Spielen ohne GUI-Unterstützung.</summary>
    public GuiProfileSettings? Gui { get; set; }
}

/// <summary>Einordnung von GUI-Blöcken für den GUI-Editor (siehe docs/superpowers/specs/2026-09-22-gui-editor-design.md).</summary>
public sealed class GuiProfileSettings
{
    /// <summary>Blöcke mit Schlüssel-Wert-Inhalt, die keine Widgets sind (z.B. modify_texture, glow, state).</summary>
    public List<string> StructuredBlocks { get; set; } = new();

    /// <summary>Blöcke, deren Inhalt Widgets sind, die aber selbst kein Widget sind (z.B. tooltipwidget, item).</summary>
    public List<string> WidgetHolders { get; set; } = new();

    /// <summary>Widget-Halter, deren Inhalt im Layout des Elternelements liegt (z.B. item in Rastern, scrollwidget in scrollarea). Andere Halter (Tooltips, Kontextmenüs) erscheinen nicht im Layout.</summary>
    public List<string> LayoutHolders { get; set; } = new();
}
