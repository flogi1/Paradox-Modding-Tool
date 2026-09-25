using System.Windows;
using System.Windows.Controls;
using Border = System.Windows.Controls.Border;
using FontFamily = System.Windows.Media.FontFamily;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;

namespace ParadoxModTool.App;

/// <summary>
/// Anleitung zum Erstellen von Zählregeln mit Beispielen. Der Text steht als Liste von Überschriften, Absätzen und Beispielkästen in <see cref="Blocks"/>,
/// damit er sich leicht pflegen lässt.
/// </summary>
public sealed class RuleHelpWindow : Window
{
    private enum Kind
    {
        Title,
        Heading,
        Paragraph,
        Example,
    }

    private static readonly (Kind Kind, string Text)[] Blocks =
    {
        (Kind.Title, "Anleitung: Zählregeln"),

        (Kind.Heading, "Was ist eine Regel?"),
        (Kind.Paragraph, "Der Template-Generator entscheidet für jede Zahl in deinem Snippet, ob sie hochgezählt wird. Eine Zählregel sagt ihm bei bestimmten Namen genau, was er tun soll, zum Beispiel: „In Namen wie iv2_ga_select_historic_ARA_1_1 zählt nur die Zahl hinter ARA“. Die Regel legst du einmal an, sie gilt danach bei jedem Erzeugen."),
        (Kind.Paragraph, "Eine Regel besteht aus drei Angaben:\n1. Muster: Welche Namen sind gemeint?\n2. Wo: Überall im Snippet oder nur innerhalb eines bestimmten Eintrags?\n3. Aktion: Was passiert mit den Zahlen dieser Namen?"),
        (Kind.Paragraph, "Der schnellste Weg: Rechtsklick auf eine Zahl im Snippet und „Regel aus dieser Zahl erstellen“ (oder in der Zählpunkte-Liste eine Zeile wählen und „Regel erstellen“). Der Dialog ist dann schon ausgefüllt, du musst das Muster nur noch mit * allgemeiner machen, wenn die Regel für mehr Namen gelten soll."),

        (Kind.Heading, "Die Teile einer Regel"),
        (Kind.Paragraph, "Name: Nur für dich, damit du die Regel in der Liste wiederfindest. Er erscheint auch als Begründung, wenn du auf eine Zahl klickst („Regel „…“ (Ebene)“)."),
        (Kind.Paragraph, "Ebene: Wo die Regel gespeichert wird und für wen sie gilt.\n• Vorlage: nur für die Vorlage, die gerade offen ist.\n• Mod: für alle Vorlagen dieser Mod (die Mod wählst du im Template-Fenster im Feld „Mod:“).\n• Spiel: für alle Mods dieses Spiels.\n• Alle Spiele: für mehrere Spiele; dort wählst du, für welche."),
        (Kind.Paragraph, "Wo:\n• Überall: jeder Name im Snippet.\n• Innerhalb von Schlüssel: nur im Eintrag „Schlüssel = …“, also im Wert und, bei Blöcken, in allem darin. Beispiel: Schlüssel position gilt für „position = { 105 37 }“."),
        (Kind.Paragraph, "Aktion:\n• Ignorieren: Keine Zahl des Namens zählt.\n• Nur markierte zählt: Nur die Zahl bei [#] zählt, die anderen Zahlen des Namens nicht.\n• Alle zählen: Alle Zahlen des Namens zählen, auch Namenszahlen wie in ck3_… und einzelne Zahlen wie „cost = 10“.\n• Markierte zählt nicht: Nur die Zahl bei [#] zählt nicht. Über die anderen Zahlen des Namens entscheiden weiter die übrigen Regeln."),

        (Kind.Heading, "Die Mustersprache"),
        (Kind.Example, "*     beliebiger Text (auch nichts)\n#     eine Zahl (eine ganze Folge von Ziffern)\n[#]   die Zahl, um die es geht (höchstens einmal je Muster)\nText  genau dieser Text; Groß- und Kleinschreibung ist egal"),
        (Kind.Paragraph, "Wichtig:\n• Das Muster muss auf den ganzen Namen passen, nicht nur auf einen Teil. „button“ trifft nur den Namen „button“; „*button*“ trifft jeden Namen, in dem button vorkommt.\n• Anführungszeichen gehören nicht zum Muster. Für einen String wie \"Idee 5\" schreibst du Idee [#].\n• # steht immer für eine ganze Zahl. „iv#*“ passt auf iv2_ga_… und auf iv23_x (mit der Zahl 23).\n• Schreibst du mehrere Muster untereinander (eines pro Zeile), genügt es, wenn eines passt.\n• Die Groß-/Kleinschreibung kannst du im Dialog einschalten."),

        (Kind.Heading, "Beispiele zum Nachbauen"),
        (Kind.Paragraph, "Beispiel 1: Nur die Zahl hinter ARA soll zählen."),
        (Kind.Example, "Name im Snippet:  iv2_ga_select_historic_ARA_1_1\nMuster:           iv#_ga_*_ARA_[#]_#\nWo:               Überall\nAktion:           Nur markierte zählt\n\nErgebnis: Bei drei Durchläufen entstehen\n  iv2_ga_select_historic_ARA_1_1\n  iv2_ga_select_historic_ARA_2_1\n  iv2_ga_select_historic_ARA_3_1"),
        (Kind.Paragraph, "Soll stattdessen die letzte Zahl (die Stufe) zählen, setze die Markierung dorthin: iv#_ga_*_ARA_#_[#]"),
        (Kind.Paragraph, "Beispiel 2: Texte mit mehreren Zahlen."),
        (Kind.Example, "String im Snippet:  \"Idee 5 von 10\"\nMuster:             Idee # von [#]\nAktion:             Nur markierte zählt\n\nErgebnis: Es zählt die 10, aus „Idee 5 von 10“ wird „Idee 5 von 11“."),
        (Kind.Paragraph, "Beispiel 3: Alle Namen mit „button“ ignorieren (so ist die mitgelieferte Regel „Button“ gebaut)."),
        (Kind.Example, "Muster:   *button*\nWo:       Überall\nAktion:   Ignorieren"),
        (Kind.Paragraph, "Beispiel 4: Alles in position ignorieren (so ist die mitgelieferte Regel „position-Eintrag“ gebaut)."),
        (Kind.Example, "Muster:     *\nWo:         Innerhalb von Schlüssel\nSchlüssel:  position\nAktion:     Ignorieren"),
        (Kind.Paragraph, "Beispiel 5: Die Zahl direkt hinter einem Präfix soll nie zählen, die übrigen Zahlen aber wie gewohnt (so ist die mitgelieferte Regel „Namenspräfix“ gebaut)."),
        (Kind.Example, "Muster (zwei Zeilen):  iv[#]*\n                       iv_[#]*\nAktion:                Markierte zählt nicht\n\nErgebnis: In iv_2_tooltip_idea_1_1 zählt die 2 nicht; die 1 hinter idea zählt weiter\nnach der Einstellung „Mehrere Zahlen“ (erste zählt)."),
        (Kind.Paragraph, "Beispiel 6: Dieselbe Regel für eine ganze Mod oder nur für eine Vorlage. Lege die Regel auf der Ebene „Mod“ an, wenn sie für alle Vorlagen dieser Mod gelten soll, auf der Ebene „Vorlage“, wenn nur diese Vorlage sie braucht. Eine Regel kannst du später mit „Verschieben“ auf eine andere Ebene bringen oder mit „Kopie erstellen“ auf einer zweiten Ebene zusätzlich anlegen."),

        (Kind.Heading, "Rangfolge und Ebenen"),
        (Kind.Paragraph, "• Die Regeln stehen im Fenster „Zählregeln“ in der Reihenfolge, in der sie geprüft werden: zuerst die Vorlage, dann Mod, Spiel, Alle Spiele und zuletzt die mitgelieferten. Innerhalb einer Ebene entscheidet die Reihenfolge der Liste; mit „Nach oben“ und „Nach unten“ änderst du sie.\n• Für jede Zahl entscheidet die erste Regel, die für sie ein Ergebnis hat. Trifft eine Regel den Namen, aber nicht diese Zahl (etwa „Markierte zählt nicht“ bei einer anderen Zahl), entscheidet die nächste.\n• Die Häkchen in der Zählpunkte-Liste und die Kästchen je Bezeichner-Form gehen jeder Regel vor. Beispiele im Snippet, die sich wirklich ändern (idea_1, idea_2, idea_3 …), gehen vor Regeln, die ausschalten.\n• Der Haken vor einer Regel schaltet sie ein oder aus. Bei einer Regel einer weniger spezifischen Ebene (auch bei den mitgelieferten) wirkt der Haken nur auf der oben gewählten Ebene: Auf „Vorlage“ ausgeschaltet gilt für diese Vorlage, auf „Mod“ für alle Vorlagen der Mod.\n• Mitgelieferte Regeln lassen sich nicht ändern. Mit „Kopie erstellen“ legst du eine eigene Fassung an; das Original wird auf der gewählten Ebene dabei ausgeschaltet."),

        (Kind.Heading, "Meine Regel wirkt nicht"),
        (Kind.Paragraph, "Gehe die Liste durch:\n• Die Spalte „Treffer im Snippet“ steht auf 0: Das Muster passt nicht auf den ganzen Namen. Prüfe Anfang und Ende (fehlt ein * ?) und schau in die Vorschau des Dialogs, dort siehst du die Treffer sofort.\n• Groß-/Kleinschreibung: Ist sie im Dialog eingeschaltet, muss sie stimmen.\n• Die Aktion „Nur markierte zählt“ und „Markierte zählt nicht“ brauchen in jedem Muster ein [#].\n• Ist die Regel eingeschaltet (Haken) und liegt sie auf einer Ebene, die gilt? Die Ebene „Mod“ gibt es nur, wenn im Template-Fenster eine Mod gewählt ist. Regeln auf „Alle Spiele“ gelten nur für die eingetragenen Spiele.\n• Eine Regel weiter oben in der Liste entscheidet vielleicht zuerst über dieselbe Zahl.\n• Ein Haken oder Kästchen in der Zählpunkte-Liste geht der Regel vor.\n• Enthält das Snippet mehrere Beispiele mit hochgezählten Zahlen, lernt der Generator die Zählung daraus und lässt ausschaltende Regeln dort außer Acht.\n• Klicke im Snippet auf die Zahl: In der Statuszeile steht, welche Regel über sie entscheidet."),
    };

    public RuleHelpWindow()
    {
        Title = "Anleitung: Zählregeln";
        Width = 820;
        Height = 760;
        MinWidth = 520;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var panel = new StackPanel { Margin = new Thickness(18, 14, 18, 18) };
        foreach (var (kind, text) in Blocks)
        {
            panel.Children.Add(Build(kind, text));
        }

        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panel };
    }

    private static UIElement Build(Kind kind, string text)
    {
        switch (kind)
        {
            case Kind.Title:
                return new TextBlock { Text = text, FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) };
            case Kind.Heading:
                return new TextBlock { Text = text, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 6) };
            case Kind.Example:
                var example = new TextBlock { Text = text, FontFamily = new FontFamily("Consolas"), FontSize = 13, Margin = new Thickness(10, 8, 10, 8) };
                example.SetResourceReference(TextBlock.ForegroundProperty, "Theme.EditorForeground");
                var box = new Border { BorderThickness = new Thickness(1), Margin = new Thickness(0, 2, 0, 10), Child = example };
                box.SetResourceReference(Border.BackgroundProperty, "Theme.EditorBackground");
                box.SetResourceReference(Border.BorderBrushProperty, "Theme.ControlBorder");
                return box;
            default:
                return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8), LineHeight = 20 };
        }
    }
}
