using ParadoxModTool.Localization;

namespace ParadoxModTool.GitIntegration;

/// <summary>
/// Texte der Git- und GitHub-Anbindung (Meldungen von gh und git, Prüfungen, ZIP-Varianten). Die Keys stehen in
/// <c>locales/de.json</c> und <c>locales/en.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
public static class GitTexts
{
    public static string RepoEmpty => T("git.repo.empty", "Das Repository ist leer. Lege auf GitHub einen ersten Commit an (z. B. eine README) oder pushe den Mod-Ordner im Git-Reiter.");

    public static string RepoUnreadable => T("git.gh.repoUnreadable", "Die Antwort von gh zum Repository war nicht lesbar.");

    public static string ReleaseUnreadable => T("git.gh.releaseUnreadable", "Die Antwort von gh zum Release war nicht lesbar.");

    public static string ReleaseListUnreadable => T("git.gh.releaseListUnreadable", "Die Liste der Releases war nicht lesbar.");

    public static string IssueListUnreadable => T("git.gh.issueListUnreadable", "Die Liste der Issues war nicht lesbar.");

    public static string CommentsUnreadable => T("git.gh.commentsUnreadable", "Die Kommentare waren nicht lesbar.");

    public static string GhTimeout => T("git.gh.timeout", "gh hat nicht rechtzeitig geantwortet.");

    public static string GhExitCode(int code) => F("git.gh.exitCode", "gh meldet Fehler (Exitcode {0}).", code);

    public static string GhStartFailed(string error) => F("git.gh.startFailed", "gh konnte nicht gestartet werden: {0}", error);

    public static string InstallNoPackage => T("git.ghInstall.noPackage", "Die Antwort von GitHub enthält kein gh-Paket für Windows.");

    public static string InstallUnreadable => T("git.ghInstall.unreadable", "Die Antwort von GitHub war nicht lesbar.");

    public static string InstallNoChecksum(string file) => F("git.ghInstall.noChecksum", "Die Prüfsummen-Datei enthält keine Summe für {0}.", file);

    public static string InstallChecksumMismatch => T("git.ghInstall.checksumMismatch", "Die Prüfsumme der heruntergeladenen Datei stimmt nicht. Es wurde nichts installiert.");

    public static string InstallNoParent => T("git.ghInstall.noParent", "Der Zielordner hat keinen Elternordner.");

    public static string InstallNotZip => T("git.ghInstall.notZip", "Die heruntergeladene Datei ist kein gültiges ZIP-Archiv.");

    public static string InstallNoExe => T("git.ghInstall.noExe", "Das Archiv enthält keine bin\\gh.exe.");

    public static string SettingsUnreadable => T("git.settings.unreadable", "github.json war nicht lesbar; eine Kopie liegt als github.json.bad neben der Datei.");

    public static string CommitMessageMissing => T("git.commit.messageMissing", "Die Commit-Nachricht fehlt.");

    public static string StageFailed => T("git.commit.stageFailed", "Die Änderungen konnten nicht vorgemerkt werden.");

    public static string Committed => T("git.commit.done", "Die Änderungen wurden committet.");

    public static string CommitFailed => T("git.commit.failed", "Der Commit ist fehlgeschlagen.");

    public static string PushDetached => T("git.push.detached", "Der Ordner steht auf einem losgelösten HEAD; gepusht wird nur ein Branch.");

    public static string PushBehind(int behind, string? upstream) => F("git.push.behind", "Der Branch ist {0} Commit(s) hinter {1}. Hole die Änderungen mit deinem Git-Werkzeug, dann pushe erneut.", behind, upstream);

    public static string Pushed => T("git.push.done", "Der Branch wurde gepusht.");

    public static string PushNoCredentials => T("git.push.noCredentials", " Git hat keine Anmeldung für GitHub. Melde dich einmal in deinem Git-Werkzeug an (z. B. mit dem Git Credential Manager).");

    public static string PushFailed => T("git.push.failed", "Das Pushen ist fehlgeschlagen.");

    public static string Timeout => T("git.timeout", "Zeitüberschreitung.");

    public static string ZipsBuilding => T("git.publish.zipsBuilding", "ZIP-Dateien werden erstellt …");

    public static string ZipsFailed(string error) => F("git.publish.zipsFailed", "Die ZIP-Dateien konnten nicht erstellt werden: {0}", error);

    public static string TagInvalid(string tag) => F("git.publish.tagInvalid", "Der Tag „{0}“ ist ungültig. Prüfe das Tag-Format.", tag);

    public static string CheckingRepo(string repo) => F("git.publish.checkingRepo", "[{0}] Repository wird geprüft …", repo);

    public static string NoWriteAccess(string repo) => F("git.publish.noWriteAccess", "Du hast kein Schreibrecht in {0}.", repo);

    public static string ReleaseExists(string tag, string repo) => F("git.publish.releaseExists", "Der Tag {0} hat in {1} schon ein Release. Schalte \"Vorhandenes Release aktualisieren\" ein oder ändere das Tag-Format.", tag, repo);

    public static string CreatingRelease(string repo, string tag) => F("git.publish.creating", "[{0}] Release {1} wird angelegt …", repo, tag);

    public static string ReleaseCreated(string tag, string repo) => F("git.publish.created", "Das Release {0} wurde in {1} angelegt.", tag, repo);

    public static string UpdatingRelease(string repo, string tag) => F("git.publish.updating", "[{0}] Release {1} wird aktualisiert …", repo, tag);

    public static string ReleaseUpdated(string tag, string repo) => F("git.publish.updated", "Das Release {0} in {1} wurde aktualisiert.", tag, repo);

    public static string PublishCancelled => T("git.publish.cancelled", "Abgebrochen. Ein Release kann in diesem Repo schon angelegt sein; unter \"Aufräumen\" lässt es sich löschen.");

    public static string CheckGhMissing => T("git.check.ghMissing", "gh (GitHub CLI) ist nicht eingerichtet.");

    public static string CheckNotLoggedIn => T("git.check.notLoggedIn", "Bei GitHub ist keine Anmeldung aktiv.");

    public static string CheckNoRepo => T("git.check.noRepo", "Es ist kein Repository angehakt.");

    public static string CheckTitleMissing => T("git.check.titleMissing", "Der Titel des Releases fehlt.");

    public static string CheckNotesTooLong(int length, int max) => F("git.check.notesTooLong", "Die Release-Notes sind zu lang ({0:N0} von {1:N0} Zeichen).", length, max);

    public static string CheckNoChangelogSection => T("git.check.noChangelogSection", "Im Changelog gibt es keinen Abschnitt für diese Version; die Release-Notes stammen nur aus deinem Text.");

    public static string CheckWillUpdate(string tag, string repo) => F("git.check.willUpdate", "Das vorhandene Release {0} in {1} wird aktualisiert (Notes und ZIP-Dateien werden ersetzt).", tag, repo);

    public static string CheckTagExists(string tag, string repo) => F("git.check.tagExists", "Der Tag {0} existiert schon in {1}. Schalte \"Vorhandenes Release aktualisieren\" ein oder ändere das Tag-Format.", tag, repo);

    public static string WarnUncommitted(int count) => F("git.warn.uncommitted", "Im Mod-Ordner gibt es {0} ungespeicherte Änderung(en); die ZIP enthält sie, der Tag nicht.", count);

    public static string WarnNotPushed => T("git.warn.notPushed", "Der aktuelle Commit ist noch nicht gepusht (nicht gepusht = der Tag kann nicht auf ihn zeigen).");

    public static string WarnBehind(int behind, string? upstream) => F("git.warn.behind", "Der Branch ist {0} Commit(s) hinter {1}.", behind, upstream);

    public static string WarnDetached => T("git.warn.detached", "Der Ordner steht auf einem losgelösten HEAD (kein Branch).");

    public static string ZipSameName(string first, string second, string file) => F("git.zip.sameName", "Die ZIP-Varianten „{0}“ und „{1}“ ergeben denselben Dateinamen {2}.", first, second, file);

    public static string ZipEmpty(string variant) => F("git.zip.empty", "Die ZIP-Variante „{0}“ enthält keine Datei.", variant);

    public static string ModFolderUnreadable(string error) => F("git.zip.folderUnreadable", "Der Mod-Ordner ist nicht lesbar: {0}", error);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
