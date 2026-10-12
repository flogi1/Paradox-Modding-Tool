using ParadoxModTool.Localization;

namespace ParadoxModTool.App;

/// <summary>
/// Texte des Hauptfensters (Start, Dateien, Statusleiste, Wiederherstellung), der Projekt-Funktionen (Übertragen, Abgleich, Sperren,
/// Projekt zuordnen) und kleiner allgemeiner Dialoge. Die Keys stehen in <c>locales/de.json</c> und <c>locales/en.json</c>; der deutsche
/// Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
public static class MainTexts
{
    // Hauptfenster: Statusleiste und Ergebnisleiste (MainWindow.xaml)

    public static string ModStatusTip => T("main.modStatus.tip", "Die aktive Mod (z. B. Ziel für GUI-Übernahmen und Vorgabe für Patch-Rezepte und Generator). Klick: andere Mod wählen oder \"Automatisch (offene Datei)\"; auch im Menü \"Mods\".");

    public static string ModStatusFixed(string name) => F("main.modStatus.fixed", "Mod: {0}", name);

    public static string ModStatusAutomatic(string name) => F("main.modStatus.automatic", "Mod (automatisch): {0}", name);

    public static string ModStatusUnmanagedName(string folder) => F("main.modStatus.unmanagedName", "— ({0} ist nicht angelegt, über \"Mods verwalten\" anlegen)", folder);

    public static string ModChoiceLost => T("main.modStatus.choiceLost", "gewählte Mod fehlt");

    public static string ModStatusLockedFile => T("main.modStatus.lockedFile", "Datei");

    public static string ModStatusLockedFolder => T("main.modStatus.lockedFolder", "Ordner");

    public static string ResultsHideTip => T("main.results.hide.tip", "Ergebnisleiste ausblenden (Treffer bleiben im Suchen-Fenster erhalten)");

    public static string ResultsCodeOnly => T("main.results.codeOnly", "Nur gefundenen Code anzeigen");

    public static string ResultsTitle => T("main.results.title", "Suchergebnisse");

    public static string ResultsListTip => T("main.results.list.tip", "Doppelklick springt zum Treffer");

    // Start (Ladebildschirm und Meldungen beim Laden)

    public static string StartupLoadingProfile => T("main.startup.loadingProfile", "Spielprofil wird geladen…");

    public static string StartupLoadingSyntax => T("main.startup.loadingSyntax", "Syntax-Hervorhebung wird geladen…");

    public static string StartupRestoringSession => T("main.startup.restoringSession", "Sitzung wird wiederhergestellt…");

    public static string StartupCheckingRecovery => T("main.startup.checkingRecovery", "Wiederherstellung wird geprüft…");

    public static string StartupAlmostDone => T("main.startup.almostDone", "Fast fertig…");

    public static string StartupDocument(int done, int total, string file) => F("main.startup.document", "Dokument {0}/{1}: {2}", done, total, file);

    public static string ProfileNotFound(string gameId) => F(
        "main.profile.notFound",
        "Kein Spielprofil für '{0}' gefunden.\nLäuft das Projekt aus dem Build-Ordner (bin/Debug/net8.0-windows)? Dort muss ein Ordner 'profiles' liegen.",
        gameId);

    public static string ProfileLoadFailed(string message) => F("main.profile.loadFailed", "Spielprofil konnte nicht geladen werden: {0}", message);

    public static string SyntaxFileMissing(string path) => F("main.syntax.fileMissing", "Syntax-Highlighting-Datei nicht gefunden unter: {0}", path);

    public static string SyntaxInnerException(string type, string message) => F("main.syntax.innerException", "Innere Ausnahme: {0}: {1}", type, message);

    public static string SyntaxLoadFailedTitle => T("main.syntax.loadFailed.title", "Syntax-Highlighting konnte nicht geladen werden");

    // Dateidialoge

    public static string FilterAllFiles => T("main.filter.allFiles", "Alle Dateien (*.*)");

    public static string FilterAllSupported(string patterns) => F("main.filter.allSupported", "Alle unterstützten Dateitypen ({0})", patterns);

    public static string FilterScriptFiles => T("main.filter.scriptFiles", "Paradox-Skriptdateien (*.txt)");

    public static string FilterPrograms => T("main.filter.programs", "Programme (*.exe)");

    public static string PlaysetsChooseTitle => T("main.playsets.choose.title", "Playset-Datei auswählen");

    public static string GameExeChooseTitle => T("main.launch.chooseExe.title", "Spiel-.exe auswählen");

    // Dateien speichern, anlegen, löschen; Rückgängig/Wiederholen

    public static string SaveUndoName(string file) => F("main.save.undoName", "Datei gespeichert: {0}", file);

    public static string SaveFailed(string message) => F("main.save.failed", "Datei konnte nicht gespeichert werden: {0}", message);

    /// <summary>Hinweis, dass ein neues Dokument erst gespeichert werden muss; <paramref name="action"/> ist einer der Texte <c>NoFileYet…</c>.</summary>
    public static string NoFileYet(string action) => F(
        "main.noFileYet",
        "Für dieses Dokument gibt es noch keine Datei auf der Platte - bitte zuerst speichern, bevor {0} werden kann.",
        action);

    public static string NoFileYetBookmark => T("main.noFileYet.bookmark", "ein Bookmark gesetzt");

    public static string NoFileYetLesezeichen => T("main.noFileYet.bookmarkPanel", "ein Lesezeichen gesetzt");

    public static string NoFileYetNote => T("main.noFileYet.note", "eine Notiz hinzugefügt");

    public static string NoDocumentOpen => T("main.noDocument", "Kein Dokument geöffnet.");

    public static string FormatFailed(string error) => F(
        "main.format.failed",
        "Struktur-Neuformatierung nicht möglich - die Datei konnte nicht als Paradox-Skript gelesen werden:\n\n{0}\n\nEs wurde nichts am Dokument geändert.",
        error);

    public static string FormatTitle => T("main.format.title", "Formatieren");

    public static string ReplaceUndoName(string file) => F("main.replace.undoName", "Ersetzen: {0}", file);

    public static string ReplaceFailed(string message) => F("main.replace.failed", "Ersetzen in Dateien fehlgeschlagen: {0}", message);

    public static string NewFileExists(string name) => F("main.newFile.exists", "Die Datei \"{0}\" existiert in diesem Ordner bereits.", name);

    public static string NewFileTitle => T("main.newFile.title", "Neue Datei anlegen");

    public static string NewFileUndoName(string name) => F("main.newFile.undoName", "Datei angelegt: {0}", name);

    public static string NewFileFailed(string message) => F("main.newFile.failed", "Datei konnte nicht angelegt werden: {0}", message);

    public static string NewFolderExists(string name) => F("main.newFolder.exists", "\"{0}\" existiert in diesem Ordner bereits.", name);

    public static string NewFolderTitle => T("main.newFolder.title", "Neuen Ordner anlegen");

    public static string FolderFilterCreateTitle => T("main.folderFilter.create.title", "Ordner-Filter anlegen");

    public static string FolderFilterCreatePrompt(string folder) => F("main.folderFilter.create.prompt", "Name des Filters für {0}. In der Filterleiste jedes Arbeitsbereichs zeigt er dann nur Tabs mit Dateien aus diesem Ordner.", folder);

    public static string FolderFilterRenameTitle => T("main.folderFilter.rename.title", "Ordner-Filter umbenennen");

    public static string FolderFilterRenamePrompt(string folder) => F("main.folderFilter.rename.prompt", "Neuer Name des Filters für {0}:", folder);

    public static string NewFolderUndoName(string name) => F("main.newFolder.undoName", "Ordner angelegt: {0}", name);

    public static string NewFolderFailed(string message) => F("main.newFolder.failed", "Ordner konnte nicht angelegt werden: {0}", message);

    public static string DeleteFailed(string message) => F("main.delete.failed", "Löschen fehlgeschlagen: {0}", message);

    public static string UndoFailed(string message) => F("main.undo.failed", "Rückgängig fehlgeschlagen: {0}", message);

    public static string RedoFailed(string message) => F("main.redo.failed", "Wiederholen fehlgeschlagen: {0}", message);

    public static string BookmarkSaveFailed(string message) => F("main.bookmark.saveFailed", "Lesezeichen konnte nicht gespeichert werden: {0}", message);

    // Spiel starten

    public static string LaunchMore(int count) => F("main.launch.more", "… und {0} weitere", count);

    public static string LaunchUnsaved(string list) => F(
        "main.launch.unsaved",
        "Diese Mod-Dateien haben ungespeicherte Änderungen; das Spiel sieht sie erst nach dem Speichern:\n\n{0}\n\nJa: speichern und starten\nNein: ohne Speichern starten\nAbbrechen: nicht starten",
        list);

    public static string LaunchTitle => T("main.launch.title", "Spiel starten");

    public static string LaunchPlaysetFailed => T("main.launch.playsetFailed", "Playset konnte nicht umgeschaltet werden");

    public static string LaunchFailed => T("main.launch.failed", "Spiel konnte nicht gestartet werden");

    // Weiteres Spielfenster

    public static string NewWindowFailed(string message) => F("main.newWindow.failed", "Das Fenster konnte nicht gestartet werden: {0}", message);

    public static string NewWindowTitle => T("main.newWindow.title", "Neues Fenster");

    public static string NewWindowNoProcessPath => T("main.newWindow.noProcessPath", "Programmpfad unbekannt.");

    // Unbekannter Mod-Ordner

    public static string AddModPrompt(string folder) => F(
        "main.addMod.prompt",
        "Der Ordner\n{0}\nenthält eine Metadaten-Datei und sieht wie eine Mod aus.\n\nAls Mod anlegen?\n\nJa: anlegen\nNein: nicht mehr fragen\nAbbrechen: später",
        folder);

    public static string AddModTitle => T("main.addMod.title", "Mod anlegen");

    public static string AddModName => T("main.addMod.name", "Name der Mod:");

    // Schließen, externe Änderungen, Wiederherstellung

    public static string CloseUnsaved(string document) => F("main.close.unsaved", "\"{0}\" hat ungespeicherte Änderungen.\n\nVor dem Schließen speichern?", document);

    public static string CloseUnsavedTitle => T("main.close.unsaved.title", "Ungespeicherte Änderungen");

    public static string ExternalChanged(string document) => F(
        "main.external.changed",
        "Die Datei \"{0}\" wurde außerhalb des Programms geändert, und hier gibt es ungespeicherte Änderungen.\n\nDie geänderte Fassung von der Platte laden? Die eigenen ungespeicherten Änderungen lassen sich danach mit Strg+Z zurückholen.",
        document);

    public static string ExternalChangedTitle => T("main.external.title", "Datei extern geändert");

    public static string RecoveryChanged(string file) => F(
        "main.recovery.changed",
        "Die Datei \"{0}\" wurde seit der letzten Sicherung außerhalb des Programms verändert.\n\nJa: die gesicherten, ungespeicherten Änderungen wiederherstellen (beim Speichern wird die geänderte Datei überschrieben).\nNein: die Datei von der Platte behalten und die Sicherung verwerfen.",
        file);

    public static string RecoveryTitle => T("main.recovery.title", "Wiederherstellung");

    // Projekte: Sperren, Übertragen, Vergleich

    public static string ProjectCaption => T("branch.caption", "Projekt");

    public static string ModFolderMissing(string mod) => F("branch.modFolderMissing", "Der Ordner der Mod \"{0}\" wurde nicht gefunden.", mod);

    public static string UnlockConfirmMod(int count, string mod) => F(
        "branch.unlock.confirmMod",
        "{0} Sperre(n) in der Mod \"{1}\" aufheben?\n\nDanach können Übertragen und Abgleich diese Dateien wieder überschreiben, verschieben und kopieren.",
        count,
        mod);

    public static string UnlockConfirmFolder(int count, string folder) => F(
        "branch.unlock.confirmFolder",
        "{0} Sperre(n) im Ordner \"{1}\" und allen Unterordnern aufheben?\n\nDanach können Übertragen und Abgleich diese Dateien wieder überschreiben, verschieben und kopieren.",
        count,
        folder);

    public static string UnlockTitle => T("branch.unlock.title", "Sperren aufheben");

    public static string UnlockDone(int count) => F("branch.unlock.done", "{0} Sperre(n) aufgehoben.", count);

    public static string CompareFileMissing(string file, string mod) => F("branch.compare.fileMissing", "Die Datei {0} gibt es in \"{1}\" nicht.", file, mod);

    public static string TransferNoFiles => T("branch.transfer.noFiles", "Keine Dateien zum Übertragen.");

    public static string TransferUnsaved(string list) => F(
        "branch.transfer.unsaved",
        "Diese Dateien haben ungespeicherte Änderungen im Editor. Speichere oder verwirf sie zuerst:\n\n{0}",
        list);

    public static string TransferExistsOne(string file, string mod) => F(
        "branch.transfer.existsOne",
        "Die Datei\n{0}\nexistiert schon in \"{1}\".\n\nÜberschreiben? Die alte Datei wird vorher gesichert.",
        file,
        mod);

    public static string TransferExistsMany(int count, string mod) => F(
        "branch.transfer.existsMany",
        "{0} Dateien existieren schon in \"{1}\".\n\nJa: alle überschreiben (die alten Dateien werden vorher gesichert)\nNein: vorhandene überspringen\nAbbrechen: nichts übertragen",
        count,
        mod);

    public static string TransferFailed(string message) => F("branch.transfer.failed", "Die Übertragung wurde abgebrochen:\n{0}", message);

    public static string TransferPartial(int done) => F("branch.transfer.partial", "{0} Datei(en) übertragen, dann Fehler.", done);

    public static string TransferMoved(int done, string mod) => F("branch.transfer.moved", "{0} Datei(en) nach {1} verschoben", done, mod);

    public static string TransferCopied(int done, string mod) => F("branch.transfer.copied", "{0} Datei(en) nach {1} kopiert", done, mod);

    public static string TransferSkipped(int count) => F("branch.transfer.skipped", ", {0} übersprungen", count);

    public static string TransferLocked(int count) => F("branch.transfer.locked", ", {0} gesperrt", count);

    // Abgleich (BranchSyncWindow)

    public static string SyncTitle => T("branch.sync.title", "Abgleich");

    public static string SyncWindowTitle(string project) => F("branch.sync.windowTitle", "Abgleich — Projekt {0}", project);

    public static string SyncNoProject(string mod) => F(
        "branch.sync.noProject",
        "Die Mod \"{0}\" gehört zu keinem Projekt. Ordne sie im Fenster \"Mods\" einem Projekt zu.",
        mod);

    public static string SyncNoActiveMod => T("branch.sync.noActiveMod", "Es ist keine Mod aktiv. Öffne eine Datei einer Mod oder starte den Abgleich im Fenster \"Mods\".");

    public static string SyncApply => T("branch.sync.apply", "Übernehmen");

    public static string SyncApplyTo(string target) => F("branch.sync.applyTo", "Nach {0} übernehmen", target);

    public static string SyncApplyTip(string from, string to) => F(
        "branch.sync.apply.tip",
        "Die gewählten Dateien von \"{0}\" nach \"{1}\" kopieren (vorhandene Dateien werden vorher gesichert, gesperrte übersprungen)",
        from,
        to);

    public static string SyncLeftLower => T("branch.sync.leftLower", "links");

    public static string SyncRightLower => T("branch.sync.rightLower", "rechts");

    public static string SyncBoth => T("branch.sync.both", "beide");

    public static string SyncLeft => T("branch.sync.left", "Links");

    public static string SyncRight => T("branch.sync.right", "Rechts");

    public static string SyncCompareFile => T("branch.sync.compareFile", "Datei vergleichen");

    public static string SyncCompareFileTip => T("branch.sync.compareFile.tip", "Die gewählte Datei aus beiden Mods im Vergleichsfenster öffnen (auch per Doppelklick)");

    public static string SyncRun => T("branch.sync.run", "Vergleichen");

    public static string SyncOnlyLeft => T("branch.sync.onlyLeft", "Nur links");

    public static string SyncOnlyRight => T("branch.sync.onlyRight", "Nur rechts");

    public static string SyncDifferent => T("branch.sync.different", "Unterschiedlich");

    public static string SyncSame => T("branch.sync.same", "Gleich");

    public static string SyncPathFilter => T("branch.sync.pathFilter", "Pfad enthält:");

    public static string SyncColumnPath => T("branch.sync.column.path", "Pfad");

    public static string SyncColumnLocked => T("branch.sync.column.locked", "Gesperrt");

    public static string SyncColumnLeftSize => T("branch.sync.column.leftSize", "Größe links");

    public static string SyncColumnLeftDate => T("branch.sync.column.leftDate", "Datum links");

    public static string SyncColumnRightSize => T("branch.sync.column.rightSize", "Größe rechts");

    public static string SyncColumnRightDate => T("branch.sync.column.rightDate", "Datum rechts");

    public static string SyncSameMod => T("branch.sync.sameMod", "Links und rechts sind dieselbe Mod.");

    public static string SyncComparing => T("branch.sync.comparing", "Vergleiche Ordner…");

    public static string SyncFailed(string message) => F("branch.sync.failed", "Der Abgleich ist fehlgeschlagen:\n{0}", message);

    public static string SyncSummary(int onlyLeft, int onlyRight, int different, int same, int shown) => F(
        "branch.sync.summary",
        "{0} nur links, {1} nur rechts, {2} unterschiedlich, {3} gleich. Angezeigt: {4}.",
        onlyLeft,
        onlyRight,
        different,
        same,
        shown);

    // Projekt zuordnen (ProjectAssignDialog)

    public static string AssignTitle => T("project.assign.title", "Projekt zuordnen");

    public static string AssignAsChild => T("project.assign.asChild", "Diese Mod gehört zu einem Hauptmod");

    public static string AssignAsMain => T("project.assign.asMain", "Diese Mod ist Hauptmod eines Projekts");

    public static string AssignNoProject => T("project.assign.noProject", "Kein Projekt (eigenständige Mod)");

    public static string AssignRoleDevLong => T("project.assign.roleDevLong", "Dev-Branch (Testversion, Beta)");

    public static string AssignRoleSubmodLong => T("project.assign.roleSubmodLong", "Submod (hängt vom Hauptmod ab)");

    public static string AssignRoleDev => T("project.assign.roleDev", "Dev-Branch");

    public static string AssignMainMod => T("project.assign.mainMod", "Hauptmod des Projekts:");

    public static string AssignRole => T("project.assign.role", "Rolle:");

    public static string AssignMembers => T("project.assign.members", "Mods, die zu diesem Projekt gehören:");

    public static string AssignNoCandidates => T("project.assign.noCandidates", "Es gibt keine weiteren Mods, die zugeordnet werden können.");

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
