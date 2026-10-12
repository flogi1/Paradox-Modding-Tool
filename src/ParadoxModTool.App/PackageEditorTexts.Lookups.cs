namespace ParadoxModTool.App;

/// <summary>Texte der Maske "Nachschlagen" und der Feld-Angabe "Aus Datenbank" (Teil 5b, Keys <c>packageEditor.lookups.*</c>, <c>packageEditor.lookup.*</c>).</summary>
internal static partial class PackageEditorTexts
{
    public static string LookupsIntro => T("packageEditor.lookups.intro",
        "Quellen für \"Aus Datenbank\": Ein Feld nennt eine oder mehrere Quellen, der Generator zeigt ihre Einträge in einer Tabelle mit Suchfeld. Quellen lesen Blöcke aus dem Spielordner oder Einträge aus einer JSON-Datei des Pakets.");

    public static string LookupsNone => T("packageEditor.lookups.none", "Noch keine Quelle. \"Neu\" legt eine an.");

    public static string NewSourceTip => T("packageEditor.lookups.new.tip", "Legt eine neue Quelle \"Aus dem Spiel\" an; die Art lässt sich danach ändern.");

    public static string DuplicateSourceTip => T("packageEditor.lookups.duplicate.tip", "Legt eine Kopie der gewählten Quelle mit neuer Kennung an.");

    public static string RemoveSourceTip => T("packageEditor.lookups.remove.tip", "Entfernt die gewählte Quelle und nimmt sie aus allen Feldern.");

    public static string RemoveSourceConfirm(string id, int fields) =>
        F("packageEditor.lookups.removeConfirm", "Die Quelle \"{0}\" wird von {1} Feldern benutzt. Entfernen und aus diesen Feldern nehmen?", id, fields);

    public static string SourceId => T("packageEditor.lookups.id", "Kennung");

    public static string SourceIdTip => T("packageEditor.lookups.id.tip",
        "Kennung der Quelle; Felder nennen sie unter \"Aus Datenbank\". Beim Umbenennen ziehen die Felder mit. Beispiel: advances");

    public static string SourceLabel => T("packageEditor.lookups.label", "Bezeichnung");

    public static string SourceLabelTip => T("packageEditor.lookups.label.tip", "Erscheint als \"Art\" in der Tabelle von \"Aus Datenbank\". Beispiel: Advance");

    public static string SourceKind => T("packageEditor.lookups.kind", "Art");

    public static string SourceKindTip => T("packageEditor.lookups.kind.tip",
        "\"Aus dem Spiel\" liest Blöcke aus .txt-Dateien des Spielordners und ihre offiziellen Texte. \"JSON-Datei\" liest Einträge aus einer Datei, die das Paket als Asset nennt.");

    public static string KindGameKeys => T("packageEditor.lookups.kind.gameKeys", "Aus dem Spiel");

    public static string KindJson => T("packageEditor.lookups.kind.json", "JSON-Datei");

    public static string KindChanged => T("packageEditor.lookups.kindChanged", "Die Angaben der bisherigen Art wurden entfernt.");

    public static string SourceFolder => T("packageEditor.lookups.folder", "Ordner");

    public static string SourceFolderTip => T("packageEditor.lookups.folder.tip",
        "Ordner relativ zum Spielinhalt; jede .txt-Datei darin wird gelesen. Beispiel: in_game/common/advances");

    public static string SourceDepth => T("packageEditor.lookups.depth", "Tiefe");

    public static string SourceDepthTip => T("packageEditor.lookups.depth.tip",
        "1 = nur oberste Blöcke werden Einträge; 2 = auch ihre direkten Unterblöcke (z. B. Gesetze und ihre Policies).");

    public static string SourceNamedOnly => T("packageEditor.lookups.namedOnly", "Nur Blöcke mit Namen");

    public static string SourceNamedOnlyTip => T("packageEditor.lookups.namedOnly.tip",
        "Nur Blöcke, deren Schlüssel einen Text in der Lokalisierung hat. So fallen Blöcke wie potential weg.");

    public static string SourceTagFromFile => T("packageEditor.lookups.tagFromFile", "Tag aus Dateiname");

    public static string SourceTagFromFileTip => T("packageEditor.lookups.tagFromFile.tip",
        "Regulärer Ausdruck auf den Dateinamen; Gruppe 1 ist der Tag. Beispiel: ^country_([A-Za-z]{2,5})\\.txt$");

    public static string SourceTagInBlock => T("packageEditor.lookups.tagInBlock", "Tag im Block");

    public static string SourceTagInBlockTip => T("packageEditor.lookups.tagInBlock.tip",
        "Schlüssel, deren Wert im Block ein Tag ist, durch Komma getrennt; gilt, wenn der Dateiname keinen Tag liefert. Beispiel: has_or_had_tag, tag");

    public static string SourceAsset => T("packageEditor.lookups.asset", "Asset");

    public static string SourceAssetTip => T("packageEditor.lookups.asset.tip", "JSON-Datei aus der Asset-Liste des Pakets (Abschnitt Assets).");

    public static string SourceItems => T("packageEditor.lookups.items", "Elemente-Pfad");

    public static string SourceItemsTip => T("packageEditor.lookups.items.tip",
        "Weg zu den Einträgen in der JSON-Datei; name[] steigt in jedes Element einer Liste. Beispiel: nations[].ideas[]");

    public static string SourceFieldsHeading => T("packageEditor.lookups.fieldsHeading", "Zuordnung");

    public static string SourceFieldTip => T("packageEditor.lookups.field.tip",
        "Eigenschaft des Eintrags in der JSON-Datei; ../x liest x aus dem übergeordneten Element. Leer = bleibt leer.");

    public static string EntryKey => T("packageEditor.lookups.entry.key", "Schlüssel");

    public static string EntryName => T("packageEditor.lookups.entry.name", "Name");

    public static string EntryDesc => T("packageEditor.lookups.entry.desc", "Beschreibung");

    public static string EntryTag => T("packageEditor.lookups.entry.tag", "Tag");

    public static string EntryNation => T("packageEditor.lookups.entry.nation", "Nation");

    public static string Preview => T("packageEditor.lookups.preview", "Vorschau");

    public static string PreviewTip => T("packageEditor.lookups.preview.tip",
        "Liest die Quelle mit dem aktuellen Stand wie \"Aus Datenbank\" und zeigt die Anzahl und die ersten 50 Einträge.");

    public static string PreviewCount(int count) => F("packageEditor.lookups.previewCount", "{0} Einträge", count);

    public static string PreviewChooseGameFolder => T("packageEditor.lookups.chooseGameFolder", "Spielordner einstellen");

    public static string PreviewChooseAsset => T("packageEditor.lookups.chooseAsset", "Zu den Assets");

    public static string LookupEnable => T("packageEditor.lookup.enable", "Aus Datenbank nachschlagen");

    public static string LookupEnableTip => T("packageEditor.lookup.enable.tip",
        "Zeigt neben dem Feld \"Aus Datenbank\": Ein Eintrag aus den gewählten Quellen füllt Name und Beschreibung.");

    public static string LookupSources => T("packageEditor.lookup.sources", "Quellen");

    public static string LookupSourcesTip => T("packageEditor.lookup.sources.tip", "Quellen aus \"Nachschlagen\", deren Einträge die Tabelle zeigt; mindestens eine.");

    public static string LookupNoSources => T("packageEditor.lookup.noSources", "Das Paket hat noch keine Quelle. Lege sie unter \"Nachschlagen\" an.");

    public static string LookupGoToSources => T("packageEditor.lookup.goToSources", "Zu Nachschlagen");

    public static string LookupFillName => T("packageEditor.lookup.fillName", "Name füllt");

    public static string LookupFillDesc => T("packageEditor.lookup.fillDesc", "Beschreibung füllt");

    public static string LookupFillTip => T("packageEditor.lookup.fill.tip",
        "Textfeld derselben Ebene, das den Namen bzw. die Beschreibung des Eintrags bekommt. Mindestens eins setzen.");

    public static string LookupFillNone => T("packageEditor.lookup.fillNone", "—");
}
