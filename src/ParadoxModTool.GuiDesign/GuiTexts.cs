using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.Localization;

namespace ParadoxModTool.GuiDesign;

/// <summary>
/// Alle Oberflächen- und Hinweistexte des GUI-Editors an einer Stelle. Die Schlüssel stehen in <c>locales/de.json</c> und
/// <c>locales/en.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen (Test <c>UiTextTests</c>).
/// Neue Sprachen brauchen nur eine weitere Datei <c>locales/&lt;sprache&gt;.json</c>.
/// </summary>
public static class GuiTexts
{
    // Hinweise der Layout-Engine an Vorschau-Kästen.
    public static string LayoutSizeUnresolved(string value) => F("gui.layout.sizeUnresolved", "Größe nicht auswertbar: {0}", value);

    public static string LayoutTextureMissing(string path) => F("gui.layout.textureMissing", "Textur nicht gefunden: {0}", path);

    public static string LayoutTextEstimated => T("gui.layout.textEstimated", "Textgröße geschätzt");

    public static string LayoutTextFontMissing(string font) => F("gui.layout.textFontMissing", "Schrift {0} fehlt: Ersatzschrift Segoe UI, Breite geschätzt", font);

    public static string LayoutTextApproximated(string what) => F("gui.layout.textApproximated", "Text angenähert ({0})", what);

    public static string LayoutTextLineBreakAsSpace => T("gui.layout.textLineBreakAsSpace", "Text angenähert (Zeilenumbruch ohne multiline als Leerzeichen)");

    public static string LayoutTextIconMissing(string icon) => F("gui.layout.textIconMissing", "Text-Icon {0} fehlt", icon);

    public static string LayoutTextPlaceholder => T("gui.layout.textPlaceholder", "Text enthält Platzhalter (Datenbindung)");

    public static string LayoutTextKeyMissing(string key) => F("gui.layout.textKeyMissing", "Text {0} fehlt in der Lokalisierung", key);

    public static string LayoutTextFontUnknown(string font) => F("gui.layout.textFontUnknown", "Schrift {0} unbekannt: das Spiel zeigt diesen Text nicht", font);

    public static string LayoutSizeUnknown => T("gui.layout.sizeUnknown", "Größe unbekannt");

    public static string LayoutPositionUnresolved(string value) => F("gui.layout.positionUnresolved", "Position nicht auswertbar: {0}", value);

    public static string LayoutGridApproximated => T("gui.layout.gridApproximated", "Rasteranordnung angenähert");

    public static string LayoutHidden => T("gui.layout.hidden", "Element ist ausgeblendet");

    public static string LayoutBoxInZeroParent => T("gui.layout.boxInZeroParent", "Box in einem widget ohne size: Größe angenähert (Inhalt)");

    public static string LayoutFreeSpaceUnmeasured => T("gui.layout.freeSpaceUnmeasured", "Freier Platz nach maximumsize/Stretch-Faktor nicht gemessen; Kinder ab Anfang gepackt");

    public static string LayoutGameError(string detail) => F("gui.layout.gameError", "Das Spiel meldet hier einen Fehler ({0}); die Lage kann abweichen", detail);

    public static string LayoutBoxInContainer(string container) => F("gui.layout.boxInContainer", "hbox/vbox direkt in {0}: das Spiel stürzt ab", container);

    public static string LayoutFlowChildUnmeasured(string detail) => F("gui.layout.flowChildUnmeasured", "In flowcontainer nicht gemessen ({0}); ignoriert", detail);

    public static string LayoutIgnoreLayoutApproximated => T("gui.layout.ignoreLayoutApproximated", "ignore_layout: Lage angenähert (nicht gemessen)");

    public static string LayoutFillIgnoresAnchor => T("gui.layout.fillIgnoresAnchor", "Box füllt ihr Elternelement; position/Anker nicht gemessen und ignoriert");

    public static string LayoutMinusOneUnmeasured(string kind) => F("gui.layout.minusOneUnmeasured", "size -1 bei {0} nicht gemessen; als 0 angenommen", kind);

    public static string LayoutMinSizeBoxAnchorUnmeasured => T("gui.layout.minSizeBoxAnchorUnmeasured", "Box mit set_parent_size_to_minimum: position/Anker nicht gemessen; wie in einem widget angewendet");

    public static string LayoutPercentInSizelessBox => T("gui.layout.percentInSizelessBox", "Prozentgröße in Box ohne size: Bezug angenähert");

    public static string LayoutTextureDynamic(string path) => F("gui.layout.textureDynamic", "Textur aus Datenbindung: {0}", path);

    public static string LayoutTextureFormat(string path, string format) => F("gui.layout.textureFormat", "Bildformat nicht unterstützt ({1}): {0}", path, format);

    public static string LayoutTextureApproximated(string what) => F("gui.layout.textureApproximated", "Textur angenähert ({0})", what);

    /// <summary><c>scale</c> in einem nicht gemessenen Fall (Text, verschachtelt, Ränder, Kacheln); Kasten und Drehpunkt sind gemessen (Lauf 6).</summary>
    public static string LayoutScaleApproximated(string what) => F("gui.layout.scaleApproximated", "scale angenähert ({0} nicht gemessen)", what);

    /// <summary><c>scale</c> ist keine positive Zahl (0, negativ, Prozent, Text); gezeigt wie 1.</summary>
    public static string LayoutScaleInvalid(string value) => F("gui.layout.scaleInvalid", "scale ungültig ({0}): als 1 angenommen", value);

    /// <summary><c>scale</c> aus einer Datenbindung oder einer unbekannten Konstante; nicht angewandt.</summary>
    public static string LayoutScaleUnresolved(string value) => F("gui.layout.scaleUnresolved", "scale nicht auswertbar: {0}", value);

    /// <summary>Die Kette aus <c>_show</c> und <c>next</c> wiederholt sich; gezeigt werden die Werte bis vor die erste Wiederholung (ein fehlendes Ziel beendet die Kette ohne Hinweis).</summary>
    public static string LayoutShowNextApproximated => T("gui.layout.showNextApproximated", "Zustand _show mit next: Animation angenähert (Endwert der Kette gezeigt)");

    public static string LayoutWidgetNotDrawn(string kind) => F("gui.layout.widgetNotDrawn", "{0} wird nicht dargestellt (Platzhalter)", kind);

    public static string LayoutFrameOutOfRange(int frame, int count) => F("gui.layout.frameOutOfRange", "frame {0} gibt es nicht (nur {1}); letztes Bild gezeigt", frame, count);

    public static string LayoutAlphaInvisible => T("gui.layout.alphaInvisible", "alpha 0: unsichtbar (andere Zustände und Animationen nicht dargestellt)");

    public static string LayoutAlphaTextUnmeasured => T("gui.layout.alphaTextUnmeasured", "alpha für Text nicht gemessen; Text voll deckend gezeigt");

    // Bezeichnungen nicht lesbarer Texturen (Platzhalter und Hinweis "Bildformat nicht unterstützt"); technische wie "DDS DXGI 83 (BC5)" bleiben fest.
    public static string TextureFormatNone => T("gui.texture.formatNone", "kein Bildformat");

    public static string TextureFormatEmptyFile => T("gui.texture.formatEmptyFile", "leere Datei");

    public static string TextureFormatDdsTruncated => T("gui.texture.formatDdsTruncated", "DDS unvollständig");

    public static string TextureFormatDdsBadSize => T("gui.texture.formatDdsBadSize", "DDS Größe ungültig");

    // Hinweise der klassischen Layout-Engine (HOI IV, EU IV).
    public static string ClassicSpriteMissing(string name) => F("gui.classic.spriteMissing", "Sprite fehlt: {0}", name);

    public static string ClassicTextureMissing(string path) => F("gui.classic.textureMissing", "Textur fehlt: {0}", path);

    public static string ClassicTextureUnreadable(string path) => F("gui.classic.textureUnreadable", "Textur unlesbar (Größe nicht lesbar): {0}", path);

    public static string ClassicBlockIgnored(string key) => F("gui.classic.blockIgnored", "Block {0} nicht ausgewertet", key);

    public static string ClassicSpriteCaseMismatch(string name) => F("gui.classic.spriteCaseMismatch", "Sprite {0} nur in anderer Groß-/Kleinschreibung gefunden; ob das Spiel es so findet, ist nicht gemessen", name);

    public static string ClassicSpriteKindApproximated(string kind) => F("gui.classic.spriteKindApproximated", "Sprite-Art {0} angenähert", kind);

    public static string ClassicFrameClamped(int frame, int frames) => F("gui.classic.frameClamped", "Frame {0} gibt es nicht ({1} Frames), der letzte wird gezeigt", frame, frames);

    public static string ClassicCorneredMinimum(int borderX, int borderY) => F("gui.classic.corneredMinimum", "9-Slice kleiner als 2 × borderSize ({0}×{1}): das Spiel zeichnet größer, angenähert", borderX, borderY);

    public static string ClassicFontMissing(string font) => F("gui.classic.fontMissing", "Schrift fehlt: {0}, Textgröße geschätzt", font);

    public static string ClassicGlyphsMissing => T("gui.classic.glyphsMissing", "Zeichen fehlen in der Schrift");

    public static string ClassicTextTooWide(double maxWidth) =>
        F("gui.classic.textTooWide", "Text breiter als {0} Pixel (maxWidth bzw. Knopfbreite; Wort ohne Umbruchstelle), ragt hinaus", maxWidth.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));

    public static string ClassicTextCut(int shown, int total) => F("gui.classic.textCut", "Text höher als maxHeight: {0} von {1} Zeilen gezeigt", shown, total);

    public static string ClassicSizeFromChildren => T("gui.classic.sizeFromChildren", "Ohne Größe: aus den Kindern geschätzt");

    public static string ClassicGridContent => T("gui.classic.gridContent", "Inhalt kommt aus dem Spiel; leere Beispielplätze gezeigt");

    public static string ClassicApproximated(string key) => F("gui.classic.approximated", "{0} angenähert", key);

    public static string ClassicUnknownElement(string key) => F("gui.classic.unknownElement", "Unbekannter Elementtyp {0}, als Container gezeigt", key);

    public static string ClassicAnchorUnknown(string value) => F("gui.classic.anchorUnknown", "Unbekannter Ankerwert {0}, UPPER_LEFT angenommen", value);

    public static string ClassicAnchorAlias(string value) => F("gui.classic.anchorAlias", "Alias `{0}`, Bedeutung nicht belegt", value);

    public static string ClassicFromIfResolution(string key, string condition) => F("gui.classic.fromIfResolution", "{0} aus if_resolution ({1})", key, condition);

    public static string ClassicOriginIfResolution(string condition) => F("gui.classic.originIfResolution", "aus if_resolution ({0})", condition);

    public static string ClassicNoInterface(string install) => F("gui.classic.noInterface", "Kein Ordner interface in {0}: Vorschau nur schematisch, ohne Sprites und Schriften des Spiels.", install);

    public static string ClassicFormatUnknown(string format) => F("gui.classic.formatUnknown", "Ausrichtung format = {0} nicht ausgewertet, linksbündig gezeigt", format);

    public static string ClassicFormatVertical(string format) => F("gui.classic.formatVertical", "Senkrechter Teil von format = {0} nicht ausgewertet, Text von oben gezeichnet", format);

    public static string ClassicNoTypes => T("gui.classic.noTypes", "HOI IV und EU IV kennen keine Typen.");

    public static string ClassicBlocksLater => T("gui.classic.blocksLater", "Bausteine und neue GUI-Dateien kommen für HOI IV und EU IV mit dem nächsten Ausbau des GUI-Editors; die vorhandenen Bausteine sind im Jomini-Format der neueren Spiele.");
    public static string ClassicVanillaNameTitle => T("gui.classic.vanillaNameTitle", "Name aus dem Spiel");
    public static string ClassicVanillaNameConfirm(string name) => F("gui.classic.vanillaNameConfirm", "„{0}“ ist ein Name aus den Vanilla-Dateien. Der Spielcode nutzt diesen Namen vielleicht. Trotzdem ändern?", name);

    // Eigenschaftenfeld für klassische Elemente (HOI IV, EU IV).
    public static string ClassicValueInvalid(string value) => F("gui.classic.valueInvalid", "„{0}“ ist kein bekannter Wert; die Vorschau nimmt den Standard.", value);

    public static string ClassicValueAlias(string value) => F("gui.classic.valueAlias", "„{0}“ ist ein Alias, dessen Bedeutung nicht belegt ist.", value);

    public static string ClassicEditsIfResolution(string condition) => F("gui.classic.editsIfResolution", "Änderung geht in den if_resolution-Block ({0})", condition);

    public static string ClassicChangeBase => T("gui.classic.changeBase", "Grundwert ändern");

    public static string ClassicChangeBaseTip => T("gui.classic.changeBaseTip", "Schreibt den Wert in die Zeile direkt im Element statt in den if_resolution-Block.");

    public static string ClassicSpritePick => T("gui.classic.spritePick", "Auswählen…");

    public static string ClassicSpriteCreate => T("gui.classic.spriteCreate", "Sprite anlegen…");

    public static string ClassicSpriteUnknown => T("gui.classic.spriteUnknown", "Sprite unbekannt – anlegen?");

    public static string ClassicSpriteNameInvalid => T("gui.classic.spriteNameInvalid", "Der Name ist leer oder enthält andere Zeichen als Buchstaben, Ziffern und _.");

    public static string ClassicSpriteNameTaken(string name, string where) => F("gui.classic.spriteNameTaken", "Das Sprite {0} gibt es schon ({1}). Vorhandene Sprites ersetzt PMT hier nicht.", name, where);

    public static string ClassicSpriteNameCaseOnly(string name, string existing, string where) => F("gui.classic.spriteNameCaseOnly", "Es gibt schon das Sprite {1} ({2}); {0} unterscheidet sich nur in der Groß-/Kleinschreibung. Ob das Spiel beides trennt, ist nicht gemessen, deshalb ist der Name gesperrt.", name, existing, where);

    public static string ClassicSpriteImageUnreadable(string path) => F("gui.classic.spriteImageUnreadable", "Das Bild lässt sich nicht lesen (erwartet wird eine DDS-, TGA- oder PNG-Datei mit gültigem Kopf): {0}", path);

    public static string ClassicSpriteCopyExists(string path) => F("gui.classic.spriteCopyExists", "Im Mod liegt schon eine Datei mit diesem Namen: {0}. Sie wird nur bei gewähltem Überschreiben ersetzt.", path);

    public static string ClassicSpriteGfxPathInvalid(string path) => F("gui.classic.spriteGfxPathInvalid", "Die .gfx-Datei muss unter interface/ liegen und auf .gfx enden: {0}", path);

    public static string ClassicSpriteGfxPathShadows(string path) => F("gui.classic.spriteGfxPathShadows", "Eine Datei {0} gibt es schon in Vanilla oder einer Abhängigkeit; eine Mod-Datei mit diesem Pfad ersetzte sie ganz.", path);

    public static string ClassicSpriteWindowTitle => T("gui.classic.spriteWindowTitle", "Sprite anlegen");

    public static string ClassicSpriteImage => T("gui.classic.spriteImage", "Bilddatei");

    public static string ClassicSpriteImageFromGfx => T("gui.classic.spriteImageFromGfx", "Aus gfx/ wählen…");

    public static string ClassicSpriteImageExternal => T("gui.classic.spriteImageExternal", "Datei von außen…");

    public static string ClassicSpriteImageFilter => T("gui.classic.spriteImageFilter", "Bilder (*.dds, *.tga, *.png)|*.dds;*.tga;*.png|Alle Dateien (*.*)|*.*");

    public static string ClassicSpriteName => T("gui.classic.spriteName", "Name");

    public static string ClassicSpriteKindLabel => T("gui.classic.spriteKindLabel", "Art");

    public static string ClassicSpriteFrames => T("gui.classic.spriteFrames", "Frames (Bildstreifen)");

    public static string ClassicSpriteBorder => T("gui.classic.spriteBorder", "Rand (borderSize) X / Y");

    public static string ClassicSpriteGfxFile => T("gui.classic.spriteGfxFile", ".gfx-Datei im Mod");

    public static string ClassicSpriteDoCreate => T("gui.classic.spriteDoCreate", "Anlegen");

    public static string ClassicSpriteCancel => T("gui.classic.spriteCancel", "Abbrechen");

    public static string ClassicSpriteNoImage => T("gui.classic.spriteNoImage", "Wähle zuerst ein Bild (DDS, TGA oder PNG).");

    public static string ClassicSpriteFramesInvalid => T("gui.classic.spriteFramesInvalid", "Die Zahl der Frames muss eine ganze Zahl ab 1 sein.");

    public static string ClassicSpriteBorderInvalid => T("gui.classic.spriteBorderInvalid", "Der Rand muss aus ganzen Zahlen ab 0 bestehen.");

    public static string ClassicSpritePreviewNone => T("gui.classic.spritePreviewNone", "(keine Vorschau)");

    public static string ClassicSpriteOverwriteTitle => T("gui.classic.spriteOverwriteTitle", "Datei überschreiben?");

    public static string ClassicSpriteOverwriteText(string path) => F("gui.classic.spriteOverwriteText", "Im Mod liegt schon {0}. Das gewählte Bild ersetzt die Datei; Rückgängig stellt sie wieder her.", path);

    public static string ClassicSpriteCopyWillAsk(string path) => F("gui.classic.spriteCopyWillAsk", "Im Mod liegt schon {0}; beim Anlegen fragt PMT, ob die Datei ersetzt wird.", path);

    public static string ClassicSpritePickerTitle => T("gui.classic.spritePickerTitle", "Sprite auswählen");

    public static string ClassicSpritePickerSearch => T("gui.classic.spritePickerSearch", "Suchen (Teil des Namens)");

    public static string ClassicSpritePickerCount(int shown, int total) => F("gui.classic.spritePickerCount", "{0} von {1} Sprites", shown, total);

    public static string ClassicSpritePickerColName => T("gui.classic.spritePickerColName", "Name");

    public static string ClassicSpritePickerColKind => T("gui.classic.spritePickerColKind", "Art");

    public static string ClassicSpritePickerColFrames => T("gui.classic.spritePickerColFrames", "Frames");

    public static string ClassicSpritePickerColFile => T("gui.classic.spritePickerColFile", "Datei");

    public static string ClassicSpritePickerOk => T("gui.classic.spritePickerOk", "Übernehmen");

    public static string ClassicSpritePickerTexture(string path) => F("gui.classic.spritePickerTexture", "Textur: {0}", path);

    public static string ClassicSpritePickerTextureMissing(string path) => F("gui.classic.spritePickerTextureMissing", "Textur nicht gefunden: {0}", path);

    public static string ClassicSpritePickerNoTexture => T("gui.classic.spritePickerNoTexture", "Dieses Sprite hat keine Textur.");

    public static string ClassicSpriteAction(string name) => F("gui.classic.spriteAction", "Sprite {0} anlegen", name);

    public static string ClassicSpriteImageAction(string file) => F("gui.classic.spriteImageAction", "Bild kopieren: {0}", file);

    public static string ClassicSpriteSnapshotNote => T("gui.classic.spriteSnapshot", "vor Sprite anlegen");

    public static string ClassicLocNoKey => T("gui.classic.locNoKey", "(kein Lokalisierungsschlüssel)");

    public static string ClassicLocMissing => T("gui.classic.locMissing", "(fehlt in der Lokalisierung)");

    public static string ClassicLocLoading => T("gui.classic.locLoading", "(Lokalisierung wird geladen …)");

    public static string ClassicFrameRange(int frames) => F("gui.classic.frameRange", "1 … {0}", frames);

    public static string ClassicFrameZero => T("gui.classic.frameZero", "0 = Frame 1 (gemessen)");

    public static string ClassicPercentTip => T("gui.classic.percentTip", "Wechselt zwischen Pixel (px), Prozent (%) und gemessenem Prozent (%%)");

    public static string ClassicUiScale(double scale) => F("gui.classic.uiScale", "UI-Skalierung {0} angenähert (nicht gemessen)", scale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));

    // Erklärungsfeld der klassischen Eigenschaften (Sprite-Skizzen, Beispiele, Quellen).
    public static string ExplainNone => T("gui.explain.none", "Keine Erklärung vorhanden.");

    public static string ExplainUncertain => T("gui.explain.uncertain", "Unsicher: nicht durch Wiki, Messung oder Vanilla-Zählung belegt.");

    public static string ExplainDefault(string value) => F("gui.explain.default", "Standard: {0}", value);

    public static string ExplainPitfalls => T("gui.explain.pitfalls", "Stolperfallen");

    public static string ExplainChildren(string children) => F("gui.explain.children", "Typische Kinder: {0}", children);

    public static string ExplainExamples => T("gui.explain.examples", "Beispiele aus Vanilla");

    public static string ExplainExamplesLoading => T("gui.explain.examplesLoading", "Beispiele werden gesucht…");

    public static string ExplainExample(string name, string path, int line) => F("gui.explain.example", "{0} ({1}:{2})", name, path, line);

    public static string ExplainExampleTip => T("gui.explain.exampleTip", "Öffnet die Vanilla-Datei schreibgeschützt an dieser Zeile.");

    public static string ExplainSources(string sources) => F("gui.explain.sources", "Quelle: {0}", sources);

    public static string ExplainSourceWiki => T("gui.explain.sourceWiki", "Wiki");

    public static string ExplainSourceMeasured(string date) => F("gui.explain.sourceMeasured", "gemessen am {0}", date);

    public static string ExplainSourceMeasuredNoDate => T("gui.explain.sourceMeasuredNoDate", "gemessen");

    public static string ExplainSourceVanilla => T("gui.explain.sourceVanilla", "Vanilla-Zählung");

    public static string ExplainSourceUnsure => T("gui.explain.sourceUnsure", "unsicher");

    public static string ExplainSketchParent => T("gui.explain.sketchParent", "Eltern");

    // Elementbaum.
    public static string TreeUnknownType => T("gui.tree.unknownType", "unbekannter Typ");

    public static string TreeNotOverridden => T("gui.tree.notOverridden", "nicht überschrieben");

    // Herkunft einer Eigenschaft ({1} ist " (datei.gui:zeile)" oder leer).
    public static string OriginHere => T("gui.origin.here", "hier gesetzt");

    public static string OriginType(string definition, string where) => F("gui.origin.type", "geerbt von {0}{1}", definition, where);

    public static string OriginTemplate(string definition, string where) => F("gui.origin.template", "aus Template {0}{1}", definition, where);

    public static string OriginBlockOverride(string definition, string where) => F("gui.origin.blockOverride", "aus blockoverride \"{0}\"{1}", definition, where);

    // Gruppen im Eigenschaftenfeld.
    public static string GroupData => T("gui.group.data", "Daten");

    public static string GroupLayout => T("gui.group.layout", "Layout");

    public static string GroupAppearance => T("gui.group.appearance", "Aussehen");

    public static string GroupText => T("gui.group.text", "Text");

    public static string GroupBehavior => T("gui.group.behavior", "Verhalten");

    public static string GroupOther => T("gui.group.other", "Sonstiges");

    // Vorschau.
    public static string PreviewTemplate => T("gui.preview.template", "Templates haben keine eigene Vorschau.");

    public static string PreviewImpossible => T("gui.preview.impossible", "Für dieses Element ist keine Vorschau möglich.");

    public static string PreviewNone => T("gui.preview.none", "Keine Vorschau: Cursor in ein Element setzen.");

    public static string PreviewRunning => T("gui.preview.running", "Vorschau wird berechnet");

    public static string PreviewFailed(string message) => F("gui.preview.failed", "Vorschau fehlgeschlagen: {0}", message);

    // Designer-Ansicht.
    public static string DesignerShowConditional => T("gui.designer.showConditional", "Bedingt sichtbare zeigen");

    public static string DesignerShowConditionalTip => T("gui.designer.showConditional.tip", "Elemente mit visible = \"[…]\" anzeigen (die Bedingung lässt sich ohne Spiel nicht auswerten)");

    public static string DesignerShowHidden => T("gui.designer.showHidden", "Ausgeblendete zeigen");

    public static string DesignerShowHiddenTip => T("gui.designer.showHidden.tip", "Elemente mit visible = no trotzdem anzeigen");

    public static string DesignerListCount => T("gui.designer.listCount", "Listen-Einträge");

    public static string DesignerListCountTip => T("gui.designer.listCount.tip", "Wie viele Einträge Listen und Raster in der Vorschau simulieren");

    public static string DesignerFit => T("gui.designer.fit", "Einpassen");

    public static string DesignerFitTip => T("gui.designer.fit.tip", "Vorschau wieder ganz einpassen (Zoom mit dem Mausrad, Verschieben mit der rechten Maustaste)");

    // Designer-Leiste für beide Formate (klassisch HOI IV/EU IV und Jomini): Auflösung, UI-Skalierung, Rahmen, Überstand.
    public static string DesignerResolution => T("gui.designer.resolution", "Auflösung");

    public static string DesignerResolutionTip => T("gui.designer.resolution.tip", "Bildschirmauflösung der Vorschau. „Spiel“ ist die Auflösung aus den Spieleinstellungen (klassisch settings.txt, Jomini pdx_settings; steht dort keine, die des Hauptbildschirms).");

    public static string DesignerResolutionGame(int width, int height) => F("gui.designer.resolution.game", "Spiel ({0}×{1})", width, height);

    public static string DesignerResolutionCustom => T("gui.designer.resolution.custom", "Eigene…");

    public static string DesignerResolutionCustomTip => T("gui.designer.resolution.custom.tip", "Eigene Auflösung als Breite×Höhe, z. B. 2048×1152; Enter übernimmt");

    public static string DesignerUiScale => T("gui.designer.uiScale", "UI-Skalierung");

    public static string DesignerUiScaleTip => T("gui.designer.uiScale.tip", "UI-Skalierung des Spiels, Voreinstellung aus den Spieleinstellungen (klassisch gui_scale bzw. game_ui_scale in settings.txt, Jomini GUI.scale in pdx_settings), sonst 1. Enter übernimmt. Klassisch: Werte ungleich 1 sind angenähert.");

    // Sprache der Jomini-Vorschautexte.
    public static string DesignerLanguage => T("gui.designer.language", "Sprache");

    public static string DesignerLanguageGame(string language) => F("gui.designer.languageGame", "Spiel ({0})", language);

    public static string DesignerLanguageTip => T("gui.designer.languageTip", "Sprache der Texte in der Vorschau; fehlende Texte englisch");

    public static string DesignerShowBounds => T("gui.designer.showBounds", "Rahmen zeigen");

    public static string DesignerShowBoundsTip => T("gui.designer.showBounds.tip", "Jedes Element umranden wie guibounds im Spiel.");

    public static string DesignerMarkOverflow => T("gui.designer.markOverflow", "Überstand markieren");

    public static string DesignerMarkOverflowTip => T("gui.designer.markOverflow.tip", "Jedes Element rot umranden, das über seinen Elternrahmen oder den Bildschirm hinausragt. Aus: nur beim gewählten Element.");

    public static string DesignerHideConditional => T("gui.designer.hideConditional", "Bedingtes Element ausblenden");

    public static string DesignerUnhideConditional => T("gui.designer.unhideConditional", "Wieder einblenden");

    public static string DesignerShowAllConditionals => T("gui.designer.showAllConditionals", "Alle einblenden");

    public static string DesignerShowAllConditionalsTip => T("gui.designer.showAllConditionals.tip", "Blendet alle in dieser Datei ausgeblendeten bedingten Elemente wieder ein");

    /// <summary>Banner bei genau einem ausgeblendeten bedingten Element (Einzahl).</summary>
    public static string DesignerHiddenConditional => T("gui.designer.hiddenConditional", "1 bedingtes Element ausgeblendet");

    /// <summary>Banner mit der Zahl ausgeblendeter bedingter Elemente; bei 1 die Einzahl (<see cref="DesignerHiddenConditional"/>).</summary>
    public static string DesignerHiddenConditionals(int count) =>
        count == 1 ? DesignerHiddenConditional : F("gui.designer.hiddenConditionals", "{0} bedingte Elemente ausgeblendet", count);

    public static string DesignerConditionalTip => T("gui.designer.conditionalTip", "Bedingt sichtbar (visible = \"[…]\"): im Spiel nur unter einer Bedingung zu sehen");

    public static string ColumnProperty => T("gui.designer.column.property", "Eigenschaft");

    public static string ColumnValue => T("gui.designer.column.value", "Wert");

    public static string ColumnOrigin => T("gui.designer.column.origin", "Herkunft");

    public static string PropertiesTip => T("gui.designer.properties.tip", "Fett: hier gesetzt. Grau: geerbt. Doppelklick springt zur Stelle, an der der Wert gesetzt ist.");

    public static string NoProperties => T("gui.designer.noProperties", "Keine aufgelösten Eigenschaften an dieser Stelle.");

    public static string TypeChain(string chain) => F("gui.designer.typeChain", "Typkette: {0}", chain);

    public static string Stale => T("gui.designer.stale", "Der Code hat Fehler. Der Designer zeigt den letzten gültigen Stand.");

    public static string StaleAt(int line, string problem) => F("gui.designer.staleAt", "Code hat Fehler in Zeile {0}: {1}. Der Designer zeigt den letzten gültigen Stand.", line, problem);

    /// <summary>Kurztext einer Parser-Diagnose (die Meldungen des Parsers selbst sind nur für Protokolle gedacht).</summary>
    public static string Diagnostic(GuiDiagnosticKind kind) => kind switch
    {
        GuiDiagnosticKind.UnexpectedCloseBrace => T("gui.diagnostic.unexpectedCloseBrace", "Überzählige schließende Klammer \"}\""),
        GuiDiagnosticKind.MissingCloseBrace => T("gui.diagnostic.missingCloseBrace", "Schließende Klammer \"}\" fehlt"),
        GuiDiagnosticKind.UnterminatedString => T("gui.diagnostic.unterminatedString", "Zeichenkette ohne schließendes Anführungszeichen"),
        GuiDiagnosticKind.MissingValue => T("gui.diagnostic.missingValue", "Wert fehlt"),
        _ => T("gui.diagnostic.unexpectedToken", "Unerwartetes Zeichen"),
    };

    // Umschalter im Editor.
    public static string ModeCode => T("gui.mode.code", "Code");

    public static string ModeCodeTip => T("gui.mode.code.tip", "Nur den Code der .gui-Datei zeigen");

    public static string ModeDesigner => T("gui.mode.designer", "Designer");

    public static string ModeDesignerTip => T("gui.mode.designer.tip", "Nur den Designer zeigen: Vorschau, Elementbaum und Eigenschaften (nur lesend)");

    public static string ModeSplit => T("gui.mode.split", "Geteilt");

    public static string ModeSplitTip => T("gui.mode.split.tip", "Designer oben, Code unten");

    // GUI-Index und Menü.
    public static string IndexLoading => T("gui.index.loading", "GUI-Index wird geladen.");

    public static string IndexToolsOff => T("gui.index.toolsOff", "GUI-Werkzeuge sind ausgeschaltet.");

    public static string IndexNoGamePath => T("gui.index.noGamePath", "GUI-Index fehlt: Es ist kein Spielpfad bekannt (Einstellungen › Spielpfad einstellen).");

    public static string IndexFailed(string message) => F("gui.index.failed", "GUI-Index konnte nicht geladen werden: {0}", message);

    public static string IndexCanceled => T("gui.index.canceled", "GUI-Index: Aufbau abgebrochen.");

    public static string IndexSummary(int files, int types, double seconds, string suffix) =>
        F("gui.index.summary", "GUI-Index: {0:N0} Dateien, {1:N0} Typen, geladen in {2:0.0} s{3}.", files, types, seconds, suffix);

    public static string IndexCatalogLearned => T("gui.index.catalogLearned", ", Katalog neu gelernt");

    public static string IndexTitle => T("gui.index.title", "GUI-Index");

    public static string MenuDesigner => T("gui.menu.designer", "GUI-Designer für .gui-Dateien anbieten");

    public static string MenuDesignerTip => T("gui.menu.designer.tip", "Zeigt bei .gui-Dateien über dem Editor den Umschalter Code | Designer | Geteilt");

    public static string MenuTools => T("gui.menu.tools", "GUI-Werkzeuge aktivieren");

    public static string MenuToolsTip => T("gui.menu.tools.tip", "GUI-Index, Designer, Inspektor und „Wird angezeigt in“ für .gui-Dateien. Ausgeschaltet spart das Rechenzeit und Speicher, wenn du nicht mit GUI-Dateien arbeitest.");

    public static string MenuRebuild => T("gui.menu.rebuild", "GUI-Index neu aufbauen");

    public static string MenuRebuildTip => T("gui.menu.rebuild.tip", "Liest alle .gui-Dateien von Spiel und Mod neu ein und lernt den Eigenschaften-Katalog neu (geschieht sonst automatisch nach Spiel-Updates)");

    // Kontextmenüs, Brotkrumen-Leiste und Menü (Plan 2b).
    public static string ContextInspect => T("gui.context.inspect", "Inspizieren");

    public static string ContextShownIn(string word) => F("gui.context.shownIn", "Wird angezeigt in (GUI): {0}", word);

    public static string BreadcrumbInspectTip => T("gui.breadcrumb.inspect.tip", "Element am Cursor im GUI-Inspektor zeigen");

    public static string BreadcrumbNone => T("gui.breadcrumb.none", "Cursor steht außerhalb eines Elements");

    public static string MenuInspect => T("gui.menu.inspect", "GUI-Inspektor");

    public static string MenuInspectTip => T("gui.menu.inspect.tip", "Typen, Templates, benannte Widgets und ganze Fenster einer Datei aus Spiel und Mod suchen und im GUI-Inspektor zeigen (Strg+H)");

    public static string MenuBreadcrumb => T("gui.menu.breadcrumb", "Brotkrumen-Leiste in .gui-Dateien");

    public static string MenuBreadcrumbTip => T("gui.menu.breadcrumb.tip", "Zeigt über dem Code von .gui-Dateien den Pfad zum Element am Cursor; jedes Glied springt zur Stelle");

    public static string MenuShownIn => T("gui.menu.shownIn", "Wird angezeigt in (GUI) in Skriptdateien anbieten");

    public static string MenuShownInTip => T("gui.menu.shownIn.tip", "Rechtsklick in .txt- und .yml-Dateien sucht das Wort unter der Maus in allen .gui-Dateien von Spiel und Mod");

    // Arten von Elementen.
    public static string KindOf(GuiDefinitionKind kind) => kind switch
    {
        GuiDefinitionKind.Type => T("gui.kind.type", "Typ"),
        GuiDefinitionKind.Template => T("gui.kind.template", "Template"),
        _ => T("gui.kind.namedWidget", "Benanntes Widget"),
    };

    public static string KindWidget => T("gui.kind.widget", "Widget");

    public static string KindUnnamedRoot => T("gui.kind.unnamedRoot", "Oberstes Fenster ohne Namen");

    // GUI-Inspektor.
    public static string InspectorTitle => T("gui.inspector.title", "GUI-Inspektor");

    public static string InspectorTitleWith(string element) => F("gui.inspector.titleWith", "GUI-Inspektor: {0}", element);

    public static string InspectorBack => T("gui.inspector.back", "Zurück");

    public static string InspectorBackTip => T("gui.inspector.back.tip", "Zum vorher gezeigten Element zurück");

    public static string InspectorForward => T("gui.inspector.forward", "Vorwärts");

    public static string InspectorForwardTip => T("gui.inspector.forward.tip", "Wieder zu dem Element, von dem aus Zurück gedrückt wurde");

    public static string InspectorPick => T("gui.inspector.pick", "Element wählen…");

    public static string InspectorPickTip => T("gui.inspector.pick.tip", "Öffnet die Suche: Typ, Template, benanntes Widget oder ein ganzes Fenster einer Datei wählen");

    public static string InspectorShowInCode => T("gui.inspector.showInCode", "Im Code zeigen");

    public static string InspectorShowInCodeTip => T("gui.inspector.showInCode.tip", "Öffnet die Datei an der Zeile des Elements");

    public static string InspectorLocation(string kind, string path, int line) => F("gui.inspector.location", "{0} in {1}, Zeile {2}", kind, path, line);

    public static string InspectorChainLabel => T("gui.inspector.chainLabel", "Typkette:");

    public static string InspectorResolved => T("gui.inspector.resolved", "Aufgelöst");

    public static string InspectorResolvedTip => T("gui.inspector.resolved.tip", "Aufgelöste Elemente: Typkette, Templates und blockoverrides sind eingesetzt. Doppelklick springt zur Stelle im Code.");

    public static string InspectorWritten => T("gui.inspector.written", "Wie geschrieben");

    public static string InspectorCode => T("gui.inspector.code", "Aufgelöster Code");

    public static string InspectorRelations => T("gui.inspector.relations", "Beziehungen");

    public static string InspectorCopy => T("gui.inspector.copy", "Kopieren");

    public static string InspectorCopyTip => T("gui.inspector.copy.tip", "Aufgelösten Code in die Zwischenablage kopieren");

    public static string InspectorNoResolved => T("gui.inspector.noResolved", "Templates werden nicht als eigenes Element aufgelöst; siehe Wie geschrieben und Beziehungen.");

    public static string ReferencesHeader(int count) => F("gui.inspector.references", "Verweise im Code ({0})", count);

    public static string ReferenceDetail(string kind, int line) => F("gui.inspector.referenceDetail", " ({0}, Zeile {1})", kind, line);

    public static string ReferencesNone => T("gui.inspector.referencesNone", "Keine Verweise auf Typen oder Templates.");

    public static string RelationsInstanceOf => T("gui.relations.instanceOf", "Instanz von");

    public static string RelationsDerived(int count) => F("gui.relations.derived", "Abgeleitete Typen ({0})", count);

    public static string RelationsBlocks(int count) => F("gui.relations.blocks", "Angebotene Blöcke ({0})", count);

    public static string RelationsOverridden(int count) => F("gui.relations.overridden", "an {0} Stellen überschrieben", count);

    public static string RelationsShowOverrides => T("gui.relations.showOverrides", "Stellen zeigen");

    public static string RelationsUsages(int count) => F("gui.relations.usages", "Verwendungen im Spiel ({0})", count);

    public static string RelationsOverridesOf(string block, int count) => F("gui.relations.overridesOf", "Überschreibungen von block \"{0}\" ({1})", block, count);

    public static string RelationsShowAll => T("gui.relations.showAll", "Alle Verwendungen zeigen");

    public static string RelationsNone => T("gui.relations.none", "keine");

    public static string RelationsEngineWidget => T("gui.relations.engineWidget", "Engine-Widget ohne eigene Definition: keine Beziehungen.");

    public static string RelationsUsagesTip => T("gui.relations.usages.tip", "Doppelklick öffnet die Stelle; Rechtsklick › Inspizieren zeigt das Element dort");

    // Suchfenster "GUI-Element inspizieren".
    public static string SearchTitle => T("gui.search.title", "GUI-Element inspizieren");

    public static string SearchName => T("gui.search.name", "Name enthält");

    public static string SearchKind => T("gui.search.kind", "Art");

    public static string SearchFile => T("gui.search.file", "Datei enthält");

    public static string SearchFileTip => T("gui.search.file.tip", "Teil des Dateipfads, z. B. window_county_view; mit der Art \"Oberste Fenster einer Datei\" stehen dann nur noch die ganzen Fenster dieser Datei in der Liste");

    public static string SearchCount(int visible, int total) => F("gui.search.count", "{0:N0} von {1:N0} Einträgen", visible, total);

    public static string SearchBuilding => T("gui.search.building", "Liste wird aufgebaut");

    public static string SearchFailed(string message) => F("gui.search.failed", "Suche fehlgeschlagen: {0}", message);

    public static string SearchInspectTip => T("gui.search.inspect.tip", "Gewähltes Element im GUI-Inspektor zeigen (auch Enter oder Doppelklick)");

    public static string FilterText(GuiElementFilter filter) => filter switch
    {
        GuiElementFilter.Button => T("gui.filter.button", "Buttons"),
        GuiElementFilter.Tooltip => T("gui.filter.tooltip", "Tooltips"),
        GuiElementFilter.Window => T("gui.filter.window", "Fenster"),
        GuiElementFilter.FileRoot => T("gui.filter.fileRoot", "Oberste Fenster einer Datei"),
        GuiElementFilter.Text => T("gui.filter.text", "Texte"),
        GuiElementFilter.Icon => T("gui.filter.icon", "Icons"),
        GuiElementFilter.Container => T("gui.filter.container", "Container"),
        GuiElementFilter.List => T("gui.filter.list", "Listen"),
        GuiElementFilter.Template => T("gui.filter.template", "Templates"),
        GuiElementFilter.Other => T("gui.filter.other", "Sonstige"),
        _ => T("gui.filter.all", "Alle"),
    };

    public static string ColumnName => T("gui.column.name", "Name");

    public static string ColumnKind => T("gui.column.kind", "Art");

    public static string ColumnBaseWidget => T("gui.column.baseWidget", "Basis-Widget");

    public static string ColumnFile => T("gui.column.file", "Datei");

    public static string ColumnLayer => T("gui.column.layer", "Herkunft");

    public static string ColumnLine => T("gui.column.line", "Zeile");

    public static string ColumnElement => T("gui.column.element", "Element");

    public static string ColumnText => T("gui.column.text", "Textstelle");

    // "Wird angezeigt in (GUI)".
    public static string ShownInTitle(string word) => F("gui.shownIn.title", "Wird angezeigt in (GUI): {0}", word);

    public static string ShownInSearching(int files) => F("gui.shownIn.searching", "Suche in {0:N0} .gui-Dateien", files);

    public static string ShownInResult(int total, int files) => F("gui.shownIn.result", "{0:N0} Fundstellen in {1:N0} Dateien", total, files);

    public static string ShownInLimited(int total, int shown) => F("gui.shownIn.limited", "{0:N0} Fundstellen, die ersten {1:N0} werden gezeigt", total, shown);

    public static string ShownInNone => T("gui.shownIn.none", "Keine Fundstelle in den .gui-Dateien von Spiel und Mod.");

    public static string ShownInTooShort(int minimum) => F("gui.shownIn.tooShort", "Zu kurz für eine Suche (mindestens {0} Zeichen).", minimum);

    public static string ShownInHint => T("gui.shownIn.hint", "Reine Textsuche nach dem Namen (z.B. scripted_gui, Lokalisierungsschlüssel, Texturpfad). Datenbindungen lassen sich damit keiner Skriptstelle zuordnen.");

    public static string ShownInOpen => T("gui.shownIn.open", "Öffnen");

    public static string ShownInOpenTip => T("gui.shownIn.open.tip", "Datei an der Fundstelle öffnen (auch Doppelklick)");

    public static string ShownInInspectTip => T("gui.shownIn.inspect.tip", "Element an der Fundstelle im GUI-Inspektor zeigen");

    // Designer bearbeitend (Plan 3a): Leiste, Elementbaum, Tasten, Schreibschutz, Aktionen.
    public static string DesignerUndo => T("gui.designer.undo", "Rückgängig");

    public static string DesignerUndoTip => T("gui.designer.undo.tip", "Letzte Änderung zurücknehmen (Strg+Z)");

    public static string DesignerRedo => T("gui.designer.redo", "Wiederholen");

    public static string DesignerRedoTip => T("gui.designer.redo.tip", "Zurückgenommene Änderung wiederholen (Strg+Y)");

    public static string TreeShowInCode => T("gui.tree.showInCode", "Im Code zeigen");

    public static string TreeOpenDefinition => T("gui.tree.openDefinition", "Definition öffnen");

    public static string TreeOverrideBlock => T("gui.tree.overrideBlock", "Block überschreiben");

    public static string TreeClearBlock => T("gui.tree.clearBlock", "Block leeren (Inhalt ausblenden)");

    public static string InnerFrom(string definition, string block) => F("gui.inner.from", "Stammt aus {0}, block \"{1}\"", definition, block);

    public static string InnerNoBlock(string definition) => F("gui.inner.noBlock", "Stammt aus {0} und liegt in keinem block: hier nicht anpassbar, nur über den Typ.", definition);

    public static string InnerAdapt => T("gui.inner.adapt", "Über blockoverride anpassen");

    public static string InnerAdaptTip => T("gui.inner.adapt.tip", "Legt an dieser Instanz einen blockoverride mit dem bisherigen Inhalt an; danach steht das Element in dieser Datei und lässt sich bearbeiten");

    public static string TreeDuplicate => T("gui.tree.duplicate", "Duplizieren");

    public static string TreeDelete => T("gui.tree.delete", "Löschen");

    public static string TreeMoveUp => T("gui.tree.moveUp", "Nach oben");

    public static string TreeMoveDown => T("gui.tree.moveDown", "Nach unten");

    public static string TreeWrapIn(string box) => F("gui.tree.wrapIn", "In {0} einpacken", box);

    public static string TreeInsertChild(string entry) => F("gui.tree.insertChild", "Als Kind einfügen: {0}", entry);

    public static string TreeInsertBefore(string entry) => F("gui.tree.insertBefore", "Davor einfügen: {0}", entry);

    public static string TreeInsertAfter(string entry) => F("gui.tree.insertAfter", "Danach einfügen: {0}", entry);

    public static string TreeAddUsing(string entry) => F("gui.tree.addUsing", "using hinzufügen: {0}", entry);

    public static string KeyDuplicate => T("gui.key.duplicate", "Strg+D");

    public static string KeyDelete => T("gui.key.delete", "Entf");

    public static string KeyMoveUp => T("gui.key.moveUp", "Alt+Pfeil hoch");

    public static string KeyMoveDown => T("gui.key.moveDown", "Alt+Pfeil runter");

    public static string KeyInspect => T("gui.key.inspect", "Strg+H");

    public static string ReadOnly(GuiReadOnlyReason reason) => reason switch
    {
        GuiReadOnlyReason.Vanilla => T("gui.readOnly.vanilla", "Vanilla-Datei: Der Designer zeigt sie nur an und ändert sie nicht."),
        GuiReadOnlyReason.Dependency => T("gui.readOnly.dependency", "Datei einer Abhängigkeits-Mod: Der Designer zeigt sie nur an und ändert sie nicht."),
        _ => T("gui.readOnly.locked", "Gesperrte Datei: Der Designer zeigt sie nur an und ändert sie nicht."),
    };

    public static string EditBlockedByErrors => T("gui.edit.blockedByErrors", "Der Code hat Fehler. Der Designer ändert nichts, bis sie behoben sind.");

    public static string EditElementGone => T("gui.edit.elementGone", "Das Element steht nicht mehr so im Code. Bitte neu auswählen.");

    public static string EditNothing => T("gui.edit.nothing", "Diese Aktion ändert hier nichts.");

    // Eigenschaften bearbeiten und Geltungsbereich.
    public static string ScopeHere(string definition, int usages) => F("gui.scope.here", "Diese Stelle: wirkt in {0}, an {1:N0} Stellen verwendet", definition, usages);

    public static string ScopeHereOnly => T("gui.scope.hereOnly", "Diese Stelle: wirkt nur hier");

    public static string ScopeTip => T("gui.scope.tip", "Liegt die Stelle in einer Typ- oder Template-Definition, wirkt eine Änderung überall, wo diese verwendet wird.");

    public static string PropertyNone => T("gui.property.none", "Eigenschaft in der Liste wählen oder unten hinzufügen");

    public static string PropertyApply => T("gui.property.apply", "Übernehmen");

    public static string PropertyApplyTip => T("gui.property.apply.tip", "Wert in den Code schreiben (auch Enter)");

    public static string PropertyReset => T("gui.property.reset", "Zurücksetzen");

    public static string PropertyResetTip => T("gui.property.reset.tip", "Die Zeile dieses Werts aus dem Code entfernen; danach gilt der geerbte Wert");

    public static string PropertyAddLabel => T("gui.property.addLabel", "Eigenschaft hinzufügen:");

    public static string PropertyAdd => T("gui.property.add", "Hinzufügen");

    public static string PropertyAddTip => T("gui.property.add.tip", "Eigenschaften des Basis-Widgets, häufigste zuerst; freie Eingabe möglich");

    public static string PropertyBlockReadOnly => T("gui.property.blockReadOnly", "Mehrzeiliger Block: im Code bearbeiten");

    public static string PropertyPickColor => T("gui.property.pickColor", "Farbe wählen");

    public static string PropertyBrowse => T("gui.property.browse", "Durchsuchen");

    public static string PropertyBrowseTip => T("gui.property.browse.tip", "Textur aus den Ordnern gfx von Spiel und Mod wählen");

    public static string PropertyTextureFilter => T("gui.property.textureFilter", "Texturen (*.dds, *.tga, *.png)|*.dds;*.tga;*.png");

    public static string PropertyTextureSize(int width, int height) => F("gui.property.textureSize", "Größe {0} × {1}", width, height);

    public static string PropertyTextureOutside => T("gui.property.textureOutside", "Die Datei liegt in keinem Texturordner von Spiel oder Mod.");

    public static string PropertyInvalid => T("gui.property.invalid", "Ungültiger Wert für diese Eigenschaft.");

    public static string PropertyInvalidName => T("gui.property.invalidName", "Ungültiger Eigenschaftsname (nur Buchstaben, Ziffern und _).");

    public static string PropertyPercentTip => T("gui.property.percent.tip", "Zwischen Pixeln und Prozent umschalten");

    // Palette.
    public static string PaletteSearchTip => T("gui.palette.search.tip", "Palette nach Namen filtern");

    public static string PaletteEngineGroup => T("gui.palette.engineGroup", "Engine-Widgets");

    public static string PaletteBlocksGroup => T("gui.palette.blocksGroup", "Bausteine");

    public static string PaletteBuilding => T("gui.palette.building", "Palette wird aufgebaut");

    public static string PaletteFailed(string message) => F("gui.palette.failed", "Palette fehlgeschlagen: {0}", message);

    public static string PaletteUsages(int count) => F("gui.palette.usages", "{0:N0}×", count);

    public static string PaletteTip => T("gui.palette.tip", "Doppelklick fügt an der Auswahl ein, Ziehen in den Elementbaum an der Ablagestelle. Templates kommen als using in die Auswahl.");

    public static string PaletteInsert => T("gui.palette.insert", "Einfügen");

    // Menü "GUI" und "Neue GUI-Datei".
    public static string MenuGui => T("gui.menu.gui", "GUI");

    public static string MenuNewFile => T("gui.menu.newFile", "Neue GUI-Datei");

    public static string MenuNewFileTip => T("gui.menu.newFile.tip", "Neue .gui-Datei im Ordner gui der aktiven Mod anlegen, leer oder aus einem Baustein");

    public static string MenuBlocks => T("gui.menu.blocks", "Baustein einfügen");

    public static string MenuBlocksTip => T("gui.menu.blocks.tip", "GUI-Baustein auswählen, ausfüllen und an der Cursorstelle einfügen");

    public static string NewFileTitle => T("gui.newFile.title", "Neue GUI-Datei");

    public static string NewFileName => T("gui.newFile.name", "Dateiname:");

    public static string NewFileBlock => T("gui.newFile.block", "Baustein:");

    public static string NewFileNoBlock => T("gui.newFile.noBlock", "Kein Baustein gewählt: Die Suche findet keinen passenden.");

    public static string NewFileTarget(string folder) => F("gui.newFile.target", "Wird angelegt in: {0}", folder);

    public static string NewFileHint => T("gui.newFile.hint", "Ein neues Fenster erscheint im Spiel erst, wenn Skript es öffnet (z.B. über scripted_gui). Auch der Schließen-Button braucht dafür eine Aktion. Dieses Skript erzeugt der Editor nicht.");

    public static string NewFileCreate => T("gui.newFile.create", "Anlegen");

    public static string NewFileCancel => T("gui.newFile.cancel", "Abbrechen");

    public static string NewFileInvalidName => T("gui.newFile.invalidName", "Der Dateiname ist leer oder enthält unzulässige Zeichen.");

    public static string NewFileExists(string name) => F("gui.newFile.exists", "Die Datei {0} gibt es dort schon.", name);

    public static string NewFileNoMod => T("gui.newFile.noMod", "Keine aktive Mod: Wähle zuerst eine Mod oder öffne eine Datei aus ihr.");

    public static string NewFileAction(string name) => F("gui.newFile.action", "GUI-Datei angelegt: {0}", name);

    public static string NewFileFailed(string message) => F("gui.newFile.failed", "Die Datei konnte nicht angelegt werden: {0}", message);

    // Vanilla-Entwürfe und "In Mod übernehmen" (Plan 3b).
    public static string DraftEdit => T("gui.draft.edit", "Für Mod bearbeiten");

    public static string DraftEditTip => T("gui.draft.edit.tip", "Die Vanilla-Datei als Entwurf bearbeiten. Änderungen kommen über \"In Mod übernehmen\" in die Mod; die Vanilla-Datei bleibt unverändert.");

    public static string DraftLabel => T("gui.draft.label", "Entwurf für Mod");

    public static string DraftLabelTip => T("gui.draft.label.tip", "Diese Vanilla-Datei ist ein Entwurf. Definitionen, die die Mod schon überschreibt, stehen in ihrer Mod-Fassung da. Speichern (Strg+S) öffnet \"In Mod übernehmen\".");

    public static string DraftTakeOver => T("gui.draft.takeOver", "In Mod übernehmen");

    public static string DraftTakeOverTip => T("gui.draft.takeOver.tip", "Zeigt, welche Mod-Dateien entstehen oder sich ändern, und schreibt sie nach Bestätigung");

    public static string DraftDiscard => T("gui.draft.discard", "Entwurf verwerfen");

    public static string DraftDiscardTip => T("gui.draft.discard.tip", "Alle Änderungen am Entwurf verwerfen; die Vanilla-Datei ist danach wieder schreibgeschützt");

    public static string DraftDiscardConfirm => T("gui.draft.discardConfirm", "Alle Änderungen an diesem Entwurf verwerfen?");

    public static string DraftTitle => T("gui.draft.title", "Entwurf für Mod");

    public static string DraftNoIndex => T("gui.draft.noIndex", "Der GUI-Index wird noch aufgebaut. Bitte gleich noch einmal versuchen.");

    public static string DraftNotInIndex => T("gui.draft.notInIndex", "Die Datei steht nicht im GUI-Index. \"GUI-Index neu aufbauen\" im Menü GUI hilft, wenn sich der Spielordner geändert hat.");

    public static string DraftModCopy(string path) => F("gui.draft.modCopy", "Die Mod ersetzt diese Datei schon ganz ({0}). Geöffnet wird die Mod-Datei.", path);

    public static string DraftSaveVanilla => T("gui.draft.saveVanilla", "Vanilla-GUI-Dateien werden nicht gespeichert. \"Für Mod bearbeiten\" macht aus der Datei einen Entwurf, der über \"In Mod übernehmen\" in die Mod kommt.");

    public static string ScopeCopiesFile => T("gui.scope.copiesFile", "Diese Stelle: Widget auf oberster Ebene. Beim Übernehmen wird die ganze Datei in die Mod kopiert.");

    /// <summary>Klassischer Entwurf: Die Einheit der Übernahme ist das oberste Fenster.</summary>
    public static string ScopeClassicWindow(string window) => F("gui.scope.classicWindow", "Beim Übernehmen schreibt PMT das ganze Fenster {0} in eine eigene Mod-Datei.", window);

    public static string TakeOverTitle => T("gui.takeOver.title", "In Mod übernehmen");

    public static string TakeOverIntro(string modRoot) => F("gui.takeOver.intro", "Diese Änderungen kommen in die Mod ({0}):", modRoot);

    public static string TakeOverEmpty => T("gui.takeOver.empty", "Der Entwurf unterscheidet sich nicht von Vanilla bzw. von der Mod. Es gibt nichts zu übernehmen.");

    public static string TakeOverColumnAction => T("gui.takeOver.column.action", "Aktion");

    public static string TakeOverColumnName => T("gui.takeOver.column.name", "Name");

    public static string TakeOverColumnFile => T("gui.takeOver.column.file", "Datei");

    public static string TakeOverColumnUsages => T("gui.takeOver.column.usages", "Verwendungen");

    public static string TakeOverColumnRule => T("gui.takeOver.column.rule", "Warum diese Datei");

    public static string TakeOverWarnings => T("gui.takeOver.warnings", "Hinweise:");

    public static string TakeOverCopyFile => T("gui.takeOver.copyFile", "Ganze Datei kopieren…");

    public static string TakeOverCopyFileConfirm => T("gui.takeOver.copyFileConfirm", "Statt nur der geänderten Fenster kommt die ganze Vanilla-Datei unter demselben Pfad in die Mod und ersetzt sie im Spiel ganz. Nachteil: Andere Mods, die Fenster dieser Datei ändern, und spätere Vanilla-Updates der Datei stoßen dann auf die Kopie; neue Vanilla-Fenster dieser Datei kommen nicht mehr an. Trotzdem die ganze Datei kopieren?");

    /// <summary>
    /// Ein Mod-Fenster, das umzieht (<see cref="Designer.ClassicOverridePlanner.Moves"/>): Es verliert in seiner Mod-Datei. <paramref name="firstWins"/> (EU IV): Die
    /// Datei lädt später als die gewinnende; sonst (HOI IV) früher (gemessen 2026-10-17). <paramref name="dependency"/>: Etikett der Abhängigkeits-Mod, die
    /// gewinnt; null = Vanilla.
    /// </summary>
    public static string ClassicMoveWarning(string window, string fromFile, string toFile, bool firstWins, string? dependency) => (firstWins, dependency) switch
    {
        (true, null) => F("gui.takeOver.moveFirstWins", "Das Fenster {0} steht in {1}, verliert dort aber gegen Vanilla (lädt später). PMT entfernt es dort und schreibt es nach {2}.", window, fromFile, toFile),
        (false, null) => F("gui.takeOver.move", "Das Fenster {0} steht in {1}, verliert dort aber gegen Vanilla (lädt früher). PMT entfernt es dort und schreibt es nach {2}.", window, fromFile, toFile),
        (true, _) => F("gui.takeOver.moveDependencyFirstWins", "Das Fenster {0} steht in {1}, verliert dort aber gegen die Abhängigkeits-Mod {3} (lädt später). PMT entfernt es dort und schreibt es nach {2}.", window, fromFile, toFile, dependency),
        _ => F("gui.takeOver.moveDependency", "Das Fenster {0} steht in {1}, verliert dort aber gegen die Abhängigkeits-Mod {3} (lädt früher). PMT entfernt es dort und schreibt es nach {2}.", window, fromFile, toFile, dependency),
    };

    /// <summary>Rückfrage vor dem Übernehmen, wenn Fenster umziehen (Florian 2026-10-17).</summary>
    public static string ClassicMoveConfirm(IReadOnlyList<(string Window, string FromFile, string ToFile, string? Dependency)> moves, bool firstWins) =>
        string.Join("\n", moves.Select(m => ClassicMoveWarning(m.Window, m.FromFile, m.ToFile, firstWins, m.Dependency))) + " " + T("gui.takeOver.moveContinue", "Fortfahren?");

    /// <summary>Aktion der Übersicht; bei klassischen Fenstern (<see cref="GuiDefinitionKind.NamedWidget"/>) heißt die Einheit "Fenster" statt Typ.</summary>
    public static string TakeOverKind(GuiPlanEntryKind kind, string? detail, GuiDefinitionKind? definitionKind) => (kind, definitionKind) switch
    {
        (GuiPlanEntryKind.Override, GuiDefinitionKind.NamedWidget) => T("gui.takeOver.kind.window.override", "Fenster überschreiben"),
        (GuiPlanEntryKind.Replace, GuiDefinitionKind.NamedWidget) => T("gui.takeOver.kind.window.replace", "Fenster in der Mod ersetzen"),
        (GuiPlanEntryKind.NewDefinition, GuiDefinitionKind.NamedWidget) => T("gui.takeOver.kind.window.new", "Neues Fenster anlegen"),
        (GuiPlanEntryKind.CopiedDefinition, GuiDefinitionKind.NamedWidget) => T("gui.takeOver.kind.window.copied", "Fenster kommt mit der Datei"),
        (GuiPlanEntryKind.RemoveFromMod, GuiDefinitionKind.NamedWidget) => T("gui.takeOver.kind.window.remove", "Fenster aus Mod-Datei entfernen"),
        _ => TakeOverKind(kind, detail),
    };

    public static string TakeOverKind(GuiPlanEntryKind kind, string? detail) => kind switch
    {
        GuiPlanEntryKind.Override => T("gui.takeOver.kind.override", "Überschreiben"),
        GuiPlanEntryKind.Replace => T("gui.takeOver.kind.replace", "In der Mod ersetzen"),
        GuiPlanEntryKind.NewDefinition => T("gui.takeOver.kind.new", "Neu anlegen"),
        GuiPlanEntryKind.CopyFile => T("gui.takeOver.kind.copyFile", "Ganze Datei kopieren"),
        GuiPlanEntryKind.CopiedDefinition => T("gui.takeOver.kind.copied", "Kommt mit der Datei"),
        GuiPlanEntryKind.RemoveFromMod => T("gui.takeOver.kind.remove", "Aus Mod-Datei entfernen"),
        GuiPlanEntryKind.CopyConstant => T("gui.takeOver.kind.copyConstant", "Konstante mitkopieren"),
        _ => F("gui.takeOver.kind.renameConstant", "Konstante umbenennen in @{0}", detail ?? string.Empty),
    };

    /// <summary>Warum die ganze Datei kopiert wird; <paramref name="classic"/> (HOI IV, EU IV): Die Einheit ist das Fenster statt type und template.</summary>
    public static string TakeOverReason(GuiCopyReason reason, bool classic) => (reason, classic) switch
    {
        (GuiCopyReason.Requested, _) => T("gui.takeOver.reason.requested", "\"Ganze Datei kopieren\" ist gewählt. Die Kopie ersetzt die Vanilla-Datei; spätere Vanilla-Updates dieser Datei kommen dann nicht mehr an."),
        (GuiCopyReason.TopLevelChanged, true) => T("gui.takeOver.reason.classicTopLevel", "Inhalt außerhalb der Fenster wurde geändert (z.B. eine @-Konstante oder ein Block ohne name in guiTypes). Das geht nur, indem die ganze Datei in die Mod kopiert wird. Die Kopie ersetzt die Vanilla-Datei; spätere Vanilla-Updates dieser Datei kommen dann nicht mehr an."),
        (GuiCopyReason.DefinitionDeleted, true) => T("gui.takeOver.reason.classicDeleted", "Ein Fenster wurde gelöscht. Das geht nur, indem die ganze Datei ohne es in die Mod kopiert wird. Die Kopie ersetzt die Vanilla-Datei; spätere Vanilla-Updates dieser Datei kommen dann nicht mehr an."),
        _ => TakeOverReason(reason),
    };

    public static string TakeOverReason(GuiCopyReason reason) => reason == GuiCopyReason.Requested ? TakeOverReason(reason, false)
        : reason == GuiCopyReason.TopLevelChanged
        ? T("gui.takeOver.reason.topLevel", "Inhalt außerhalb von type- und template-Definitionen wurde geändert (z.B. ein Widget auf oberster Ebene). Das geht nur, indem die ganze Datei in die Mod kopiert wird. Die Kopie ersetzt die Vanilla-Datei; spätere Vanilla-Updates dieser Datei kommen dann nicht mehr an.")
        : T("gui.takeOver.reason.deleted", "Eine Definition wurde gelöscht. Das geht nur, indem die ganze Datei ohne sie in die Mod kopiert wird. Die Kopie ersetzt die Vanilla-Datei; spätere Vanilla-Updates dieser Datei kommen dann nicht mehr an.");

    public static string TakeOverBlocked => T("gui.takeOver.blocked", "Übernehmen ist so nicht möglich:");

    /// <summary>Text eines Hindernisses; <paramref name="classic"/>: Kopieren scheitert dort an einem DLC-Ordner.</summary>
    public static string TakeOverBlocker(GuiPlanBlocker blocker, bool classic) => blocker.Kind switch
    {
        GuiPlanBlockerKind.CopyNotPossible when classic => F("gui.takeOver.blocker.classicCopy", "Die ganze Datei müsste kopiert werden, sie stammt aber aus dem DLC-Ordner \"{1}\" ({0}). Ob eine Mod-Datei eine Datei eines DLC-Ordners ersetzt, ist nicht gemessen (unsicher). Ändere hier nur Fenster, ohne eines zu löschen.", blocker.Name, string.Join(", ", blocker.Places)),
        _ => TakeOverBlocker(blocker),
    };

    public static string TakeOverBlocker(GuiPlanBlocker blocker) => blocker.Kind switch
    {
        GuiPlanBlockerKind.TargetLoses => TargetLosesText(blocker),
        GuiPlanBlockerKind.DraftErrors => T("gui.takeOver.blocker.errors", "Der Entwurf hat Syntaxfehler. Zuerst beheben."),
        GuiPlanBlockerKind.DuplicateInMod => F("gui.takeOver.blocker.duplicate", "\"{0}\" ist in der Mod mehrfach definiert: {1}", blocker.Name, string.Join(", ", blocker.Places)),
        GuiPlanBlockerKind.ModLoadsAfterVanilla => F("gui.takeOver.blocker.loadsLater", "\"{0}\" ist in der Mod in {1} definiert, aber {2} lädt vorher und gewinnt; eine Änderung in der Mod-Datei wirkte nicht. Verschiebe die Definition in eine Datei, die früher lädt (z. B. gui/00_…), dann erneut übernehmen.", blocker.Name, blocker.Places[0], blocker.Places[1]),
        GuiPlanBlockerKind.NameTaken => F("gui.takeOver.blocker.nameTaken", "Den Namen \"{0}\" gibt es schon: {1}", blocker.Name, string.Join(", ", blocker.Places)),
        GuiPlanBlockerKind.ModChangedSince => F("gui.takeOver.blocker.modChanged", "\"{0}\" hat sich in der Mod geändert, seit der Entwurf begonnen hat ({1}). Entwurf verwerfen und neu beginnen, damit die neue Mod-Fassung die Grundlage ist.", blocker.Name, string.Join(", ", blocker.Places)),
        _ => F("gui.takeOver.blocker.copy", "Die ganze Datei müsste kopiert werden, sie stammt aber aus dem Spielordner \"{1}\" ({0}). Ob eine Mod solche Dateien ersetzen kann, ist ungeklärt. Ändere hier nur type- und template-Definitionen.", blocker.Name, string.Join(", ", blocker.Places)),
    };

    /// <summary>
    /// Klassische Zieldatei, die nicht wirkt (<see cref="GuiPlanBlocker.TargetProblem"/>). Der Rat, den Dateinamen zu ändern, gilt nur einem gesetzten Namen;
    /// hat PMT den Namen gewählt, gibt es keinen gewinnenden.
    /// </summary>
    private static string TargetLosesText(GuiPlanBlocker blocker) => blocker.TargetProblem switch
    {
        GuiTargetProblem.ReplacesFile => F("gui.takeOver.blocker.targetReplaces", "\"{0}\" käme nach {1}. Diese Datei hat denselben Pfad wie eine Datei des Spiels oder einer Abhängigkeits-Mod und ersetzte sie im Spiel ganz. Wähle in den Mod-Einstellungen einen anderen Dateinamen für Überschreibungen oder lass das Feld leer.", blocker.Name, blocker.Places[0]),
        GuiTargetProblem.NotFound => F("gui.takeOver.blocker.targetNotFound", "\"{0}\" stünde nach dem Schreiben nicht als Fenster in {1}. Bitte die Datei prüfen (z.B. ein nicht geschlossener Block); so wird nichts übernommen.", blocker.Name, blocker.Places[0]),
        GuiTargetProblem.InvalidName => F("gui.takeOver.blocker.targetInvalidName", "Der Dateiname \"{1}\" für Überschreibungen aus den Mod-Einstellungen enthält einen Ordner oder unzulässige Zeichen. Trage dort nur einen Dateinamen ein (z.B. zz_meine_mod.gui) oder lass das Feld leer.", blocker.Name, blocker.Places[0]),
        _ when blocker.ConfiguredTarget => F("gui.takeOver.blocker.targetLoses", "\"{0}\" käme nach {1}, im Spiel gälte aber weiter {2}: Die Datei lädt zur falschen Zeit (HOI IV: die zuletzt geladene gewinnt, EU IV: die zuerst geladene; gemessen 2026-10-17). Wähle in den Mod-Einstellungen einen anderen Dateinamen für Überschreibungen oder lass das Feld leer.", blocker.Name, blocker.Places[0], blocker.Places[^1]),
        _ => F("gui.takeOver.blocker.targetLosesDefault", "\"{0}\" käme nach {1}, im Spiel gälte aber weiter {2}. PMT findet keinen Dateinamen, der zur passenden Zeit lädt (HOI IV: die zuletzt geladene Datei gewinnt, EU IV: die zuerst geladene; gemessen 2026-10-17). Ändere das Fenster dort, wo es gilt, oder setze in den Mod-Einstellungen einen eigenen Dateinamen für Überschreibungen.", blocker.Name, blocker.Places[0], blocker.Places[^1]),
    };

    public static string TakeOverWarning(GuiPlanWarning warning) => warning.Kind == GuiPlanWarningKind.DependencyWins
        ? F("gui.takeOver.warning.dependencyWins", "\"{0}\": Im Spiel gilt weiter die Fassung der Abhängigkeits-Mod in {1}, weil sie nach der Ladereihenfolge gewinnt. Die Änderung wirkt nur ohne diese Mod.", warning.Name, string.Join(", ", warning.Places))
        : F("gui.takeOver.warning.uncertainOrder", "\"{0}\" kommt nach {1}. Ob das Fenster dort gewinnt, hängt an einer Datei aus einem DLC-Ordner; wie das Spiel DLC-Ordner einsortiert, ist nicht gemessen (unsicher).", warning.Name, string.Join(", ", warning.Places));

    /// <summary>Warum ein klassisches Fenster in diese Datei kommt (Übersicht).</summary>
    public static string TakeOverRule(ClassicTargetRule rule) => rule switch
    {
        ClassicTargetRule.RootLastWins => T("gui.takeOver.rule.rootLastWins", "lädt als letzte Datei direkt in interface/ (HOI IV: die zuletzt geladene gewinnt; gemessen 2026-10-17)"),
        ClassicTargetRule.SubfolderLastWins => T("gui.takeOver.rule.subfolderLastWins", "lädt als letzte Datei im Unterordner des Vanilla-Fensters (HOI IV: die zuletzt geladene gewinnt; gemessen 2026-10-17)"),
        ClassicTargetRule.ExistingWinner => T("gui.takeOver.rule.existingWinner", "gewinnt nach der Ladereihenfolge (geprüft)"),
        ClassicTargetRule.RootFirstWins => T("gui.takeOver.rule.rootFirstWins", "lädt vor allen Vanilla-Dateien (EU IV: die zuerst geladene gewinnt; gemessen 2026-10-17)"),
        _ => T("gui.takeOver.rule.configured", "Dateiname aus den Mod-Einstellungen"),
    };

    public static string TakeOverOpenModified(string file) => F("gui.takeOver.openModified", "{0} ist geöffnet und hat ungespeicherte Änderungen. Zuerst speichern oder verwerfen.", file);

    public static string TakeOverFiles(int created, int changed) => F("gui.takeOver.files", "{0:N0} neue und {1:N0} geänderte Datei(en). Vor dem Ändern bestehender Dateien legt PMT eine Sicherung an; \"Rückgängig\" im Hauptfenster nimmt die Übernahme als Ganzes zurück.", created, changed);

    public static string TakeOverApply => T("gui.takeOver.apply", "Übernehmen");

    public static string TakeOverCancel => T("gui.takeOver.cancel", "Abbrechen");

    public static string TakeOverAction(string file) => F("gui.takeOver.action", "GUI in Mod übernommen: {0}", file);

    public static string TakeOverFileAction(string file) => F("gui.takeOver.fileAction", "GUI-Datei geschrieben: {0}", file);

    public static string TakeOverSnapshotNote => T("gui.takeOver.snapshot", "vor GUI-Übernahme");

    public static string TakeOverSnapshotFailed(string message) => F("gui.takeOver.snapshotFailed", "Die Sicherung konnte nicht angelegt werden, deshalb wurde nichts geschrieben: {0}", message);

    public static string TakeOverFailed(string message) => F("gui.takeOver.failed", "Das Schreiben ist fehlgeschlagen: {0}", message);

    // GUI-Einstellungen der Mod.
    public static string MenuModSettings => T("gui.menu.modSettings", "GUI-Einstellungen der Mod");

    public static string MenuModSettingsTip => T("gui.menu.modSettings.tip", "Zieldateien für \"In Mod übernehmen\" und Abhängigkeits-Mods für den GUI-Index der aktiven Mod");

    public static string ModSettingsTitle(string mod) => F("gui.modSettings.title", "GUI-Einstellungen: {0}", mod);

    public static string ModSettingsOverrideFile => T("gui.modSettings.overrideFile", "Überschreibdatei (liegt je Kontext in in_game/gui, main_menu/gui bzw. loading_screen/gui):");

    public static string ModSettingsTypesFile => T("gui.modSettings.typesFile", "Typdatei für neue Typen:");

    public static string ModSettingsOverrideFileFlat => T("gui.modSettings.overrideFileFlat", "Überschreibdatei (liegt in gui/):");

    public static string ModSettingsFirstWinsHint => T("gui.modSettings.firstWinsHint", "In diesem Spiel gewinnt die zuerst geladene Datei: Die Standardnamen beginnen deshalb mit 00_, damit sie vor den anderen Dateien der Mod laden.");

    public static string ModSettingsFirstWinsVanillaHint => T("gui.modSettings.firstWinsVanillaHint", "In diesem Spiel gewinnt die zuerst geladene Datei, und Vanilla und Mods laden gemeinsam nach Pfad: Die Standardnamen beginnen deshalb mit 00_ und liegen direkt in gui/, damit sie vor den Vanilla-Dateien laden. Ein eigener Name, der später sortiert, verliert gegen Vanilla.");

    /// <summary>Der Hinweis zu den Dateinamen je nach Ladereihenfolge des Spiels (Victoria 3: auch vor Vanilla).</summary>
    public static string ModSettingsFirstWinsHintFor(ParadoxModTool.GuiDesign.Index.GuiLayout layout) =>
        layout.ModsBeforeVanilla ? ModSettingsFirstWinsHint : ModSettingsFirstWinsVanillaHint;

    public static string ModSettingsOverrideFileClassic => T("gui.modSettings.overrideFileClassic", "Überschreibdatei für geänderte Vanilla-Fenster (HOI IV: im Ordner der Vanilla-Datei unter interface/, EU IV: immer direkt in interface/):");

    /// <summary>Gemessene Regel des klassischen Spiels für die Überschreibdatei; <paramref name="firstWins"/>: EU IV, sonst HOI IV.</summary>
    public static string ModSettingsClassicHint(bool firstWins) => firstWins
        ? T("gui.modSettings.classicHintFirstWins", "EU IV: Bei doppeltem Fensternamen gewinnt die zuerst geladene Datei; die Dateien direkt in interface/ laden nach Namen, Vanilla und Mods gemischt, vor den Unterordnern (gemessen 2026-10-17). Der Standardname beginnt deshalb mit 00_; sortiert eine Vanilla-Datei noch früher, verlängert PMT das Präfix (000_). Ein eigener Name gilt wie angegeben; verliert er, blockiert die Übernahme.")
        : T("gui.modSettings.classicHintLastWins", "HOI IV: Bei doppeltem Fensternamen gewinnt die zuletzt geladene Datei; die Dateien eines Ordners laden nach Namen, Vanilla und Mods gemischt (gemessen 2026-10-17). Der Standardname beginnt deshalb mit zz_; sortiert eine Vanilla-Datei noch später, verlängert PMT das Präfix (zzz_). Ein eigener Name gilt wie angegeben; verliert er, blockiert die Übernahme.");

    public static string ModSettingsDependencies => T("gui.modSettings.dependencies", "Abhängigkeits-Mods (ihre Typen und Templates kommen in den GUI-Index):");

    public static string ModSettingsAdd => T("gui.modSettings.add", "Ordner hinzufügen");

    public static string ModSettingsAddTitle => T("gui.modSettings.addTitle", "Ordner der Abhängigkeits-Mod wählen");

    public static string ModSettingsRemove => T("gui.modSettings.remove", "Entfernen");

    public static string ModSettingsDefaults => T("gui.modSettings.defaults", "Vorbelegung");

    public static string ModSettingsDefaultsTip => T("gui.modSettings.defaults.tip", "Stellt die Vorbelegung wieder her: die Hauptmod eines Submods bzw. die Ziele eines Kompatchs");

    public static string ModSettingsInvalidFile => T("gui.modSettings.invalidFile", "Dateinamen müssen auf .gui enden und dürfen keinen Ordner enthalten.");

    public static string ModSettingsSave => T("gui.modSettings.save", "Speichern");

    public static string ModSettingsCancel => T("gui.modSettings.cancel", "Abbrechen");

    public static string ModSettingsNoMod => T("gui.modSettings.noMod", "Keine aktive Mod aus dem Register. Einstellen lässt sich das nur für eine registrierte Mod; sonst gelten die Standardnamen.");

    public static string NewFileContext => T("gui.newFile.context", "Kontext:");

    public static string NewFileContextTip => T("gui.newFile.context.tip", "in_game: im laufenden Spiel, main_menu: Hauptmenü, loading_screen: Ladebildschirm");

    // GUI-Bausteine (docs/superpowers/specs/2026-09-27-gui-building-blocks-design.md).
    public static string BlocksDefaultGroup => T("gui.blocks.defaultGroup", "Eigene");

    public static string BlocksEmptyFileName => T("gui.blocks.emptyFile", "Leere Datei");

    public static string BlocksEmptyFileDescription => T("gui.blocks.emptyFile.description", "Leere .gui-Datei ohne Inhalt");

    public static string BlocksTitle => T("gui.blocks.title", "Bausteine");

    public static string BlocksSearchTip => T("gui.blocks.search.tip", "Nach Name, Gruppe oder Beschreibung filtern");

    public static string BlocksCount => T("gui.blocks.count", "Anzahl:");

    public static string BlocksOwnCopy => T("gui.blocks.ownCopy", "Als eigene Kopie bearbeiten");

    public static string BlocksOwnCopyTip => T("gui.blocks.ownCopy.tip", "Kopie in den eigenen Bausteinen anlegen und im Editor öffnen; PMT-Updates überschreiben sie nicht");

    public static string BlocksOwnCopyGame => T("gui.blocks.ownCopy.game", "Für alle Mods dieses Spiels");

    public static string BlocksOwnCopyMod => T("gui.blocks.ownCopy.mod", "Nur für die aktive Mod");

    public static string BlocksOwnCopyOverwrite(string name) => F("gui.blocks.ownCopy.overwrite", "Einen eigenen Baustein {0} gibt es dort schon. Überschreiben?", name);

    public static string BlocksOwnCopyFailed(string message) => F("gui.blocks.ownCopy.failed", "Die Kopie konnte nicht angelegt werden: {0}", message);

    public static string ContextSaveBlock => T("gui.blocks.save.context", "Als Baustein speichern");

    public static string BlocksSaveTitle => T("gui.blocks.save.title", "Als Baustein speichern");

    public static string BlocksSaveName => T("gui.blocks.save.name", "Name:");

    public static string BlocksSaveGroup => T("gui.blocks.save.group", "Gruppe:");

    public static string BlocksSaveDescription => T("gui.blocks.save.description", "Beschreibung:");

    public static string BlocksSaveLayer => T("gui.blocks.save.layer", "Speichern für:");

    public static string BlocksSaveLayerGame => T("gui.blocks.save.layer.game", "alle Mods dieses Spiels");

    public static string BlocksSaveLayerMod => T("gui.blocks.save.layer.mod", "nur die aktive Mod");

    public static string BlocksSaveCode => T("gui.blocks.save.code", "Code:");

    public static string BlocksSaveKeys => T("gui.blocks.save.keys", "Platzhalter und Wiederholungen:");

    public static string BlocksSaveKey => T("gui.blocks.save.key", "Schlüssel");

    public static string BlocksSaveLabel => T("gui.blocks.save.label", "Beschriftung");

    public static string BlocksSaveDefault => T("gui.blocks.save.default", "Vorgabe");

    public static string BlocksSaveKind => T("gui.blocks.save.kind", "Art");

    public static string BlocksSaveKindText => T("gui.blocks.save.kind.text", "Text");

    public static string BlocksSaveKindModifier => T("gui.blocks.save.kind.modifier", "Modifier");

    public static string BlocksSaveRepeatCount => T("gui.blocks.save.repeatCount", "Anzahl");

    public static string BlocksSavePreview => T("gui.blocks.save.preview", "So sieht die Eingabe aus:");

    public static string BlocksSaveHelp => T("gui.blocks.save.help", "$name$ setzt einen Wert ein. $repeat tabs$ … $end tabs$ wiederholt einen Abschnitt, darin ist $tabs.id$ ein Wert je Eintrag und $index$ die laufende Nummer. $$ ergibt ein Dollarzeichen.");

    public static string BlocksSaveButton => T("gui.blocks.save.button", "Speichern");

    public static string BlocksSaveCancel => T("gui.blocks.save.cancel", "Abbrechen");

    public static string BlocksSaveNameMissing => T("gui.blocks.save.nameMissing", "Bitte einen Namen eingeben.");

    public static string BlocksSaveOverwrite(string name) => F("gui.blocks.save.overwrite", "Einen eigenen Baustein {0} gibt es in dieser Ebene schon. Überschreiben?", name);

    public static string BlocksSaveFailed(string message) => F("gui.blocks.save.failed", "Der Baustein konnte nicht gespeichert werden: {0}", message);

    public static string ShareExport => T("gui.blocks.share.export", "Exportieren");

    public static string ShareExportTip => T("gui.blocks.share.export.tip", "Eigene Bausteine als Paket (.pmtblocks) speichern, z.B. zum Teilen im Discord oder Forum");

    public static string ShareImport => T("gui.blocks.share.import", "Importieren");

    public static string ShareImportTip => T("gui.blocks.share.import.tip", "Bausteine aus einem Paket (.pmtblocks) oder einer .guiblock-Datei übernehmen");

    public static string ShareExportTitle => T("gui.blocks.share.exportTitle", "Bausteine exportieren");

    public static string ShareImportTitle => T("gui.blocks.share.importTitle", "Bausteine importieren");

    public static string ShareFilter => T("gui.blocks.share.filter", "PMT-Bausteinpaket (*.pmtblocks)|*.pmtblocks");

    public static string ShareImportFilter => T("gui.blocks.share.importFilter", "Bausteine (*.pmtblocks;*.guiblock)|*.pmtblocks;*.guiblock");

    public static string ShareNoOwnBlocks => T("gui.blocks.share.noOwnBlocks", "Es gibt noch keine eigenen Bausteine zum Exportieren.");

    public static string ShareOtherGame(string game) => F("gui.blocks.share.otherGame", "Das Paket stammt von einem anderen Spiel ({0}). Bausteine passen womöglich nicht.", game);

    public static string ShareManifestProblem => T("gui.blocks.share.manifestProblem", "Die Paketbeschreibung fehlt oder ist unlesbar; die Bausteine werden trotzdem gelesen.");

    public static string ShareStatusOk => T("gui.blocks.share.status.ok", "neu");

    public static string ShareStatusConflict => T("gui.blocks.share.status.conflict", "gibt es schon");

    public static string ShareStatusErrors => T("gui.blocks.share.status.errors", "fehlerhaft");

    public static string ShareOverwrite => T("gui.blocks.share.overwrite", "Überschreiben");

    public static string ShareSkipped(string list) => F("gui.blocks.share.skipped", "Übersprungen: {0}", list);

    public static string ShareResult(int imported, int skipped) => F("gui.blocks.share.result", "{0} importiert, {1} übersprungen.", imported, skipped);

    public static string ShareExported(int count, string path) => F("gui.blocks.share.exported", "{0} Bausteine exportiert nach {1}.", count, path);

    public static string ShareFailed(string message) => F("gui.blocks.share.failed", "Das Paket konnte nicht gelesen oder geschrieben werden: {0}", message);

    public static string SuggestButton => T("gui.blocks.suggest.button", "Platzhalter vorschlagen");

    public static string SuggestButtonTip => T("gui.blocks.suggest.button.tip", "PMT sucht Namen, Texte, Texturen, Größen, Variablen und gleichartige Elemente und schlägt Platzhalter und Wiederholungen vor");

    public static string SuggestTitle => T("gui.blocks.suggest.title", "Platzhalter vorschlagen");

    public static string SuggestApply => T("gui.blocks.suggest.apply", "Übernehmen");

    public static string SuggestAll => T("gui.blocks.suggest.all", "Alle");

    public static string SuggestNoneButton => T("gui.blocks.suggest.noneButton", "Keine");

    public static string SuggestCancel => T("gui.blocks.suggest.cancel", "Abbrechen");

    public static string SuggestNone => T("gui.blocks.suggest.none", "Keine Vorschläge gefunden.");

    public static string SuggestKindValue => T("gui.blocks.suggest.kind.value", "Wert");

    public static string SuggestKindRepeat => T("gui.blocks.suggest.kind.repeat", "Wiederholung");

    public static string SuggestFound(string found, int count) => F("gui.blocks.suggest.found", "{0} ({1}×)", found, count);

    public static string SuggestName => T("gui.blocks.suggest.name", "Name");

    public static string SuggestWidth => T("gui.blocks.suggest.width", "Breite");

    public static string SuggestHeight => T("gui.blocks.suggest.height", "Höhe");

    public static string SuggestText(int number) => F("gui.blocks.suggest.text", "Text {0}", number);

    public static string SuggestTexture(int number) => F("gui.blocks.suggest.texture", "Textur {0}", number);

    public static string SuggestVariable(int number) => F("gui.blocks.suggest.variable", "Variable {0}", number);

    public static string SuggestId(int number) => F("gui.blocks.suggest.id", "Kennung {0}", number);

    public static string SuggestRepeat(string elementKey) => F("gui.blocks.suggest.repeat", "Einträge {0}", elementKey);

    public static string BlocksModifierTip => T("gui.blocks.modifier.tip", "Modifier aus der Datenbank des Spiels wählen; freie Eingabe bleibt möglich");

    public static string BlocksEntryFor(string label) => F("gui.blocks.entryFor", "{0} für:", label);

    public static string BlocksPreview => T("gui.blocks.preview", "Vorschau");

    public static string BlocksMissing(string names) => F("gui.blocks.missing", "Im GUI-Index nicht gefunden: {0}. Der Code funktioniert erst, wenn es diese Typen oder Templates gibt.", names);

    public static string BlocksHasErrors => T("gui.blocks.hasErrors", "Der Baustein hat Fehler und kann nicht eingefügt werden:");

    public static string BlocksErrorLine(int line, string message) => F("gui.blocks.errorLine", "Zeile {0}: {1}", line, message);

    public static string BlocksInsert => T("gui.blocks.insert", "Einfügen");

    public static string BlocksOpenFile => T("gui.blocks.openFile", "Datei öffnen");

    public static string BlocksDelete => T("gui.blocks.delete", "Löschen");

    public static string BlocksClose => T("gui.blocks.close", "Schließen");

    public static string BlocksDeleteConfirm(string name) => F("gui.blocks.deleteConfirm", "Baustein {0} löschen?", name);

    public static string BlocksDeleteFailed(string message) => F("gui.blocks.deleteFailed", "Der Baustein konnte nicht gelöscht werden: {0}", message);

    public static string BlocksLayerGame => T("gui.blocks.layer.game", "(Spiel)");

    public static string BlocksLayerMod => T("gui.blocks.layer.mod", "(Mod)");

    public static string BlocksLayerShipped => T("gui.blocks.layer.shipped", "(mitgeliefert)");

    public static string BlocksHidden(string layers) => F("gui.blocks.hidden", "Verdeckt: {0}", layers);

    public static string BlocksNoShipped => T("gui.blocks.noShipped", "Für dieses Spiel liefert PMT noch keine Bausteine mit; die Liste zeigt nur eigene.");

    public static string ContextBlocks => T("gui.blocks.context", "Baustein einfügen");

    public static string KeyBlocks => T("gui.blocks.key", "Strg+Umschalt+B");

    public static string BlocksButton => T("gui.blocks.button", "Bausteine");

    public static string BlocksButtonTip => T("gui.blocks.button.tip", "GUI-Baustein auswählen, ausfüllen und an der Cursorstelle einfügen (Strg+Umschalt+B)");

    public static string BlocksError(Blocks.GuiBlockErrorKind kind, string detail) => kind switch
    {
        Blocks.GuiBlockErrorKind.MissingBlock => T("gui.blocks.error.missingBlock", "Die Kopfzeile \"# block:\" mit dem Namen fehlt."),
        Blocks.GuiBlockErrorKind.DuplicateBlock => F("gui.blocks.error.duplicateBlock", "\"# block:\" steht mehrfach im Kopf: {0}", detail),
        Blocks.GuiBlockErrorKind.BlockNotFirst => F("gui.blocks.error.blockNotFirst", "\"# block:\" muss die erste Kopfzeile sein: {0}", detail),
        Blocks.GuiBlockErrorKind.InvalidHeader => F("gui.blocks.error.invalidHeader", "Kopfzeile ungültig: {0}", detail),
        Blocks.GuiBlockErrorKind.InvalidKey => F("gui.blocks.error.invalidKey", "Ungültiger Schlüssel (erlaubt sind a-z, 0-9, _ und Punkte; \"index\" ist reserviert): {0}", detail),
        Blocks.GuiBlockErrorKind.UndeclaredParent => F("gui.blocks.error.undeclaredParent", "Die Wiederholung zu diesem Schlüssel ist nicht vorher deklariert: {0}", detail),
        Blocks.GuiBlockErrorKind.DuplicateKey => F("gui.blocks.error.duplicateKey", "Schlüssel doppelt deklariert: {0}", detail),
        Blocks.GuiBlockErrorKind.UnknownPlaceholder => F("gui.blocks.error.unknownPlaceholder", "Unbekannter Platzhalter oder unbekannte Wiederholung: {0}", detail),
        Blocks.GuiBlockErrorKind.OutsideRepeat => F("gui.blocks.error.outsideRepeat", "Platzhalter oder Wiederholung außerhalb der zugehörigen Wiederholung: {0}", detail),
        Blocks.GuiBlockErrorKind.MissingEnd => F("gui.blocks.error.missingEnd", "Zu $repeat {0}$ fehlt das passende $end {0}$.", detail),
        Blocks.GuiBlockErrorKind.MismatchedEnd => F("gui.blocks.error.mismatchedEnd", "$end {0}$ passt zu keinem offenen $repeat$.", detail),
        Blocks.GuiBlockErrorKind.LoneDollar => F("gui.blocks.error.loneDollar", "Einzelnes $ (für ein Dollarzeichen $$ schreiben): {0}", detail),
        Blocks.GuiBlockErrorKind.DuplicateName => F("gui.blocks.error.duplicateName", "Ein anderer Baustein dieser Ebene hat schon diesen Namen: {0}", detail),
        _ => F("gui.blocks.error.unreadable", "Datei nicht lesbar: {0}", detail),
    };

    public static string InspectorOpenInDesigner => T("gui.inspector.openInDesigner", "Im Designer öffnen");

    public static string InspectorOpenInDesignerTip => T("gui.inspector.openInDesigner.tip", "Öffnet die Definition im Designer; eine Vanilla-Datei wird dabei zum Entwurf für die Mod");

    // Plan 3c: Geltungsbereich, eigene Typen, Widget-Schlüssel, Kopie aus dem Inspektor, Entwurf auf Abhängigkeits-Basis.
    public static string ScopeNewType => T("gui.scope.newType", "Neuer Typ");

    public static string ScopeNewTypeTip => T("gui.scope.newType.tip", "Legt einen eigenen Typ auf Basis dieses Elements an und stellt die Stelle darauf um. Danach ist der neue Typ gewählt; Änderungen an ihm wirken an allen Stellen, die ihn verwenden.");

    public static string ScopeTypeEverywhere => T("gui.scope.typeEverywhere", "Typ überall");

    public static string ScopeTypeEverywhereTip => T("gui.scope.typeEverywhere.tip", "Öffnet die Definition des Typs. Änderungen dort wirken überall, wo er verwendet wird; eine Vanilla-Definition wird dabei zum Entwurf für die Mod.");

    public static string TypeEverywhereNone(string key) => F("gui.scope.typeEverywhere.none", "Zu {0} gibt es keine Typ-Definition.", key);

    public static string TreeExtractType => T("gui.tree.extractType", "Als neuen Typ herausziehen");

    public static string TreeRetype => T("gui.tree.retype", "Widget-Schlüssel umstellen");

    public static string NewTypeTitle => T("gui.newType.title", "Neuer Typ");

    public static string NewTypeExtractTitle => T("gui.newType.extractTitle", "Als neuen Typ herausziehen");

    public static string NewTypeCopyTitle => T("gui.newType.copyTitle", "Als neuen Typ in Mod übernehmen");

    public static string NewTypeBase(string element) => F("gui.newType.base", "Ausgangspunkt: {0}", element);

    public static string NewTypeName => T("gui.newType.name", "Name des neuen Typs:");

    public static string NewTypeSpots => T("gui.newType.spots", "Weitere Stellen in dieser Datei ebenfalls umstellen:");

    public static string NewTypeSpot(int line, string label) => F("gui.newType.spot", "Zeile {0}: {1}", line, label);

    public static string NewTypeElsewhere(int count) => F("gui.newType.elsewhere", "{0:N0} Verwendungen in anderen Dateien bleiben unverändert. Dort lässt sich der Schlüssel mit \"Widget-Schlüssel umstellen\" ändern.", count);

    public static string NewTypeExtractHint => T("gui.newType.extractHint", "Alle Eigenschaften und Kinder außer name kommen in den neuen Typ. Die Stelle behält nur name.");

    public static string NewTypeWhereDraft => T("gui.newType.whereDraft", "Der Typ kommt in einen types-Container dieser Datei. \"In Mod übernehmen\" schreibt ihn in die Typdatei der Mod.");

    public static string NewTypeWhereFile => T("gui.newType.whereFile", "Der Typ kommt in einen types-Container dieser Datei.");

    public static string NewTypeWhereCopy(string file) => F("gui.newType.whereCopy", "Der Typ kommt in die Typdatei der Mod ({0}). Vorher zeigt eine Übersicht, was geschrieben wird.", file);

    public static string NewTypeInvalidName => T("gui.newType.invalidName", "Der Name darf nur Buchstaben, Ziffern und \"_\" enthalten und nicht mit einer Ziffer beginnen.");

    public static string NewTypeNameTaken(string name) => F("gui.newType.nameTaken", "Den Namen {0} gibt es schon.", name);

    public static string NewTypeCreate => T("gui.newType.create", "Anlegen");

    public static string NewTypeCancel => T("gui.newType.cancel", "Abbrechen");

    public static string RetypeTitle => T("gui.retype.title", "Widget-Schlüssel umstellen");

    public static string RetypeCurrent(string key) => F("gui.retype.current", "Bisher: {0}", key);

    public static string RetypeKey => T("gui.retype.key", "Neuer Schlüssel (Typ oder Engine-Widget):");

    public static string RetypeUnknown(string key) => F("gui.retype.unknown", "{0} ist weder ein bekannter Typ noch ein Engine-Widget.", key);

    public static string RetypeLostBlocks(string key, string blocks) => F("gui.retype.lostBlocks", "{0} bietet diese Blöcke nicht an; ihre blockoverride wirken danach nicht mehr: {1}", key, blocks);

    public static string RetypeSame => T("gui.retype.same", "Der Schlüssel ist unverändert.");

    public static string RetypeApply => T("gui.retype.apply", "Umstellen");

    public static string InspectorTakeOverAsType => T("gui.inspector.takeOverAsType", "Als neuen Typ in Mod übernehmen");

    public static string InspectorTakeOverAsTypeTip => T("gui.inspector.takeOverAsType.tip", "Kopiert das Element unter neuem Namen in die Typdatei der Mod, samt der @-Konstanten, die es braucht. Geht für Elemente aus Vanilla und aus Abhängigkeits-Mods.");

    public static string InspectorTakeOverAsTypeNotPossible => T("gui.inspector.takeOverAsType.notPossible", "Übernehmen geht nur für Typen, Templates und Widgets aus Vanilla oder einer Abhängigkeits-Mod, so wie sie gespeichert sind.");

    public static string TakeOverNewTypeAction(string name) => F("gui.takeOver.newTypeAction", "Neuer Typ in Mod: {0}", name);

    public static string DraftDependencyCopy(string mod) => F("gui.draft.dependencyCopy", "Die Abhängigkeits-Mod {0} ersetzt diese Datei ganz. Der Entwurf beginnt mit ihrer Fassung. Wird Inhalt außerhalb von Definitionen geändert, kopiert \"In Mod übernehmen\" diese Fassung samt Änderungen in die Mod.", mod);

    // Plan 4: Spiel-Anbindung.
    public static string ViewInGame => T("gui.game.viewInGame", "Im Spiel ansehen");

    public static string ViewInGameTip => T("gui.game.viewInGame.tip", "Speichert die Mod-Dateien (Entwürfe über \"In Mod übernehmen\") und startet das Spiel mit -debug_mode, falls es noch nicht läuft. Im Debug-Modus lädt das Spiel geänderte GUI-Dateien selbst nach, neu angelegte Dateien erst nach einem Neustart.");

    public static string ViewInGameUnsaved => T("gui.game.viewInGame.unsaved", "Nicht alle Mod-Dateien sind gespeichert bzw. übernommen. Das Spiel wird nicht gestartet.");

    public static string ViewInGameUnsavedRunning => T("gui.game.viewInGame.unsavedRunning", "Nicht alle Mod-Dateien sind gespeichert bzw. übernommen. Das Spiel läuft bereits und sieht nur gespeicherte Dateien.");

    public static string ViewInGameRunningUnknown => T("gui.game.viewInGame.runningUnknown", "Hot-Reload wirkt nur im Debug-Modus. Läuft das Spiel mit -debug_mode?");

    public static string ViewInGameRestartNeeded(string files) => F("gui.game.viewInGame.restartNeeded", "Diese Dateien sind neu, seit das Spiel läuft. Das Spiel lädt sie erst nach einem Neustart: {0}", files);

    public static string GameEditorHeading => T("gui.game.editor.heading", "Spiel-Anbindung");

    public static string GameEditorRegister(string game) => F("gui.game.editor.register", "PMT als Editor für {0} eintragen", game);

    public static string GameEditorRegisterTip => T("gui.game.editor.register.tip", "Trägt PMT in pdx_settings.json des Spiels als externen Editor ein (System.editor). Das geht nur bei geschlossenem Spiel und wirkt beim nächsten Start. Im Spiel mit -debug_mode öffnet Alt+Rechtsklick (UI Bounds) die Datei dann in PMT. Vorher werden die Datei und der alte Wert gesichert. Wer den Pfad stattdessen im Spiel einträgt, muss das Spiel danach neu starten.");

    public static string GameEditorRestore => T("gui.game.editor.restore", "Alten Editor wiederherstellen");

    public static string GameEditorRestoreTip => T("gui.game.editor.restore.tip", "Trägt den Editor wieder ein, der vor PMT eingetragen war");

    public static string GameEditorTitle => T("gui.game.editor.title", "PMT als Editor");

    public static string GameEditorStatus(string editor) => F("gui.game.editor.status", "Eingetragener Editor: {0}", editor);

    public static string GameEditorStatusNone => T("gui.game.editor.statusNone", "Im Spiel ist kein externer Editor eingetragen.");

    public static string GameEditorStatusPmt => T("gui.game.editor.statusPmt", "PMT ist im Spiel als Editor eingetragen.");

    public static string GameEditorUnavailable(string path) => F("gui.game.editor.unavailable", "Keine pdx_settings.json gefunden ({0}). Das Spiel legt sie beim ersten Start an.", path);

    public static string GameEditorRegistered => T("gui.game.editor.registered", "PMT ist jetzt als Editor eingetragen. Die Datei und der alte Wert sind gesichert.");

    public static string GameEditorAlreadyRegistered => T("gui.game.editor.alreadyRegistered", "PMT ist schon als Editor eingetragen.");

    public static string GameEditorRestored(string editor) => F("gui.game.editor.restored", "Der alte Editor ist wieder eingetragen: {0}", editor);

    public static string GameEditorRestoredNone => T("gui.game.editor.restoredNone", "Der Eintrag ist entfernt; vorher war kein Editor eingetragen.");

    public static string GameEditorNothingToRestore => T("gui.game.editor.nothingToRestore", "Es gibt keinen gesicherten alten Editor.");

    public static string GameEditorChangedSince(string editor) => F("gui.game.editor.changedSince", "Der Editor wurde inzwischen geändert ({0}). PMT stellt nichts wieder her.", editor);

    public static string GameEditorUnknownFormat(string path) => F("gui.game.editor.unknownFormat", "pdx_settings.json hat ein unbekanntes Format und bleibt unverändert: {0}", path);

    public static string GameEditorWriteFailed(string detail) => F("gui.game.editor.writeFailed", "Schreiben fehlgeschlagen: {0}", detail);

    public static string GameEditorLocked(string path) => F("gui.game.editor.locked", "pdx_settings.json wird gerade von einem anderen Programm verwendet (Spiel oder Launcher?) und bleibt unverändert: {0}", path);

    public static string GameEditorNoPath => T("gui.game.editor.noPath", "Pfad zu pdx_settings.json unbekannt: Stelle die playsets.json oder im Spielprofil die Vorlage für den Mod-Ordner ein.");

    public static string GameEditorGameRunning => T("gui.game.editor.gameRunning", "Das Spiel läuft. Bitte zuerst das Spiel schließen; es schreibt seine Einstellungen beim Beenden vermutlich neu.");

    public static string GameEditorNoExe => T("gui.game.editor.noExe", "Der Pfad zur Spiel-Exe ist nicht eingestellt. Ohne ihn kann PMT nicht prüfen, ob das Spiel läuft.");

    public static string GameEditorDevHost => T("gui.game.editor.devHost", "PMT läuft nicht als ParadoxModTool.exe (z.B. über dotnet). Eintragen geht nur mit der gebauten Exe.");

    public static string HandOffMissing(string path) => F("gui.game.handOff.missing", "Die Datei aus dem Spiel gibt es nicht: {0}", path);

    public static string HandOffReadFailed(string path, string detail) => F("gui.game.handOff.readFailed", "Die Datei aus dem Spiel lässt sich nicht lesen: {0} ({1})", path, detail);

    public static string HandOffNotRecognised(string log) => F("gui.game.handOff.notRecognised", "PMT hat im Aufruf keine vorhandene Datei erkannt und öffnet deshalb nichts. Die Kommandozeile steht im Protokoll: {0}", log);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
