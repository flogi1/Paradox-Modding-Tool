using ParadoxModTool.Localization;

namespace ParadoxModTool.SteamIntegration;

/// <summary>
/// Texte der Steam-Anbindung (Upload über den Steam-Client oder SteamCMD, Steamworks, Workshop-Suche). Die Keys stehen in
/// <c>locales/de.json</c> und <c>locales/en.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
public static class SteamTexts
{
    public static string AppIdNotNumber => T("steam.appIdNotNumber", "Die AppID muss eine Zahl sein.");

    public static string SteamIdNotNumber => T("steam.steamIdNotNumber", "Die SteamID muss eine Zahl sein.");

    public static string WorkshopIdMustBeNumber => T("steam.workshopIdMustBeNumber", "Die Workshop-ID muss eine Zahl sein.");

    public static string CreatingItem => T("steam.upload.creatingItem", "Workshop-Seite wird angelegt …");

    public static string CreateRejected => T("steam.upload.createRejected", "Steam hat die Anfrage zum Anlegen der Workshop-Seite nicht angenommen.");

    public static string CreateUnreadable => T("steam.upload.createUnreadable", "Steams Antwort auf das Anlegen der Workshop-Seite war nicht lesbar.");

    public static string WorkshopIdNotNumber => T("steam.upload.workshopIdNotNumber", "Die Workshop-ID ist keine Zahl.");

    public static string UpdateNotStarted => T("steam.upload.updateNotStarted", "Steam konnte die Aktualisierung nicht beginnen. Prüfe die Workshop-ID.");

    public static string TitleRejected => T("steam.upload.titleRejected", "Steam hat den Titel abgelehnt (höchstens 128 Zeichen).");

    public static string DescriptionRejected => T("steam.upload.descriptionRejected", "Steam hat die Beschreibung abgelehnt (höchstens 8000 Zeichen).");

    public static string PreviewRejected => T("steam.upload.previewRejected", "Steam hat das Vorschaubild abgelehnt (Datei fehlt oder ist zu groß).");

    public static string VisibilityRejected => T("steam.upload.visibilityRejected", "Steam hat die Sichtbarkeit abgelehnt.");

    public static string ContentRejected => T("steam.upload.contentRejected", "Steam hat den Inhaltsordner abgelehnt.");

    public static string Submitting => T("steam.upload.submitting", "Änderung wird an Steam übergeben …");

    public static string SubmitRejected => T("steam.upload.submitRejected", "Steam hat die Anfrage zum Hochladen nicht angenommen.");

    public static string SubmitUnreadable => T("steam.upload.submitUnreadable", "Steams Antwort auf das Hochladen war nicht lesbar.");

    public static string Uploaded => T("steam.upload.done", "Die Version wurde auf Steam hochgeladen.");

    public static string ClientCancelled => T("steam.upload.clientCancelled", "Der Upload wurde abgebrochen. Steam kann die Übertragung trotzdem noch abschließen.");

    public static string ClientTimeout => T("steam.upload.clientTimeout", "Steam hat nicht rechtzeitig geantwortet. Prüfe später auf der Workshop-Seite, ob die Version angekommen ist.");

    public static string PreparingConfig => T("steam.upload.preparingConfig", "Steam bereitet die Änderung vor …");

    public static string PreparingContent => T("steam.upload.preparingContent", "Steam liest die Dateien ein …");

    public static string UploadingContent => T("steam.upload.uploadingContent", "Inhalt wird hochgeladen …");

    public static string UploadingContentPercent(ulong percent) => F("steam.upload.uploadingContentPercent", "Inhalt wird hochgeladen … {0} %", percent);

    public static string UploadingPreview => T("steam.upload.uploadingPreview", "Vorschaubild wird hochgeladen …");

    public static string Committing => T("steam.upload.committing", "Änderung wird festgeschrieben …");

    public static string UploadCancelled => T("steam.upload.cancelled", "Der Upload wurde abgebrochen.");

    public static string HelperStartFailed(string error) => F("steam.helper.startFailed", "Der Steam-Hilfsprozess konnte nicht gestartet werden: {0}", error);

    public static string HelperNoResult(string exitCode) => F("steam.helper.noResult", "Der Steam-Hilfsprozess endete ohne Ergebnis (Exitcode {0}).", exitCode);

    public static string HelperWriteFailed(string error) => F("steam.helper.writeFailed", "Die Anfrage an den Steam-Hilfsprozess konnte nicht geschrieben werden: {0}", error);

    public static string HelperRequestUnreadable(string error) => F("steam.helper.requestUnreadable", "Die Anfrage an den Steam-Hilfsprozess war nicht lesbar: {0}", error);

    public static string RequestEmpty => T("steam.helper.requestEmpty", "Die Anfrage ist leer.");

    public static string ProgramPathUnknown => T("steam.helper.programPathUnknown", "Der Pfad des Programms ist unbekannt.");

    public static string UserNameInvalid => T("steam.cmd.userNameInvalid", "Der Steam-Benutzername fehlt oder enthält Leerzeichen oder Anführungszeichen.");

    public static string CmdStarting => T("steam.cmd.starting", "SteamCMD wird gestartet …");

    public static string CmdStartFailed(string error) => F("steam.cmd.startFailed", "SteamCMD konnte nicht gestartet werden: {0}", error);

    public static string CmdNoWorkshopId => T("steam.cmd.noWorkshopId", "SteamCMD hat keinen Fehler gemeldet, aber auch keine Workshop-ID geliefert. Prüfe die Ausgabe.");

    public static string CmdTimeout => T("steam.cmd.timeout", "SteamCMD hat nicht rechtzeitig geantwortet und wurde beendet.");

    public static string CmdGuardRequired => T("steam.cmd.guardRequired", "Steam verlangt einen Steam-Guard-Code. Führe „Steam-Anmeldung einrichten“ erneut aus und gib den Code dort ein.");

    public static string CmdLoginRequired => T("steam.cmd.loginRequired", "Die Steam-Anmeldung ist abgelaufen oder fehlt. Führe „Steam-Anmeldung einrichten“ erneut aus.");

    public static string CmdExitCode(int code) => F("steam.cmd.exitCode", "SteamCMD endete mit dem Code {0}.", code);

    public static string CmdNoError => T("steam.cmd.noError", "SteamCMD hat keinen Fehler gemeldet.");

    public static string InstallNoParent => T("steam.cmdInstall.noParent", "Der Zielordner hat keinen Elternordner.");

    public static string InstallNotZip => T("steam.cmdInstall.notZip", "Die heruntergeladene Datei ist kein gültiges ZIP-Archiv.");

    public static string InstallNoExe => T("steam.cmdInstall.noExe", "Das Archiv enthält keine steamcmd.exe.");

    public static string SettingsUnreadable => T("steam.settings.unreadable", "steam.json war nicht lesbar; eine Kopie liegt als steam.json.bad neben der Datei.");

    public static string ResultGeneric => T("steam.result.2", "Steam meldet einen allgemeinen Fehler (2).");

    public static string ResultNoConnection => T("steam.result.3", "Es besteht keine Verbindung zu Steam (3). Prüfe deine Internetverbindung.");

    public static string ResultInvalidParam => T("steam.result.8", "Steam hat eine Angabe abgelehnt (8), zum Beispiel Titel, Beschreibung oder Vorschaubild.");

    public static string ResultFileNotFound => T("steam.result.9", "Steam hat den Workshop-Eintrag oder eine Datei nicht gefunden (9).");

    public static string ResultBusy => T("steam.result.10", "Steam ist gerade beschäftigt (10). Versuche es später erneut.");

    public static string ResultAccessDenied => T("steam.result.15", "Steam verweigert den Zugriff (15). Prüfe, ob dieses Konto den Workshop-Eintrag besitzt und das Spiel besitzt.");

    public static string ResultTimeout => T("steam.result.16", "Steam antwortet nicht rechtzeitig (16). Versuche es später erneut.");

    public static string ResultLimit => T("steam.result.25", "Steam meldet ein Limit (25), etwa bei der Größe oder Anzahl. Prüfe die Größe der Mod.");

    public static string ResultOther(int result) => F("steam.result.other", "Steam meldet Fehler {0}.", result);

    public static string ApiDllMissing => T("steam.api.dllMissing", "Die Steamworks-Bibliothek des Spiels wurde nicht gefunden. Ist das Spiel installiert?");

    public static string ApiLoadFailed(string error) => F("steam.api.loadFailed", "Die Steamworks-Bibliothek konnte nicht geladen werden: {0}", error);

    public static string ApiNotRunning => T("steam.api.notRunning", "Steam läuft nicht oder du bist nicht angemeldet. Starte Steam und melde dich an.");

    public static string ApiNoUgc => T("steam.api.noUgc", "Steam hat die Workshop-Schnittstelle nicht bereitgestellt.");

    public static string ApiMissingExport(string name) => F("steam.api.missingExport", "Die Steamworks-Bibliothek dieser Version bietet {0} nicht an.", name);

    public static string SourceSubscribed => T("steam.finder.subscribed", "abonniert (lokal)");

    public static string SourceProfile => T("steam.finder.profile", "Steam-Profil");

    public static string Staging => T("steam.publish.staging", "Dateien werden zusammengestellt …");

    public static string StagingFailed(string error) => F("steam.publish.stagingFailed", "Die Dateien konnten nicht zusammengestellt werden: {0}", error);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
