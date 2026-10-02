using ParadoxModTool.Localization;

namespace ParadoxModTool.ModOverview;

/// <summary>
/// Texte der Übersicht "Meine Mods" (Hinweise, Spielstand). Die Schlüssel stehen in allen <c>locales/*.json</c>; der deutsche Text hier ist der
/// Rückfall und muss mit de.json übereinstimmen.
/// </summary>
public static class OverviewTexts
{
    public static string AlertMissingFolder(string path) => F("overview.alert.missingFolder", "Mod-Ordner fehlt: {0}", path);

    public static string AlertMissingDescriptor => T("overview.alert.missingDescriptor", "Keine lesbare Descriptor-Datei gefunden");

    public static string AlertOutdated(string supported, string installed) =>
        F("overview.alert.outdated", "Unterstützt {0}, installiert ist {1}", supported, installed);

    public static string AlertReleaseStatus(string hints) => F("overview.alert.releaseStatus", "Release-Status: {0}", hints);

    public static string AlertSteamHidden => T("overview.alert.steamHidden", "Steam-Seite ist versteckt");

    public static string AlertSteamNotFound => T("overview.alert.steamNotFound", "Steam-Seite nicht gefunden oder privat");

    public static string AlertSubmodWithoutDependency(string main) =>
        F("overview.alert.submodWithoutDependency", "Submod ohne dependency-Eintrag auf \"{0}\"", main);

    public static string AlertDependencyNotInProject(string dependency) =>
        F("overview.alert.dependencyNotInProject", "Braucht \"{0}\", ist aber nicht als Hauptmod des Projekts eingetragen", dependency);

    public static string AlertBugSourceFailed(string source) => F("overview.alert.bugSourceFailed", "Bug-Quelle {0} nicht erreichbar", source);

    public static string GameVersionUnknown => T("overview.gameVersionUnknown", "Spielversion unbekannt");

    public static string GameLoadFailed(string game, string message) =>
        F("overview.gameLoadFailed", "{0}: Mod-Liste konnte nicht gelesen werden ({1})", game, message);

    public static string WindowTitle => T("overview.window.title", "Meine Mods");

    public static string ViewTiles => T("overview.view.tiles", "Kacheln");

    public static string ViewList => T("overview.view.list", "Liste");

    public static string GroupGames => T("overview.group.games", "Nach Spielen");

    public static string GroupAll => T("overview.group.all", "Alle Spiele");

    public static string SortName => T("overview.sort.name", "Name");

    public static string SortSubscribers => T("overview.sort.subscribers", "Abonnenten");

    public static string SortOpenBugs => T("overview.sort.openBugs", "Offene Bugs");

    public static string SortLastRelease => T("overview.sort.lastRelease", "Letzter Release");

    public static string SortTip => T("overview.sort.tip", "Sortierung");

    public static string SortDirectionTip => T("overview.sort.direction.tip", "Reihenfolge umkehren");

    public static string SearchTip => T("overview.search.tip", "Nach Mod-Namen suchen");

    public static string FilterPublished => T("overview.filter.published", "Nur veröffentlichte");

    public static string FilterOpenBugs => T("overview.filter.openBugs", "Nur mit offenen Bugs");

    public static string FilterWarnings => T("overview.filter.warnings", "Nur mit Warnungen");

    public static string Refresh => T("overview.refresh", "Aktualisieren");

    public static string RefreshTip => T("overview.refresh.tip", "Mod-Listen neu lesen und Steam-Zahlen abrufen");

    public static string FetchedAt(string time) => F("overview.fetchedAt", "Stand: {0}", time);

    public static string Fetching => T("overview.fetching", "Wird aktualisiert");

    public static string SteamOffline(string time) => F("overview.steamOffline", "Steam nicht erreichbar, Stand: {0}", time);

    public static string GamesHeader => T("overview.games.header", "Spiele");

    public static string GamesUpTip => T("overview.games.up.tip", "Weiter nach oben");

    public static string GamesDownTip => T("overview.games.down.tip", "Weiter nach unten");

    public static string AlertsHeader(int count) => F("overview.alerts.header", "Hinweise ({0})", count);

    public static string Empty => T("overview.empty", "Keine Mods gefunden");

    public static string ProjectHeader(string name) => F("overview.project", "Projekt: {0}", name);

    public static string TileVersion => T("overview.tile.version", "Version");

    public static string TileGameVersion => T("overview.tile.gameVersion", "Spielversion");

    public static string TileRequires => T("overview.tile.requires", "Braucht");

    public static string TileSubscribers => T("overview.tile.subscribers", "Abonnenten");

    public static string TileFavoritesViews => T("overview.tile.favoritesViews", "Favoriten · Aufrufe");

    public static string TileSteamUpdate => T("overview.tile.steamUpdate", "Steam-Update");

    public static string TileBugs => T("overview.tile.bugs", "Bugs offen / neu");

    public static string TileLastRelease => T("overview.tile.lastRelease", "Letzter Release");

    public static string TileDevBranch(string name, string version) => F("overview.tile.devBranch", "Dev-Branch: {0} · {1}", name, version);

    public static string TileNewBugs(int count) => F("overview.tile.newBugs", "{0} neu", count);

    public static string TileNotPublished => T("overview.tile.notPublished", "nicht veröffentlicht");

    public static string RoleMain => T("overview.role.main", "Hauptmod");

    public static string RoleSubmod => T("overview.role.submod", "Submod");

    public static string RoleSingle => T("overview.role.single", "Einzelmod");

    public static string RoleCompatch(string targets) => F("overview.role.compatch", "Kompatibilitätspatch für {0}", targets);

    public static string Locked => T("overview.locked", "Gesperrt");

    public static string LinkWorkshop => T("overview.link.workshop", "Workshop");

    public static string LinkForum => T("overview.link.forum", "Forum");

    public static string LinkDiscord => T("overview.link.discord", "Discord");

    public static string LinkGitHub => T("overview.link.github", "GitHub");

    public static string ColumnName => T("overview.column.name", "Name");

    public static string ColumnGame => T("overview.column.game", "Spiel");

    public static string ColumnRole => T("overview.column.role", "Rolle");

    public static string ColumnSubscribers => T("overview.column.subscribers", "Abonnenten");

    public static string ColumnBugs => T("overview.column.bugs", "Bugs");

    public static string ColumnWarnings => T("overview.column.warnings", "Warnungen");

    public static string ColumnLastRelease => T("overview.column.lastRelease", "Letzter Release");

    public static string ActionOpenInPmt => T("overview.action.openInPmt", "In PMT öffnen");

    public static string ActionOpenInPmtTip => T("overview.action.openInPmt.tip", "Öffnet PMT für das Spiel dieser Mod und zeigt ihren Ordner im Explorer");

    public static string ActionOpenFolder => T("overview.action.openFolder", "Ordner öffnen");

    public static string ActionReleases => T("overview.action.releases", "Releases");

    public static string ActionReleasesTip => T("overview.action.releases.tip", "Öffnet die Releases dieser Mod im PMT ihres Spiels");

    public static string ActionBugs => T("overview.action.bugs", "Bugs");

    public static string ActionBugsTip => T("overview.action.bugs.tip", "Öffnet den Bugtracker des Projekts im PMT ihres Spiels");

    public static string ActionEditLinks => T("overview.action.editLinks", "Links bearbeiten");

    public static string LinksTitle(string mod) => F("overview.links.title", "Links bearbeiten: {0}", mod);

    public static string LinksForum => T("overview.links.forum", "Forum-Link");

    public static string LinksDiscord => T("overview.links.discord", "Discord-Einladung");

    public static string LinksGitHub => T("overview.links.github", "GitHub-Link");

    public static string LinksGitHubFromRepo => T("overview.links.githubFromRepo", "Kommt aus dem eingetragenen GitHub-Repo");

    public static string LinksInvalid(string value) => F("overview.links.invalid", "Keine gültige http- oder https-Adresse: {0}", value);

    public static string LinksSave => T("overview.links.save", "Speichern");

    public static string LinksCancel => T("overview.links.cancel", "Abbrechen");

    public static string HandOffTimeout(string game) => F("overview.handoff.timeout", "PMT für {0} hat nicht rechtzeitig geantwortet.", game);

    public static string WorkshopMissing(int count) => F("overview.workshop.missing", "{0} Mods ohne bekannte Workshop-ID", count);

    public static string WorkshopSearch => T("overview.workshop.search", "Auf Steam suchen");

    public static string WorkshopSearchTip => T("overview.workshop.search.tip", "Liest die öffentliche Workshop-Seite deines Steam-Profils und schlägt Seiten mit gleichem Namen vor");

    public static string WorkshopFound(string title, string id) => F("overview.workshop.found", "Workshop-Seite gefunden: \"{0}\" (ID {1})", title, id);

    public static string WorkshopAdopt => T("overview.workshop.adopt", "Übernehmen");

    public static string WorkshopAdoptAll => T("overview.workshop.adoptAll", "Alle übernehmen");

    public static string WorkshopNone => T("overview.workshop.none", "Keine passende Workshop-Seite gefunden");

    public static string WorkshopNoAccount => T("overview.workshop.noAccount", "Auf diesem Rechner ist kein Steam-Konto zu finden.");

    public static string WorkshopFailed(string message) => F("overview.workshop.failed", "Die Workshop-Seite des Steam-Profils ist nicht abrufbar: {0}", message);

    public static string BugsFetching(int done, int total) => F("overview.bugs.fetching", "Bugs werden abgerufen ({0}/{1})", done, total);

    public static string BugsNew(int count) => F("overview.bugs.new", "{0} neue Bugreports", count);

    public static string BugsAllSeen => T("overview.bugs.allSeen", "Alle gesehen");

    public static string BugsCollapsed(int count) => F("overview.bugs.collapsed", "{0} neue Bugs ▸", count);

    public static string BugsCollapse => T("overview.bugs.collapse", "Zuklappen ▾");

    public static string BugsSeen => T("overview.bugs.seen", "Gesehen");

    public static string BugsOpenBrowser => T("overview.bugs.openBrowser", "Im Browser öffnen");

    public static string BugsOpenTracker => T("overview.bugs.openTracker", "Im Bugtracker öffnen");

    public static string BugsClose => T("overview.bugs.close", "Schließen");

    public static string BugsNoBody => T("overview.bugs.noBody", "Kein Text");

    public static string MenuSettings => T("overview.menu.settings", "Einstellungen");

    public static string MenuAlerts => T("overview.menu.alerts", "Hinweise");

    public static string MenuStart => T("overview.menu.start", "Beim Öffnen");

    public static string MenuStartMaximized => T("overview.menu.start.maximized", "Vollbild");

    public static string MenuStartWindowed => T("overview.menu.start.windowed", "Fenster");

    public static string AlertFilterTip => T("overview.alertFilter.tip", "Hinweisarten ein- oder ausblenden; ausgeblendete zählen auch bei \"Nur mit Warnungen\" nicht");

    public static string AlertFilterItem(string category, int count) => F("overview.alertFilter.item", "{0} ({1})", category, count);

    /// <summary>Anzeigename einer Hinweisart aus <see cref="OverviewAlertFilter.Categories"/>.</summary>
    public static string AlertCategory(string category) => category switch
    {
        OverviewAlertFilter.NewBugs => T("overview.alertKind.newBugs", "Neue Bugs"),
        nameof(OverviewAlertKind.BugSourceFailed) => T("overview.alertKind.bugSourceFailed", "Bug-Quelle nicht erreichbar"),
        nameof(OverviewAlertKind.MissingFolder) => T("overview.alertKind.missingFolder", "Mod-Ordner fehlt"),
        nameof(OverviewAlertKind.MissingDescriptor) => T("overview.alertKind.missingDescriptor", "Descriptor fehlt"),
        nameof(OverviewAlertKind.OutdatedGameVersion) => T("overview.alertKind.outdatedGameVersion", "Spielversion veraltet"),
        nameof(OverviewAlertKind.ReleaseStatus) => T("overview.alertKind.releaseStatus", "Release-Status"),
        nameof(OverviewAlertKind.SteamPage) => T("overview.alertKind.steamPage", "Steam-Seite versteckt/entfernt"),
        nameof(OverviewAlertKind.SubmodWithoutDependency) => T("overview.alertKind.submodWithoutDependency", "Submod ohne dependency"),
        nameof(OverviewAlertKind.DependencyNotInProject) => T("overview.alertKind.dependencyNotInProject", "dependency nicht im Projekt"),
        OverviewAlertFilter.WorkshopId => T("overview.alertKind.workshopId", "Workshop-ID fehlt"),
        _ => category,
    };

    public static string HandOffModMissing => T("overview.handoff.modMissing", "Diese Mod ist in diesem Spiel nicht mehr eingetragen.");

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
