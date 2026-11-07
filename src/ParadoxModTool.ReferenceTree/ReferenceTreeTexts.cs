using ParadoxModTool.Localization;

namespace ParadoxModTool.ReferenceTree;

/// <summary>
/// Texte des Verweisbaums. Die Schlüssel stehen in <c>locales/de.json</c> und <c>locales/en.json</c>; der deutsche Text hier ist der
/// Rückfall und muss mit de.json übereinstimmen (Test <c>ReferenceTreeTextsTests</c>). Jeder Aufruf von <c>T</c>/<c>F</c> steht in einer Zeile.
/// </summary>
public static class ReferenceTreeTexts
{
    public static string TabNotes => T("referencetree.tab.notes", "Notizen");

    public static string TabCheck => T("referencetree.tab.check", "Prüfung");

    public static string TabCheckCount(int count) => F("referencetree.tab.checkCount", "Prüfung ({0})", count);

    public static string TabReferences => T("referencetree.tab.references", "Verweise");

    public static string ContextShow(string name) => F("referencetree.contextShow", "Im Verweisbaum zeigen: {0}", name);

    public static string MenuItem => T("referencetree.menuItem", "Im Verweisbaum zeigen");

    public static string MenuKey => T("referencetree.menuKey", "Strg+Umschalt+R");

    public static string Back => T("referencetree.back", "Zurück");

    public static string Refresh => T("referencetree.refresh", "Neu laden");

    public static string InputTip => T("referencetree.inputTip", "Name eingeben und Enter drücken");

    public static string NotFound => T("referencetree.notFound", "Nicht gefunden. Für andere Namen die Suche benutzen.");

    public static string NoModOrPlayset => T("referencetree.noModOrPlayset", "Keine aktive Mod oder kein Playset gewählt.");

    public static string NotSupported => T("referencetree.notSupported", "Für dieses Spiel nicht verfügbar.");

    public static string Building => T("referencetree.building", "Daten werden aufgebaut …");

    public static string ReloadHint => T("referencetree.reloadHint", "Mod oder Playset gewechselt: neu laden (⟳).");

    public static string DataUpdated => T("referencetree.dataUpdated", "Daten aktualisiert, ⟳ zum Neuladen.");

    public static string BuildFailed(string message) => F("referencetree.buildFailed", "Aufbau fehlgeschlagen: {0}", message);

    public static string Partial => T("referencetree.partial", "nur Event-Aufrufe bekannt");

    public static string OpenAsRoot => T("referencetree.openAsRoot", "Als Wurzel öffnen");

    public static string FileMissing(string path) => F("referencetree.fileMissing", "Datei nicht gefunden: {0}", path);

    public static string BranchSet => T("referencetree.branch.set", "gesetzt");

    public static string BranchRead => T("referencetree.branch.read", "gelesen");

    public static string BranchRemove => T("referencetree.branch.remove", "entfernt");

    public static string BranchUsedBy => T("referencetree.branch.usedBy", "benutzt von");

    public static string BranchUses => T("referencetree.branch.uses", "benutzt selbst");

    public static string BranchSets => T("referencetree.branch.sets", "setzt");

    public static string BranchReads => T("referencetree.branch.reads", "liest");

    public static string GroupOtherMods => T("referencetree.group.otherMods", "Andere Mods");

    public static string GroupVanilla => T("referencetree.group.vanilla", "Vanilla");

    public static string KindEvent => T("referencetree.kind.event", "Event");

    public static string KindOnAction => T("referencetree.kind.onAction", "on_action");

    public static string KindEffect => T("referencetree.kind.effect", "Effekt");

    public static string KindTrigger => T("referencetree.kind.trigger", "Trigger");

    public static string KindValue => T("referencetree.kind.value", "Script Value");

    public static string KindVariable => T("referencetree.kind.variable", "Variable");

    public static string KindFlag => T("referencetree.kind.flag", "Flag");

    public static string KindOther => T("referencetree.kind.other", "Block");

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
