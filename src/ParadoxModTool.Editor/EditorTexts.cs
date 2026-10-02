using ParadoxModTool.Localization;

namespace ParadoxModTool.Editor;

/// <summary>
/// Texte des Arbeitsbereichs (Dokumentenleiste, Filter, Pfadleiste, Tabs) und der Scope-Statusleiste. Die Keys stehen in
/// <c>locales/de.json</c> und <c>locales/en.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// Die Texte der GUI-Ansicht stehen in <c>GuiTexts</c>, die der Werkzeugleiste in <c>MenuTexts</c>.
/// </summary>
public static class EditorTexts
{
    // Filter nach Eigenschaften
    public static string FilterOnlyWith => T("editor.filter.onlyWith", "Nur mit:");

    public static string FilterNote => T("editor.filter.note", "🗒 Notiz");

    public static string FilterNoteTip => T("editor.filter.note.tip", "Nur Dokumente mit Notiz anzeigen");

    public static string FilterBookmark => T("editor.filter.bookmark", "🔖 Lesezeichen");

    public static string FilterBookmarkTip => T("editor.filter.bookmark.tip", "Nur Dokumente mit Lesezeichen anzeigen");

    public static string FilterBugTip => T("editor.filter.bug.tip", "Nur Dokumente mit offenen Bugs anzeigen");

    // Filterleiste (Herkunft und Kategorien)
    public static string FilterOrigin => T("editor.filter.origin", "Herkunft:");

    public static string FilterCategory => T("editor.filter.category", "Kategorie:");

    public static string OriginExternal => T("editor.filter.external", "Extern");

    public static string FilterVanillaTip => T("editor.filter.vanilla.tip", "Dateien des Basisspiels ein-/ausblenden");

    public static string FilterModTip => T("editor.filter.mod.tip", "Dateien aus Mod-Ordnern ein-/ausblenden");

    public static string FilterExternalTip => T("editor.filter.external.tip", "Dateien außerhalb von Spiel- und Mod-Ordnern (auch ungespeicherte) ein-/ausblenden");

    public static string FilterCategoryTip(string category) => F("editor.filter.category.tip", "'{0}' ein-/ausblenden", category);

    public static string HideFromBar => T("editor.filter.hideFromBar", "Aus Leiste ausblenden");

    public static string FilterFolders => T("editor.filter.folders", "Nur Ordner:");

    public static string FilterFolderTip(string folder) => F("editor.filter.folder.tip", "Häkchen an: nur Tabs mit Dateien aus {0}. Mehrere Häkchen: Dateien aus allen gewählten Ordnern", folder);

    public static string FolderFilterRename => T("editor.filter.folder.rename", "Umbenennen");

    public static string FolderFilterDelete => T("editor.filter.folder.delete", "Filter löschen");

    // Kategorie-Menü
    public static string CategoryAutomatic(string category) => F("editor.category.automatic", "Automatisch ({0})", category);

    // Tabs
    public static string TabOpenFolder => T("editor.tab.openFolder", "Ordner öffnen");

    public static string TabModifiedTip => T("editor.tab.modified.tip", "Ungespeicherte Änderungen");

    public static string TabNoteTip => T("editor.tab.note.tip", "Es gibt eine Notiz zu dieser Datei - anklicken zum Anzeigen/Bearbeiten");

    public static string TabCloseTip => T("editor.tab.close.tip", "Dokument schließen");

    // Pfadleiste
    public static string ShowNotes => T("editor.path.showNotes", "🗒 Notizen anzeigen");

    public static string ShowNotesTip => T("editor.path.showNotes.tip", "Notiz zur aktiven Datei anzeigen/bearbeiten");

    public static string OpenFolderTip => T("editor.path.openFolder.tip", "Ordner öffnen, in dem die aktive Datei liegt");

    public static string CopyPathTip => T("editor.path.copy.tip", "Dateipfad der aktiven Datei in die Zwischenablage kopieren");

    public static string NoDocument => T("editor.path.noDocument", "Kein Dokument geöffnet");

    public static string UntitledNotSaved => T("editor.path.untitledNotSaved", "Unbenannt (noch nicht gespeichert)");

    // Dokument (Tab-Name und Tooltips)
    public static string Untitled => T("editor.document.untitled", "Unbenannt");

    public static string BookmarkTip => T("editor.document.bookmark.tip", "In dieser Datei gibt es Lesezeichen - anklicken zum Anzeigen");

    public static string BugTip => T("editor.document.bug.tip", "In dieser Datei gibt es offene Bugs");

    public static string OriginVanillaTip => T("editor.document.origin.vanilla", "Vanilla-Datei (Spielordner)");

    public static string OriginModTip => T("editor.document.origin.mod", "Mod-Datei");

    // Scope-Statusleiste
    public static string ScopeNotLoaded => T("editor.scope.notLoaded", "Scope: Script-Datenbank noch nicht geladen");

    public static string ScopeField(string field) => F("editor.scope.field", "Feld: {0}", field);

    public static string ScopeFieldWithStart(string field, string startScope, double support) =>
        F("editor.scope.fieldWithStart", "Feld: {0}  (Start-Scope {1}, Zustimmung {2:P0})", field, startScope, support);

    public static string ScopeSaved(string scopes) => F("editor.scope.saved", "Gespeichert: {0}", scopes);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
