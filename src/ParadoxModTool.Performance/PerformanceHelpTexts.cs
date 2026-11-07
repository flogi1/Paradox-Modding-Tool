using ParadoxModTool.Localization;

namespace ParadoxModTool.Performance;

/// <summary>
/// Texte der Anleitung zum Performance-Checker (Fenster <c>PerformanceHelpWindow</c>). Die Schlüssel stehen in <c>locales/de.json</c> und
/// <c>locales/en.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen (Test <c>PerformanceTextsTests</c>).
/// Jeder Aufruf von <c>T</c> steht in einer Zeile. Die Texte enthalten geschweifte Klammern nicht, werden aber nie formatiert.
/// </summary>
public static class PerformanceHelpTexts
{
    /// <summary>Sprungziele der Kapitel (<c>GuideBlock.Key</c>); F1 springt je nach Reiter zu einem davon.</summary>
    public const string SectionWhat = "performance.help.what";
    public const string SectionFlow = "performance.help.flow";
    public const string SectionOrigin = "performance.help.origin";
    public const string SectionHints = "performance.help.hints";
    public const string SectionCompare = "performance.help.compare";
    public const string SectionGames = "performance.help.games";
    public const string SectionAi = "performance.help.ai";

    public static string Title => T("performance.help.title", "Anleitung: Performance-Checker");

    // Kapitel 1: Was misst der Profiler
    public static string WhatHeading => T("performance.help.what.heading", "Was misst der Profiler");
    public static string WhatColumns => T("performance.help.what.columns", "Der Script-Profiler des Spiels zählt für jede Stelle im Skript (Datei:Zeile), wie oft sie lief und wie lange. Im Dump stehen je Zeile drei Werte: Calls (Aufrufe), Self (Eigenzeit, ohne das Skript, das die Stelle selbst aufruft) und Total (Gesamtzeit samt aufgerufenem Skript).");
    public static string WhatUnit => T("performance.help.what.unit", "Die Zeiten sind vermutlich Sekunden. Der Dump nennt keine Einheit, PMT rechnet nichts um und zeigt die Rohwerte.");
    public static string WhatCumulative => T("performance.help.what.cumulative", "Der Profiler zählt kumulativ ab dem letzten Script.Profiling.Restart, auch über einen Playset-Wechsel hinweg. Darum beginnt jede Messung mit Restart, und darum sind zwei Dumps nur vergleichbar, wenn beide gleich lange nach einem Restart entstanden.");
    public static string WhatSelfTotal => T("performance.help.what.selfTotal", "Self zeigt, wo die Zeit wirklich verbraucht wird. Total zeigt, welcher Einstieg teuer ist. Nur Self lässt sich addieren: Total mehrerer Zeilen zählt dieselbe Zeit mehrfach, weil jede Zeile die Zeit ihrer Unterzeilen enthält.");

    // Kapitel 2: Ablauf einer Messung
    public static string FlowHeading => T("performance.help.flow.heading", "Ablauf einer Messung");
    public static string FlowIntro => T("performance.help.flow.intro", "Im Reiter Messung wählst du Playset, Mess-Save und Soll-Dauer und startest das Spiel. PMT startet es mit -debug_mode und wartet auf den Dump. Im Spiel gehst du so vor:");
    public static string FlowSteps => T("performance.help.flow.steps", "1. Im Hauptmenü warten, bis das Spiel fertig geladen ist, dann das Mess-Save laden (von Hand oder, siehe unten, durch PMT).\n2. Die Konsole mit ^ öffnen und Script.Profiling.Restart eingeben.\n3. 12 Monate spielen, am besten mit höchster Geschwindigkeit. Die Dauer zählt PMT später aus dem Dump nach.\n4. In der Konsole Script.Profiling.Stop eingeben: Erst danach enthält der Dump Daten.\n5. In der Konsole Script.Profiling.DumpAll eingeben. Das Spiel schreibt logs/profiling_roots.csv. Jeden Befehl kannst du in der Checkliste mit „Kopieren“ in die Zwischenablage legen.");
    public static string FlowPickup => T("performance.help.flow.pickup", "PMT beobachtet den logs-Ordner. Ändert sich die Datei 2 Sekunden lang nicht mehr, gilt der Dump als fertig: PMT kopiert ihn in einen neuen Lauf und zeigt die Auswertung des neuen Laufs. Das Original bleibt in logs/ liegen. Ein zweites DumpAll ohne neue Messung legt keinen zweiten Lauf an.");
    public static string FlowAutosave => T("performance.help.flow.autosave", "Save-Laden: Das Spiel lädt beim Start mit -continuelastsave den letzten Stand aus continue_game.json. Weil es im Intervall und beim Beenden Autosaves schreibt, zeigt diese Datei nach einem Lauf fast immer auf ein Autosave. Darum trägt PMT vor jedem Start den gewählten Spielstand dort ein (die alte Datei bleibt als continue_game.json.pmt-backup daneben) und das Spiel lädt dein Mess-Save von selbst. Fehlt die Datei oder lässt sie sich nicht schreiben, lädst du das Save von Hand; die Checkliste sagt es dir.");
    public static string FlowImport => T("performance.help.flow.import", "Dumps aus früheren Läufen (profiling*.csv) holst du mit „Dump manuell wählen“ in die Liste der Läufe.");

    // Kapitel 3: Herkunft und Rauschen
    public static string OriginHeading => T("performance.help.origin.heading", "Herkunft und Rauschen");
    public static string OriginKinds => T("performance.help.origin.kinds", "Jede Zeile bekommt eine Herkunft nach der Datei, in der sie steht: Mod (Datei nur in der aktiven Mod), Mod und Vanilla (die Mod überschreibt eine Vanilla-Datei gleichen Pfads), Vanilla (nur im Spiel) und Andere Mod (Datei einer anderen Mod, die PMT für dieses Spiel kennt). Das Ranking zeigt standardmäßig Mod und Mod und Vanilla; der Filter zeigt alle.");
    public static string OriginJump => T("performance.help.origin.jump", "Doppelklick oder Enter auf eine Zeile öffnet die Datei an dieser Zeile im Editor. Zeilen aus Vanilla und fremden Mods lassen sich nicht öffnen.");
    public static string OriginNoise => T("performance.help.origin.noise", "Rauschen: Self schwankt schon zwischen zwei Läufen mit derselben Mod um 7 bis 10 Prozent. Eine Änderung heißt darum erst besser oder schlechter, wenn sie über der relativen Schwelle (Vorgabe 10 %) und über dem absoluten Boden (Vorgabe 0,05 s) liegt. Alles darunter heißt unverändert. Die Bewertung je Zeile und je Datei prüft die Gesamtzeit (Total), die Summenzeile Mod-Self die Eigenzeit der Mod-Seite. Beide Schwellen änderst du im Reiter Vergleich, PMT merkt sie sich.");
    public static string OriginCalls => T("performance.help.origin.calls", "Verhalten geändert: Ändern sich die Calls einer Zeile um mehr als 25 Prozent, ist nicht mehr dieselbe Arbeit gelaufen. Die Zeile bekommt diese Bewertung statt besser oder schlechter. So fällt auf, wenn etwa KIs passiv wurden, weil Daily-Aktionen nur noch monatlich liefen, und das Spiel dadurch schneller aussieht. Bei ungleicher Dauer ändern sich die Calls vieler Zeilen schon dadurch, dann überwiegt „Verhalten geändert“; jede Bewertung trägt dann den Zusatz „(Dauer ungleich oder unbekannt)“.");

    // Kapitel 4: Hinweise
    public static string HintsHeading => T("performance.help.hints.heading", "Hinweise");
    public static string HintsIntro => T("performance.help.hints.intro", "Rechts neben der Rangliste stehen die Hinweise zum gewählten Lauf. Sie kommen aus festen Regeln: Jede Regel prüft die teuersten Zeilen der Mod-Seite und, wenn vorhanden, das error.log des Laufs. Jeder Hinweis nennt die Stelle (Datei:Zeile), eine Maßnahme und den Grund mit Zahlen; der Tooltip einer Zeile zeigt die ganze Erklärung. Warnungen (⚠) stehen vor einfachen Hinweisen (ℹ). Doppelklick oder Enter öffnet die Stelle im Editor, „Bericht kopieren“ nimmt die Hinweise mit in den Bericht.");
    public static string HintsErrorSpam => T("performance.help.hints.errorSpam", "Fehler-Spam an dieser Stelle (⚠): An einer Stelle der Mod stehen mindestens 100 Einträge im error.log der Messung. Jeder Eintrag kostet beim Schreiben Zeit, viele Einträge an einer Stelle bremsen das Spiel spürbar. Behebe den Fehler, den die häufigste Meldung nennt.");
    public static string HintsUnguarded => T("performance.help.hints.unguarded", "Variablenzugriff ohne Guard (⚠): Ein Script Value oder Trigger liest eine Variable, ohne vorher im selben Block zu prüfen, ob es sie gibt. Fehlt sie, schreibt jeder Aufruf einen Fehler ins error.log. Prüfe vorher mit has_variable = NAME oder exists = var:NAME, oder lies sie mit ?=. Ein Guard innerhalb von NOT oder NOR zählt nicht.");
    public static string HintsFrequentTick => T("performance.help.hints.frequentTick", "Schwerer Block auf häufigem Tick (⚠): Ein Block mit mindestens 0,5 s Gesamtzeit läuft öfter als alle 30 Tage. Bei einer Generic Action zählt der wirksame Abstand: ai_tick (daily = 1 Tag, monthly = 30 Tage) mal ai_tick_frequency (ohne Angabe 1). ai_tick = daily mit ai_tick_frequency = 180 läuft also alle 180 Tage; so sind die Aktionen von IV2 gebaut, sie sind in Ordnung und bekommen keinen Hinweis. Abhilfe: ai_tick_frequency erhöhen oder den Trigger enger fassen. Ein seltenerer Tick kann die KI passiver machen, engere Trigger sind darum oft die bessere Wahl. Ist ai_tick_frequency keine feste Zahl (etwa ein Script Value), ist der Abstand unbekannt; dann gibt es nur einen einfachen Hinweis (ℹ).");
    public static string HintsVanillaCopy => T("performance.help.hints.vanillaCopy", "Mod-Kopie einer Vanilla-Datei (ℹ): Ein Hotspot liegt in einer Datei, die die Mod aus Vanilla übernimmt und überschreibt. Oft steckt die Zeit im übernommenen Vanilla-Teil und nicht im eigenen. Prüfe mit dem Compare und Update Tool, welcher Teil sich von Vanilla unterscheidet. Je Datei gibt es einen Hinweis, an der teuersten Zeile.");
    public static string HintsManyCalls => T("performance.help.hints.manyCalls", "Script Value mit sehr vielen Aufrufen (ℹ): Ein Script Value läuft hochgerechnet mehr als 100.000-mal pro Spieljahr. Ändert sich sein Ergebnis selten, berechne es einmal (etwa monatlich) und speichere es als globale Variable; die Aufrufer lesen dann nur noch die Variable.");
    public static string HintsErrorLog => T("performance.help.hints.errorLog", "Die Fehler-Regeln brauchen einen gemessenen Lauf: Übernimmt PMT den Dump einer Messung, kopiert es das error.log der Spielsitzung mit in den Lauf (das Original bleibt in logs/). Ein manuell gewählter Dump hat kein error.log. Dann steht unter der Liste „Kein error.log zu diesem Lauf“, und es laufen nur die Regeln, die den Dump auswerten (Variablenzugriff ohne Guard, Schwerer Block, Mod-Kopie, viele Aufrufe). Läufe aus älteren PMT-Versionen haben ebenfalls kein error.log.");

    public static string HintsErrorLogWindow => T("performance.help.hints.errorLogWindow", "Dieselben Fehler-Regeln (Fehler-Spam und Variablenzugriff ohne Guard) laufen auch im Fehlerprotokoll, im Bereich „Fehler-Hotspots“ über dem Fehlerbaum: dort nur aus dem error.log des gewählten Laufs, für jedes Spiel mit Fehlerkatalog, auch ohne Messung.");

    // Kapitel 5: Vergleich je Zeile und je Datei
    public static string CompareHeading => T("performance.help.compare.heading", "Vergleich je Zeile und je Datei");
    public static string CompareChecks => T("performance.help.compare.checks", "Im Reiter Vergleich wählst du Lauf A (vorher) und Lauf B (nachher). Die Prüfzeile meldet, wenn Save, Playset oder Dauer der Läufe ungleich sind, und hakt ab, was gleich ist. Was PMT nicht prüfen kann (etwa Playset und Mod-Stand eines manuell gewählten Dumps), steht als „nicht geprüft“ da. Bei ungleicher oder unbekannter Dauer steht „(Dauer ungleich oder unbekannt)“ hinter der Bewertung; solche Zahlen sind nicht direkt vergleichbar.");
    public static string CompareRows => T("performance.help.compare.rows", "Je Zeile: Bearbeitest du einen Block, wechselt er oft seine Zeilennummer. Dieselbe Stelle erscheint dann als „fehlt in B“ und als „neu in B“. Schalte in diesem Fall auf „je Datei“.");
    public static string CompareFiles => T("performance.help.compare.files", "Je Datei: Die Ansicht summiert alle Zeilen eines Dateipfads je Lauf und vergleicht die Summen. Verschobene Zeilen stören dort nicht. Die Gesamtzeit je Datei ist inklusiv und nicht additiv: Eine Zeile kann Zeit enthalten, die auch eine andere Zeile derselben Datei zählt. Lies sie als Hinweis, welche Datei sich verändert hat, nicht als genaue Summe.");
    public static string CompareNewRun => T("performance.help.compare.newRun", "„Neue Messung mit B als Basis“ springt in den Reiter Messung und übernimmt Save und Dauer von Lauf B. So misst du die nächste Änderung unter denselben Bedingungen.");

    // Kapitel 6: KI-Analyse
    public static string AiHeading => T("performance.help.ai.heading", "KI-Analyse");
    public static string AiWhat => T("performance.help.ai.what", "Die Schaltfläche „Mit KI analysieren“ im Reiter Auswertung macht aus dem gewählten Lauf einen Auftrag an eine KI: Sie erklärt, warum die teuersten Stellen deiner Mod Zeit kosten, und schlägt Änderungen am Skript vor. PMT ruft keine KI selbst auf. Du kopierst den Auftrag in eine Chat-KI oder gibst einer KI mit Dateizugriff den erzeugten Ordner und holst die Antwort zurück.");
    public static string AiContent => T("performance.help.ai.content", "Im Auftrag stehen die Anleitung für die KI mit einem Beispiel, der Messlauf (Name, Mod, Monate, Spielstand, Playset, Stand der Moddateien, Rauschschwellen), die Zeiten je Herkunft, die Hotspots der Mod-Seite, die Hinweise, die betroffenen Dateien und die Codeblöcke um die Hotspots. Dazu kommen die Dateien und Codeblöcke, die du im Fenster anhakst, und deine Beschreibung. Es sind nur Zeilen aus Mod und Spiel dabei, die PMT wählt oder du ankreuzt, nichts sonst; Zugangsdaten und Dateien außerhalb der Mod und des Spiels gehören nicht dazu. Sieh dir den Text vor dem Kopieren an.");
    public static string AiAnswer => T("performance.help.ai.answer", "Die Antwort der KI fügst du im Fenster ein. PMT speichert den Text und zeigt ihn an. Ist ein Dateiblock fehlerhaft, nennt PMT das Problem, und du kannst es mit „Fehler für die KI kopieren“ zurückgeben.");
    public static string AiStored => T("performance.help.ai.stored", "Die Beratung wird zum Lauf gespeichert; „KI-Rat anzeigen“ im Reiter Auswertung zeigt sie wieder, auch nach einem Neustart von PMT. Vergleiche nach einer Änderung mit einer neuen Messung unter denselben Bedingungen (gleiches Save, gleiche Monate, gleiches Playset): Erst der Vergleich zeigt, ob der Vorschlag etwas gebracht hat.");
    public static string AiRecipe => T("performance.help.ai.recipe", "Rezept: Ist eine Änderung mechanisch und wiederholbar, kann die KI zusätzlich ein Patch-Rezept liefern. Das erlaubst du im Fenster; dann hängt PMT das Rezeptformat an den Auftrag. Ein Rezept aus der Antwort übergibt PMT an die Rezepte, wo du es prüfst und anwendest wie jedes andere. Ohne diese Option liefert die KI nur Text.");

    // Kapitel 7: Welche Spiele
    public static string GamesHeading => T("performance.help.games.heading", "Welche Spiele");
    public static string GamesList => T("performance.help.games.list", "• Europa Universalis V: unterstützt (geprüft mit Spielversion 1.4.1).\n• Crusader Kings III: gesperrt. Das Spiel hat keinen Dump-Befehl, der Profiler zeigt seine Daten nur in einem Fenster (script.profiling.gui).\n• Victoria 3: gesperrt. In Spielversion 1.13.11 schreibt der Dump nur eine leere Kopfzeile oder stürzt das Spiel ab. Nach einem Patch ist das neu zu prüfen.\n• Hearts of Iron IV und Europa Universalis IV: Die Spiele haben keinen Script-Profiler.\n• Stellaris: hat einen eigenen Profiler mit anderem Format und kommt in einer späteren Runde.");

    // Nur UiText.Get, kein Format: die Texte werden nie formatiert.
    private static string T(string key, string german) => UiText.Get(key, german);
}
