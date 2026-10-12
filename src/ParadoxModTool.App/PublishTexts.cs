using ParadoxModTool.Localization;

namespace ParadoxModTool.App;

/// <summary>
/// Texte der Fenster zum Veröffentlichen (Steam, GitHub), der Releases und ihrer Dialoge. Die Keys stehen in <c>locales/de.json</c> und
/// <c>locales/en.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
public static class PublishTexts
{
    // ---- Gemeinsam

    public static string Publish => T("publish.publish", "Veröffentlichen");

    public static string Checks => T("publish.checks", "Prüfungen");

    public static string Output => T("publish.output", "Ausgabe");

    public static string Add => T("publish.add", "Hinzufügen");

    public static string Edit => T("publish.edit", "Bearbeiten");

    public static string List => T("publish.list", "Liste");

    public static string SaveChange => T("publish.saveChange", "Änderung speichern");

    public static string Revert => T("publish.revert", "Verwerfen");

    public static string AndMore(int count) => F("publish.andMore", "… und {0} weitere", count);

    public static string DownloadFailed(string error) => F("publish.downloadFailed", "Der Download ist fehlgeschlagen: {0}", error);

    public static string ReleaseMarkFailed(string error) => F("publish.releaseMarkFailed", "Der Vermerk in den Releases konnte nicht gespeichert werden: {0}", error);

    // ---- GitHub

    public static string GitHubTitle => T("publish.github.title", "Auf GitHub veröffentlichen");

    public static string GitHubTitleFor(string mod) => F("publish.github.titleFor", "Auf GitHub veröffentlichen: {0}", mod);

    public static string GitHubPublishTip => T("publish.github.publish.tip", "Die Version dieser Mod als Release auf GitHub veröffentlichen (mehrere Repositories und ZIP-Varianten möglich)");

    public static string GhDownload => T("publish.github.ghDownload", "gh herunterladen");

    public static string GhDownloadTip => T("publish.github.ghDownload.tip", "gh ist das offizielle Kommandozeilen-Werkzeug von GitHub. Das Programm braucht es, um Releases anzulegen; es startet es nur dafür. Der Download kommt von github.com (ca. 15 MB), die Prüfsumme wird kontrolliert. Deine Anmeldung bei GitHub verwaltet gh selbst, das Programm sieht sie nie.");

    public static string GhLogin => T("publish.github.ghLogin", "Bei GitHub anmelden");

    public static string GhLoginTip => T("publish.github.ghLogin.tip", "Öffnet gh in einem eigenen Fenster. Dort meldest du dich im Browser bei GitHub an; das Programm sieht nichts davon.");

    public static string GhRefresh => T("publish.github.ghRefresh", "Status aktualisieren");

    public static string GhRefreshTip => T("publish.github.ghRefresh.tip", "Prüft erneut, ob gh vorhanden und angemeldet ist, und ob die Repositories erreichbar sind");

    public static string RepoOrigin => T("publish.github.repoOrigin", "Repo aus origin übernehmen");

    public static string RepoOriginTip => T("publish.github.repoOrigin.tip", "Der Mod-Ordner ist ein Git-Ordner mit einem GitHub-origin, der noch nicht in der Liste steht");

    public static string ReleaseTitleLabel => T("publish.github.releaseTitle", "Titel:");

    public static string NotesLabel => T("publish.github.notes", "Release-Notes (Markdown):");

    public static string NotesTip => T("publish.github.notes.tip", "Vorbelegt aus dem Changelog. Änderungen hier gelten nur für diesen Lauf und schreiben nicht in den Changelog.");

    public static string Draft => T("publish.github.draft", "Als Entwurf anlegen");

    public static string DraftTip => T("publish.github.draft.tip", "Ein Entwurf ist auf GitHub nur für Mitarbeiter mit Schreibrecht sichtbar, bis du ihn dort veröffentlichst");

    public static string PreRelease => T("publish.github.preRelease", "Als Vorabversion markieren");

    public static string UpdateExisting => T("publish.github.updateExisting", "Vorhandenes Release aktualisieren, falls der Tag schon existiert");

    public static string UpdateExistingTip => T("publish.github.updateExisting.tip", "Ersetzt Titel, Notes und ZIP-Dateien eines schon vorhandenen Releases mit diesem Tag");

    public static string ZipVariants => T("publish.github.zipVariants", "ZIP-Varianten");

    public static string VariantListTip => T("publish.github.variantList.tip", "Zeigt, was bei der gewählten Variante ausgeschlossen wird");

    public static string VariantBuild => T("publish.github.variantBuild", "ZIP lokal erstellen");

    public static string VariantBuildTip => T("publish.github.variantBuild.tip", "Erstellt die angehakten ZIP-Dateien in einem Ordner deiner Wahl, ohne etwas zu veröffentlichen");

    public static string Result => T("publish.github.result", "Ergebnis");

    public static string OpenRelease => T("publish.github.openRelease", "Release öffnen");

    public static string GitRefresh => T("publish.github.git.refresh", "Aktualisieren");

    public static string GitCommit => T("publish.github.git.commit", "Alle Änderungen committen");

    public static string GitCommitTip => T("publish.github.git.commit.tip", "Führt git add -A und git commit aus. Vorher siehst du die Dateiliste und bestätigst.");

    public static string GitPush => T("publish.github.git.push", "Pushen");

    public static string GitPushTip => T("publish.github.git.push.tip", "Pusht den Branch (nie erzwungen). Ohne Upstream wird er mit -u origin angelegt.");

    public static string CommitMessageLabel => T("publish.github.git.commitMessage", "Commit-Nachricht:");

    public static string CleanupTab => T("publish.github.cleanup.tab", "Aufräumen");

    public static string CleanupLoad => T("publish.github.cleanup.load", "Laden");

    public static string CleanupLoadTip => T("publish.github.cleanup.load.tip", "Holt Releases, Tags und Branches des gewählten Repositories von GitHub");

    public static string CleanupWithTag => T("publish.github.cleanup.withTag", "Auch den Tag löschen");

    public static string PublishRunning => T("publish.github.running", "Die Veröffentlichung läuft noch. Brich sie ab oder warte, bis sie fertig ist.");

    public static string NotesCount(int length, int max) => F("publish.github.notesCount", "{0:N0} von {1:N0} Zeichen", length, max);

    public static string GhChecking => T("publish.github.ghChecking", "gh wird geprüft …");

    public static string GhMissing => T("publish.github.ghMissing", "gh (GitHub CLI) ist nicht eingerichtet. Lade es herunter oder installiere es selbst (winget install GitHub.cli).");

    public static string GhLoggedIn(string path) => F("publish.github.ghLoggedIn", "gh: {0}\nBei GitHub angemeldet.", path);

    public static string GhNotLoggedIn(string path) => F("publish.github.ghNotLoggedIn", "gh: {0}\nBei GitHub ist keine Anmeldung aktiv.", path);

    public static string GhDownloadConfirm(string folder) => F("publish.github.ghDownload.confirm", "gh (GitHub CLI) wird von GitHub heruntergeladen.\n\nQuelle: github.com/cli/cli (offizielle Veröffentlichung)\nGröße: ca. 15 MB\nZiel: {0}\n\nDie Prüfsumme der Datei wird kontrolliert; bei Abweichung wird nichts installiert.\n\nFortfahren?", folder);

    public static string GhLoginStarted => T("publish.github.ghLogin.started", "gh wurde in einem eigenen Fenster gestartet.\n\nFolge dort dem Ablauf (GitHub im Browser bestätigen). Das Programm sieht nichts davon. Wenn gh fertig ist, klicke hier auf \"Status aktualisieren\".");

    public static string ReposSaveFailed(string error) => F("publish.github.reposSaveFailed", "Die Repositories konnten nicht gespeichert werden: {0}", error);

    public static string RepoRemoveConfirm(string repo) => F("publish.github.repoRemove.confirm", "Das Repository {0} aus der Liste dieser Mod entfernen?\nAuf GitHub wird nichts gelöscht.", repo);

    public static string NotGitFolder => T("publish.github.git.notGit", "Der Mod-Ordner ist kein Git-Ordner (oder git ist nicht installiert). Die Git-Funktionen sind aus.");

    public static string GitInfo(string folder, string origin, string branch, string upstream, string head, string subject, int ahead, int behind, int changes, string pushed) =>
        F("publish.github.git.info", "Ordner: {0}\norigin: {1}\nBranch: {2}    Upstream: {3}\nHEAD: {4}  {5}\nvorn: {6}    hinten: {7}    Änderungen: {8}    HEAD gepusht: {9}", folder, origin, branch, upstream, head, subject, ahead, behind, changes, pushed);

    public static string GitNone => T("publish.github.git.none", "keiner");

    public static string GitDetached => T("publish.github.git.detached", "(losgelöster HEAD)");

    public static string GitYes => T("publish.github.git.yes", "ja");

    public static string GitNo => T("publish.github.git.no", "nein");

    public static string CommitConfirm(int count, string files, string more, string message) =>
        F("publish.github.git.commit.confirm", "{0} Datei(en) werden committet (git add -A, die .gitignore gilt):\n\n{1}{2}\n\nNachricht: {3}\n\nFortfahren?", count, files, more, message);

    public static string PushConfirm(string? branch, string target) => F("publish.github.git.push.confirm", "Den Branch {0} nach {1} pushen? (nie erzwungen)", branch, target);

    public static string VariantOnly(string folders) => F("publish.github.variant.only", "nur {0}", folders);

    public static string VariantRoot(string folder) => F("publish.github.variant.root", "Wurzel {0}", folder);

    public static string VariantInfo(int files, string size, int excluded) => F("publish.github.variant.info", "Dateien: {0:N0} ({1}), ausgeschlossen: {2:N0} Einträge", files, size, excluded);

    public static string VariantsSaveFailed(string error) => F("publish.github.variantsSaveFailed", "Die ZIP-Varianten konnten nicht gespeichert werden: {0}", error);

    public static string VariantRemoveConfirm(string name) => F("publish.github.variantRemove.confirm", "Die ZIP-Variante \"{0}\" entfernen?", name);

    public static string VariantNothingExcluded => T("publish.github.variant.nothingExcluded", "Bei dieser Variante wird nichts ausgeschlossen.");

    public static string VariantExcludedList(string lines) => F("publish.github.variant.excludedList", "Nicht im ZIP:\n\n{0}", lines);

    public static string ZipNeedsVersion => T("publish.github.zipNeedsVersion", "Ohne lesbare metadata.json mit gültiger Version kann ich keine ZIP-Dateien erstellen.");

    public static string ZipNeedsVersionClassic => T("publish.github.zipNeedsVersionClassic", "Ohne lesbare .mod-Datei mit gültiger Version kann ich keine ZIP-Dateien erstellen.");

    public static string ZipNeedsVersionFor(bool classic) => classic ? ZipNeedsVersionClassic : ZipNeedsVersion;

    public static string NoVariantChecked => T("publish.github.noVariantChecked", "Es ist keine ZIP-Variante angehakt.");

    public static string ZipFolderPick => T("publish.github.zipFolderPick", "Ordner für die ZIP-Dateien wählen");

    public static string ZipsCreated(int count, string target) => F("publish.github.zipsCreated", "{0} ZIP-Datei(en) erstellt in:\n{1}", count, target);

    public static string GitHubNotReady => T("publish.github.notReady", "Die Veröffentlichung ist noch nicht möglich. Die roten Hinweise unter \"Prüfungen\" zeigen, was fehlt.");

    public static string GitNotClean(string warnings) => F("publish.github.gitNotClean", "Der Git-Stand des Mod-Ordners ist nicht sauber:\n\n{0}\n\nJa: Trotzdem veröffentlichen (der Tag zeigt auf den Standard-Branch des Repos).\nNein: Zum Git-Reiter.\nAbbrechen: nichts tun.", warnings);

    public static string GitHubConfirm(string? version, string mod) => F("publish.github.confirm", "Version {0} von „{1}“ als Release veröffentlichen?", version, mod);

    public static string ConfirmZips(string zips) => F("publish.github.confirm.zips", "ZIP-Dateien: {0}", zips);

    public static string ConfirmNoZips => T("publish.github.confirm.noZips", "keine");

    public static string ConfirmDraft => T("publish.github.confirm.draft", "Als Entwurf");

    public static string ConfirmPreRelease => T("publish.github.confirm.preRelease", "Als Vorabversion");

    public static string GitHubPublishFailed(string error) => F("publish.github.publishFailed", "Die Veröffentlichung ist fehlgeschlagen: {0}", error);

    public static string ResultSummary(int succeeded, int total) => F("publish.github.resultSummary", "{0} von {1} Repositories veröffentlicht.", succeeded, total);

    public static string CleanupGhMissing => T("publish.github.cleanup.ghMissing", "gh ist nicht eingerichtet oder nicht angemeldet.");

    public static string CleanupNoRepo => T("publish.github.cleanup.noRepo", "Es ist kein Repository gewählt.");

    public static string CleanupLoading => T("publish.github.cleanup.loading", "Wird geladen …");

    public static string CleanupDraft => T("publish.github.cleanup.draft", "(Entwurf)");

    public static string CleanupPreRelease => T("publish.github.cleanup.preRelease", "(Vorabversion)");

    public static string CleanupDefaultBranch => T("publish.github.cleanup.defaultBranch", "(Standard-Branch)");

    public static string CleanupProtected => T("publish.github.cleanup.protected", "(geschützt)");

    public static string CleanupSummary(string repo, int releases, int tags, int branches) =>
        F("publish.github.cleanup.summary", "{0}: {1} Release(s), {2} Tag(s), {3} Branch(es).", repo, releases, tags, branches);

    public static string CleanupLoadFailed(string error) => F("publish.github.cleanup.loadFailed", "Das Laden ist fehlgeschlagen: {0}", error);

    public static string CleanupConfirmDelete(string what, string repo, string extra) =>
        F("publish.github.cleanup.confirmDelete", "{0} in {1} löschen?{2}\n\nDas lässt sich nicht rückgängig machen.", what, repo, extra);

    public static string CleanupMarkFailed(string error) => F("publish.github.cleanup.markFailed", "Der Vermerk in den Releases konnte nicht entfernt werden: {0}", error);

    public static string CleanupDeleteFailed(string error) => F("publish.github.cleanup.deleteFailed", "Das Löschen ist fehlgeschlagen: {0}", error);

    public static string CleanupReleaseWhat(string name) => F("publish.github.cleanup.releaseWhat", "Das Release {0}", name);

    public static string CleanupTagAlso => T("publish.github.cleanup.tagAlso", "Der Tag wird mitgelöscht.");

    public static string CleanupTagWhat(string name) => F("publish.github.cleanup.tagWhat", "Den Tag {0}", name);

    public static string CleanupBranchWhat(string name) => F("publish.github.cleanup.branchWhat", "Den Branch {0}", name);

    public static string CleanupDefaultBranchKept => T("publish.github.cleanup.defaultBranchKept", "Der Standard-Branch eines Repositories wird nie gelöscht.");

    public static string CleanupBranchPromptTitle => T("publish.github.cleanup.branchPrompt.title", "Branch löschen");

    public static string CleanupBranchPrompt(string name) => F("publish.github.cleanup.branchPrompt", "Tippe zur Bestätigung den Namen des Branches ein:\n{0}", name);

    public static string CleanupBranchMismatch => T("publish.github.cleanup.branchMismatch", "Der Name stimmt nicht überein. Der Branch wurde nicht gelöscht.");

    // ---- Repository- und ZIP-Varianten-Dialog

    public static string RepoAddTitle => T("publish.repoDialog.addTitle", "Repository hinzufügen");

    public static string RepoEditTitle => T("publish.repoDialog.editTitle", "Repository bearbeiten");

    public static string TagFormatLabel => T("publish.repoDialog.tagFormat", "Tag-Format:");

    public static string TagFormatHint => T("publish.repoDialog.tagFormat.hint", "Platzhalter: {version} (Pflicht) und {mod}. Beispiel: v{version} ergibt v2.0.5.");

    public static string RepoInvalid => T("publish.repoDialog.repoInvalid", "Das Repository muss die Form owner/name haben (Buchstaben, Ziffern, - _ .).");

    public static string TagFormatInvalid => T("publish.repoDialog.tagFormatInvalid", "Das Tag-Format braucht den Platzhalter {version}.");

    public static string ZipAddTitle => T("publish.zipDialog.addTitle", "ZIP-Variante hinzufügen");

    public static string ZipEditTitle => T("publish.zipDialog.editTitle", "ZIP-Variante bearbeiten");

    public static string ZipFileName => T("publish.zipDialog.fileName", "Dateiname:");

    public static string ZipFileNameHint => T("publish.zipDialog.fileName.hint", "Platzhalter: {mod}, {version}, {tag}, {variant}. \".zip\" wird bei Bedarf ergänzt.");

    public static string ZipExcludedFolders => T("publish.zipDialog.excludedFolders", "Zusätzlich ausgeschlossene Ordner (nach Name, an beliebiger Tiefe):");

    public static string ZipExcludedFoldersHint => T("publish.zipDialog.excludedFolders.hint", "Ein Ordnername je Zeile, z. B. docs");

    public static string ZipExcludedFiles => T("publish.zipDialog.excludedFiles", "Zusätzlich ausgeschlossene Dateien (Name oder *.Endung):");

    public static string ZipExcludedFilesHint => T("publish.zipDialog.excludedFiles.hint", "Ein Eintrag je Zeile, z. B. *.psd");

    public static string ZipOnlyFolders => T("publish.zipDialog.onlyFolders", "Nur diese Ordner (relativ zum Mod-Ordner; leer = alles):");

    public static string ZipOnlyFoldersHint => T("publish.zipDialog.onlyFolders.hint", "Ein Ordner je Zeile, z. B. common oder gfx/icons");

    public static string ZipRootFolder => T("publish.zipDialog.rootFolder", "Wurzelordner im Archiv (optional):");

    public static string ZipRootFolderHint => T("publish.zipDialog.rootFolder.hint", "Leer = die Dateien liegen direkt im Archiv");

    public static string ZipNameMissing => T("publish.zipDialog.nameMissing", "Der Name fehlt.");

    // ---- Steam

    public static string SteamTitle => T("publish.steam.title", "Auf Steam veröffentlichen");

    public static string SteamTitleFor(string mod) => F("publish.steam.titleFor", "Auf Steam veröffentlichen: {0}", mod);

    public static string SteamPublishTip => T("publish.steam.publish.tip", "Die Version dieser Mod auf den Steam Workshop hochladen (erstes Veröffentlichen oder Update)");

    public static string SteamGameLine(string game, string appId) => F("publish.steam.gameLine", "Spiel: {0}   ·   Steam-AppID: {1}", game, appId);

    public static string OpenPage => T("publish.steam.openPage", "Workshop-Seite öffnen");

    public static string FindId => T("publish.steam.findId", "Workshop-ID suchen");

    public static string FindIdTip => T("publish.steam.findId.tip", "Sucht die Workshop-ID dieser Mod. Zuerst lokal: Steam legt abonnierte Workshop-Mods im eigenen Ordner ab, der Ordnername ist die ID. Findet sich dort nichts, kann das Programm nach Rückfrage die öffentliche Workshop-Seite deines Steam-Profils abrufen und den Titel vergleichen. Es nutzt dafür weder deine Anmeldung noch Cookies.");

    public static string WorkshopIdLabel => T("publish.steam.workshopId", "Workshop-ID:");

    public static string WorkshopIdTip => T("publish.steam.workshopId.tip", "Leer oder 0: Es wird eine neue Workshop-Seite angelegt. Für eine schon vorhandene Seite: mit \"Workshop-ID suchen\" finden lassen, die ID oder die ganze Workshop-Adresse hier einfügen.");

    public static string NewBanner => T("publish.steam.newBanner", "Neue Version im Changelog anlegen");

    public static string NewBannerTip => T("publish.steam.newBanner.tip", "Legt oben im Changelog ein Banner für diese Version an");

    public static string VersionTip => T("publish.steam.version.tip", "Ändert nur das Feld version der Datei (nicht das in relationships). Mit Enter übernehmen, danach speichern.");

    public static string VersionTipClassic => T("publish.steam.version.tipClassic", "Ändert nur das Feld version der Datei. Mit Enter übernehmen, danach speichern.");

    public static string NextVersionTip => T("publish.steam.nextVersion.tip", "Den letzten Teil der Version um eins erhöhen (2.0.5 wird 2.0.6)");

    public static string MetaSaveTip => T("publish.steam.metaSave.tip", "Schreibt die Datei (Strg+S). Vorher wird eine Sicherung angelegt; ungültiges JSON wird nicht gespeichert.");

    public static string UploadVia => T("publish.steam.uploadVia", "Hochladen über");

    public static string MethodClient => T("publish.steam.method.client", "Steam-Client (empfohlen)");

    public static string MethodClientTip => T("publish.steam.method.client.tip", "Lädt über dein laufendes, angemeldetes Steam hoch. Es ist nichts einzurichten, keine Anmeldung, keine Konsole. Das Programm nutzt dafür die Steamworks-Bibliothek deines installierten Spiels; es sieht dein Passwort nie.");

    public static string MethodSteamCmdTip => T("publish.steam.method.steamCmd.tip", "Valves Kommandozeilen-Werkzeug. Braucht einen Download und eine einmalige Anmeldung in einer Konsole.");

    public static string SteamCmdDownload => T("publish.steam.steamCmdDownload", "SteamCMD herunterladen");

    public static string SteamCmdDownloadTip => T("publish.steam.steamCmdDownload.tip", "Lädt SteamCMD (ca. 3 MB) direkt von Valve herunter und legt es in den Programmordner. SteamCMD ist Valves Kommandozeilen-Werkzeug zum Hochladen von Workshop-Inhalten. Das Programm braucht es nur zum Veröffentlichen und startet es sonst nie. Dein Passwort gibst du selbst in SteamCMDs eigenem Fenster ein, das Programm sieht es nicht.");

    public static string PickExisting => T("publish.steam.pickExisting", "Vorhandene wählen");

    public static string PickExistingTip => T("publish.steam.pickExisting.tip", "Eine schon installierte steamcmd.exe verwenden");

    public static string SteamLogin => T("publish.steam.login", "Steam-Anmeldung einrichten");

    public static string SteamLoginTip => T("publish.steam.login.tip", "Öffnet SteamCMD in einem eigenen Fenster. Dort gibst du Passwort und Steam-Guard-Code selbst ein; das Programm sieht sie nicht.");

    public static string UserLabel => T("publish.steam.user", "Steam-Benutzername:");

    public static string UserTip => T("publish.steam.user.tip", "Der Kontoname, mit dem du dich bei Steam anmeldest (nicht die E-Mail-Adresse). Ist das Feld leer, wird das zuletzt bei Steam angemeldete Konto vorgeschlagen.");

    public static string ChangeNote => T("publish.steam.changeNote", "Changenote");

    public static string BbCode => T("publish.steam.bbCode", "In Steam-BBCode umwandeln");

    public static string BbCodeTip => T("publish.steam.bbCode.tip", "Wandelt Aufzählungen (- Text) und Unterbanner in Steam-Formatierung um");

    public static string NoteTip => T("publish.steam.note.tip", "Vorbelegt aus dem Changelog. Änderungen hier gelten nur für diesen Upload und schreiben nicht in den Changelog.");

    public static string VisibilityLabel => T("publish.steam.visibility", "Sichtbarkeit:");

    public static string VisibilityUnchanged => T("publish.steam.visibility.unchanged", "Unverändert");

    public static string VisibilityPublic => T("publish.steam.visibility.public", "Öffentlich");

    public static string VisibilityFriends => T("publish.steam.visibility.friends", "Nur Freunde");

    public static string VisibilityPrivate => T("publish.steam.visibility.private", "Nur ich");

    public static string Listing => T("publish.steam.listing", "Titel, Beschreibung und Vorschaubild auch aktualisieren");

    public static string ListingTip => T("publish.steam.listing.tip", "Überschreibt Titel (name), Beschreibung (short_description) und Vorschaubild (picture) der Workshop-Seite mit den Werten aus der metadata.json. Beim ersten Veröffentlichen immer an.");

    public static string ListingTipClassic => T("publish.steam.listing.tipClassic", "Überschreibt Titel (name) und Vorschaubild (picture) der Workshop-Seite mit den Werten aus der .mod-Datei; die Beschreibung bleibt. Beim ersten Veröffentlichen immer an.");

    public static string ScopeListTip => T("publish.steam.scopeList.tip", "Zeigt, was aus dem Upload ausgeschlossen wird");

    public static string UploadRunning => T("publish.steam.running", "Der Upload läuft noch. Brich ihn ab oder warte, bis er fertig ist.");

    public static string MetaMissing => T("publish.steam.metaMissing", "Es wurde keine lesbare metadata.json im Ordner der Mod gefunden.");

    public static string MetaReadFailed(string error) => F("publish.steam.metaReadFailed", "metadata.json konnte nicht gelesen werden: {0}", error);

    public static string VersionInvalid => T("publish.steam.versionInvalid", "Das ist keine gültige Version (erwartet z. B. 2.0.6).");

    public static string MetaNotSaved(string error) => F("publish.steam.metaNotSaved", "{0} Die Datei wurde nicht gespeichert.", error);

    public static string MetaChangedOutside => T("publish.steam.metaChangedOutside", "Die metadata.json wurde von außen geändert.\nTrotzdem überschreiben?");

    public static string MetaSaveFailed(string error) => F("publish.steam.metaSaveFailed", "Die Datei konnte nicht gespeichert werden: {0}", error);

    public static string MetaUnsaved => T("publish.steam.metaUnsaved", "Die metadata.json hat ungespeicherte Änderungen.\nJetzt speichern?");

    // Klassische .mod-Datei (EU IV, CK III): die Texte nennen die gezeigte Datei statt der metadata.json.
    public static string MetaMissingClassic => T("publish.steam.metaMissingClassic", "Für diese Mod wurde keine lesbare .mod-Datei gefunden.");

    public static string MetaReadFailedFile(string file, string error) => F("publish.steam.metaReadFailedFile", "{0} konnte nicht gelesen werden: {1}", file, error);

    public static string MetaChangedOutsideFile(string file) => F("publish.steam.metaChangedOutsideFile", "{0} wurde von außen geändert.\nTrotzdem überschreiben?", file);

    public static string MetaUnsavedFile(string file) => F("publish.steam.metaUnsavedFile", "{0} hat ungespeicherte Änderungen.\nJetzt speichern?", file);

    public static string MetaSaveTipClassic => T("publish.steam.metaSave.tipClassic", "Schreibt die Datei (Strg+S). Vorher wird eine Sicherung angelegt; Text mit unausgeglichenen Klammern oder offenem Anführungszeichen wird nicht gespeichert.");

    public static string MetaShownFile(string file) => F("publish.steam.metaShownFile", "Angezeigt: {0}", file);

    public static string MetaShownFileSynced(string file, string other) => F("publish.steam.metaShownFileSynced", "Angezeigt: {0}. Eine Änderung der Version wird auch in {1} geschrieben; alle anderen Änderungen nur in die angezeigte Datei.", file, other);

    public static string MetaSyncFailed(string file, string other, string error) => F("publish.steam.metaSyncFailed", "{0} wurde gespeichert, aber {1} konnte nicht angepasst werden: {2}", file, other, error);

    public static string MetaSyncBeforeUploadFailed(string version, string other, string error) => F("publish.steam.metaSyncBeforeUploadFailed", "Vor dem Upload ließ sich {1} nicht auf Version {0} bringen, deshalb wurde nichts hochgeladen: {2}", version, other, error);

    public static string WorkshopIdFromMod => T("publish.steam.workshopIdFromMod", "Workshop-ID aus remote_file_id der .mod-Datei übernommen.");

    public static string SteamBannerCreated(string version) => F("publish.steam.bannerCreated", "Das Banner für Version {0} steht jetzt oben im Changelog. Trage die Änderungen im Fenster \"Releases\" ein (Textfeld rechts).", version);

    public static string ScopeMore(int count) => F("publish.steam.scope.more", " … (+{0} weitere)", count);

    public static string ScopeText(int files, string size, string excluded) => F("publish.steam.scope.text", "Dateien: {0:N0} ({1}). Ausgeschlossen: {2}", files, size, excluded);

    public static string ScopeNothing => T("publish.steam.scope.nothing", "nichts");

    public static string ScopeNothingExcluded => T("publish.steam.scope.nothingExcluded", "Es wird nichts ausgeschlossen.");

    public static string ScopeExcludedList(string lines) => F("publish.steam.scope.excludedList", "Nicht hochgeladen werden:\n\n{0}", lines);

    public static string NoteCount(int length, int max) => F("publish.steam.noteCount", "{0:N0} / {1:N0} Zeichen", length, max);

    public static string FindNeedsMetadata => T("publish.steam.find.needsMetadata", "Ohne lesbare metadata.json und ohne Steam-AppID im Spielprofil kann ich nicht suchen.");

    public static string FindNeedsMetadataClassic => T("publish.steam.find.needsMetadataClassic", "Ohne lesbare .mod-Datei und ohne Steam-AppID im Spielprofil kann ich nicht suchen.");

    public static string FindSeveralLocal => T("publish.steam.find.severalLocal", "Mehrere abonnierte Workshop-Mods passen zu dieser Mod. Wähle die richtige:");

    public static string FindNoAccount => T("publish.steam.find.noAccount", "Lokal wurde keine passende Workshop-Seite gefunden, und auf diesem Rechner ist kein Steam-Konto bekannt.\n\nTrage die ID von Hand ein oder füge die Adresse der Workshop-Seite in das Feld ein.");

    public static string FindAskProfile(string persona, string steamId) =>
        F("publish.steam.find.askProfile", "Lokal wurde keine passende Workshop-Seite gefunden.\n\nSoll ich die öffentliche Workshop-Seite des Profils „{0}“ auf steamcommunity.com abrufen?\n(steamcommunity.com/profiles/{1}/myworkshopfiles)\n\nEs wird keine Anmeldung und kein Cookie benutzt. Als „Nur ich“ oder „Versteckt“ hochgeladene Seiten sind dort nicht sichtbar.", persona, steamId);

    public static string FindFetchFailed(string error) => F("publish.steam.find.fetchFailed", "Die Workshop-Seite konnte nicht abgerufen werden: {0}", error);

    public static string FindNoPages(string persona) => F("publish.steam.find.noPages", "Auf dem öffentlichen Profil „{0}“ gibt es keine Workshop-Seiten für dieses Spiel. Vielleicht ist die Seite nur für dich sichtbar.\n\nTrage die ID von Hand ein oder füge die Adresse der Workshop-Seite in das Feld ein.", persona);

    public static string FindSeveralExact => T("publish.steam.find.severalExact", "Mehrere Seiten haben genau den Titel der Mod. Wähle die richtige:");

    public static string FindNoExact => T("publish.steam.find.noExact", "Keine Seite hat genau den Titel der Mod. Wähle die richtige oder brich ab:");

    public static string FindTaken(string id, string title, string source) => F("publish.steam.find.taken", "Workshop-ID {0} übernommen: „{1}“ ({2}). Sie wird gespeichert, sobald du hochlädst.", id, title, source);

    public static string SteamNotRunning => T("publish.steam.client.notRunning", "Steam läuft nicht. Starte Steam und melde dich an.");

    public static string ClientReady(string game) => F("publish.steam.client.ready", "Bereit: Steam läuft. Der Upload nutzt deine bestehende Steam-Anmeldung, es ist nichts einzurichten. Steam zeigt dich nur solange der Upload läuft als \"{0} spielend\" an; starte das Spiel nicht gleichzeitig.", game);

    public static string SteamCmdMissing => T("publish.steam.cmd.missing", "SteamCMD ist nicht eingerichtet. Lade es herunter oder wähle eine vorhandene steamcmd.exe.");

    public static string SteamCmdUser(string user) => F("publish.steam.cmd.user", "Benutzer: {0} (ob die Anmeldung gilt, zeigt sich beim Upload)", user);

    public static string SteamCmdDownloadConfirm(string folder) => F("publish.steam.cmd.download.confirm", "SteamCMD wird von Valve heruntergeladen.\n\nDatei: steamcmd.zip\nQuelle: steamcdn-a.akamaihd.net (Valve)\nGröße: ca. 3 MB\nZiel: {0}\n\nBeim ersten Start lädt sich SteamCMD noch selbst nach (ca. 100 MB).\n\nFortfahren?", folder);

    public static string SteamCmdPickTitle => T("publish.steam.cmd.pickTitle", "steamcmd.exe wählen");

    public static string SteamCmdPickFilter => T("publish.steam.cmd.pickFilter", "steamcmd.exe|steamcmd.exe|Programme (*.exe)|*.exe");

    public static string SteamCmdLoginStarted => T("publish.steam.cmd.loginStarted", "SteamCMD wurde in einem eigenen Fenster gestartet.\n\nGib dort dein Passwort und den Steam-Guard-Code ein (das Programm sieht sie nicht). Warte, bis „Logged in OK“ erscheint, und schließe das Fenster mit dem Befehl quit.\n\nBeim allerersten Start lädt SteamCMD sich zuerst selbst nach; das kann einige Minuten dauern.");

    public static string SteamNotReady => T("publish.steam.notReady", "Der Upload ist noch nicht möglich. Die roten Hinweise unter \"Prüfungen\" zeigen, was fehlt.");

    public static string NoteTooLong(int length, int max) => F("publish.steam.noteTooLong", "Die Changenote ist zu lang ({0:N0} von {1:N0} Zeichen). Kürze sie.", length, max);

    public static string NoteEmpty => T("publish.steam.noteEmpty", "Die Changenote ist leer. Trotzdem hochladen?");

    public static string SteamConfirm(string version, string name) => F("publish.steam.confirm", "Version {0} von „{1}“ auf Steam hochladen?", version, name);

    public static string ConfirmNewPage => T("publish.steam.confirm.newPage", "Es wird eine neue Workshop-Seite angelegt.");

    public static string ConfirmUpdatePage(string id) => F("publish.steam.confirm.updatePage", "Die Workshop-Seite {0} wird aktualisiert.", id);

    public static string ConfirmVisibility(string visibility) => F("publish.steam.confirm.visibility", "Sichtbarkeit: {0}", visibility);

    public static string ConfirmVisibilityUnchanged => T("publish.steam.confirm.visibilityUnchanged", "unverändert");

    public static string ConfirmFiles(int? files) => F("publish.steam.confirm.files", "Dateien: {0:N0}", files);

    public static string WorkshopIdSaveFailed(string error) => F("publish.steam.workshopIdSaveFailed", "Die Workshop-ID konnte nicht gespeichert werden: {0}", error);

    public static string RemoteFileIdWriteFailed(string file, string error) => F(
        "publish.steam.remoteFileIdWriteFailed", "Die Workshop-ID konnte nicht als remote_file_id in {0} eingetragen werden: {1}", file, error);

    public static string PageAlreadyCreated(string id) => F("publish.steam.pageAlreadyCreated", "Die Workshop-Seite {0} wurde schon angelegt; ein erneuter Versuch aktualisiert sie.", id);

    public static string LegalAgreement => T("publish.steam.legalAgreement", "Steam verlangt, dass du der Workshop-Vereinbarung zustimmst, bevor die Seite sichtbar wird. Das machst du selbst auf der Steam-Seite.\n\nSeite jetzt öffnen?");

    public static string WorkshopPickTitle => T("publish.workshopPick.title", "Workshop-Seite wählen");

    // ---- Releases

    public static string ReleasesTitleFor(string mod) => F("publish.releases.titleFor", "Releases von {0}", mod);

    public static string ReleasesInfo => T("publish.releases.info", "Der Changelog liegt im Mod-Ordner und wird mit hochgeladen. Sicherungen vor jeder Änderung liegen in den Programmdaten.");

    public static string PickChangelog => T("publish.releases.pickChangelog", "Wählen");

    public static string PickChangelogTip => T("publish.releases.pickChangelog.tip", "Die Changelog-Datei dieser Mod festlegen (innerhalb des Mod-Ordners)");

    public static string ChangelogFileLabel => T("publish.releases.changelogFile", "Changelog-Datei:");

    public static string RecordedReleases => T("publish.releases.recorded", "Erfasste Releases");

    public static string Record => T("publish.releases.record", "Release erfassen");

    public static string RecordTip => T("publish.releases.record.tip", "Die Version aus der Metadaten-Datei der Mod mit dem heutigen Datum als Release erfassen");

    public static string Import => T("publish.releases.import", "Aus Changelog übernehmen");

    public static string ImportTip => T("publish.releases.import.tip", "Versionen aus dem Changelog erfassen, die noch fehlen (ohne Datum)");

    public static string NewVersion => T("publish.releases.newVersion", "Neue Version anlegen");

    public static string NewVersionTip => T("publish.releases.newVersion.tip", "Legt ein Versions-Banner oben im Changelog an und erfasst die Release. Den Text trägst du danach rechts ein.");

    public static string ChangeDate => T("publish.releases.changeDate", "Datum ändern");

    public static string DeleteTip => T("publish.releases.delete.tip", "Die Release aus der Erfassung entfernen (der Changelog bleibt unberührt)");

    public static string ChangelogTextHeader => T("publish.releases.textHeader", "Changelog-Text der gewählten Version (direkt bearbeitbar)");

    public static string SaveTextTip => T("publish.releases.saveText.tip", "Schreibt den Text in den Changelog (Strg+S). Vorher wird eine Sicherung angelegt.");

    public static string RevertTextTip => T("publish.releases.revertText.tip", "Die Änderung verwerfen und den Text aus dem Changelog neu laden");

    public static string NoChangelogSet => T("publish.releases.noChangelogSet", "— keine festgelegt —");

    public static string ChangelogFileMissing(string file) => F("publish.releases.fileMissing", "{0}  — Datei fehlt", file);

    public static string StatusLine(string metadata, string top, string latest) =>
        F("publish.releases.status", "metadata.json: {0}     Changelog oben: {1}     Neueste Release: {2}", metadata, top, latest);

    public static string StatusLineClassic(string metadata, string top, string latest) =>
        F("publish.releases.statusClassic", ".mod-Datei: {0}     Changelog oben: {1}     Neueste Release: {2}", metadata, top, latest);

    public static string ChangelogReadFailed(string error) => F("publish.releases.readFailed", "Der Changelog konnte nicht gelesen werden: {0}", error);

    public static string NoChangelogAvailable => T("publish.releases.noChangelog", "(Kein Changelog verfügbar.)");

    public static string VersionNotInChangelog => T("publish.releases.versionNotInChangelog", "(Diese Version steht nicht im Changelog.)");

    public static string TextChanged(string? version) => F("publish.releases.textChanged", "Der Text der Version {0} wurde geändert.\nIn den Changelog schreiben?", version);

    public static string NoChangelogFile => T("publish.releases.noChangelogFile", "Es ist keine Changelog-Datei festgelegt.");

    public static string ModFolderMissing => T("publish.releases.modFolderMissing", "Der Ordner der Mod ist nicht vorhanden. Verknüpfe ihn zuerst im Mods-Fenster neu.");

    public static string PickChangelogTitle => T("publish.releases.pickChangelog.title", "Changelog-Datei wählen");

    public static string PickChangelogFilter => T("publish.releases.pickChangelog.filter", "Textdateien (*.txt;*.md)|*.txt;*.md|Alle Dateien (*.*)|*.*");

    public static string ChangelogOutsideMod => T("publish.releases.changelogOutsideMod", "Die Changelog-Datei muss im Ordner der Mod liegen, damit sie mit hochgeladen wird.");

    public static string MetadataNoVersion => T("publish.releases.metadataNoVersion", "Die Metadaten-Datei der Mod enthält keine gültige Version. Trage sie dort ein (z. B. 1.0.0).");

    public static string AlreadyRecorded(string version) => F("publish.releases.alreadyRecorded", "Die Version {0} ist schon als Release erfasst.", version);

    public static string AllRecorded => T("publish.releases.allRecorded", "Alle Versionen aus dem Changelog sind schon erfasst.");

    public static string NewVersionPrompt => T("publish.releases.newVersion.prompt", "Nummer der neuen Version:");

    public static string NotAVersion(string version) => F("publish.releases.notAVersion", "\"{0}\" ist keine Versionsnummer (erwartet z. B. 2.0.6).", version);

    public static string ReleasesBannerCreated(string version) => F("publish.releases.bannerCreated", "Das Banner für Version {0} steht jetzt oben im Changelog. Trage dort die Änderungen ein (rechts im Textfeld).", version);

    public static string DatePrompt(string version) => F("publish.releases.datePrompt", "Datum der Release {0} (JJJJ-MM-TT, leer = unbekannt):", version);

    public static string DeleteConfirm(string version) => F("publish.releases.delete.confirm", "Die Release {0} aus der Erfassung entfernen?\nDer Changelog bleibt unberührt.", version);

    public static string NoDate => T("publish.releases.noDate", "kein Datum");

    public static string NotInChangelog => T("publish.releases.notInChangelog", "nicht im Changelog");

    // ---- Aus Changelog übernehmen

    public static string ImportAll => T("publish.changelogImport.all", "Alle");

    public static string ImportNone => T("publish.changelogImport.none", "Keine");

    public static string ImportHeader => T("publish.changelogImport.header", "Diese Versionen stehen im Changelog, sind aber noch nicht als Release erfasst. Das Datum bleibt leer und lässt sich danach ändern.");

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
