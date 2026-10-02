using ParadoxModTool.GameProfiles;
using ParadoxModTool.Localization;

namespace ParadoxModTool.SaveGames;

/// <summary>
/// Texte des Spielstand-Explorers. Die Schlüssel stehen in <c>locales/de.json</c> und <c>locales/en.json</c>; der deutsche Text hier ist der
/// Rückfall und muss mit de.json übereinstimmen (Test <c>SaveExplorerTextsTests</c>). Jeder Aufruf von <c>T</c>/<c>F</c> steht in einer
/// Zeile. Die Namen der Objektarten stehen im Spielprofil (<c>labelKey</c>/<c>label</c>) und in den Locale-Dateien.
/// </summary>
public static class SaveExplorerTexts
{
    public static string Global => T("saveexplorer.global", "Global");

    public static string Other => T("saveexplorer.other", "Sonstige");

    public static string Yes => T("saveexplorer.yes", "ja");

    public static string No => T("saveexplorer.no", "nein");

    public static string DebugHint => T("saveexplorer.debugHint", "Nur Spielstände aus dem Debug-Modus sind lesbar. Das Spiel mit -debug_mode starten und neu speichern.");

    public static string CompressedUnreadable => T("saveexplorer.compressedUnreadable", "Spielstand komprimiert, Inhalt nicht lesbar (kein Eintrag gamestate gefunden).");

    public static string KindLabel(SaveObjectKindSetting kind) => UiText.Get(kind.LabelKey, kind.Label);

    // Menü, Fenster, Kopfzeile (Plan 2a).
    public static string Menu => T("saveexplorer.menu", "Spielstand-Explorer");

    public static string MenuTip => T("saveexplorer.menu.tip", "Variablen, Variablenlisten und Flag-Werte aus Debug-Spielständen lesen, durchsuchen und vergleichen");

    public static string NotAvailable => T("saveexplorer.notAvailable", "Für dieses Spiel noch nicht verfügbar");

    public static string WindowTitle(string game) => F("saveexplorer.window.title", "Spielstand-Explorer - {0}", game);

    public static string SaveLabel => T("saveexplorer.save", "Stand:");

    public static string SaveEntry(string fileName, string? gameDate, DateTime localTime) => F("saveexplorer.save.entry", "{0} ({1}, gespeichert {2:g})", fileName, gameDate ?? "?", localTime);

    public static string Compare => T("saveexplorer.compare", "Vergleichen");

    public static string ComparisonLabel => T("saveexplorer.comparison", "Vergleich mit:");

    public static string NoComparison => T("saveexplorer.comparison.none", "Kein Vergleich");

    public static string Reload => T("saveexplorer.reload", "Neu laden");

    public static string ReloadTip => T("saveexplorer.reload.tip", "Den gewählten Spielstand neu einlesen");

    public static string Newer(string fileName) => F("saveexplorer.newer", "Neuerer Spielstand da: {0}", fileName);

    public static string Load => T("saveexplorer.load", "Laden");

    public static string SearchLabel => T("saveexplorer.search", "Suche:");

    public static string OwnOnly => T("saveexplorer.ownOnly", "Nur Namen aus meinen Mods");

    public static string OwnOnlyTip => T("saveexplorer.ownOnly.tip", "Nur Namen, die im Code einer eigenen Mod vorkommen (Symbolindex des Fehlerprotokolls)");

    public static string OwnOnlyWaiting => T("saveexplorer.ownOnly.waiting", "Der Symbolindex wird noch aufgebaut; bis dahin zeigt der Explorer alle Namen.");

    public static string ChangesOnly => T("saveexplorer.changesOnly", "Nur Änderungen");

    public static string Loading(string fileName, double fraction) => F("saveexplorer.loading", "{0} wird gelesen: {1:P0}", fileName, fraction);

    public static string Cancel => T("saveexplorer.cancel", "Abbrechen");

    // Reiter und Spalten.
    public static string VariablesTab => T("saveexplorer.tab.variables", "Variablen");

    public static string ObjectsTab => T("saveexplorer.tab.objects", "Objekte");

    public static string ColumnKind => T("saveexplorer.column.kind", "Objektart");

    public static string ColumnObject => T("saveexplorer.column.object", "Objekt");

    public static string ColumnName => T("saveexplorer.column.name", "Name");

    public static string ColumnNameKind => T("saveexplorer.column.nameKind", "Art");

    public static string ColumnCount => T("saveexplorer.column.count", "Objekte");

    public static string ColumnType => T("saveexplorer.column.type", "Typ");

    public static string ColumnValue => T("saveexplorer.column.value", "Wert");

    public static string ColumnBefore => T("saveexplorer.column.before", "Vorher");

    public static string ColumnChange => T("saveexplorer.column.change", "Änderung");

    public static string Summary(int names, int objects) => F("saveexplorer.summary", "{0} Namen, {1} Objekte", names, objects);

    public static string CountryFilter => T("saveexplorer.countryFilter", "Land:");

    public static string AllCountries => T("saveexplorer.allCountries", "Alle Länder");

    public static string CopyId => T("saveexplorer.copyId", "ID kopieren");

    public static string CopyIdGesture => T("saveexplorer.copyId.gesture", "Strg+C");

    public static string CopyScript(string reference) => F("saveexplorer.copyScript", "{0} kopieren", reference);

    public static string ShowInErrorLog => T("saveexplorer.showInErrorLog", "Im Fehlerprotokoll zeigen");

    public static string ColumnErrorLog => T("saveexplorer.column.errorLog", "Fehlerprotokoll");

    public static string ShowInCode => T("saveexplorer.showInCode", "Im Code zeigen");

    public static string ShowInCodeTip => T("saveexplorer.showInCode.tip", "Stellen im Code der Mods, an denen der Name vorkommt");

    public static string ShowInCodeNone(string name) => F("saveexplorer.showInCode.none", "{0} kommt im Code der Mods nicht vor.", name);

    public static string ShowInCodeEntry(string path, int line, string mod) => F("saveexplorer.showInCode.entry", "{0}:{1} ({2})", path, line, mod);

    public static string NameKind(SaveNameKind kind) => kind switch
    {
        SaveNameKind.List => T("saveexplorer.kind.list", "Liste"),
        SaveNameKind.FlagValue => T("saveexplorer.kind.flagValue", "Flag-Wert"),
        _ => T("saveexplorer.kind.variable", "Variable"),
    };

    /// <summary>Text der Hinweiszeile; leer bei <see cref="SaveNotice.None"/>. Ohne lesbaren Stand der Debug-Hinweis.</summary>
    public static string Notice(SaveNotice notice, string? folder, string? detail) => notice switch
    {
        SaveNotice.FolderMissing => F("saveexplorer.notice.folderMissing", "Ordner für Spielstände nicht gefunden: {0}", folder ?? string.Empty),
        SaveNotice.NoReadableSave => DebugHint,
        SaveNotice.StillWriting => T("saveexplorer.notice.stillWriting", "Spielstand wird noch geschrieben, gleich erneut versuchen."),
        SaveNotice.Incomplete => T("saveexplorer.notice.incomplete", "Spielstand unvollständig gelesen: Klammern gehen nicht auf. Angezeigt wird, was gelesen wurde."),
        SaveNotice.CannotRead => F("saveexplorer.notice.cannotRead", "Spielstand kann gerade nicht gelesen werden: {0}", detail ?? string.Empty),
        _ => string.Empty,
    };

    /// <summary>Änderung im Vergleich; leer bei <see cref="SaveChange.Unchanged"/>.</summary>
    public static string Change(SaveChange change) => change switch
    {
        SaveChange.Added => T("saveexplorer.change.added", "neu"),
        SaveChange.Removed => T("saveexplorer.change.removed", "entfernt"),
        SaveChange.Changed => T("saveexplorer.change.changed", "geändert"),
        _ => string.Empty,
    };

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
