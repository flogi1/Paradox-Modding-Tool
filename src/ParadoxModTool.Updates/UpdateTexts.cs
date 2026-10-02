using ParadoxModTool.Localization;

namespace ParadoxModTool.Updates;

/// <summary>
/// Texte der Update-Funktion (Hinweisleiste, Dialoge, Fortschritt, Update-Modus). Keys in <c>locales/de.json</c> und
/// <c>locales/en.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen (Test <c>UpdateTextsTests</c>).
/// </summary>
public static class UpdateTexts
{
    // Hinweisleiste
    public static string BarAvailable(AppVersion available, AppVersion installed) => F("update.bar.available", "Neue Version {0} verfügbar (installiert: {1}).", available, installed);

    public static string BarUpdated(AppVersion version) => F("update.bar.updated", "PMT wurde auf Version {0} aktualisiert.", version);

    public static string BarInstall => T("update.bar.install", "Jetzt aktualisieren");

    public static string BarInstallTip => T("update.bar.install.tip", "Neue Version herunterladen, prüfen und PMT damit neu starten");

    public static string BarReleasePage => T("update.bar.releasePage", "Release-Seite");

    public static string BarReleasePageTip => T("update.bar.releasePage.tip", "Öffnet die Release-Seite im Browser");

    public static string BarSkip => T("update.bar.skip", "Diese Version überspringen");

    public static string BarLaterTip => T("update.bar.later.tip", "Später erinnern");

    // Prüfen
    public static string CheckTitle => T("update.check.title", "Nach Updates suchen");

    public static string AvailableTitle => T("update.available.title", "Update verfügbar");

    public static string AvailableQuestion(AppVersion available, AppVersion installed) => F("update.available.question", "Version {0} ist verfügbar (installiert: {1}).\n\nJa: jetzt aktualisieren und neu starten\nNein: Release-Seite im Browser öffnen\nAbbrechen: nichts tun", available, installed);

    public static string UpToDate(AppVersion installed) => F("update.check.upToDate", "Du verwendest die neueste Version ({0}).", installed);

    public static string NoRelease => T("update.check.noRelease", "Es wurde noch keine veröffentlichte Version gefunden.");

    public static string CheckFailed(string? error) => F("update.check.failed", "Die Prüfung ist fehlgeschlagen: {0}", error);

    // Herunterladen und Vorbereiten
    public static string DownloadTitle => T("update.download.title", "Update wird heruntergeladen");

    public static string DownloadHint => T("update.download.hint", "PMT lädt die neue Version von GitHub und prüft ihre Signatur.");

    public static string DownloadStatus(AppVersion version) => F("update.download.status", "Version {0}", version);

    public static string DownloadCount(string text, int done, int total) => F("update.download.count", "{0}: {1} von {2} MB", text, done, total);

    public static string Cancel => T("update.cancel", "Abbrechen");

    public static string NotPossibleTitle => T("update.notPossible.title", "Update nicht möglich");

    public static string OfferReleasePage(string reason) => F("update.notPossible.offer", "{0}\n\nRelease-Seite im Browser öffnen und die neue Version von Hand installieren?", reason);

    public static string NoInstallList => T("update.notPossible.noInstallList", "Dieser Programmordner stammt nicht aus einem PMT-Release (die Dateiliste pmt-install-files.txt fehlt). Das Selbst-Update gibt es nur für ausgelieferte Versionen.");

    public static string Problem(UpdateProblem problem, string? detail) => problem switch
    {
        UpdateProblem.NoSignedPackage => T("update.problem.noSignedPackage", "Dieses Release enthält kein signiertes Update-Paket."),
        UpdateProblem.BadSignature => T("update.problem.badSignature", "Die Signatur des Update-Pakets ist ungültig. Das Paket stammt womöglich nicht vom Entwickler."),
        UpdateProblem.InvalidManifest => T("update.problem.invalidManifest", "Die Paketbeschreibung des Releases ist unlesbar."),
        UpdateProblem.VersionMismatch => T("update.problem.versionMismatch", "Die Version im Paket passt nicht zum Release."),
        UpdateProblem.NotNewer => T("update.problem.notNewer", "Das Paket ist nicht neuer als die installierte Version."),
        UpdateProblem.ProtocolTooNew => T("update.problem.protocolTooNew", "Diese Version kann nur von Hand installiert werden."),
        UpdateProblem.AssetMissing => T("update.problem.assetMissing", "Das Update-Paket fehlt im Release."),
        UpdateProblem.SizeMismatch => T("update.problem.sizeMismatch", "Die Größe des heruntergeladenen Pakets stimmt nicht."),
        UpdateProblem.HashMismatch => T("update.problem.hashMismatch", "Die Prüfsumme des heruntergeladenen Pakets stimmt nicht."),
        UpdateProblem.ExtractFailed => T("update.problem.extractFailed", "Das Paket konnte nicht entpackt werden."),
        UpdateProblem.Network => F("update.problem.network", "Download fehlgeschlagen: {0}", detail),
        _ => "",
    };

    // Bestätigen und Schließen
    public static string ReadyTitle => T("update.ready.title", "Update bereit");

    public static string ReadyQuestion(AppVersion version) => F("update.ready.question", "Version {0} ist bereit. PMT wird geschlossen und neu gestartet.\n\nUngespeicherte Änderungen werden wie beim normalen Schließen gesichert und nach dem Neustart wiederhergestellt.\n\nJetzt neu starten?", version);

    public static string CloseCancelled => T("update.ready.closeCancelled", "PMT wurde nicht geschlossen. Das Update ist bereit und startet beim nächsten Klick auf Jetzt aktualisieren.");

    public static string ProtectedFolder(string folder) => F("update.notPossible.protectedFolder", "PMT liegt in einem geschützten Ordner ({0}). Das Selbst-Update läuft bewusst nie mit Administratorrechten. Lege PMT in einen Ordner deines Benutzerprofils, z. B. %LocalAppData%\\Programs\\Paradox Modding Tool, dann aktualisiert es sich künftig selbst.", folder);

    public static string AlreadyRunning => T("update.alreadyRunning", "Für diesen Programmordner läuft gerade schon ein Update. Warte, bis es fertig ist.");

    public static string LaunchFailed(string error, string logPath) => F("update.launchFailed", "Der Update-Modus konnte nicht gestartet werden: {0}\n\nProtokoll: {1}", error, logPath);

    // Update-Modus
    public static string ApplyTitle => T("update.apply.title", "PMT wird aktualisiert");

    public static string ApplyVersions(AppVersion from, AppVersion to) => F("update.apply.versions", "Version {0} wird durch {1} ersetzt.", from, to);

    public static string Phase(UpdateRunnerPhase phase, int waitingFor) => phase switch
    {
        UpdateRunnerPhase.Waiting => F("update.apply.phase.waiting", "Warte auf {0} offene PMT-Fenster. Bitte schließen.", waitingFor),
        UpdateRunnerPhase.Swapping => T("update.apply.phase.swapping", "Dateien werden ersetzt"),
        UpdateRunnerPhase.Starting => T("update.apply.phase.starting", "Neue Version wird gestartet"),
        UpdateRunnerPhase.Checking => T("update.apply.phase.checking", "Warte auf den Start der neuen Version"),
        _ => T("update.apply.phase.rollingBack", "Update fehlgeschlagen, alte Version wird wiederhergestellt"),
    };

    public static string KeepWaitingTitle => T("update.apply.keepWaiting.title", "Noch offene Fenster");

    public static string KeepWaiting(int count) => F("update.apply.keepWaiting", "Es sind noch {0} PMT-Fenster offen. Das Update wartet, bis alle geschlossen sind.\n\nWeiter warten?", count);

    public static string RollbackFailed(string backupFolder, string logPath) => F("update.apply.rollbackFailed", "Die alte Version konnte nicht vollständig wiederhergestellt werden.\n\nWas noch fehlt, liegt im Ordner {0} (falls vorhanden). Verschiebe dessen Inhalt von Hand in den Programmordner und ersetze vorhandene Dateien.\n\nProtokoll: {1}", backupFolder, logPath);

    public static string InvalidArguments => T("update.apply.invalidArguments", "Ungültiger Aufruf des Update-Modus.");

    // Start nach dem Update
    public static string StartFailedTitle => T("update.start.failed.title", "Update fehlgeschlagen");

    public static string StartFailed(AppVersion version, string logPath) => F("update.start.failed", "Das Update auf Version {0} ist fehlgeschlagen. Die alte Version wurde wiederhergestellt.\n\nDetails im Protokoll: {1}", version, logPath);

    public static string StartCancelledTitle => T("update.start.cancelled.title", "Update abgebrochen");

    public static string StartCancelled(AppVersion version) => F("update.start.cancelled", "Das Update auf Version {0} wurde abgebrochen. Es wurde nichts verändert.", version);

    // Browser
    public static string NotOpenedTitle => T("update.browser.notOpened.title", "Nicht geöffnet");

    public static string NotGitHub => T("update.browser.notGitHub", "Die Release-Adresse verweist nicht auf GitHub und wird deshalb nicht geöffnet.");

    public static string ErrorTitle => T("update.browser.error.title", "Fehler");

    public static string BrowserFailed(string error) => F("update.browser.failed", "Der Browser konnte nicht geöffnet werden: {0}", error);

    public static string CheckHttpStatus(int status) => F("update.check.httpStatus", "GitHub antwortete mit Status {0}.", status);

    public static string CheckUnreadable => T("update.check.unreadable", "Die Antwort von GitHub konnte nicht gelesen werden.");

    // Vorlage für einen neuen GitHub-Fehlerbericht (Hilfe > Fehler melden), in der Sprache der Oberfläche.
    public static string IssueWhatHappened => T("update.issue.whatHappened", "Was ist passiert?");

    public static string IssueSteps => T("update.issue.steps", "Schritte zum Nachstellen");

    public static string IssueExpectedActual => T("update.issue.expectedActual", "Erwartet / Tatsächlich");

    public static string IssueEnvironment => T("update.issue.environment", "Umgebung");

    public static string IssueOs => T("update.issue.os", "Betriebssystem");

    public static string IssueRuntime => T("update.issue.runtime", "Laufzeit");

    private static string T(string key, string fallback) => UiText.Get(key, fallback);

    private static string F(string key, string fallback, params object?[] args) => UiText.Format(key, fallback, args);
}
