using ParadoxModTool.Localization;

namespace ParadoxModTool.App;

/// <summary>Oberflächentexte des Playset-Fensters (Keys <c>playsetUi.*</c> in <c>locales/*.json</c>). Der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.</summary>
public static class PlaysetWindowTexts
{
    public static string Title => UiText.Get("playsetUi.title", "Playsets");

    public static string List => UiText.Get("playsetUi.list", "Playsets");

    public static string New => UiText.Get("playsetUi.new", "Neu");

    public static string Copy => UiText.Get("playsetUi.copy", "Kopieren");

    public static string Rename => UiText.Get("playsetUi.rename", "Umbenennen");

    public static string SetActive => UiText.Get("playsetUi.setActive", "Aktiv setzen");

    public static string Active => UiText.Get("playsetUi.active", "aktiv");

    public static string Cloud => UiText.Get("playsetUi.cloud", "Konto");

    public static string ModsHeader(string value) => UiText.Format("playsetUi.modsHeader", "Mods in „{0}“ (oben lädt zuerst, weiter unten gewinnt)", value);

    public static string Search => UiText.Get("playsetUi.search", "Suche in den Mods (Stichwörter)");

    public static string AddMod => UiText.Get("playsetUi.addMod", "+ Mod hinzufügen");

    public static string Up => UiText.Get("playsetUi.up", "Nach oben");

    public static string Down => UiText.Get("playsetUi.down", "Nach unten");

    public static string ReorderHint => UiText.Get("playsetUi.reorderHint", "Zum Umsortieren die Suche leeren.");

    public static string FolderMissing => UiText.Get("playsetUi.folderMissing", "Ordner fehlt");

    public static string Dlcs => UiText.Get("playsetUi.dlcs", "DLCs");

    public static string Discard => UiText.Get("playsetUi.discard", "Verwerfen");

    public static string Start => UiText.Get("playsetUi.start", "Mit diesem Playset starten");

    public static string Backups => UiText.Get("playsetUi.backups", "Sicherungen");

    public static string NoBackups => UiText.Get("playsetUi.noBackups", "Noch keine Sicherungen");

    public static string RestoreConfirm(string value) => UiText.Format("playsetUi.restoreConfirm", "Sicherung „{0}“ wiederherstellen? Der aktuelle Stand wird vorher als neue Sicherung gespeichert.", value);

    public static string Restored => UiText.Get("playsetUi.restored", "Sicherung wiederhergestellt.");

    public static string NewPrompt => UiText.Get("playsetUi.newPrompt", "Name des neuen Playsets:");

    public static string CopyPrompt => UiText.Get("playsetUi.copyPrompt", "Name der Kopie:");

    public static string RenamePrompt => UiText.Get("playsetUi.renamePrompt", "Neuer Name:");

    public static string DeleteConfirm(string value) => UiText.Format("playsetUi.deleteConfirm", "Playset „{0}“ löschen? PMT sichert vorher die Dateien (siehe Sicherungen).", value);

    public static string Unsaved(string value) => UiText.Format("playsetUi.unsaved", "Es gibt ungespeicherte Änderungen am Playset „{0}“. Speichern?", value);

    public static string CloudHint(string value) => UiText.Format("playsetUi.cloudHint", "„{0}“ ist mit deinem Paradox-Konto verknüpft. PMT ändert es nur lokal; der Launcher kann die Änderung beim nächsten Abgleich überschreiben. Trotzdem speichern?", value);

    public static string NoFile => UiText.Get("playsetUi.noFile", "Keine Playset-Datei gefunden. Stelle sie in den Einstellungen unter Allgemein ein.");

    public static string AddTitle => UiText.Get("playsetUi.addTitle", "Mod hinzufügen");

    public static string AddMessage => UiText.Get("playsetUi.addMessage", "Mods wählen (Suchfeld oben, Mehrfachauswahl mit Strg oder Umschalt):");

    public static string NoMods => UiText.Get("playsetUi.noMods", "Keine weiteren Mods gefunden.");

    public static string StartFailed(string value) => UiText.Format("playsetUi.startFailed", "Das Spiel konnte nicht gestartet werden: {0}", value);

    public static string QuickStart => UiText.Get("playsetUi.quickStart", "Mit aktuellem Playset plus dieser Mod starten");

    public static string QuickNew => UiText.Get("playsetUi.quickNew", "Neues Playset aus aktuellem Playset plus dieser Mod");

    public static string QuickJsonConfirm(string first, string second) => UiText.Format("playsetUi.quickJsonConfirm", "Dieses Spiel liest seine Playsets selbst. PMT legt deshalb das Playset „{0}“ an (aktuelles Playset plus {1}) und startet damit. Fortfahren?", first, second);

    public static string QuickNewName(string value) => UiText.Format("playsetUi.quickNewName", "Name des neuen Playsets (aktuelles Playset plus {0}):", value);

    public static string QuickNoPlayset => UiText.Get("playsetUi.quickNoPlayset", "Kein Playset gewählt. Wähle zuerst in der Werkzeugleiste ein Playset.");
}
