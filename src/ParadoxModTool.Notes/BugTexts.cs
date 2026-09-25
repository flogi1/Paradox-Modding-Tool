using ParadoxModTool.Localization;

namespace ParadoxModTool.Notes;

/// <summary>Alle Oberflächentexte des Bugtrackers (Keys <c>bugs.*</c> in <c>locales/*.json</c>). Jeder Aufruf steht auf einer Zeile (Test liest per Regex).</summary>
public static class BugTexts
{
    public static string LineText(int? line, int? endLine) => line is not int start
        ? string.Empty
        : endLine is int end && end > start ? F("bugs.lines", "Zeilen {0}–{1}", start, end) : F("bugs.line", "Zeile {0}", start);

    public static string Status(BugStatus status) => status switch
    {
        BugStatus.Done => T("bugs.status.done", "Erledigt"),
        BugStatus.Rejected => T("bugs.status.rejected", "Verworfen"),
        _ => T("bugs.status.open", "Offen"),
    };

    public static string Source(BugSource source) => source switch
    {
        BugSource.GitHub => T("bugs.source.github", "GitHub"),
        BugSource.Discord => T("bugs.source.discord", "Discord"),
        _ => T("bugs.source.local", "Lokal"),
    };

    public static string Removed => T("bugs.removed", "entfernt");

    public static string NewCount(int count) => F("bugs.newCount", "{0} neu", count);

    // Notizleiste
    public static string PanelHeader => T("bugs.panel.header", "🐞 Bugs");

    public static string PanelThisFile => T("bugs.panel.thisFile", "Diese Datei");

    public static string PanelThisFileTip => T("bugs.panel.thisFile.tip", "Nur Meldungen mit Bezug auf die aktive Datei");

    public static string PanelProject => T("bugs.panel.project", "Ganzes Projekt");

    public static string PanelProjectTip => T("bugs.panel.project.tip", "Alle Meldungen des Projekts der aktiven Datei");

    public static string PanelShowClosed => T("bugs.panel.showClosed", "Erledigte");

    public static string PanelShowClosedTip => T("bugs.panel.showClosed.tip", "Erledigte und verworfene Meldungen in der Liste anzeigen");

    public static string PanelNew => T("bugs.panel.new", "Neu");

    public static string PanelNewTip => T("bugs.panel.new.tip", "Neuen Bug zur aktiven Datei anlegen");

    public static string PanelEdit => T("bugs.panel.edit", "Bearbeiten");

    public static string PanelEditTip => T("bugs.panel.edit.tip", "Lokalen Bug bearbeiten oder die Meldung im Bugtracker zeigen");

    public static string PanelListTip => T("bugs.panel.list.tip", "Doppelklick öffnet die Datei an der Zeile");

    // Aktionen
    public static string Done => T("bugs.action.done", "Erledigt");

    public static string DoneTip => T("bugs.action.done.tip", "Als erledigt markieren");

    public static string Reject => T("bugs.action.reject", "Verwerfen");

    public static string RejectTip => T("bugs.action.reject.tip", "Verwerfen, z. B. bei einer unsinnigen Meldung (Entf); rückgängig mit Wieder öffnen");

    public static string Reopen => T("bugs.action.reopen", "Wieder öffnen");

    public static string ReopenTip => T("bugs.action.reopen.tip", "Wieder als offen markieren");

    public static string DeleteTip => T("bugs.action.delete.tip", "Lokalen Bug endgültig löschen");

    // Dialog
    public static string DialogTitle => T("bugs.dialog.title", "Bug");

    public static string DialogNew => T("bugs.dialog.new", "Neuer Bug");

    public static string DialogAttach => T("bugs.dialog.attach", "An bestehende Meldung anhängen");

    public static string DialogBugTitle => T("bugs.dialog.bugTitle", "Titel");

    public static string DialogDescription => T("bugs.dialog.description", "Beschreibung");

    public static string DialogUseLine(string lineText) => F("bugs.dialog.useLine", "Auf {0} beziehen", lineText);

    public static string DialogSearch => T("bugs.dialog.search", "Suchen");

    public static string DialogNoReports => T("bugs.dialog.noReports", "Keine offenen Meldungen im Projekt.");

    public static string Save => T("bugs.dialog.save", "Speichern");

    public static string Cancel => T("bugs.dialog.cancel", "Abbrechen");

    // Meldungen
    public static string HintCaption => T("bugs.msg.hint", "Hinweis");

    public static string ErrorCaption => T("bugs.msg.error", "Fehler");

    public static string NoFileYet => T("bugs.msg.noFile", "Für dieses Dokument gibt es noch keine Datei. Bitte zuerst speichern.");

    public static string SaveFailed(string reason) => F("bugs.msg.saveFailed", "Die Bugs konnten nicht gespeichert werden: {0}", reason);

    public static string LinkFileMissing(string path) => F("bugs.msg.fileMissing", "Die Datei {0} wurde in keinem Ordner des Projekts gefunden.", path);

    public static string DeleteConfirm(string title) => F("bugs.msg.deleteConfirm", "Den Bug \"{0}\" endgültig löschen?", title);

    // Fenster
    public static string WindowTitle => T("bugs.window.title", "Bugtracker");

    public static string WindowProject => T("bugs.window.project", "Projekt");

    public static string WindowNoProject => T("bugs.window.noProject", "Ohne Projekt");

    public static string WindowRefresh => T("bugs.window.refresh", "Aktualisieren");

    public static string WindowRefreshTip => T("bugs.window.refresh.tip", "Meldungen neu laden");

    public static string WindowNewBug => T("bugs.window.newBug", "Neuer Bug");

    public static string WindowNewBugTip => T("bugs.window.newBug.tip", "Neuen Bug im gewählten Projekt anlegen, ohne Datei-Bezug");

    public static string WindowAllSources => T("bugs.window.allSources", "Alle Quellen");

    public static string WindowOnlyNew => T("bugs.window.onlyNew", "Nur neue");

    public static string WindowWithoutLink => T("bugs.window.withoutLink", "Ohne Bezug");

    public static string WindowSearch => T("bugs.window.search", "Suchen");

    public static string WindowEmpty => T("bugs.window.empty", "Keine Meldungen für diese Filter.");

    public static string WindowNoSelection => T("bugs.window.noSelection", "Keine Meldung gewählt.");

    public static string WindowAuthor(string author) => F("bugs.window.author", "von {0}", author);

    public static string WindowOpenInBrowser => T("bugs.window.openInBrowser", "Im Browser öffnen");

    public static string WindowDelete => T("bugs.window.delete", "Löschen");

    public static string WindowLinks => T("bugs.window.links", "Bezüge");

    public static string WindowAddCurrent => T("bugs.window.addCurrent", "Aktuelle Datei hinzufügen");

    public static string WindowAddCurrentTip => T("bugs.window.addCurrent.tip", "Aktive Datei mit Cursorzeile oder Markierung als Bezug hinzufügen");

    public static string WindowRemoveLink => T("bugs.window.removeLink", "Entfernen");

    public static string WindowNoLinks => T("bugs.window.noLinks", "Kein Bezug. Mit \"Aktuelle Datei hinzufügen\" verknüpfen.");

    public static string WindowNoActiveFile => T("bugs.window.noActiveFile", "Es gibt keine aktive Datei mit Pfad.");

    public static string WindowLinksTip => T("bugs.window.links.tip", "Doppelklick öffnet die Datei an der Zeile");

    private static string T(string key, string fallback) => UiText.Get(key, fallback);

    private static string F(string key, string fallback, params object?[] args) => UiText.Format(key, fallback, args);
}
