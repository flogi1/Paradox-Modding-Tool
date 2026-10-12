using ParadoxModTool.Localization;

namespace ParadoxModTool.Mods;

/// <summary>
/// Texte der Mod-Verwaltung (Register, Projekte, Sperren, Releases, Fenster "Mods"). Die Schlüssel stehen in <c>locales/de.json</c>
/// und <c>locales/en.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
public static class ModsTexts
{
    // Rollen.
    public static string RoleDevBranch(string root) => F("mods.role.devBranch", "Dev-Branch von {0}", root);

    public static string RoleSubmod(string root) => F("mods.role.submod", "Submod von {0}", root);

    public static string RoleMain(int count) => F("mods.role.main", "Hauptmod (Projekt, {0} Mods)", count);

    /// <summary>Gleicher Schlüssel wie <c>CompatchTexts.RoleText</c> (Mods kennt das Kompatch-Projekt nicht).</summary>
    public static string CompatchRole(string targets) => F("compatch.role", "Kompatch für {0}", targets);

    // Übertragen zwischen Mods eines Projekts.
    public static string TransferSameMod => T("mods.transfer.sameMod", "Quelle und Ziel sind dieselbe Mod.");

    public static string TransferNotSameProject => T("mods.transfer.notSameProject", "Die Mods gehören nicht zum selben Projekt.");

    public static string TransferTargetLocked(string mod) => F("mods.transfer.targetLocked", "\"{0}\" ist gesperrt.", mod);

    public static string TransferSourceLocked(string mod) => F("mods.transfer.sourceLocked", "\"{0}\" ist gesperrt; daraus wird nichts übertragen.", mod);

    public static string NoExistingFolder(string mod) => F("mods.noExistingFolder", "Die Mod \"{0}\" hat keinen vorhandenen Ordner.", mod);

    public static string TransferSizeMismatch(string file) => F("mods.transfer.sizeMismatch", "Die Zieldatei {0} hat eine andere Größe als die Quelle; die Quelle bleibt erhalten.", file);

    // Changelog, Versionen, Releases.
    public static string ChangelogNoVersion(string version) => F("mods.changelog.noVersion", "Der Changelog enthält keine Version {0}.", version);

    public static string NotAVersion(string version) => F("mods.notAVersion", "\"{0}\" ist keine Versionsnummer.", version);

    public static string ChangelogVersionExists(string version) => F("mods.changelog.versionExists", "Der Changelog enthält die Version {0} schon.", version);

    public static string ReleasesLoadWarning => T("mods.release.loadWarning", "releases.json war nicht lesbar; eine Kopie liegt als releases.json.bad neben der Datei.");

    public static string ReleaseExists(string version) => F("mods.release.exists", "Die Version {0} ist schon als Release erfasst.", version);

    public static string ReleaseNotRecorded(string version) => F("mods.release.notRecorded", "Die Version {0} ist nicht erfasst.", version);

    public static string NotADate(string date) => F("mods.release.notDate", "\"{0}\" ist kein Datum (erwartet yyyy-MM-dd).", date);

    public static string NoReadableMetadata => T("mods.noReadableMetadata", "Keine lesbare metadata.json gefunden.");

    public static string NoValidVersion => T("mods.noValidVersion", "metadata.json enthält keine gültige Version.");

    /// <summary>Wie <see cref="NoReadableMetadata"/>, für Spiele mit klassischer <c>.mod</c>-Datei (EU IV, CK III).</summary>
    public static string NoReadableMetadataClassic => T("mods.noReadableMetadataClassic", "Keine lesbare .mod-Datei gefunden.");

    /// <summary>Wie <see cref="NoValidVersion"/>, für Spiele mit klassischer <c>.mod</c>-Datei.</summary>
    public static string NoValidVersionClassic => T("mods.noValidVersionClassic", "Die .mod-Datei enthält keine gültige Version.");

    public static string NoReadableMetadataFor(bool classic) => classic ? NoReadableMetadataClassic : NoReadableMetadata;

    public static string NoValidVersionFor(bool classic) => classic ? NoValidVersionClassic : NoValidVersion;

    public static string StatusNoBanners => T("mods.status.noBanners", "Der Changelog enthält keine Versions-Banner.");

    public static string StatusMissingSection(string version) => F("mods.status.missingSection", "Im Changelog fehlt ein Abschnitt für Version {0}.", version);

    public static string StatusMismatch(string metadataVersion, string? top) => F("mods.status.mismatch", "metadata.json steht auf {0}, der Changelog beginnt mit {1}.", metadataVersion, top);

    public static string StatusMismatchClassic(string metadataVersion, string? top) => F("mods.status.mismatchClassic", "Die .mod-Datei steht auf {0}, der Changelog beginnt mit {1}.", metadataVersion, top);

    public static string StatusNotReleased(string version) => F("mods.status.notReleased", "Version {0} ist noch nicht als Release erfasst.", version);

    // Veröffentlichen (Prüfungen).
    public static string PublishNoAppId => T("mods.publish.noAppId", "Das Spielprofil enthält keine Steam-AppID.");

    public static string PublishUnsaved => T("mods.publish.unsaved", "metadata.json hat ungespeicherte Änderungen. Speichere sie vor dem Veröffentlichen.");

    public static string PublishUnsavedClassic => T("mods.publish.unsavedClassic", "Die .mod-Datei hat ungespeicherte Änderungen. Speichere sie vor dem Veröffentlichen.");

    public static string PublishWorkshopIdNumber => T("mods.publish.workshopIdNumber", "Die Workshop-ID muss eine Zahl sein.");

    public static string PublishWorkshopIdOwned(string? mod) => F("mods.publish.workshopIdOwned", "Diese Workshop-ID gehört schon zur Mod „{0}“. Ein Upload würde deren Workshop-Seite überschreiben.", mod);

    public static string PublishSteamCmdMissing => T("mods.publish.steamCmdMissing", "SteamCMD ist nicht eingerichtet.");

    public static string PublishSteamUserMissing => T("mods.publish.steamUserMissing", "Der Steam-Benutzername fehlt.");

    public static string PublishNoChangelogSection(string? version) => F("mods.publish.noChangelogSection", "Im Changelog gibt es keinen Abschnitt für Version {0}. Die Changenote bleibt sonst leer.", version);

    public static string PublishNoPreview => T("mods.publish.noPreview", "Das Vorschaubild fehlt. Beim ersten Veröffentlichen braucht die Workshop-Seite eines (Feld picture in der metadata.json).");

    public static string PublishNoPreviewClassic => T("mods.publish.noPreviewClassic", "Das Vorschaubild fehlt. Beim ersten Veröffentlichen braucht die Workshop-Seite eines (Feld picture in der .mod-Datei).");

    public static string PublishAlreadyPublished(string version) => F("mods.publish.alreadyPublished", "Version {0} ist schon auf Steam veröffentlicht. Ein erneuter Upload überschreibt den Inhalt.", version);

    public static string PublishNoFiles => T("mods.publish.noFiles", "Der Mod-Ordner enthält nach dem Ausschluss keine Dateien.");

    public static string PublishDescriptorMissing => T("mods.publish.descriptorMissing", "Im Mod-Ordner fehlt descriptor.mod. Steam lädt nur den Ordner hoch; ohne descriptor.mod erkennt der Launcher die Mod nicht.");

    // Metadaten-Datei.
    public static string MetadataNotObject => T("mods.metadata.notObject", "Die Datei muss ein JSON-Objekt sein.");

    public static string MetadataInvalidJsonAt(long line, long column) => F("mods.metadata.invalidJsonAt", "Kein gültiges JSON (Zeile {0}, Spalte {1}).", line, column);

    public static string MetadataInvalidJson => T("mods.metadata.invalidJson", "Kein gültiges JSON.");

    public static string MetadataEmptyVersion => T("mods.metadata.emptyVersion", "Die Version darf nicht leer sein.");

    public static string MetadataVersionType => T("mods.metadata.versionType", "Das Feld version hat weder Text noch eine Zahl als Wert.");

    public static string MetadataFileInvalidJson => T("mods.metadata.fileInvalidJson", "Die Datei ist kein gültiges JSON.");

    public static string MetadataFileNotObject => T("mods.metadata.fileNotObject", "Die Datei ist kein JSON-Objekt.");

    // Klassische .mod-Datei (EU IV, CK III).
    public static string ClassicModUnexpectedBrace(int line) => F("mods.classicMod.unexpectedBrace", "Zeile {0}: Diese schließende Klammer hat keine öffnende.", line);

    public static string ClassicModUnclosedBrace(int line) => F("mods.classicMod.unclosedBrace", "Zeile {0}: Diese Klammer wird nie geschlossen.", line);

    public static string ClassicModUnclosedQuote(int line) => F("mods.classicMod.unclosedQuote", "Zeile {0}: Dieses Anführungszeichen wird nie geschlossen.", line);

    public static string ClassicModVersionNotOnOwnLine => T("mods.classicMod.versionNotOnOwnLine", "Das Feld version steht nicht am Anfang einer eigenen Zeile. Ändere die Version direkt im Text.");

    public static string ClassicModRemoteFileIdNotOnOwnLine => T("mods.classicMod.remoteFileIdNotOnOwnLine", "Das Feld remote_file_id steht nicht am Anfang einer eigenen Zeile. Trage die Workshop-ID direkt im Text ein.");

    public static string ClassicModInvalidRemoteFileId => T("mods.classicMod.invalidRemoteFileId", "Die Workshop-ID darf nur aus Ziffern bestehen.");

    public static string TextFileNotUtf8 => T("mods.textFile.notUtf8", "Die Datei ist nicht UTF-8-kodiert und wird deshalb nicht bearbeitet.");

    // Sperren.
    public static string LockInvalidPath(string path) => F("mods.locks.invalidPath", "Ungültiger Pfad innerhalb der Mod: \"{0}\".", path);

    public static string LockWholeMod => T("mods.locks.wholeMod", "Die ganze Mod ist gesperrt.");

    public static string LockThisFolder => T("mods.locks.thisFolder", "Dieser Ordner ist gesperrt.");

    public static string LockThisFile => T("mods.locks.thisFile", "Diese Datei ist gesperrt.");

    public static string LockParentFolder(string? folder) => F("mods.locks.parentFolder", "Der Ordner {0} darüber ist gesperrt.", folder);

    public static string LockEffect => T("mods.locks.effect", "Übertragen und Abgleich lassen sie unangetastet: nichts wird hineingeschrieben, nichts herauskopiert oder verschoben. Der Editor bleibt unberührt.");

    // Speicher.
    public static string StoreNoFolder(string layer) => F("mods.store.noFolder", "Für die Ebene {0} gibt es keinen Ordner.", layer);

    public static string StagingFolderMissing(string folder) => F("mods.staging.folderMissing", "Der Ordner {0} existiert nicht.", folder);

    // Register.
    public static string RegistryLoadWarning => T("mods.registry.loadWarning", "mods.json war nicht lesbar; eine Kopie liegt als mods.json.bad neben der Datei.");

    public static string RegistryEmptyName => T("mods.registry.emptyName", "Der Name der Mod darf nicht leer sein.");

    public static string RegistryWorkshopDigits => T("mods.registry.workshopDigits", "Die Workshop-ID besteht nur aus Ziffern.");

    public static string RegistryNotRepository(string repo) => F("mods.registry.notRepository", "\"{0}\" ist kein Repository (erwartet owner/name).", repo);

    public static string RegistryRepositoryDuplicate(string repo) => F("mods.registry.repositoryDuplicate", "Das Repository {0} ist doppelt.", repo);

    public static string RegistryTagFormat => T("mods.registry.tagFormat", "Das Tag-Format braucht den Platzhalter {version}.");

    public static string RegistryVariantEmptyName => T("mods.registry.variantEmptyName", "Der Name der ZIP-Variante darf nicht leer sein.");

    public static string RegistryVariantDuplicate(string name) => F("mods.registry.variantDuplicate", "Die ZIP-Variante \"{0}\" ist doppelt.", name);

    public static string RegistryVariantRelative => T("mods.registry.variantRelative", "Ordnerangaben müssen relativ zum Mod-Ordner sein.");

    public static string RegistryVariantRoot => T("mods.registry.variantRoot", "Der Wurzelordner darf keine Pfadzeichen enthalten.");

    public static string RegistryBugSourceKind(string kind) => F("mods.registry.bugSourceKind", "Unbekannte Art von Bug-Quelle: {0}.", kind);

    public static string RegistryBugSourceInvalid(string key) => F("mods.registry.bugSourceInvalid", "\"{0}\" ist keine gültige Quelle.", key);

    public static string RegistryDiscordTags => T("mods.registry.discordTags", "Ein Discord-Forum braucht drei verschiedene Status-Tags.");

    public static string RegistryBugSourceDuplicate(string key) => F("mods.registry.bugSourceDuplicate", "Die Quelle {0} ist doppelt.", key);

    public static string RegistryDiscordFilter => T("mods.registry.discordFilter", "Discord-Filter sind Tag-IDs.");

    public static string RegistryFolderNotOfMod => T("mods.registry.folderNotOfMod", "Dieser Ordner gehört nicht zur Mod.");

    public static string RegistryOldFolderNotOfMod => T("mods.registry.oldFolderNotOfMod", "Der alte Ordner gehört nicht zur Mod.");

    public static string RegistryChildHasParent => T("mods.registry.childHasParent", "Eine Mod mit Hauptmod ist ein Dev-Branch oder eine Submod.");

    public static string RegistryCompatchNoProject(string mod) => F("mods.registry.compatchNoProject", "\"{0}\" ist ein Kompatch und gehört zu keinem Projekt.", mod);

    public static string RegistryParentMissing => T("mods.registry.parentMissing", "Der Hauptmod existiert nicht.");

    public static string RegistryCompatchNotMain(string mod) => F("mods.registry.compatchNotMain", "\"{0}\" ist ein Kompatch und kann kein Hauptmod sein.", mod);

    public static string RegistryOwnParent => T("mods.registry.ownParent", "Eine Mod kann nicht ihr eigener Hauptmod sein.");

    public static string RegistryParentInProject(string mod) => F("mods.registry.parentInProject", "\"{0}\" gehört selbst zu einem Projekt. Wähle dessen Hauptmod.", mod);

    public static string RegistryHasMembers(string mod) => F("mods.registry.hasMembers", "Der Mod \"{0}\" sind selbst Mods zugeordnet und kann deshalb keinem Projekt angehören.", mod);

    public static string RegistryMainInProject(string mod) => F("mods.registry.mainInProject", "\"{0}\" gehört selbst zu einem Projekt und kann nicht Hauptmod sein.", mod);

    public static string RegistryMemberHasParent => T("mods.registry.memberHasParent", "Eine zugeordnete Mod ist ein Dev-Branch oder eine Submod.");

    public static string RegistryMemberDuplicate(string mod) => F("mods.registry.memberDuplicate", "Die Mod \"{0}\" steht mehrfach in der Liste.", mod);

    public static string RegistryMemberOtherProject(string mod) => F("mods.registry.memberOtherProject", "Die Mod \"{0}\" gehört schon zu einem anderen Projekt.", mod);

    public static string RegistryProjectToCompatch(string mod) => F("mods.registry.projectToCompatch", "\"{0}\" gehört zu einem Projekt. Löse sie erst daraus, bevor sie ein Kompatch wird.", mod);

    public static string RegistryTargetOneSource => T("mods.registry.targetOneSource", "Ein Ziel braucht genau eine Angabe: Register, Workshop-ID oder Ordner.");

    public static string RegistryTargetSelf => T("mods.registry.targetSelf", "Ein Kompatch kann nicht sein eigenes Ziel sein.");

    public static string RegistryTargetMissing => T("mods.registry.targetMissing", "Die Ziel-Mod existiert nicht im Register.");

    public static string RegistryTargetDuplicate(string target) => F("mods.registry.targetDuplicate", "Das Ziel \"{0}\" steht mehrfach in der Liste.", target);

    public static string RegistryNoTargets => T("mods.registry.noTargets", "Ein Kompatch braucht mindestens ein Ziel.");

    public static string RegistryUnknownId(string id) => F("mods.registry.unknownId", "Es gibt keine Mod mit der ID {0}.", id);

    public static string RegistryFolderOwned(string mod) => F("mods.registry.folderOwned", "Der Ordner gehört schon zur Mod \"{0}\".", mod);

    // Fenster "Mods" (auch vom Fenster Mod-Kompatibilität benutzt).
    public static string WindowInfo => T("mods.window.info", "Die Mod-Daten liegen in den Programmdaten, nie im Mod-Ordner (Steam lädt den ganzen Ordner hoch).");

    public static string WindowModsHeader => T("mods.window.modsHeader", "Mods dieses Spiels");

    public static string Add => T("mods.window.add", "Hinzufügen");

    public static string AddTip => T("mods.window.add.tip", "Einen Ordner als neue Mod anlegen");

    public static string Rename => T("mods.window.rename", "Umbenennen");

    public static string RemoveModTip => T("mods.window.remove.tip", "Die Mod aus der Verwaltung entfernen (die Dateien der Mod bleiben unberührt)");

    public static string Upload => T("mods.window.upload", "Mod hochladen");

    public static string UploadTip => T("mods.window.upload.tip", "Release-Versionen und Changelog der gewählten Mod verwalten und auf Steam hochladen");

    public static string AssignProject => T("mods.window.assignProject", "Projekt zuordnen");

    public static string AssignProjectTip => T("mods.window.assignProject.tip", "Die gewählte Mod einem Projekt zuordnen (Dev-Branch oder Submod eines Hauptmods) oder daraus lösen");

    public static string Lock => T("mods.window.lock", "Sperren");

    public static string Unlock => T("mods.window.unlock", "Entsperren");

    public static string LockTip => T("mods.window.lock.tip", "Aus gesperrten Mods überträgt PMT nichts und schreibt auch nichts hinein (Übertragen, Abgleich)");

    public static string Sync => T("mods.window.sync", "Abgleich");

    public static string SyncTip => T("mods.window.sync.tip", "Die Mod ordnerweit mit einer anderen Mod ihres Projekts abgleichen");

    public static string Compat => T("mods.window.compat", "Kompatibilität");

    public static string CompatTip => T("mods.window.compat.tip", "Die gewählte Mod ins Kompatibilitätsset aufnehmen und das Fenster Mod-Kompatibilität öffnen");

    public static string AddToCompatSet => T("mods.window.addToCompatSet", "Zum Kompatibilitätsset hinzufügen");

    public static string FoldersHeader => T("mods.window.foldersHeader", "Ordner der gewählten Mod");

    public static string AddFolder => T("mods.window.addFolder", "Ordner hinzufügen");

    public static string AddFolderTip => T("mods.window.addFolder.tip", "Einen weiteren Ordner derselben Mod hinzufügen, z. B. die Steam-Kopie");

    public static string Relink => T("mods.window.relink", "Neu verknüpfen");

    public static string RelinkTip => T("mods.window.relink.tip", "Den gewählten Ordner durch einen anderen ersetzen, wenn er verschoben oder umbenannt wurde");

    public static string RemoveFolder => T("mods.window.removeFolder", "Ordner entfernen");

    public static string PickModFolder => T("mods.window.pickModFolder", "Mod-Ordner wählen");

    public static string AddModTitle => T("mods.window.addModTitle", "Mod hinzufügen");

    public static string ModNamePrompt => T("mods.window.modNamePrompt", "Name der Mod:");

    public static string RenameModTitle => T("mods.window.renameModTitle", "Mod umbenennen");

    public static string NewNamePrompt => T("mods.window.newNamePrompt", "Neuer Name:");

    public static string RemoveConfirm(string mod) => F("mods.window.removeConfirm", "Die Mod \"{0}\" aus der Verwaltung entfernen?\nDie Dateien der Mod bleiben unberührt.", mod);

    public static string DeleteDataConfirm(int count) => F("mods.window.deleteDataConfirm", "Zu dieser Mod gehören {0} gespeicherte Datei(en) (z. B. Vorlagen).\nAuch löschen? Bei \"Nein\" bleiben sie im Programmordner liegen.", count);

    public static string DeleteDataFailed(string error) => F("mods.window.deleteDataFailed", "Die gespeicherten Dateien konnten nicht gelöscht werden:\n{0}", error);

    public static string PickAnotherFolder => T("mods.window.pickAnotherFolder", "Weiteren Ordner der Mod wählen");

    public static string PickNewFolder => T("mods.window.pickNewFolder", "Neuen Ordner für diese Mod wählen");

    public static string PathMissing(string text) => F("mods.window.pathMissing", "{0}  — Pfad fehlt", text);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
