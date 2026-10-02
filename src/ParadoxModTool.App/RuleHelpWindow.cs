using ParadoxModTool.Localization;

namespace ParadoxModTool.App;

/// <summary>
/// Anleitung zum Erstellen von Zählregeln mit Beispielen. Der Text steht als Liste von Überschriften, Absätzen und Beispielkästen in <see cref="Blocks"/>,
/// damit er sich leicht pflegen lässt.
/// </summary>
public sealed class RuleHelpWindow : GuideWindow
{
    /// <summary>Die Blöcke in der geladenen Oberflächensprache; bei jedem Öffnen neu gebaut, damit die Sprache stimmt.</summary>
    private static GuideBlock[] Blocks => new[]
    {
        new GuideBlock(GuideBlockKind.Title, T("rulehelp.title", "Anleitung: Zählregeln")),

        new GuideBlock(GuideBlockKind.Heading, T("rulehelp.whatIs.heading", "Was ist eine Regel?")),
        new GuideBlock(GuideBlockKind.Paragraph, T("rulehelp.whatIs.intro", "Der Template-Generator entscheidet für jede Zahl in deinem Snippet, ob sie hochgezählt wird. Eine Zählregel sagt ihm bei bestimmten Namen genau, was er tun soll, zum Beispiel: „In Namen wie iv2_ga_select_historic_ARA_1_1 zählt nur die Zahl hinter ARA“. Die Regel legst du einmal an, sie gilt danach bei jedem Erzeugen.")),
        new GuideBlock(GuideBlockKind.Paragraph, T("rulehelp.whatIs.parts", "Eine Regel besteht aus drei Angaben:\n1. Muster: Welche Namen sind gemeint?\n2. Wo: Überall im Snippet oder nur innerhalb eines bestimmten Eintrags?\n3. Aktion: Was passiert mit den Zahlen dieser Namen?")),
        new GuideBlock(GuideBlockKind.Paragraph, T("rulehelp.whatIs.quickest", "Der schnellste Weg: Rechtsklick auf eine Zahl im Snippet und „Regel aus dieser Zahl erstellen“ (oder in der Zählpunkte-Liste eine Zeile wählen und „Regel erstellen“). Der Dialog ist dann schon ausgefüllt, du musst das Muster nur noch mit * allgemeiner machen, wenn die Regel für mehr Namen gelten soll.")),

        new GuideBlock(GuideBlockKind.Heading, T("rulehelp.parts.heading", "Die Teile einer Regel")),
        new GuideBlock(GuideBlockKind.Paragraph, T("rulehelp.parts.name", "Name: Nur für dich, damit du die Regel in der Liste wiederfindest. Er erscheint auch als Begründung, wenn du auf eine Zahl klickst („Regel „…“ (Ebene)“).")),
        new GuideBlock(GuideBlockKind.Paragraph, T("rulehelp.parts.layer", "Ebene: Wo die Regel gespeichert wird und für wen sie gilt.\n• Vorlage: nur für die Vorlage, die gerade offen ist.\n• Mod: für alle Vorlagen dieser Mod (die Mod wählst du im Template-Fenster im Feld „Mod:“).\n• Spiel: für alle Mods dieses Spiels.\n• Alle Spiele: für mehrere Spiele; dort wählst du, für welche.")),
        new GuideBlock(GuideBlockKind.Paragraph, T("rulehelp.parts.where", "Wo:\n• Überall: jeder Name im Snippet.\n• Innerhalb von Schlüssel: nur im Eintrag „Schlüssel = …“, also im Wert und, bei Blöcken, in allem darin. Beispiel: Schlüssel position gilt für „position = { 105 37 }“.")),
        new GuideBlock(GuideBlockKind.Paragraph, T("rulehelp.parts.action", "Aktion:\n• Ignorieren: Keine Zahl des Namens zählt.\n• Nur markierte zählt: Nur die Zahl bei [#] zählt, die anderen Zahlen des Namens nicht.\n• Alle zählen: Alle Zahlen des Namens zählen, auch Namenszahlen wie in ck3_… und einzelne Zahlen wie „cost = 10“.\n• Markierte zählt nicht: Nur die Zahl bei [#] zählt nicht. Über die anderen Zahlen des Namens entscheiden weiter die übrigen Regeln.")),

        new GuideBlock(GuideBlockKind.Heading, T("rulehelp.patterns.heading", "Die Mustersprache")),
        new GuideBlock(GuideBlockKind.Example, T("rulehelp.patterns.syntax", "*     beliebiger Text (auch nichts)\n#     eine Zahl (eine ganze Folge von Ziffern)\n[#]   die Zahl, um die es geht (höchstens einmal je Muster)\nText  genau dieser Text; Groß- und Kleinschreibung ist egal")),
        new GuideBlock(GuideBlockKind.Paragraph, T("rulehelp.patterns.important", "Wichtig:\n• Das Muster muss auf den ganzen Namen passen, nicht nur auf einen Teil. „button“ trifft nur den Namen „button“; „*button*“ trifft jeden Namen, in dem button vorkommt.\n• Anführungszeichen gehören nicht zum Muster. Für einen String wie \"Idee 5\" schreibst du Idee [#].\n• # steht immer für eine ganze Zahl. „iv#*“ passt auf iv2_ga_… und auf iv23_x (mit der Zahl 23).\n• Schreibst du mehrere Muster untereinander (eines pro Zeile), genügt es, wenn eines passt.\n• Die Groß-/Kleinschreibung kannst du im Dialog einschalten.")),

        new GuideBlock(GuideBlockKind.Heading, T("rulehelp.examples.heading", "Beispiele zum Nachbauen")),
        new GuideBlock(GuideBlockKind.Paragraph, T("rulehelp.examples.one", "Beispiel 1: Nur die Zahl hinter ARA soll zählen.")),
        new GuideBlock(GuideBlockKind.Example, T("rulehelp.examples.oneBox", "Name im Snippet:  iv2_ga_select_historic_ARA_1_1\nMuster:           iv#_ga_*_ARA_[#]_#\nWo:               Überall\nAktion:           Nur markierte zählt\n\nErgebnis: Bei drei Durchläufen entstehen\n  iv2_ga_select_historic_ARA_1_1\n  iv2_ga_select_historic_ARA_2_1\n  iv2_ga_select_historic_ARA_3_1")),
        new GuideBlock(GuideBlockKind.Paragraph, T("rulehelp.examples.oneNote", "Soll stattdessen die letzte Zahl (die Stufe) zählen, setze die Markierung dorthin: iv#_ga_*_ARA_#_[#]")),
        new GuideBlock(GuideBlockKind.Paragraph, T("rulehelp.examples.two", "Beispiel 2: Texte mit mehreren Zahlen.")),
        new GuideBlock(GuideBlockKind.Example, T("rulehelp.examples.twoBox", "String im Snippet:  \"Idee 5 von 10\"\nMuster:             Idee # von [#]\nAktion:             Nur markierte zählt\n\nErgebnis: Es zählt die 10, aus „Idee 5 von 10“ wird „Idee 5 von 11“.")),
        new GuideBlock(GuideBlockKind.Paragraph, T("rulehelp.examples.three", "Beispiel 3: Alle Namen mit „button“ ignorieren (so ist die mitgelieferte Regel „Button“ gebaut).")),
        new GuideBlock(GuideBlockKind.Example, T("rulehelp.examples.threeBox", "Muster:   *button*\nWo:       Überall\nAktion:   Ignorieren")),
        new GuideBlock(GuideBlockKind.Paragraph, T("rulehelp.examples.four", "Beispiel 4: Alles in position ignorieren (so ist die mitgelieferte Regel „position-Eintrag“ gebaut).")),
        new GuideBlock(GuideBlockKind.Example, T("rulehelp.examples.fourBox", "Muster:     *\nWo:         Innerhalb von Schlüssel\nSchlüssel:  position\nAktion:     Ignorieren")),
        new GuideBlock(GuideBlockKind.Paragraph, T("rulehelp.examples.five", "Beispiel 5: Die Zahl direkt hinter einem Präfix soll nie zählen, die übrigen Zahlen aber wie gewohnt (so ist die mitgelieferte Regel „Namenspräfix“ gebaut).")),
        new GuideBlock(GuideBlockKind.Example, T("rulehelp.examples.fiveBox", "Muster (zwei Zeilen):  iv[#]*\n                       iv_[#]*\nAktion:                Markierte zählt nicht\n\nErgebnis: In iv_2_tooltip_idea_1_1 zählt die 2 nicht; die 1 hinter idea zählt weiter\nnach der Einstellung „Mehrere Zahlen“ (erste zählt).")),
        new GuideBlock(GuideBlockKind.Paragraph, T("rulehelp.examples.six", "Beispiel 6: Dieselbe Regel für eine ganze Mod oder nur für eine Vorlage. Lege die Regel auf der Ebene „Mod“ an, wenn sie für alle Vorlagen dieser Mod gelten soll, auf der Ebene „Vorlage“, wenn nur diese Vorlage sie braucht. Eine Regel kannst du später mit „Verschieben“ auf eine andere Ebene bringen oder mit „Kopie erstellen“ auf einer zweiten Ebene zusätzlich anlegen.")),

        new GuideBlock(GuideBlockKind.Heading, T("rulehelp.order.heading", "Rangfolge und Ebenen")),
        new GuideBlock(GuideBlockKind.Paragraph, T("rulehelp.order.text", "• Die Regeln stehen im Fenster „Zählregeln“ in der Reihenfolge, in der sie geprüft werden: zuerst die Vorlage, dann Mod, Spiel, Alle Spiele und zuletzt die mitgelieferten. Innerhalb einer Ebene entscheidet die Reihenfolge der Liste; mit „Nach oben“ und „Nach unten“ änderst du sie.\n• Für jede Zahl entscheidet die erste Regel, die für sie ein Ergebnis hat. Trifft eine Regel den Namen, aber nicht diese Zahl (etwa „Markierte zählt nicht“ bei einer anderen Zahl), entscheidet die nächste.\n• Die Häkchen in der Zählpunkte-Liste und die Kästchen je Bezeichner-Form gehen jeder Regel vor. Beispiele im Snippet, die sich wirklich ändern (idea_1, idea_2, idea_3 …), gehen vor Regeln, die ausschalten.\n• Der Haken vor einer Regel schaltet sie ein oder aus. Bei einer Regel einer weniger spezifischen Ebene (auch bei den mitgelieferten) wirkt der Haken nur auf der oben gewählten Ebene: Auf „Vorlage“ ausgeschaltet gilt für diese Vorlage, auf „Mod“ für alle Vorlagen der Mod.\n• Mitgelieferte Regeln lassen sich nicht ändern. Mit „Kopie erstellen“ legst du eine eigene Fassung an; das Original wird auf der gewählten Ebene dabei ausgeschaltet.")),

        new GuideBlock(GuideBlockKind.Heading, T("rulehelp.troubleshoot.heading", "Meine Regel wirkt nicht")),
        new GuideBlock(GuideBlockKind.Paragraph, T("rulehelp.troubleshoot.text", "Gehe die Liste durch:\n• Die Spalte „Treffer“ steht auf 0: Das Muster passt nicht auf den ganzen Namen. Prüfe Anfang und Ende (fehlt ein * ?) und schau in die Vorschau des Dialogs, dort siehst du die Treffer sofort.\n• Groß-/Kleinschreibung: Ist sie im Dialog eingeschaltet, muss sie stimmen.\n• Die Aktion „Nur markierte zählt“ und „Markierte zählt nicht“ brauchen in jedem Muster ein [#].\n• Ist die Regel eingeschaltet (Haken) und liegt sie auf einer Ebene, die gilt? Die Ebene „Mod“ gibt es nur, wenn im Template-Fenster eine Mod gewählt ist. Regeln auf „Alle Spiele“ gelten nur für die eingetragenen Spiele.\n• Eine Regel weiter oben in der Liste entscheidet vielleicht zuerst über dieselbe Zahl.\n• Ein Haken oder Kästchen in der Zählpunkte-Liste geht der Regel vor.\n• Enthält das Snippet mehrere Beispiele mit hochgezählten Zahlen, lernt der Generator die Zählung daraus und lässt ausschaltende Regeln dort außer Acht.\n• Klicke im Snippet auf die Zahl: In der Statuszeile steht, welche Regel über sie entscheidet.")),
    };

    public RuleHelpWindow()
        : base(T("rulehelp.title", "Anleitung: Zählregeln"), Blocks, withNavigation: false)
    {
    }

    // Nur UiText.Get, kein Format: die Texte enthalten geschweifte Klammern (position = { 105 37 }).
    private static string T(string key, string german) => UiText.Get(key, german);
}
