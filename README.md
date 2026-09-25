# Paradox Modding Tool — Code-Grundgerüst (V1, EU5-Fokus)

Dieses Verzeichnis enthält das erste Code-Grundgerüst gemäß der
„Architektur-Spezifikation V1" (liegt als Word-Dokument neben diesem Ordner).
Es wurde in einer Cloud-Umgebung ohne Zugriff auf das .NET SDK / NuGet
geschrieben — die Dateien sind also **noch nicht auf diesem Rechner
kompiliert oder getestet worden**. Bitte als Erstes lokal bauen (siehe unten)
und mir Fehlermeldungen zurückmelden, falls `dotnet build` etwas beanstandet.

**Ab Runde 5 geändert: Auslieferung nur noch als ZIP.** Auf Nutzerwunsch
(„Ich möchte ein wenig Nutzungszeit sparen") werden neue Versionen ab jetzt
ausschließlich als ZIP-Datei zur Verfügung gestellt, statt sie direkt in den
Projektordner zu übertragen. Bitte den Inhalt der ZIP-Datei manuell in
diesen Ordner kopieren (bestehende Dateien überschreiben) und danach wie
gewohnt neu bauen. Der bisherige Prüfschritt „Prüfsumme gegen die Zieldatei
gegenchecken" entfällt entsprechend ebenfalls.

## Voraussetzung

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (prüfen mit `dotnet --version`)
- Empfohlen: Visual Studio 2022 (Workload „.NET Desktop Development") — nicht zwingend, `dotnet` CLI reicht auch.

## Einmalig: Solution-Datei erzeugen

Die `.sln`-Datei wird bewusst nicht als Textdatei mitgeliefert, sondern per Skript
über die `dotnet`-CLI erzeugt — das vermeidet Tippfehler in der (fummeligen)
`.sln`-Syntax, die ich hier nicht gegenprüfen konnte:

```powershell
.\setup-solution.ps1
```

Danach existiert `ParadoxModTool.sln` im Projektordner.

## Bauen & Ausführen

```powershell
dotnet build
dotnet test tests/ParadoxModTool.ErrorLog.Tests
dotnet test tests/ParadoxModTool.VersionManager.Tests
dotnet test tests/ParadoxModTool.VanillaDiff.Tests
dotnet test tests/ParadoxModTool.ModCompat.Tests
dotnet test tests/ParadoxModTool.UndoRedo.Tests
dotnet test tests/ParadoxModTool.Session.Tests
dotnet test tests/ParadoxModTool.SearchReplace.Tests
dotnet test tests/ParadoxModTool.Notes.Tests
dotnet test tests/ParadoxModTool.Tools.Tests
dotnet test tests/ParadoxModTool.LaunchControl.Tests
dotnet test tests/ParadoxModTool.Explorer.Tests
dotnet run --project src/ParadoxModTool.App
```

Da die Solution-Datei bereits existiert, reicht für neu hinzugekommene
Testprojekte entweder ein erneutes `.\setup-solution.ps1` (fügt nur hinzu, was
fehlt) oder direkt:

```powershell
dotnet sln add tests/ParadoxModTool.UndoRedo.Tests/ParadoxModTool.UndoRedo.Tests.csproj
```

### Bequemer: Batch-Dateien statt Konsolenbefehle

Damit du nicht jedes Mal Befehle abtippen musst, liegen im Projektordner vier
Doppelklick-Dateien:

- **`build-test-run.bat`** (Nutzer-Feedback: „nur eine Datei drücken … und dann
  nacheinander alles durchläuft") — führt `build.bat`, `test.bat` und `run.bat`
  bewusst NACHEINANDER in einem einzigen Konsolenfenster aus: erst
  `dotnet clean` + `dotnet build`, dann alle zehn Testprojekte, zum Schluss
  `dotnet run`. Bricht bei JEDEM Fehlschlag (Build-Fehler, ein fehlgeschlagenes
  Testprojekt, ein Programmabsturz) sofort ab und lässt das Fenster mit
  `pause` offen, damit die Fehlermeldung kopiert werden kann; läuft dagegen
  alles fehlerfrei durch (inkl. normalem Beenden des Programms), schließt sich
  das Konsolenfenster automatisch, ohne Tastendruck. Das ist die empfohlene
  Datei für den normalen Arbeitsablauf.
- **`build.bat`** — erzeugt bei Bedarf zuerst automatisch `ParadoxModTool.sln`
  (ruft dafür `setup-solution.ps1` auf, falls die Datei noch fehlt), stellt
  danach über `dotnet sln add` sicher, dass auch nachträglich hinzugekommene
  Projekte (z. B. `ParadoxModTool.Notes` seit Runde 4) in einer BEREITS
  VORHANDENEN Projektmappe eingebunden sind (`dotnet sln add` ist idempotent —
  ein bereits eingebundenes Projekt wird nur gemeldet, nicht doppelt
  hinzugefügt), räumt dann auf (`dotnet clean`, entfernt alte Zwischendateien
  in `bin`/`obj`, damit ein geänderter Code-/XAML-Stand garantiert neu
  kompiliert wird) und baut anschließend (`dotnet build`).
- **`test.bat`** — führt nacheinander alle zehn Testprojekte aus und zeigt am
  Ende, ob alle grün waren.
- **`run.bat`** — startet das Programm (`dotnet run`).

`build.bat`/`test.bat`/`run.bat` bleiben einzeln nutzbar (z. B. um nur die
Tests erneut laufen zu lassen, ohne das Programm gleich zu starten). Alle vier
lassen sich per Doppelklick im Explorer ausführen (öffnen ein Konsolenfenster)
oder aus einer bereits offenen Konsole heraus mit z. B. `.\build-test-run.bat`.

Der Prototyp öffnet ein Fenster mit einer globalen Werkzeugleiste
(Split-View-Umschalter / Rückgängig / Wiederholen) und einem Editor-Arbeitsbereich.
Über den Button „Split-View" lässt sich das Fenster in zwei unabhängige
Arbeitsbereiche nebeneinander teilen (Abschnitt 5) — per `GridSplitter` in der
Breite verstellbar. Jeder Arbeitsbereich hat seine eigene Mini-Werkzeugleiste
(Neu / Öffnen… / Speichern / „⇄") und seine eigene Tableiste: mehrere Dokumente
können gleichzeitig in einem Arbeitsbereich offen sein, das Umschalten zwischen
Tabs wechselt den Inhalt des Editor-Steuerelements. Über „Öffnen…" lässt sich
eine `.txt`-Datei in den jeweiligen Arbeitsbereich laden; zum Ausprobieren liegt
unter `samples/sample_eu5_script.txt` eine selbst geschriebene Beispieldatei mit
typischer Paradox-Skript-Syntax (Kommentare, Strings, Datumswerte, verschachtelte
Blöcke) bereit — **keine echten Inhalte aus deinem Mod**, nur zur Demonstration
des Syntax-Highlightings. Der „⇄"-Button verschiebt das gerade aktive Dokument
des einen Arbeitsbereichs in den jeweils anderen (Ersatz für Drag&Drop in dieser
ersten Fassung — die Spezifikation erlaubt ausdrücklich „Drag&Drop ODER
Tastenkombination"). „Speichern" schreibt die Datei und legt die Änderung auf
den gemeinsamen, geteilten Undo-Stack (`ParadoxModTool.UndoRedo`) — „Rückgängig"
stellt den vorherigen Dateiinhalt wieder her (bzw. löscht die Datei, falls sie
durch „Speichern" neu angelegt wurde), „Wiederholen" schreibt sie erneut. Dieser
Undo-Stack ist bewusst global für beide Arbeitsbereiche gemeinsam — unabhängig
davon, in welchem Arbeitsbereich gespeichert wurde, landet die Aktion im selben
Verlauf.

Außerdem merkt sich das Programm beim Beenden automatisch die zuletzt offenen
Dateien (mit Cursorposition je Dokument), ob Split-View an- oder ausgeschaltet
war, und stellt das alles beim nächsten Start automatisch wieder her — analog
zu Notepad++ (Abschnitt 12). Die Session wird unter
`%APPDATA%\ParadoxModTool\session.json` gespeichert, bewusst außerhalb des
Programmordners. Nur bereits gespeicherte Dateien werden gemerkt (ein noch
nicht gespeichertes „Unbenannt"-Dokument geht beim Neustart verloren); seit
dem letzten Beenden gelöschte oder verschobene Dateien werden beim
Wiederherstellen einfach übersprungen, ohne die übrigen Dokumente zu verhindern.

Über den Werkzeugleisten-Button „Suchen…" oder die Tastenkombination **Strg+F**
öffnet sich das vereinheitlichte Suchen-&-Ersetzen-Fenster (Abschnitt 5b). Oben lässt sich
zwischen „Aktuelles Dokument" und „Ordner (Dateien)" umschalten. Die Such-/Ersetzen-Textfelder
erlauben mehrzeilige Eingabe, damit sich auch ganze Codeblöcke auf einmal ersetzen lassen
(Vorbild: die „NPP Toolbucket"-Erweiterung von Notepad++). Im Ordner-Modus lässt sich per
„Durchsuchen…" ein Ordner wählen (z. B. der lokale Mod-Ordner) und über „★ Speichern"/„Entfernen"
als Favorit merken — Favoriten werden unter `%APPDATA%\ParadoxModTool\search-favorites.json`
gespeichert und bleiben über Neustarts hinweg erhalten. „Alle finden" listet alle Treffer auf
(Doppelklick springt zur Fundstelle bzw. öffnet die jeweilige Datei); „Alle ersetzen" ersetzt
sie. Im Ordner-Modus landet eine komplette Ersetzung über beliebig viele Dateien als EIN Eintrag
im programmweiten Aktionsverlauf (über „Rückgängig" in einem Schritt zurücknehmbar) und ein
dabei betroffenes, bereits geöffnetes Dokument wird automatisch neu geladen, damit der Tab nicht
den alten Stand zeigt; im Dokument-Modus läuft „Ersetzen" dagegen über AvalonEdits eigenen
Text-Undo (Strg+Z im Textbereich), da die Änderung bis zum expliziten „Speichern" nur im
Editor-Puffer liegt. Durchsucht werden standardmäßig die Dateiendungen `.txt`, `.gui`, `.gfx`,
`.yml`/`.yaml`.

**Nachbesserung (Nutzer-Feedback nach dem ersten Test):** Ein mehrzeiliger Suchbegriff aus den
Textfeldern enthält technisch bedingt immer `\r\n` als Zeilenumbruch (WPF-TextBox-Verhalten),
Paradox-Moddateien selbst kommen aber mit `\n` ODER `\r\n` vor. Ohne Angleichung wurde ein
kompletter Codeblock daher nie gefunden, sobald die Datei eine andere Konvention als die Eingabe
verwendete — einzelne Zeilen waren davon nicht betroffen, da dort kein Zeilenumbruchzeichen im
Suchbegriff vorkommt. `TextSearch` gleicht Suchbegriff und Ersetzungstext jetzt automatisch an
die im Zieltext überwiegend vorkommende Konvention an (`NormalizeLineEndingsToMatch`), inkl. drei
neuer Tests, die genau dieses Szenario abdecken.

Weitere Nachbesserungen aus demselben Feedback: Wird ein Dokument per „⇄" in den zweiten
Arbeitsbereich verschoben, schaltet sich Split-View automatisch mit ein, falls noch aus. Jeder
Tab hat jetzt einen „✕"-Schließen-Button, zusätzlich gibt es einen globalen „Schließen"-Button in
der Werkzeugleiste für das aktive Dokument des fokussierten Arbeitsbereichs. Unter der Tableiste
wird durchgehend der volle Pfad des aktiven Dokuments angezeigt (bzw. ein Platzhalter für ein noch
nicht gespeichertes Dokument). Strg+G bzw. der Werkzeugleisten-Button „Gehe zu Zeile…" öffnen
einen kleinen Dialog, um direkt zu einer Zeilennummer im aktiven Dokument zu springen.

**Nachbesserung, Runde 2 (mehrzeilige Suche funktionierte trotz Zeilenumbruch-Angleichung immer
noch nicht):** Anhand einer echten, hochgeladenen Moddatei wurde der tatsächliche Grund gefunden —
nicht die Zeilenumbrüche (die Datei war durchgehend `\r\n`, wie erwartet), sondern die
Tabulator-Einrückung selbst. Eine WPF-`TextBox` fügt standardmäßig BEI DER TASTATUREINGABE kein
Tab-Zeichen ein, sondern bewegt stattdessen den Fokus zum nächsten Element (`AcceptsTab`, Standard
`False`) — beim manuellen Abtippen eines eingerückten Codeblocks in die Such-/Ersetzen-Felder
gingen dadurch sämtliche Einrückungen verloren, ohne dass das sichtbar aufgefallen wäre. Beide
Textfelder in `SearchReplaceWindow.xaml` haben jetzt `AcceptsTab="True"`.

Außerdem aus demselben Feedback: „Rückgängig"/„Wiederholen" laden jetzt GENERELL jeden davon
betroffenen, bereits offenen Tab automatisch neu (nicht nur bei Ersetzen-in-Dateien) — vorher
zeigte ein offener Tab nach einem Undo einer Dateiersetzung weiterhin den bereits zurückgenommenen
neuen Stand, was laut Nutzer-Feedback „zu Verwirrung" führt. Dafür geben `ActionHistory.Undo()`
und `.Redo()` jetzt die verarbeitete Aktion zurück, `MainWindow` sammelt daraus rekursiv (auch aus
verschachtelten `CompositeUndoableAction`-Schritten) alle betroffenen Dateipfade und ruft
`EditorPane.ReloadIfOpen` dafür auf.

Die Tableiste jedes Arbeitsbereichs unterstützt jetzt Mausrad-Scrollen (vertikales Scrollen wird
auf die horizontale Tableiste umgeleitet) und zeigt links an jedem Tab einen farbigen Balken für
die Dateikategorie (Text/GUI/YML/Defines/Sonstige, anhand der Dateiendung). Über vier Checkboxen
in der Mini-Werkzeugleiste jedes Arbeitsbereichs lassen sich einzelne Kategorien ein-/ausblenden;
das aktive Dokument bleibt dabei immer sichtbar, egal welche Kategorien gerade ausgeblendet sind.
Frei konfigurierbare Farben (siehe Konzept, Abschnitt 1) sind seit Runde 4 umgesetzt (siehe unten,
„Einstellungen" → „Kategorie-Farben…"). Eine zusätzliche Einfärbung nach Datei-Herkunft (Vanilla/
lokaler Mod/Steam-Workshop-Mod) ist weiterhin bewusst noch NICHT umgesetzt — das setzt die noch
fehlende Spielprofil-/Mod-Ordner-Konfiguration (Abschnitt 1d) voraus und ist für einen späteren
Ausbauschritt vorgesehen.

Die Suchergebnisse lassen sich jetzt zusätzlich zum modalen Suchen-Fenster auch in einer
einklappbaren Ergebnisleiste direkt über dem Arbeitsbereich anzeigen (wie bei Notepad) — beide
Anzeigen teilen sich dieselbe Trefferliste, ein neuer Treffer erscheint also automatisch in
beiden. Die Ergebnisleiste blendet sich automatisch ein, sobald ein Treffer hinzukommt, und lässt
sich über ein „✕" wieder schließen, ohne die Trefferdaten zu löschen (die bleiben im modalen
Fenster erhalten). In beiden Ansichten kann jede Trefferzeile über die Checkbox „Nur gefundenen
Code anzeigen" auf den reinen gefundenen Code reduziert werden (die beschreibende Zeile
„Datei — Zeile N: …" wird dann ausgeblendet) — der gefundene Code steht dabei in einer
schreibgeschützten, aber markier-/kopierbaren Textbox (ein normaler `TextBlock` unterstützt in WPF
keine Textmarkierung). Der Toggle ist zwischen beiden Ansichten synchronisiert.

**Runde 2 vom Nutzer bestätigt:** mehrzeilige Suche funktioniert jetzt, alle übrigen Runde-2-Punkte
funktionieren wie vorgesehen, `build-test-run.bat` läuft sehr gut. Daraus zwei neue Punkte:

Über der Werkzeugleiste gibt es jetzt ein **„Einstellungen"-Menü** — aktuell mit einem Punkt
„Suchergebnisse oben anzeigen" (Checkbox), der festlegt, ob die Ergebnisleiste über oder unter dem
Arbeitsbereich erscheint. Standard ist jetzt **unten** (vorher: oben) — das war explizit gewünscht.
Die Einstellung wird wie die übrige Session automatisch gespeichert und beim nächsten Start
wiederhergestellt. Dieses Menü ist bewusst als Ort für künftige Einstellungen angelegt, nicht nur
für diesen einen Punkt.

**Formatierung ignorieren beim Suchen & Ersetzen:** Manchmal kommt derselbe Codeblock in
unterschiedlichen Dateien (oder an unterschiedlichen Stellen) mit abweichender Einrückung,
Zeilenumbrüchen oder Leerzeichen vor — bisher musste man dann für jede Formatierungsvariante
einzeln suchen. Die neue Checkbox „Formatierung ignorieren" im Suchen-Fenster löst das: `TextSearch`
reduziert Suchbegriff UND Zieltext dafür auf eine Fassung ganz ohne Whitespace-Zeichen (die
Paradox-Skriptsyntax kennt keinen semantisch bedeutsamen Whitespace, er dient nur der Lesbarkeit)
und sucht darin; jeder gefundene Treffer wird über eine mitgeführte Positionszuordnung wieder exakt
auf seine tatsächliche, unverändert formatierte Textstelle im Original zurückgeführt. Eine
Ersetzung tauscht dadurch pro Fundstelle GENAU den dort tatsächlich vorgefundenen (ggf.
unterschiedlich formatierten) Textbereich gegen den Ersetzungstext aus — die Formatierung des
umgebenden Codes bleibt unangetastet, es wird nichts rückwirkend vereinheitlicht. Funktioniert
identisch im Dokument- wie im Ordner-Modus, da beide über dieselbe `TextSearch`-Kernlogik laufen;
inkl. 5 neuer Tests.

**Runde 3 vom Nutzer bestätigt, zwei Nachbesserungen:** Das Ersetzen unter „Formatierung
ignorieren" funktioniert, aber der eingefügte Ersetzungstext war danach „einen Tab weiter
eingerückt als er sein sollte". Ursache: Derselbe Suchbegriff kann (das ist ja gerade der Zweck
der Funktion) an Stellen mit UNTERSCHIEDLICHER tatsächlicher Einrückungstiefe gefunden werden —
der eingegebene Ersetzungstext wurde bisher aber überall mit GENAU der einen, absolut eingetippten
Einrückung eingefügt, die nur zu einer der Fundstellen wirklich passte. `TextSearch.ReplaceAll`
gleicht bei aktivem „Formatierung ignorieren" jetzt für JEDE Fundstelle einzeln die relative
Einrückungsstruktur des eingegebenen Ersetzungstextes an die tatsächliche Einrückung genau dieser
Stelle an: die erste Zeile braucht dafür keine eigene Einrückung (die im Originaltext bereits
vorhandene, unangetastete Einrückung reicht), jede weitere Zeile bekommt die Differenz ihrer
eigenen Einrückung gegenüber der ersten Zeile des Ersetzungstextes auf die tatsächliche Tiefe der
jeweiligen Fundstelle aufgeschlagen. Inkl. 3 neuer Tests.

Außerdem: Nach „Suchen & Ersetzen in Dateien" wurde eine bereits geöffnete, betroffene Datei nicht
automatisch aktualisiert, obwohl dieser Mechanismus schon vorhanden war. Ursache: Der Abgleich „ist
diese Datei gerade offen?" verglich Dateipfade rein als Zeichenkette — ein aus dem
Windows-Explorer-Adressfeld eingefügter Ordnerpfad endet aber häufig auf einen Backslash, wodurch
die Ordner-Dateiaufzählung Pfade mit doppeltem Backslash lieferte, die textuell nicht mehr zum
(bereits kanonischen) Pfad der über „Öffnen…" geladenen Datei passten, obwohl es dieselbe Datei
war. Behoben, indem der eingegebene Ordnerpfad vor der Dateiaufzählung normalisiert wird
(`Path.GetFullPath`) und der Datei-Abgleich in `EditorPane` (`PathsEqual`) zusätzlich zum reinen
Zeichenkettenvergleich beide Pfade normalisiert vergleicht, bevor er eine Datei als „nicht offen"
einstuft.

**Runde 4 (neue Funktionen, noch nicht vom Nutzer getestet):** Drei neue Funktionen auf einmal:

1) **Dokumente manuell als „Wichtig" oder „Hilfsdatei" markieren.** Zwei neue `ToggleButton`s in
der Mini-Werkzeugleiste jedes Arbeitsbereichs markieren das jeweils aktive Dokument als „Wichtig"
oder „Hilfsdatei" (schließen sich gegenseitig aus) — eine manuelle Markierung überschreibt dabei
die anhand der Dateiendung automatisch ermittelte Kategorie, lässt sich aber jederzeit wieder
zurücknehmen (dann gilt wieder die automatische Kategorie). Beide neuen Kategorien erscheinen als
zwei weitere Checkboxen im Kategorie-Filter, genau wie Text/GUI/YML/Defines/Sonstige. „Hilfsdatei"
ist rein organisatorisch gedacht (Nutzer-Feedback: „keine Dateien, die zum Mod gehören, aber da
kann der Modder sich Notizen oder Listen machen") — das Programm behandelt eine Hilfsdatei
technisch wie jedes andere Dokument.

2) **Kategorie-Farben sind jetzt frei konfigurierbar** (das war schon im ursprünglichen Konzept
vorgesehen, aber bisher zurückgestellt) — über „Einstellungen" → „Kategorie-Farben…" lässt sich für
jede der sieben Kategorien (einschließlich der beiden neuen) ein eigener Hex-Farbcode eintragen,
mit „Zurücksetzen" auf die Standardfarben. Zusätzlich gibt es unter „Einstellungen" jetzt die
Checkbox „Farbcodes färben den ganzen Reiter ein" (Nutzer-Feedback Runde 4) — standardmäßig aus
(wie bisher nur der schmale Balken links am Tab), bei Aktivierung wird der komplette Tab-Reiter
halbtransparent in der Kategorie-Farbe eingefärbt (bewusst NICHT deckend, damit der Tab-Text lesbar
bleibt, solange es noch kein eigenes Hell-/Dunkel-Farbschema gibt). Beide Einstellungen wirken
sofort auf bereits offene Tabs und werden wie die übrige Session automatisch gespeichert.

3) **Notizfunktion je Datei.** Der neue Werkzeugleisten-Button „Notizen…" öffnet ein kleines
Fenster mit einer Notiz zur aktiven Datei — die Notiz wird bewusst NICHT in der Moddatei selbst
gespeichert (Nutzer-Feedback: „nicht in der Datei selbst gespeichert, sondern nur für den Modder
sichtbar"), sondern in einer separaten JSON-Datei unter `%APPDATA%\ParadoxModTool\notes.json`,
genau wie die Session oder die Suchordner-Favoriten. Für ein neues, noch nicht gespeichertes
Dokument fragt das Programm zuerst nach dem Speichern, da eine Notiz an einen tatsächlichen
Dateipfad gebunden ist.

**Runde 5 (zwei Fehlerbehebungen aus Runde-4-Rückmeldung + vier neue Funktionen):**

Rückmeldung aus Runde 4: „Ganzen Reiter einfärben" und „Notizen bleiben nach Neustart erhalten"
funktionieren wie gewünscht — keine Änderung nötig. Zwei Punkte waren aber tatsächlich fehlerhaft:

1) **Bugfix: Ausblenden von „Wichtig"/„Hilfsdatei" funktionierte nicht.** Ursache war NICHT die
Absicht, dass das gerade aktive Dokument immer sichtbar bleibt (das ist weiterhin so gewollt, siehe
Runde-2-Kommentar in `FilterByCategory`) — der eigentliche Fehler war, dass beim Wechsel des aktiven
Tabs bisher gar kein Neu-Filtern der Tabliste ausgelöst wurde. Der zuvor aktive Tab blieb dadurch
fälschlich sichtbar, selbst wenn seine Kategorie gerade ausgeblendet war, bis irgendein unabhängiges
Ereignis (z. B. Umschalten eines Filter-Kästchens) zufällig einen Refresh auslöste. Behoben, indem
`Tabs_SelectionChanged` jetzt nach jedem Tabwechsel selbst `CollectionView.Refresh()` aufruft.

2) **Bugfix: manuelle „Wichtig"/„Hilfsdatei"-Markierung ging beim Neustart verloren.**
`DocumentSessionState` hatte schlicht noch kein Feld dafür. Neu: `DocumentSessionState.ManualCategory`
(Enum-Name als String, `null` = keine manuelle Zuweisung) wird beim Beenden gespeichert und beim
Wiederherstellen der Session über `OpenDocument.SetManualCategory` wieder gesetzt.

3) **Rechtsklick-Kontextmenü am Tab:** „Ordner öffnen, wo die Datei liegt" öffnet den
Windows-Explorer und markiert die Datei darin (`explorer.exe /select,"..."`).

4) **Dateipfad kopieren/öffnen.** Die Pfadanzeige unter der Tableiste ist jetzt eine
schreibgeschützte, randlose Textbox statt eines einfachen Textfelds — der Pfad lässt sich dadurch
auch direkt per Textmarkierung/Strg+C kopieren, wie gewünscht. Zusätzlich zwei kleine Buttons
daneben: „📋" kopiert den Pfad der aktiven Datei in die Zwischenablage, „📂" öffnet denselben Ordner
wie das neue Kontextmenü (Punkt 3), nur für das aktive statt eines rechtsangeklickten Dokuments.

5) **Farbauswahl-Dialog + Live-Vorschau bei den Kategorie-Farben.** Im Dialog „Kategorie-Farben…"
hat jedes der sieben Hex-Textfelder jetzt eine kleine Vorschaufläche daneben, die sich bei jeder
Eingabe sofort aktualisiert, sowie einen „🎨"-Button, der den klassischen Windows-Farbwähler öffnet
(`System.Windows.Forms.ColorDialog`, bereits über `UseWindowsForms` verfügbar) und die Auswahl als
Hex-Code zurück ins Textfeld schreibt.

6) **Notiz-Icon im Dateienreiter + „Notizen anzeigen"-Button.** Ein kleines „🗒"-Icon erscheint
jetzt automatisch in einem Tab, sobald es zu dieser Datei eine gespeicherte Notiz gibt (Klick öffnet
sie direkt); zusätzlich gibt es neben der Pfadanzeige (siehe Punkt 4) einen „🗒 Notizen anzeigen"-
Button für das aktive Dokument. Beide Wege öffnen dasselbe Notizfenster wie der bisherige
Werkzeugleisten-Button „Notizen…" (jetzt zentral in `MainWindow.ShowNoteFor` gebündelt) und
aktualisieren danach das Icon an jedem offenen Tab derselben Datei in beiden Arbeitsbereichen.

**Runde 6 (Rückmeldung zu Runde 5):** Fünf der sechs Runde-5-Punkte funktionieren wie gewünscht
(Kontextmenü, Pfad kopieren/öffnen-Buttons, Farbwähler, Persistierung der manuellen Kategorie).
Kleine Textänderung auf Nutzerwunsch: Der Kontextmenü-Eintrag am Tab heißt jetzt kürzer „Ordner
öffnen" statt „Ordner öffnen, wo die Datei liegt". Der gemeldete Punkt „Hilfsdatei lässt sich nach
Farbänderung nicht mehr ausblenden" stellte sich nach Rückfrage als KEIN Bug heraus: die betroffene
Datei war zum Testzeitpunkt noch der aktive Tab, und ein aktiver Tab bleibt seit Runde 2 absichtlich
immer sichtbar (siehe `FilterByCategory`-Kommentar), damit der Nutzer ihn nicht versehentlich
verliert — die Farbänderung selbst hat mit der Kategorie/dem Filter nichts zu tun (bei genauer
Codedurchsicht bestätigt: `FileCategoryClassifier`/`RefreshAllDocumentColors` berühren ausschließlich
`CategoryColor`/`TabBackground`, nie `Category` selbst). Kein Code musste dafür geändert werden.

**Runde 7 (Konzept Abschnitt 10, „Leerzeichen-Bereinigung & Formatierung"):** Neuer
Werkzeugleisten-Button „Formatieren…" öffnet einen Dialog mit zwei unabhängig wählbaren Optionen für
das aktive Dokument: (1) überschüssige Leerzeichen/Tabs am Zeilenende entfernen und Läufe von zwei
oder mehr Leerzeilen auf genau eine reduzieren — einzelne Leerzeilen als optische Trennung zwischen
Codeabschnitten bleiben dabei ausdrücklich erhalten, mehrfache Leerzeichen INNERHALB einer Zeile (z. B.
zur Ausrichtung von „=" untereinander) werden nicht angetastet; (2) die Struktur der Datei komplett neu
aufbauen (einheitliche Einrückung), nur für den Paradox-Skriptdialekt (.txt/.gui/.gfx/.vml) — für
andere Dateitypen (z. B. .yml) ist diese Option im Dialog von vornherein deaktiviert, und schlägt sie
dennoch fehl (fehlerhaftes Skript), wird NICHTS am Dokument verändert, sondern nur eine Fehlermeldung
mit Zeilennummer angezeigt (alles-oder-nichts, keine Teiländerungen). Beide Werkzeuge sind neu im
Projekt `ParadoxModTool.Tools` (`WhitespaceCleaner`, `ScriptPrettyPrinter`), Letzteres baut bewusst auf
dem bereits für den Vanilla-Merge (Abschnitt 7) gehärteten AST-Roundtrip von `ParadoxModTool.VanillaDiff`
auf, statt einen neuen Formatierer zu schreiben. Die Änderung landet wie bei „Ersetzen im aktuellen
Dokument" im AvalonEdit-eigenen Text-Undo (Strg+Z), noch nicht im programmweiten Aktionsverlauf.
**Vom Nutzer bestätigt: Beide Formatierungsoptionen funktionieren wie gewünscht.** Der erste Build
schlug beim Nutzer mit 22 Fehlern ausschließlich in `ParadoxModTool.Editor` fehl (durchweg „enthält
bereits eine Definition für ..." für praktisch jedes benannte Steuerelement in `EditorPane.xaml`) —
keiner der Fehler betraf eine in dieser Runde geänderte Datei. Ursache war eine veraltete, mit der
aktuellen `EditorPane.xaml` nicht mehr übereinstimmende generierte Datei im `obj`-Ordner des Nutzers,
die `dotnet clean` nicht mit entfernt hatte (nach vielen Runden ZIP-Inhalt-über-bestehenden-Ordner-
Kopieren, ohne die Build-Zwischenordner selbst zu löschen) — kein Fehler im ausgelieferten Code.
Behoben durch Entpacken in einen neuen, leeren Ordner statt Überschreiben des bestehenden.

Bei der Vorbereitung dieses Punktes wurde außerdem — NICHT vom Nutzer gemeldet, sondern bei der
gründlichen Prüfung entdeckt, ob sich der bestehende Skript-Parser gefahrlos für ein Werkzeug
wiederverwenden lässt, das direkt in die Originaldatei zurückschreibt — ein echter Datenverlust-Bug in
`ScriptParser` behoben: ein Kommentar ganz am Ende eines Blocks bzw. einer Datei, dem kein weiterer
Eintrag mehr folgt, wurde beim Parsen bisher stillschweigend verworfen. Für den bisherigen Verwendungszweck
(Vanilla-Merge) war das unkritisch, da dort ohnehin schon dokumentiert keine 1:1-Formatierungstreue
zugesichert wird — für ein Werkzeug, das die einzige Kopie einer Moddatei überschreibt, wäre das aber
inakzeptabel gewesen. Behoben über einen neuen `ScriptCommentEntry`-Eintragstyp; als Regressionstest
in `ParadoxModTool.Tools.Tests` abgesichert (siehe unten).

**Runde 8 (Konzept Abschnitt 8, „Spiel starten"):** Neue Werkzeugleisten-Elemente rechts neben
„Formatieren…": eine Dropdown-Liste mit den Startoptionen des aktiven Spielprofils sowie der Button
„▶ Spiel starten". Die drei Optionen — „Normal", „Debug-Modus (Konsole, Dev-Tools, Hotload)" und
„Nach Absturz beim Ressourcen-Laden trotzdem mit Mods starten" — kamen bereits aus einer früheren
Runde direkt aus der EU5-Wiki-Recherche in `profiles/eu5.json` (`launchOptions`) und mussten in
dieser Runde nicht neu ermittelt werden; neu ist ausschließlich der eigentliche Startmechanismus.
Der Pfad zur Spiel-.exe lässt sich über „Einstellungen" → „Spielpfad einstellen…" festlegen und wird
wie die übrige Session automatisch gespeichert/wiederhergestellt (`AppSessionState.GameExecutablePath`,
rein additiv — eine alte Session-Datei ohne dieses Feld deserialisiert automatisch zu `null`); ist
beim Klick auf „Spiel starten" noch keiner eingestellt (oder die zuvor eingestellte Datei existiert
nicht mehr), fragt das Programm automatisch danach, statt den Start einfach fehlschlagen zu lassen.
Die eigentliche Startlogik liegt im neuen, bewusst von `ParadoxModTool.GameProfiles` UNABHÄNGIGEN
Projekt `ParadoxModTool.LaunchControl` (`GameLauncher.Launch`, nimmt Pfad und Argumente als einfache
Zeichenketten entgegen — das Zuordnen der gewählten Startoption zum Spielprofil übernimmt `App`) —
derselbe Grund für die Entkopplung wie bei `ParadoxModTool.UndoRedo`/`.Session`. `ProcessStartInfo.UseShellExecute`
ist bewusst `true` (wie ein Doppelklick im Explorer), um Probleme mit Arbeitsverzeichnis-/DLL-Auflösung
zu vermeiden, die bei direktem Prozessstart ohne Shell gelegentlich auftreten — passend, da Paradox-Spiele
i. d. R. über den Steam-Client verifiziert werden. Ein Fehlschlag (kein Pfad eingestellt, Datei nicht
gefunden, Start durch das Betriebssystem selbst abgelehnt) liefert ein `LaunchResult` mit verständlicher
Fehlermeldung statt einer Ausnahme (dasselbe Muster wie `ScriptFormatResult` aus Runde 7) und wird als
`MessageBox` angezeigt. Da ein echter Spielstart in dieser Umgebung nicht automatisiert testbar ist,
nimmt `GameLauncher.Launch` optional einen injizierbaren „Starter"-Delegaten entgegen (Standard:
`Process.Start`) — die Tests in `ParadoxModTool.LaunchControl.Tests` ersetzen ihn durch ein Fake und
prüfen so sowohl die Fehlerfälle als auch die tatsächlich an `ProcessStartInfo` übergebenen Werte
(Pfad, Argumente, Arbeitsverzeichnis), ohne je einen echten Prozess zu starten. **Von dir noch zu
testen:** Spielpfad einstellen, alle drei Startoptionen ausprobieren (insbesondere den Debug-Modus
und, falls du absichtlich einen Absturz beim Ressourcen-Laden provozieren kannst, die dritte Option).

**Runde 9 (Rückmeldung zu Runde 8 + drei kleine Punkte + Playset-Auswahl):** Die Ladeprofile aus
Runde 8 funktionieren laut Rückmeldung. Vier Nachbesserungen/Ergänzungen:

1) **Kombinierte Startoption Debug + Crash-Recovery.** `profiles/eu5.json` hat einen vierten
`launchOptions`-Eintrag „Debug-Modus + trotz Absturz mit Mods starten" (`-debug_mode
--ignore-disable-mods-on-crash`) — reine Datenänderung, kein Code betroffen, community-editierbar
wie die übrigen drei Optionen.

2) **„Öffnen…"-Dialog zeigte standardmäßig nur `.txt`-Dateien.** Der Dateifilter war auf
„Paradox-Skriptdateien (*.txt)" voreingestellt, wodurch `.gui`/`.gfx`/`.vml`/`.yml`/`.lua`-Dateien
im Dialog unsichtbar blieben, obwohl sie zum Mod gehören können. Jetzt ist „Alle Dateien" der
voreingestellte (erste) Filter, sodass von Anfang an nichts ausgeblendet wird; „Alle unterstützten
Dateitypen" (alle sechs von `FileCategoryClassifier` erkannten Endungen) sowie je ein Filter pro
Kategorie stehen als bequeme Einschränkung weiterhin zur Auswahl.

3) Tooltip von „Spielpfad einstellen…" auf Wunsch geändert zu: „Wählt die EU 5.exe aus (Europa
Universalis 5/binaries)."

4) **Playset direkt beim Starten auswählen.** Neue Dropdown-Liste `PlaysetsCombo` in der
Werkzeugleiste (zwischen den Startoptionen und „▶ Spiel starten"), befüllt aus `playsets.json`
(liegt NICHT im Mod-Ordner, sondern unter `Documents/Paradox Interactive/Europa Universalis
V/playsets.json` — wird beim Programmstart automatisch dort gesucht, alternativ über
„Einstellungen" → „Playset-Datei einstellen…" festlegbar, wie beim Spielpfad per Session
gespeichert). Zeigt je Playset Name, Mod-Anzahl (aktiviert/gesamt) und eine „aktiv"-Markierung.
Klickst du „▶ Spiel starten" mit einem anderen als dem aktuell aktiven Playset ausgewählt, setzt
das neue `ParadoxModTool.LaunchControl.PlaysetStore` dieses Playset zuerst als aktiv, bevor das
Spiel startet — schlägt das fehl (Datei fehlt/nicht schreibbar/Playset nicht gefunden), wird der
Start abgebrochen, damit nie unbemerkt mit dem falschen Playset gestartet wird. **Wichtige
Einschränkung, bitte beim Testen besonders prüfen:** Ich konnte über die EU5-Wiki NICHT bestätigen,
dass das Setzen von `"isActive": true` in `playsets.json` tatsächlich das beim direkten Start von
`eu5.exe` (ohne Paradox-Launcher) geladene Playset bestimmt — das ist eine plausible, aber
unbestätigte Annahme anhand der Struktur deiner bereitgestellten Datei (genau ein Eintrag trug
`isActive: true`, alle anderen ließen das Feld ganz weg). Da `playsets.json` eine echte, vom Spiel
selbst verwaltete Konfigurationsdatei außerhalb des Mod-Projekts ist (nicht nur unser eigenes
Projekt betroffen, falls hier etwas schiefgeht), arbeitet `PlaysetStore` bewusst NICHT mit einem
festen C#-Datenmodell, sondern mit `System.Text.Json.Nodes.JsonNode` als veränderbarem DOM, damit
unbekannte Felder (DLC-Liste, Mod-Ladereihenfolge, ...) beim Zurückschreiben unangetastet bleiben,
UND legt vor jedem Schreibzugriff automatisch eine Sicherheitskopie unter `playsets.json.pmt-backup`
an. Kosmetische Nebenwirkung: Die Originaldatei ist Tab-eingerückt, nach dem Schreiben ist es
2-Leerzeichen-Einrückung (für JSON bedeutungslos, aber sichtbar in einem Diff). Inkl. 9 neuer Tests
in `ParadoxModTool.LaunchControl.Tests` (Parsing, Aktiv-Umschalten, Erhalt unbekannter Felder,
Sicherheitskopie, Fehlerfälle).

**Zurückgestellt auf deinen ausdrücklichen Wunsch für die nächste Runde:** die
Verzeichnis-Übersicht (Ordnerstruktur der Spieldateien + Mod-Ordner, zusätzliche Ordner
hinzufügbar, neue Dateien direkt dort anlegen, Öffnen-Buttons für linken/rechten Arbeitsbereich,
farbliche Markierung von Ordnern/Dateien, eigene Favoritenliste) — dieser Punkt wurde in dieser
Runde bewusst NICHT begonnen.

**Runde 10 (Verzeichnis-Übersicht, wie in Runde 9 zurückgestellt):** komplett neues Projekt
`ParadoxModTool.Explorer` (siehe eigene Tabellenzeile unten) plus Verdrahtung in `MainWindow`/
`AppSessionState`. Die Übersicht ist eine neue, standardmäßig eingeblendete linke Spalte
(Umschalt-Button „Verzeichnis-Übersicht" ganz links in der Werkzeugleiste) mit:

1) **Baumansicht** für beliebig viele Wurzeln (Ordner-Icon „Ordner hinzufügen…" in der eigenen
Mini-Werkzeugleiste über dem Baum) — lädt Unterordner beim Aufklappen NACH BEDARF nach (wichtig
bei zehntausenden Spieldateien, siehe „Performance-Dimensionierung" der Architektur-Spezifikation),
nicht alles auf einmal beim Hinzufügen einer Wurzel. Beim allerersten Start (keine gespeicherten
Wurzeln) wird automatisch der Mod-Ordner aus `profiles/eu5.json` (`defaultModRootTemplate`) als
erste Wurzel vorgeschlagen, falls er existiert.

2) **Zwei Buttons „◀"/„▶" an jeder Datei** öffnen sie gezielt im linken bzw. rechten Arbeitsbereich
(Split-View wird beim Ziel „rechts" automatisch mit eingeschaltet, falls noch aus — dasselbe
Verhalten wie beim Verschieben eines Dokuments zwischen den Arbeitsbereichen).

3) **„Neue Datei anlegen…"** im Rechtsklick-Menü eines Ordners öffnet einen kleinen Dialog
(Dateiname + Endung aus den im aktiven Spielprofil hinterlegten Dateitypen) und legt die Datei
über den programmweiten Aktionsverlauf an (Konzept Abschnitt 5: „alle Prozesse ... rückgängig
machen") — Strg+Z entfernt eine gerade angelegte Datei also wieder. Die neue Datei öffnet sich
danach sofort zur Bearbeitung im aktiven Arbeitsbereich.

4) **Farbliche Markierung** frei wählbar über das Rechtsklick-Menü (`System.Windows.Forms.ColorDialog`,
dauerhaft gespeichert unter `%APPDATA%\ParadoxModTool\explorer-colors.json`) für JEDEN Ordner/JEDE
Datei einzeln. Ordner OHNE explizit gesetzte Farbe erhalten automatisch die Standardfarbe ihrer
Kategorie aus dem aktiven Spielprofil (`GameProfile.FolderCategories`, z. B. „common" → blau) —
das löst den bisher unter „Noch offene Punkte" vermerkten Punkt zur automatischen
Ordner-Einfärbung nach Herkunft.

5) **Eigene Favoritenliste** (aufklappbarer Bereich über dem Baum) zum Anpinnen/Entfernen einzelner
Ordner/Dateien über das Rechtsklick-Menü, dauerhaft gespeichert unter
`%APPDATA%\ParadoxModTool\explorer-favorites.json`; ◀/▶-Buttons auch dort, plus „＋" (neue Datei
im gepinnten Ordner) und „✕" (Favorit entfernen).

Zusätzliche Wurzeln sowie alle Farben/Favoriten überstehen einen Neustart (`AppSessionState.ExplorerRoots`,
neu, rein additiv). **Wichtiger Hinweis:** Dieser komplette Ausbauschritt ist umfangreicher neuer
WPF-Code (neues Projekt, `TreeView` mit verschachteltem Lazy-Loading, Kontextmenüs,
Farb-/Favoriten-Dialoge) und war in dieser Umgebung ausschließlich per Python-Strukturprüfung
(Klammernbalance, XML-Wohlgeformtheit) kontrollierbar, NICHT durch einen echten Compiler — bitte
beim ersten Test besonders gründlich gegenprüfen: Auf-/Zuklappen auf mehreren Ebenen, die beiden
Öffnen-Buttons, „Neue Datei anlegen…" inkl. Strg+Z, Farbzuweisung/-Löschung samt Neustart-Test,
Favoriten anpinnen/entfernen samt Neustart-Test, zusätzliche Ordner hinzufügen/entfernen samt
Neustart-Test, der automatisch vorgeschlagene Mod-Ordner beim allerersten Start, und der
Ein-/Ausblenden-Button der Übersicht selbst.

**Runde 10, zweite Rückmeldung (Bugfixes an der Verzeichnis-Übersicht + weitere neue
Funktionen):** nach den beiden bereits ausgelieferten Bugfix-Zwischenständen (Punkt 18/19 unten)
kam eine größere Sammelrückmeldung mit weiteren Bugs UND neuen Feature-Wünschen. Alle Punkte:

*Bugfixes an bereits ausgeliefertem Runde-10-Code:*

1) **Ordnerfärbung war komplett wirkungslos** (weder Standard- noch selbst gewählte Farben
sichtbar) — der eigentliche Bug: die Farbbalken-Spalte in `ExplorerPanel.xaml` war exakt so breit
wie ihr eigener rechter Rand (`ColumnDefinition Width="4"` bei `Border`-`Margin="0,0,4,0"`),
wodurch für den `Border` selbst IMMER eine tatsächliche Breite von 0 blieb — ein reines
Layout-Problem, Binding und `HexColorConverter` waren die ganze Zeit korrekt. Behoben (Spalte 8,
Rand 3 → 5 sichtbare Pixel).
2) **◀/▶-Buttons zu groß, zu wenig Abstand vom Namen, Ordnerebenen zu eng:** kleiner (`FontSize
10`), deutlicher vom Namen abgerückt (`Margin` 8/3 statt 2), Zeilenabstand erhöht (`Grid.Margin`
0,3 statt 0,1).
3) **„Neue Datei anlegen…"-Fenster zu klein** (Dateityp-Auswahl abgeschnitten): fixe Höhe (180)
durch `SizeToContent="Height"` ersetzt (skaliert immer zum tatsächlichen Inhalt, unabhängig davon,
wie viele Zeilen der Zielordner-Pfad umbricht) plus mehr Breite (420 statt 340).
4) **Datei löschen aktualisierte die Übersicht nicht:** es gab bisher GAR KEINE echte Löschfunktion
in der Verzeichnis-Übersicht selbst (nur „Ordner öffnen (Explorer)", von wo aus man bisher von Hand
im Windows-Explorer löschen musste — dort konnte das Programm die Löschung naturgemäß nicht
mitbekommen). Jetzt ein echter „Löschen…"-Kontextmenüpunkt (Bestätigungsabfrage, Papierkorb statt
endgültigem Löschen über `Microsoft.VisualBasic.FileIO.FileSystem`, offene Tabs der gelöschten
Datei werden automatisch geschlossen) MIT sofortiger Aktualisierung des betroffenen Ordnerknotens.
Löschungen, die weiterhin von AUSSERHALB des Programms erfolgen (Windows-Explorer), aktualisieren
die Übersicht nach wie vor nicht automatisch (kein Dateisystem-Watcher in diesem Ausbauschritt) —
in diesem Fall hilft „Ordner aktualisieren" (🔄) bzw. erneutes Aufklappen.
5) **Standard-Dateiendung je Zielordner:** `NewFileWindow` bekommt jetzt eine Vorauswahl
(`GuessPreferredExtension` in `ExplorerPanel`) — `gui`-Ordner → `.gui`, `localization`-Ordner →
`.yml`, alle anderen weiterhin `.txt`.
6) **Favoriten ließen sich nicht auf-/zuklappen:** aus der bisherigen flachen `ListBox` (mit
eigenem ＋/✕-Template) wurde ein zweiter `TreeView`, der DASSELBE `HierarchicalDataTemplate` wie
der Hauptbaum wiederverwendet — ein favorisierter Ordner verhält sich dadurch jetzt identisch zu
einem normalen Ordner (inkl. Farbe, Kontextmenü, Lazy-Loading), ohne dass `TreeViewItem_Expanded`/
`LoadChildren` geändert werden mussten (beide arbeiten bereits rein über `FullPath`).
7) **Alt+F-Hotkey** zum Anpinnen des gerade ausgewählten Eintrags (Haupt- ODER Favoriten-Baum)
hinzugefügt.
8) **Ein-/Ausblenden-Button der Verzeichnis-Übersicht angeblich nicht sichtbar:** der
Werkzeugleisten-`ToggleButton` „Verzeichnis-Übersicht" existierte bereits als zweiter Eintrag der
Werkzeugleiste und konnte in dieser Cloud-Umgebung ohne echte WPF-Anzeige nicht als tatsächlich
fehlerhaft nachvollzogen werden (denkbare Ursache: WPFs eingebauter `ToolBar`-Overflow-Mechanismus
bei geringer Fensterbreite/vielen Einträgen, siehe „Bekannte Unsicherheiten" unten). Als
zuverlässige Rückfalloption zusätzlich eine neue, davon UNABHÄNGIGE Checkbox „Verzeichnis-Übersicht
anzeigen" im „Einstellungen"-Menü, mit dem Werkzeugleisten-Button synchron gehalten
(`SyncExplorerVisibleMenuItem`).

*Neue Funktionen:*

9) **Neuen Ordner anlegen** über die Verzeichnis-Übersicht (Kontextmenü „Neuen Ordner anlegen…",
`Microsoft.VisualBasic.Interaction.InputBox` für die Namenseingabe, `DelegateUndoableAction` für
Strg+Z — der Ordner wird beim Rückgängigmachen nur gelöscht, wenn er zu diesem Zeitpunkt noch LEER
ist, um versehentlich hineingelegte Dateien nicht zu vernichten).
10) **Doppelklick auf eine Datei** in der Verzeichnis-Übersicht öffnet sie im gerade AKTIVEN
Arbeitsbereich (neuer `ExplorerOpenTarget.Active`, unabhängig von Split-View-Status).
11) **Konfigurierbare Farbe für das Notiz-Icon** im Dateienreiter UND den „🗒 Notizen anzeigen"-
Button (beide teilen sich dasselbe statische `EditorPane.NoteIconBrush`-Objekt, damit eine Änderung
sofort an BEIDEN Stellen wirkt, auch über beide Arbeitsbereiche hinweg) — als farbiger „Chip" HINTER
dem Emoji umgesetzt (nicht als Emoji-Einfärbung selbst: WPFs Textrendering unterstützt farbige
Emoji-Glyphen über `Foreground` nicht zuverlässig). Einstellbar über „Einstellungen" →
„Notiz-Icon-Farbe wählen…", übersteht einen Neustart (`AppSessionState.NoteIconColorHex`).
12) **Bookmarks im Dokument:** Strg+F2 (Notepad++s eigener Standard-Hotkey für „Bookmark
umschalten") setzt/entfernt ein Bookmark auf der aktuellen Cursorzeile (neues Projekt-Modul
`ParadoxModTool.Notes.BookmarkStore`, JSON unter `%APPDATA%\ParadoxModTool\bookmarks.json`,
zeilenbasiert statt zeichen-offset-basiert — bleibt dadurch auch nach kleineren Bearbeitungen
WEITER OBEN/UNTEN im Dokument robust identifizierbar). Das Notizfenster (`FileNoteWindow`) ist
jetzt ein `TabControl` mit zwei Reitern „Notiz" (bisheriger Inhalt) und „Bookmarks" (Liste mit
„Springe zu"/„Entfernen", Doppelklick springt ebenfalls) — „Springe zu" schließt danach bewusst das
Fenster, damit der Editor sofort wieder bedienbar ist.
13) **Suchen-&-Ersetzen-Schnellzugriff-Buttons:** im „Ordner (Dateien)"-Modus jetzt 4 FESTE,
spielprofil-abhängige Buttons (Mod-Ordner, sowie „in_game"/„main_menu"/„loading_screen" — für EU5
bereits als `FolderCategories`-Einträge mit dem tatsächlichen, direkt unterhalb des Mod-Ordners
liegenden Ordnernamen hinterlegt, siehe `profiles/eu5.json`) PLUS 4 frei konfigurierbare Buttons
(Name+Pfad über „✎" festlegbar, `ParadoxModTool.SearchReplace.QuickPathStore`, JSON unter
`%APPDATA%\ParadoxModTool\search-quickpaths.json`) — bewusst als eigene, sofort sichtbare
Einzelknöpfe UMGESETZT, nicht als weiterer Eintrag der bereits vorhandenen „Favoriten"-Dropdown
(unterschiedlicher Zweck: beliebig viele vs. genau 8 feste Schnellzugriffe). Über den
„▾ Ausblenden"/„▸ Einblenden"-Umschalter dauerhaft ausblendbar.

**Wichtiger Hinweis wie schon bei der ersten Runde-10-Auslieferung:** auch dieser komplette
Ausbauschritt ist erneut umfangreicher WPF-Code (neue Dialoge, dynamisch per Code erzeugte
Controls in `SearchReplaceWindow`, `Microsoft.VisualBasic.FileIO`/`Interaction.InputBox`) und war
in dieser Umgebung ausschließlich per Python-Strukturprüfung kontrollierbar, NICHT durch einen
echten Compiler — bitte beim nächsten Build besonders die neuen `Microsoft.VisualBasic`-Aufrufe
sowie die dynamisch erzeugten Schnellzugriff-Buttons gegenprüfen (siehe „Bekannte Unsicherheiten"
unten).

## Was ist bereits implementiert

| Modul | Status | Beschreibung |
|---|---|---|
| `ParadoxModTool.ErrorLog` | **Funktionsfähig** | Parser für die 5 in deinen echten Logs gefundenen Zeilenmuster, inkl. Tests mit echten Beispielzeilen |
| `ParadoxModTool.GameProfiles` | **Funktionsfähig** | Lädt `profiles/eu5.json` in ein typisiertes C#-Modell |
| `ParadoxModTool.Syntax` | **Funktionsfähig** | Lädt AvalonEdit-XSHD-Highlighting-Dateien aus `syntax-templates/` |
| `ParadoxModTool.Editor` | **Prototyp, Runde 3 vom Nutzer bestätigt, Runde 4 neu** | Neu (Runde 4, Nutzer-Feedback „Dokument als wichtig markieren" + „Hilfsdatei"): `FileCategory` hat zwei weitere Werte `Important`/`HelperFile`, die NIE automatisch anhand der Dateiendung vergeben werden, sondern ausschließlich über `OpenDocument.SetManualCategory` (nur `Important`, `HelperFile` oder `null` zulässig) — die wirksame `Category` ist `ManualCategory ?? AutoCategory`. `OpenDocument` implementiert jetzt `INotifyPropertyChanged` (vorher nicht nötig, da sich die Kategorie eines Dokuments nie zur Laufzeit änderte) und meldet Änderungen an `Category`/`CategoryColor`/`CategoryDisplayName`/`TabBackground`, damit gebundene Tabs sofort neu zeichnen; `NotifyColorSettingsChanged()` löst dieselbe Aktualisierung aus, OHNE die Kategorie zu ändern (für globale Farbeinstellungsänderungen). Zwei neue `ToggleButton`s in der Mini-Werkzeugleiste (`MarkImportantToggle`/`MarkHelperFileToggle`, gegenseitig exklusiv) setzen/entfernen die manuelle Kategorie des aktiven Dokuments; ihr Zustand wird bei jedem Tabwechsel aus dem neu aktiven Dokument übernommen. Zwei weitere Checkboxen im Kategorie-Filter (`FilterImportantCheckBox`/`FilterHelperFileCheckBox`). Neu (Runde 4, Konzept: „Die Farben sollten frei konfigurierbar sein" + Nutzer-Feedback „selbst definierbarer Farbcode"): `FileCategoryClassifier.SetColorOverrides`/`ColorHexFor` — eine gesetzte Überschreibung hat Vorrang vor `DefaultColorHex`, ein ungültiger Hex-Code (`ParseColor`) fällt auf die Standardfarbe zurück statt abzustürzen; `TabBackgroundFor` liefert dieselbe Farbe halbtransparent für die neue „ganzen Reiter einfärben"-Einstellung (`OpenDocument.ColorizeWholeTabEnabled`, statisch für alle Dokumente/Arbeitsbereiche gemeinsam). Neu (Runde 3, Nutzer-Feedback „offene Datei nach Ordner-Ersetzen wird nicht aktualisiert"): `EditorPane.PathsEqual` vergleicht Dateipfade jetzt nicht mehr nur als reine Zeichenkette, sondern zusätzlich über `Path.GetFullPath` normalisiert (fällt bei ungültigen Pfaden auf den reinen Zeichenkettenvergleich zurück) — verwendet von `LoadFile` (kein doppelter Tab für dieselbe Datei) und `ReloadIfOpen`. Behebt einen Fall, in dem zwei Pfade dieselbe Datei bezeichneten, aber textuell nicht identisch waren (z. B. ein abschließender Backslash im durchsuchten Ordnerpfad, der bei `Directory.EnumerateFiles` zu doppelten Backslashes vor dem Dateinamen führt). `EditorPane` — ein unabhängiger Arbeitsbereich (Abschnitt 5, Split-View) mit eigener Mini-Werkzeugleiste, eigener Tableiste (`OpenDocument`-Liste, mehrere Dokumente gleichzeitig offen, je eigenes AvalonEdit-`TextDocument`, jeder Tab mit eigenem „✕"-Schließen-Button und farbigem Dateikategorie-Balken), einer Pfadanzeige unter der Tableiste (voller Pfad des aktiven Dokuments bzw. Platzhalter für „Unbenannt") und einem einzelnen AvalonEdit-Steuerelement, dessen Inhalt beim Tabwechsel umgeschaltet wird; Dateien laden/neu anlegen + Highlighting anwenden; Dokumente können per `AdoptDocument`/`ReleaseDocument` zwischen zwei `EditorPane`-Instanzen verschoben werden, ohne dabei neu eingelesen zu werden (Inhalt + AvalonEdits eigener Text-Undo-Verlauf bleiben erhalten). `CloseDocument`/`CloseActiveDocument` (ohne Rückfrage, siehe Einschränkung unten), `GoToLine` (Strg+G) und `ReloadIfOpen` (lädt ein bereits offenes Dokument neu, z. B. nach „Ersetzen in Dateien" oder nach Undo/Redo). Neu (Runde 2, Nutzer-Feedback): `FileCategory`/`FileCategoryClassifier` ordnet jedem `OpenDocument` anhand der Dateiendung eine Kategorie zu (Text/GUI/YML/Defines/Sonstige) mit Standardfarbe; ursprünglich vier, seit Runde 4 sechs Checkboxen (siehe oben) in der Mini-Werkzeugleiste blenden Kategorien per `ICollectionView`-Filter ein-/aus (das aktive Dokument bleibt dabei immer sichtbar); die Tableiste leitet vertikales Mausrad-Scrollen auf horizontales Scrollen um. Führt selbst keine Dateidialoge/Schreibvorgänge aus, sondern meldet Wünsche über Events (`NewDocumentRequested`/`OpenDocumentRequested`/`SaveDocumentRequested`/`MoveActiveDocumentRequested`/neu in Runde 5: `ShowNoteRequested`) an das `App`-Modul. Neu (Runde 5): `OpenDocument.HasNote` (mit `SetHasNote`, löst `PropertyChanged` aus) zeigt ein Icon im Tab, sobald zur Datei eine Notiz existiert — der eigentliche Abgleich mit `ParadoxModTool.Notes` geschieht bewusst im `App`-Modul, damit dieses Projekt keine Abhängigkeit auf `.Notes` braucht. `EditorPane.PathsEqual` ist jetzt `public` (vorher `private`), damit `MainWindow` beim Aktualisieren des Notiz-Icons denselben normalisierten Pfadvergleich nutzen kann. Tab-Kontextmenü „Ordner öffnen, wo die Datei liegt" sowie drei neue Buttons neben der (jetzt als schreibgeschützte Textbox statt `TextBlock` umgesetzten, damit direkt markier-/kopierbaren) Pfadanzeige: „📋 Kopieren", „📂 Ordner öffnen" (`OpenContainingFolder`, `explorer.exe /select,...`), „🗒 Notizen anzeigen". Bugfix (Runde 5, Nutzer-Feedback „Ausblenden ... funktioniert nicht"): `Tabs_SelectionChanged` ruft jetzt selbst `CollectionView.Refresh()` auf, da ein Tabwechsel den Kategorie-Filter vorher nicht neu ausgewertet hat. Neu (Runde 10, 2. Rückmeldung): `EditorPane.NoteIconBrush` (statisches, ungefrorenes `SolidColorBrush`-Feld, von BEIDEN `EditorPane`-Instanzen über `{x:Static}` geteilt) für die frei wählbare Notiz-Icon-Farbe — als Hintergrund-„Chip" hinter dem 🗒-Emoji umgesetzt (WPF färbt Emoji-Glyphen über `Foreground` nicht zuverlässig ein), siehe `MainWindow.NoteIconColor_Click`. Neue `CurrentCaretLine`/`GetLineText`-Methoden für das Bookmark-Feature (Strg+F2, siehe `ParadoxModTool.Notes`-Zeile unten und Runde-10-2.-Rückmeldung-Absatz oben). |
| `ParadoxModTool.UndoRedo` | **Funktionsfähig** | Der programmweite Undo/Redo-Aktionsverlauf aus Abschnitt 5 ("Undo für Programmaktionen ... laufen über denselben Undo-Stack"): `ActionHistory` (Execute/Record/Undo/Redo — geben seit Runde 2 die verarbeitete Aktion zurück, damit das `App`-Modul betroffene Dateien für ein automatisches Neuladen offener Tabs ermitteln kann; Beschreibungen für eine Verlaufsanzeige, optionales Größenlimit), `IUndoableAction` als Erweiterungspunkt für künftige Werkzeuge, `DelegateUndoableAction` für einfache Fälle, `CompositeUndoableAction` (mit öffentlichem `Actions`-Zugriff seit Runde 2) um mehrere Teilschritte (z. B. eine Massenumbenennung über viele Dateien) als EINEN Verlaufseintrag zu bündeln, sowie `FileWriteUndoableAction` (mit öffentlichem `FilePath`-Zugriff seit Runde 2) als wiederverwendbarer Baustein für jede dateischreibende Werkzeug-Operation (Speichern, Merge-Ergebnis, Formatierung, ...) — inkl. Tests. Bewusst als eigenes, UI-unabhängiges Projekt (aus Testbarkeitsgründen, da WPF-Code hier nicht kompiliert/getestet werden kann), vom `App`-Modul verwendet. Bekannte Einschränkung: reine Texteingaben im Editor laufen weiterhin über AvalonEdits eigenen, davon unabhängigen Undo-Mechanismus (Strg+Z im Textbereich) — eine vollständige Verschmelzung beider Stacks ist für eine spätere Ausbaustufe vorgesehen. |
| `ParadoxModTool.Session` | **Funktionsfähig** | Speichern/Laden des Programmzustands als JSON unter `%APPDATA%\ParadoxModTool\session.json` (Abschnitt 12: „offene Dateien, Split-View-Aufteilung, aktives Spielprofil, Scroll-Position"): `AppSessionState`/`PaneSessionState`/`DocumentSessionState` als Datenmodell, `SessionStore.Save`/`TryLoad` rein dateibasiert. `TryLoad` gibt bei fehlender oder beschädigter Datei bewusst `null` statt eines Fehlers zurück, damit ein defektes Session-File niemals den Programmstart blockiert — inkl. Tests. Neu (Runde 3, Nutzer-Feedback „Einstellungen"-Menü): `AppSessionState.ResultsPanelOnTop` speichert, ob die Suchergebnisleiste oben oder unten angezeigt wird (Standard `false`/unten) — eine alte Session-Datei ohne dieses Feld deserialisiert automatisch zu diesem Standardwert, `SessionStore` selbst musste dafür nicht angepasst werden (rein generische JSON-Serialisierung). Neu (Runde 4): `AppSessionState.CategoryColorOverrides` (`Dictionary<string,string>`, Schlüssel = Enum-Name von `FileCategory` als String, damit dieses Projekt nicht auf `ParadoxModTool.Editor` verweisen muss — `MainWindow` übersetzt beim Laden/Speichern) und `AppSessionState.ColorizeWholeTab` (Standard `false`) — auch hier deserialisiert eine alte Session-Datei ohne diese Felder automatisch zu ihren Standardwerten. Neu (Runde 5, Bugfix „manuelle Kategorie-Markierung übersteht Neustart nicht"): `DocumentSessionState.ManualCategory` (Enum-Name als String, `null` = keine manuelle Zuweisung) — eine alte Session-Datei ohne dieses Feld deserialisiert wie gewohnt automatisch zu `null`. Neu (Runde 8): `AppSessionState.GameExecutablePath` (nullable, Standard `null`) speichert den eingestellten Pfad zur Spiel-.exe (Konzept Abschnitt 8) — auch hier deserialisiert eine alte Session-Datei ohne dieses Feld automatisch zu `null`. Neu (Runde 9): `AppSessionState.PlaysetsFilePath` (nullable, Standard `null`) speichert den eingestellten Pfad zu `playsets.json` — ebenfalls rein additiv. Neu (Runde 10): `AppSessionState.ExplorerRoots` (`List<ExplorerRootState>`, Standard leere Liste) speichert die Wurzeln der Verzeichnis-Übersicht; `ExplorerRootState.Kind` ist bewusst ein einfacher String (Enum-Name von `ParadoxModTool.Explorer.ExplorerRootKind`) statt eines direkten Verweises, damit dieses Projekt weiterhin KEINE Abhängigkeit auf `ParadoxModTool.Explorer` braucht (dasselbe Muster wie `CategoryColorOverrides` für `FileCategory`) — `MainWindow` übersetzt beim Laden/Speichern. Bewusst als eigenes, UI-unabhängiges Projekt nach demselben Muster wie `ParadoxModTool.UndoRedo`, vom `App`-Modul verwendet. Neu (Runde 10, 2. Rückmeldung): `AppSessionState.NoteIconColorHex` (nullable, Standard `null`) speichert die gewählte Notiz-Icon-Farbe — auch hier deserialisiert eine alte Session-Datei ohne dieses Feld automatisch zu `null` (Standardfarbe bleibt bestehen). |
| `ParadoxModTool.SearchReplace` | **Funktionsfähig, Runde 2 vom Nutzer bestätigt** | Kernlogik für Suchen/Ersetzen (Abschnitt 5b): `TextSearch` (Suchen/Ersetzen in einem einzelnen Text, inkl. Groß-/Kleinschreibung, „nur ganzes Wort", mehrzeilige Suchbegriffe für Codeblock-Ersetzung — gleicht Zeilenumbrüche in Suchbegriff/Ersetzung automatisch an die Zieltext-Konvention an), `TextMatch` (liefert seit Runde 2 zusätzlich `MatchedText`, den exakt gefundenen — ggf. mehrzeiligen — Code, für die Anzeige „nur gefundenen Code anzeigen"), `FileSearchService`/`FileReplaceService` (rekursive Ordnersuche bzw. reine Berechnung von Ersetzungen über mehrere Dateien hinweg, ohne selbst zu schreiben — das Schreiben übernimmt das `App`-Modul über den Aktionsverlauf), `SearchFavoritesStore` (Favoriten-Suchordner als JSON, analog zu `SessionStore`). Neu (Runde 3, Nutzer-Feedback „Formatierung ignorieren"): `SearchOptions.IgnoreFormatting` sowie `TextSearch.FindAllIgnoringFormatting`/`StripWhitespace` — entfernen sämtliche Whitespace-Zeichen aus Suchbegriff UND Zieltext, suchen in dieser Fassung und bilden jeden Treffer über eine mitgeführte Positionszuordnung wieder auf seine echte, unverändert formatierte Stelle im Original ab (Offset/Länge in Original-Textkoordinaten, wie jeder andere Treffer auch) — dadurch mussten `FindAllNonOverlapping`, `FindNext` und `ReplaceAll` selbst NICHT angepasst werden, da sie ohnehin ausschließlich mit `TextMatch.Offset`/`Length` im Original arbeiten; funktioniert dadurch transparent sowohl im Dokument- als auch im Ordner-Modus. Inkl. Tests (24, davon 5 neu für „Formatierung ignorieren"). Neu (Runde 3, Nutzer-Feedback „Einrückung nach Ersetzung falsch"): `ReplaceAll` gleicht bei aktivem `IgnoreFormatting` zusätzlich pro Fundstelle die Einrückung des eingefügten Ersetzungstextes an die tatsächliche Einrückungstiefe DIESER Fundstelle an (`ReindentReplacementToMatchContext`/`LeadingIndentBeforeMatch`) — vorher wurde der Ersetzungstext überall mit derselben, absolut eingegebenen Einrückung eingefügt, die naturgemäß nur zu einer von mehreren unterschiedlich tief verschachtelten Fundstellen passen konnte. 3 weitere Tests in `TextSearchTests` (jetzt 25 dort) — **vom Nutzer bestätigt: „Beide Probleme aus der letzten Runde sind gefixt."** Bewusst als eigenes, UI-unabhängiges Projekt nach demselben Muster wie `ParadoxModTool.UndoRedo`/`.Session`. Neu (Runde 10, 2. Rückmeldung): `QuickPathButtonState`/`QuickPathSettings`/`QuickPathStore` (Nutzer-Feedback „4 Default Buttons ... 4 weitere Buttons, wo man den Pfad frei konfigurieren kann ... auch die Möglichkeit haben die Buttons auszublenden") — JSON unter `%APPDATA%\ParadoxModTool\search-quickpaths.json` für die 4 FREI konfigurierbaren Buttons (Label+Pfad, `Sichtbar`-Flag); die 4 FESTEN Buttons brauchen keine eigene Persistenz, da sie direkt aus dem aktiven `GameProfile.FolderCategories` gebaut werden (siehe `SearchReplaceWindow.BuildFixedQuickPathButtons` in der `App`-Zeile unten). |
| `ParadoxModTool.App` | **Prototyp, Runde 3 vom Nutzer bestätigt** | WPF-Hauptfenster mit globaler Werkzeugleiste (Split-View-Umschalter / Rückgängig / Wiederholen / Suchen… / Gehe zu Zeile… / Schließen) und zwei `EditorPane`-Instanzen (links/rechts) in einem per `GridSplitter` verstellbaren Grid — der rechte Arbeitsbereich ist standardmäßig eingeklappt (Breite 0) und wird erst beim Umschalten von „Split-View" ODER automatisch beim Verschieben eines Dokuments per „⇄" eingeblendet (Abschnitt 5: „zwei unabhängige Arbeitsbereiche"). Verdrahtet die „Requested"-Events beider Arbeitsbereiche mit den tatsächlichen Dateidialogen und dem gemeinsamen Undo-Stack; das Verschieben-Event eines Arbeitsbereichs hängt den jeweils ANDEREN Arbeitsbereich als Ziel ein. „Speichern" ist die erste über `ActionHistory` rückgängig machbare Programmaktion; weitere Werkzeuge (Merge, Massenumbenennung, Formatierung) sollen künftig ebenfalls darüber laufen. Speichert beim Schließen (`Closing`-Event) automatisch den Programmzustand über `ParadoxModTool.Session` und stellt ihn beim nächsten Start wieder her (Abschnitt 12, siehe oben). `SearchReplaceWindow` (Strg+F/Strg+H oder Werkzeugleiste) bindet `ParadoxModTool.SearchReplace` an: „Aktuelles Dokument" wirkt auf den zuletzt fokussierten Arbeitsbereich (`ActivePane`, über `IsKeyboardFocusWithin` ermittelt), „Ordner" nutzt einen klassischen WinForms-`FolderBrowserDialog` (`UseWindowsForms` im `.csproj`) und schreibt Ersetzungen als EINEN Eintrag im Aktionsverlauf; ein dabei betroffenes, bereits offenes Dokument wird automatisch neu geladen. `GoToLineWindow` (Strg+G bzw. Werkzeugleiste) für „Gehe zu Zeile", globaler „Schließen"-Button für das aktive Dokument. Da `UseWindowsForms` und `UseWPF` gemeinsam aktiv sind, sind mehrere gleichnamige Typen zwischen beiden UI-Frameworks mehrdeutig (`Application`, `MessageBox`, `KeyEventArgs`, `OpenFileDialog`, `SaveFileDialog`, sowie `TextSearch` gegenüber `System.Windows.Controls.TextSearch`) — jeweils per expliziter `using`-Alias-Direktive in der betroffenen Datei eindeutig aufgelöst (systematisch gegen die bekannten Namenskollisionen beider Frameworks geprüft). Neu (Runde 2, Nutzer-Feedback): beide Textfelder im Suchen-Fenster haben jetzt `AcceptsTab="True"` (die eigentliche Ursache für „findet Codeblock nicht", siehe Nachbesserung Runde 2 oben); `Undo_Click`/`Redo_Click` laden jetzt rekursiv ALLE davon betroffenen offenen Tabs neu (`ReloadOpenTabsAffectedBy`/`CollectFilePaths`); eine neue, einklappbare Ergebnisleiste (`ResultsPanel`) über dem Arbeitsbereich zeigt dieselbe geteilte Trefferliste wie das modale Suchen-Fenster, inkl. synchronisierter „Nur gefundenen Code anzeigen"-Checkbox (`SetHideSearchResultDescriptions`). Neu (Runde 3, Nutzer-Feedback „Einstellungen"-Menü): ein `Menu` über der Werkzeugleiste mit Menüpunkt „Einstellungen" → Checkbox „Suchergebnisse oben anzeigen" (`ResultsPanelOnTopMenuItem`); Standard ist jetzt **unten** (`ResultsPanel` per `DockPanel.Dock="Bottom"`), umschaltbar zur Laufzeit über `ApplyResultsPanelPosition(bool onTop)` (`DockPanel.SetDock`), Zustand wird über `AppSessionState.ResultsPanelOnTop` gespeichert/wiederhergestellt; das Menü ist bewusst als Ort für künftige Einstellungen angelegt, nicht nur für diesen einen Punkt. `Dock`/`DockPanel` sind zwischen WPF und WinForms NICHT mehrdeutig (WinForms kennt nur `DockStyle` und eine `Control.Dock`-Eigenschaft, keinen Typ dieses Namens) und brauchten daher keinen Alias. Neu (Runde 3, Nutzer-Feedback „Formatierung ignorieren"): `SearchReplaceWindow` hat eine neue Checkbox „Formatierung ignorieren" (`IgnoreFormattingCheckBox`), die `SearchOptions.IgnoreFormatting` setzt (siehe `ParadoxModTool.SearchReplace` oben). Neu (Runde 3, Nutzer-Feedback „offene Datei nach Ordner-Ersetzen wird nicht aktualisiert"): `SearchReplaceWindow.NormalizeFolderPath` normalisiert den eingegebenen/durchsuchten Ordnerpfad über `Path.GetFullPath`, bevor Dateien darin aufgezählt werden (siehe `ParadoxModTool.Editor`-Bullet oben für den zugehörigen `PathsEqual`-Fix). Neu (Runde 4): Das „Einstellungen"-Menü hat zwei weitere Punkte — Checkbox „Farbcodes färben den ganzen Reiter ein" (`ColorizeWholeTabMenuItem`, setzt `OpenDocument.ColorizeWholeTabEnabled`) und „Kategorie-Farben…" (öffnet `CategoryColorsWindow`: sieben feste Hex-Textfelder, eines je Kategorie, mit „Zurücksetzen" auf die Standardfarben; validiert alle sieben Felder über `ColorConverter.ConvertFromString` VOR dem Schließen, damit ein Tippfehler nicht erst unbemerkt zur Laufzeit auf die Standardfarbe zurückfällt). Beide Einstellungen wirken sofort über `MainWindow.RefreshAllDocumentColors` (ruft `OpenDocument.NotifyColorSettingsChanged()` auf jedem offenen Dokument in BEIDEN Arbeitsbereichen auf) und werden über `AppSessionState.ColorizeWholeTab`/`CategoryColorOverrides` gespeichert/wiederhergestellt (siehe `ParadoxModTool.Session` oben). Neuer Werkzeugleisten-Button „Notizen…" (`FileNoteWindow`, Nutzer-Feedback „Notizfunktion ... nicht in der Datei selbst gespeichert") liest/schreibt die Notiz zur aktiven Datei des fokussierten Arbeitsbereichs über das neue Projekt `ParadoxModTool.Notes` (siehe eigene Tabellenzeile unten); ein noch nicht gespeichertes Dokument wird abgewiesen, da eine Notiz an einen tatsächlichen Dateipfad gebunden ist. `CategoryColorsWindow.xaml.cs`/`FileNoteWindow.xaml.cs` sind wie der Rest von `ParadoxModTool.App` von der `UseWPF`+`UseWindowsForms`-Namenskollision betroffen — zusätzlich zu den bereits bekannten (`Application`, `MessageBox`, `KeyEventArgs`, `OpenFileDialog`, `SaveFileDialog`, `TextSearch`) mussten hier `TextBox` (System.Windows.Controls vs. System.Windows.Forms) sowie `Color`/`ColorConverter` (System.Windows.Media vs. System.Drawing) per Alias eindeutig aufgelöst werden, da diese Datei sie (anders als die meisten bisherigen Dateien) tatsächlich als nackten Typnamen referenziert statt nur über generierte XAML-Felder. Bekannte Einschränkungen dieses Ausbauschritts: dieselbe Datei kann aktuell in beiden Arbeitsbereichen gleichzeitig geöffnet werden, ohne dass die beiden Kopien synchronisiert würden (kein gemeinsamer Puffer); das Verschieben zwischen Arbeitsbereichen geschieht über den „⇄"-Button statt per Drag&Drop; es gibt noch keine visuelle Markierung für ungespeicherte Änderungen (Dirty-Indikator) in der Tableiste, weshalb „Dokument schließen" ohne Rückfrage schließt; „Ersetzen im aktuellen Dokument" setzt den kompletten Dokumenttext neu (statt einzelner Ersetzungen), wodurch die Cursorposition danach am Dokumentanfang steht; schlägt beim „Ersetzen in Dateien" das Schreiben EINER Datei mitten in der Operation fehl (z. B. Datei zwischenzeitlich gesperrt), sind die bereits geschriebenen Dateien der Operation nicht mehr über „Rückgängig" erreichbar (Einzelfall, wird ggf. in einer späteren Ausbaustufe transaktional abgesichert); dieser komplette Ausbauschritt ist WPF-Code und daher — wie schon `ParadoxModTool.Editor` — nur manuell und per Python-Strukturprüfung (Klammernbalance/XML-Wohlgeformtheit) kontrolliert, nicht automatisiert getestet. Neu (Runde 5): `CategoryColorsWindow` hat neben jedem der sieben Hex-Textfelder jetzt eine live aktualisierte Vorschaufläche sowie einen „🎨"-Button, der `System.Windows.Forms.ColorDialog` (`FullOpen = true`) öffnet und die Auswahl als Hex-Code zurückschreibt — zusätzlich zu den bereits bekannten Namenskollisionen mussten hier `Border`, `Button`, `TextChangedEventArgs`, `SolidColorBrush`, `Brushes` per Alias aufgelöst werden. `MainWindow.ShowNoteFor`/`UpdateHasNote` bündeln das Öffnen des Notizfensters bzw. die Aktualisierung des Notiz-Icons zentral (siehe `ParadoxModTool.Editor`-Zeile oben) und werden sowohl vom bisherigen Werkzeugleisten-Button als auch von den neuen Tab-/Pfadanzeige-Elementen genutzt. Neu (Runde 7): Werkzeugleisten-Button „Formatieren…" öffnet `FormatDocumentWindow` (zwei unabhängige Checkboxen, siehe `ParadoxModTool.Tools`-Zeile und Runde-7-Absatz oben) für das aktive Dokument des fokussierten Arbeitsbereichs; die Struktur-Checkbox ist dort von vornherein deaktiviert, wenn die Datei anhand ihrer Endung erkennbar kein Paradox-Skript ist. Neu (Runde 8): Werkzeugleiste hat jetzt eine Dropdown-Liste `LaunchOptionsCombo` (befüllt aus `_activeProfile.LaunchOptions`) und den Button „▶ Spiel starten" (`LaunchGame_Click`, bindet `ParadoxModTool.LaunchControl.GameLauncher.Launch` an); fehlt noch ein Pfad zur Spiel-.exe, fragt `PromptForGameExecutablePath` automatisch danach (auch erreichbar über den neuen Menüpunkt „Einstellungen" → „Spielpfad einstellen…"). Der eingestellte Pfad wird über `AppSessionState.GameExecutablePath` gespeichert/wiederhergestellt (siehe `ParadoxModTool.Session`-Zeile). Neu (Runde 9): Zweite Werkzeugleisten-Dropdown `PlaysetsCombo` (`DisplayText` aus `PlaysetInfo`, befüllt über `RefreshPlaysetsCombo`/`PlaysetStore.ListPlaysets`) für die Playset-Auswahl — `TryGetDefaultPlaysetsFilePath` sucht beim Programmstart automatisch unter `Documents/Paradox Interactive/Europa Universalis V/playsets.json`, alternativ über den neuen Menüpunkt „Einstellungen" → „Playset-Datei einstellen…" (`SetPlaysetsFilePath_Click`) einstellbar; Pfad wird über `AppSessionState.PlaysetsFilePath` gespeichert. `LaunchGame_Click` ruft vor dem eigentlichen Start `PlaysetStore.SetActivePlayset` auf, falls ein vom aktuell aktiven abweichendes Playset gewählt ist, und bricht bei einem Fehlschlag den Start ab, statt unbemerkt mit dem falschen Playset zu starten. Außerdem (Runde 9, Nutzer-Feedback): der Dateifilter im „Öffnen…"-Dialog (`OpenFileDialogFilter`-Konstante) zeigt jetzt standardmäßig „Alle Dateien" statt nur `*.txt`, mit „Alle unterstützten Dateitypen" und Einzelfiltern als zusätzliche Optionen; Tooltip von „Spielpfad einstellen…" geändert zu „Wählt die EU 5.exe aus (Europa Universalis 5/binaries)." Neu (Runde 10): neuer Werkzeugleisten-`ToggleButton` „Verzeichnis-Übersicht" (`ExplorerToggle_Click`/`ApplyExplorerVisibleState`, blendet die neue erste Grid-Spalte samt `ExplorerPanelControl` ein/aus, analog zu `ApplySplitViewState`); `ExplorerPanel_OpenFileRequested`/`ExplorerPanel_NewFileRequested` verdrahten die neuen `ExplorerPanel`-Events (siehe `ParadoxModTool.Explorer`-Zeile unten) mit `EditorPane.LoadFile` bzw. `FileWriteUndoableAction`; `ResolveDefaultModRoot`/`ComputeGameFilesGuess` ermitteln beim Start bzw. nach Ändern des Spielpfads die automatisch vorgeschlagene Mod-Wurzel bzw. die unverbindliche Ordner-Vorauswahl für „Ordner hinzufügen…". Neu (Runde 10, 2. Rückmeldung — siehe eigener Absatz oben für Details): `ExplorerVisibleMenuItem`/`SyncExplorerVisibleMenuItem` (Rückfalloption im „Einstellungen"-Menü zum Werkzeugleisten-Toggle), `NoteIconColor_Click` (Notiz-Icon-Farbe, `System.Windows.Forms.ColorDialog`), `ToggleBookmarkAtCursor`/`JumpToBookmarkInFile` (Strg+F2-Bookmarks, verdrahtet mit dem neuen „Bookmarks"-Reiter in `FileNoteWindow`), `ExplorerPanel_NewFolderRequested` (`DelegateUndoableAction`) und `ExplorerPanel_DeleteRequested` (`Microsoft.VisualBasic.FileIO.FileSystem`, schließt offene Tabs der gelöschten Datei automatisch). `OpenSearchReplaceWindow` übergibt jetzt zusätzlich `_activeProfile` und `ResolveDefaultModRoot` an `SearchReplaceWindow` für dessen neue Schnellzugriff-Buttons. |
| `ParadoxModTool.Notes` | **Funktionsfähig, neu in Runde 4** | Speichert/lädt Notizen zu einzelnen Dateien (Nutzer-Feedback Runde 4: „Notizfunktion ... nicht in der Datei selbst gespeichert, sondern nur für den Modder sichtbar") als JSON unter `%APPDATA%\ParadoxModTool\notes.json` — `FileNoteStore.Save`/`TryLoad` nach demselben Muster wie `SearchFavoritesStore`/`SessionStore` (fehlende/beschädigte Datei liefert bewusst ein leeres Wörterbuch statt eines Fehlers), `GetNote`/`SetNote` arbeiten auf einem bereits geladenen Wörterbuch und normalisieren den Dateipfad-Schlüssel über `Path.GetFullPath` (dieselbe Lehre aus Runde 3 wie `EditorPane.PathsEqual` — oberflächlich unterschiedliche Schreibweisen desselben Pfades dürfen nicht zu getrennten Notizen führen); eine leere/nur-Leerzeichen-Notiz entfernt den Eintrag ganz, statt eine leere Notiz zu speichern. Inkl. Tests. Bewusst als eigenes, UI-unabhängiges Projekt nach demselben Muster wie `ParadoxModTool.UndoRedo`/`.Session`/`.SearchReplace`, vom `App`-Modul (`FileNoteWindow`) verwendet. Neu (Runde 10, 2. Rückmeldung): `BookmarkEntry`/`BookmarkStore` (Nutzer-Feedback „eine Codestelle bookmarken ... über den Notizen Reiter schnell hinspringen") — JSON unter `%APPDATA%\ParadoxModTool\bookmarks.json`, zeilenbasiert statt zeichen-offset-basiert (bleibt dadurch nach kleineren Bearbeitungen robuster identifizierbar, dieselbe Konvention wie Notepad++s eigene Bookmarks), `Toggle`/`GetBookmarks`/`Remove` nach demselben „lade frisch, schreibe frisch"-Muster wie `FileNoteStore`. |
| `ParadoxModTool.VersionManager` | **Funktionsfähig** | Versionen anlegen/auflisten, Versionslock (Freeze), Dateien zwischen Versionen verschieben, Snapshot als ZIP — inkl. Tests. Arbeitet unabhängig von Git über `.pmt-versions/` im Mod-Ordner; die alte `VersionManagerPlaceholder.cs` ist überholt und kann gelöscht werden. |
| `ParadoxModTool.VanillaDiff` | **Funktionsfähig** | Struktureller Diff/Merge auf AST-Basis (Abschnitt 7): eigener Tokenizer/Parser für den Jomini-Skriptdialekt (`ScriptTokenizer`/`ScriptParser`/`ScriptDocument`), struktureller Diff mit rekursivem Kind-Diff für verschachtelte Blöcke (`ScriptDiffer`), 3-Wege-Merge mit Konflikterkennung (`MergeEngine`), Erkennung überschriebener Vanilla-Dateien anhand der Lade-Regel des Spielprofils (`VanillaFileTracker`), optionale Versionshistorie von Vanilla-Dateien (`VanillaHistoryStore`) und der komplette Merge-Ablauf inkl. automatischem Mod-Snapshot vor dem Überschreiben (`VanillaMergeWorkflow`) — inkl. Tests. Die alte `VanillaDiffPlaceholder.cs` ist überholt und kann gelöscht werden. Bekannte Einschränkung: `ScriptWriter` schreibt beim Merge immer einheitlich neu formatiert (keine 1:1-Erhaltung der Original-Formatierung); mehrfach vorkommende Schlüssel auf derselben Ebene (z. B. mehrere `modifier = {...}`) werden über ihre Reihenfolge zugeordnet, nicht über einen inhaltlichen Fingerabdruck. Bugfix (Runde 7, proaktiv entdeckt bei der Vorbereitung von `ParadoxModTool.Tools`): ein hängender Kommentar am Ende eines Blocks/einer Datei (ohne nachfolgenden Eintrag) ging beim Parsen bisher verloren — behoben über den neuen Eintragstyp `ScriptCommentEntry`, siehe dessen Klassenkommentar für Details; bestehende Diff/Merge/Mod-Compat-Tests bleiben davon unberührt. |
| `ParadoxModTool.ModCompat` | **Funktionsfähig** | Mod-Kompatibilitäts-Checker (Abschnitt 8): `ModCompatChecker.FindCommonlyModifiedFiles` (Übersicht gemeinsam betroffener Dateien), `ComputeParallelDiff` (paralleler Diff je Mod gegen Vanilla) und `AutoMergeAgainstVanilla` (Stufe 1 — automatisches Zusammenführen nicht überlappender Änderungen, überlappende Stellen als `ModCompatConflict` mit beiden Werten zur manuellen Entscheidung). Baut bewusst auf `ParadoxModTool.VanillaDiff` auf (derselbe 3-Wege-Vergleich wie beim Vanilla-Update-Merge, nur mit Vanilla/Mod A/Mod B statt Vanilla-alt/Vanilla-neu/Mod) — inkl. Tests. Die alte `ModCompatPlaceholder.cs` ist überholt und kann gelöscht werden. Stufe 2 (weitergehende automatische semantische Zusammenführung) ist noch nicht umgesetzt. Neu (Teilprojekt B, Mod-Kompatibilität): Kompatibilitätsset (`CompatSet`/`CompatSetStore`, unabhängig vom Playset des Spiels), Überschreib-Regeltabelle je Spiel (`OverrideRules`, Standard nach dem EU-V-Wiki in `profiles/eu5.json`, Spielebene in `games\<id>\compat\rules.json`), Präfix-Erkennung für `INJECT:`/`REPLACE:` (`OverrideKey`), Beitragsindex (`ContributionIndex`) und `CompatAnalyzer` (Datei- und Schlüsselkonflikte, Ladereihenfolge, Projektbeziehungen, Sinnhaftigkeitscheck für die eigene Mod, Markdown-Bericht `CompatReport`). `ModCompatChecker` bleibt für die spätere Stufe C (Kompatch-Erzeuger). Oberfläche: Fenster `ModCompatWindow` (Menü "Mod-Kompatibilität", Mods-Fenster). |
| `ParadoxModTool.Tools` | **Funktionsfähig, neu in Runde 7** | „Leerzeichen-Bereinigung & Formatierung" (Konzept Abschnitt 10): `WhitespaceCleaner.Clean` (zeilenbasiert, dateityp-unabhängig — Zeilenende-Whitespace entfernen, 2+ Leerzeilen auf 1 reduzieren, NIE auf 0) und `ScriptPrettyPrinter.TryFormat` (baut auf `ParadoxModTool.VanillaDiff.ScriptDocument`/`ScriptWriter` auf, fängt `ScriptParseException` ab und liefert ein `ScriptFormatResult` mit `Success`/`ErrorMessage` statt eine Ausnahme weiterzureichen — ein Fehlschlag ist hier der erwartete Fall bei Nicht-Skript-Dateien, kein Ausnahmezustand). Inkl. Tests, darunter ein Regressionstest für den in diesem Zuge behobenen `ScriptCommentEntry`-Bug (siehe `ParadoxModTool.VanillaDiff`-Zeile und Runde-7-Absatz oben). Die alte `ToolsPlaceholder.cs` ist überholt und wurde gelöscht. |
| `ParadoxModTool.LaunchControl` | **Funktionsfähig, neu in Runde 8, erweitert in Runde 9** | „Spiel starten" (Konzept Abschnitt 8): `GameLauncher.Launch(executablePath, arguments, starter)` prüft Pfad-Leere/Dateiexistenz selbst (liefert dafür je eine eigene, verständliche Fehlermeldung statt einer rohen Ausnahme) und startet danach über `ProcessStartInfo`/`Process.Start` (`UseShellExecute = true`, `WorkingDirectory` = Ordner der .exe); `BuildStartInfo` ist als eigene, reine Methode ausgelagert, `LaunchResult.Ok()`/`.Failure(message)` kapseln das Ergebnis (Muster wie `ScriptFormatResult`). Bewusst UNABHÄNGIG von `ParadoxModTool.GameProfiles` (nimmt nur einfache Zeichenketten entgegen) — dieselbe Entkopplung wie bei `.UndoRedo`/`.Session`. Der optionale `starter`-Delegat (Standard `Process.Start`) macht das Modul trotz eines in dieser Umgebung nicht automatisiert testbaren echten Prozessstarts vollständig testbar — inkl. Tests (Fehlerfälle, übergebene `ProcessStartInfo`-Werte, `Win32Exception`-Abfangen). Die alte `LaunchControlPlaceholder.cs` ist überholt und wurde gelöscht. Neu (Runde 9, Nutzer-Feedback „Playset direkt auswählen können"): `PlaysetInfo` (reines Anzeige-Datenmodell: Name/IsActive/ModCount/EnabledModCount/DisplayText) und `PlaysetStore.ListPlaysets`/`.SetActivePlayset` lesen bzw. ändern `playsets.json` — bewusst über `System.Text.Json.Nodes.JsonNode` als veränderbares DOM statt eines festen POCO-Modells, damit unbekannte Felder (DLC-Liste, Mod-Ladereihenfolge, ...) beim Zurückschreiben erhalten bleiben, da diese Datei eine ECHTE, vom Spiel selbst verwaltete Konfigurationsdatei außerhalb des Mod-Projekts ist. `SetActivePlayset` setzt beim Zielplayset `"isActive": true`, entfernt das Feld bei allen anderen (die es zuvor trugen) und legt davor automatisch eine Sicherheitskopie unter `playsets.json.pmt-backup` an. **Unbestätigte Annahme (siehe „Bekannte Unsicherheiten" unten):** dass `"isActive"` tatsächlich das beim direkten .exe-Start geladene Playset bestimmt. Inkl. 9 weiterer Tests. |
| `ParadoxModTool.Explorer` | **Neu in Runde 10** | Verzeichnis-Übersicht (siehe Runde-10-Absatz oben für die volle Beschreibung): `ExplorerEntry` (ein gemeinsamer, `INotifyPropertyChanged`-Knotentyp für Wurzeln/Ordner/Dateien, mit Lazy-Loading-Platzhalter-Kindern), `FolderScanner` (liest unmittelbare Kinder eines Ordners, robust gegen einzelne nicht lesbare Einträge), `PathNormalization.NormalizeKey` (Schlüssel für Farb-/Favoriten-Zuordnung, nach demselben Prinzip wie `EditorPane.PathsEqual`), `ExplorerColorStore`/`ExplorerFavoritesStore` (JSON unter `%APPDATA%\ParadoxModTool\`, fehlende/beschädigte Datei liefert leeres Ergebnis statt Ausnahme), `FolderCategoryColorResolver` (reine, testbare Zuordnung Ordnername → Standardfarbe aus `GameProfile.FolderCategories`), `ExplorerPanel` (das `UserControl` selbst: `TreeView` mit `HierarchicalDataTemplate`, Rechtsklick-Kontextmenü, Favoritenliste), `NewFileWindow` (kleiner Dialog für „Neue Datei anlegen…"). `ExplorerPanel` führt selbst keine Datei-Schreibvorgänge aus (meldet stattdessen `OpenFileRequested`/`NewFileRequested` an `MainWindow`, analog zu `EditorPane`s „Requested"-Events), damit „Neue Datei anlegen" über den programmweiten Aktionsverlauf läuft. Wie `ParadoxModTool.App` referenziert dieses Projekt zusätzlich `UseWindowsForms` (für `ColorDialog`/`FolderBrowserDialog`), wodurch dieselbe `CS0104`-Namenskollision wie dort auftritt — betrifft `HexColorConverter.cs` (`Color`/`ColorConverter`/`Brushes`) sowie, wie sich beim ersten Build des Nutzers tatsächlich zeigte, `ExplorerPanel.xaml.cs` selbst (`public partial class ExplorerPanel : UserControl` — `UserControl` existiert ebenfalls in `System.Windows.Forms`), jeweils per Alias aufgelöst. Inkl. 5 Testdateien in `ParadoxModTool.Explorer.Tests` für die fünf WPF-unabhängigen Klassen. Neu (Runde 10, 2. Rückmeldung — siehe eigener Absatz oben für Details): Farbbalken-Layout-Bugfix, kleinere/abgerückte ◀/▶-Buttons + mehr Zeilenabstand, `GuessPreferredExtension` (Standard-Dateiendung je Zielordner), echte Löschfunktion (`DeleteRequested`, `Microsoft.VisualBasic.FileIO.FileSystem`), `NewFolderRequested` („Neuen Ordner anlegen…"), Favoriten jetzt als zweiter `TreeView` statt flacher `ListBox` (`FavoritesTree`, wiederverwendet dasselbe `HierarchicalDataTemplate`), Alt+F-Hotkey (`PinToFavorites`), Doppelklick öffnet im aktiven Arbeitsbereich (`ExplorerOpenTarget.Active`, `Entry_MouseDoubleClick`). Zusätzlich zu den bereits bekannten Namenskollisionen mussten `KeyEventArgs` und `MessageBox` per Alias aufgelöst werden (neu genutzt durch den Alt+F-Hotkey bzw. die Löschbestätigung). |
| `.GitIntegration`, `.SteamIntegration`, `.Localization` | **Nur Gerüst** | Leere Projekte mit Platzhalterklasse, damit die Solution-Struktur aus der Spezifikation vollständig steht — Implementierung folgt in den nächsten Schritten |

## Bekannte Unsicherheiten (bitte beim ersten Build gegenprüfen)

- Die NuGet-Paketversion für `AvalonEdit` (`6.3.0.90`) konnte ich nicht gegen
  nuget.org verifizieren, da diese Umgebung keinen Zugriff auf nuget.org hat.
  Falls `dotnet restore` die Version nicht findet, in den `.csproj`-Dateien von
  `ParadoxModTool.Editor` und `ParadoxModTool.Syntax` einfach auf die aktuell
  in NuGet angezeigte Version anpassen (rechte Maustaste im Projekt →
  „NuGet-Pakete verwalten" in Visual Studio geht am schnellsten).
- Alles hier ist ungetestet gegen einen echten Compiler — ich habe die
  Regex-Muster und den C#-Code manuell gegen deine echten Log-Zeilen
  durchgespielt, aber ein `dotnet build`/`dotnet test` auf deiner Maschine ist
  trotzdem der erste sinnvolle nächste Schritt.
- Beim Suchen-&-Ersetzen-Ausbauschritt (Abschnitt 5b) konnte ich einzelne
  AvalonEdit-/WinForms-APIs (`TextEditor.Select`, `TextDocument.Text`-Setter,
  `System.Windows.Forms.FolderBrowserDialog`, `UseWindowsForms` im `.csproj`)
  nicht wie sonst gegen die echte Bibliotheksquelle gegenprüfen, da der
  Webzugriff dieser Umgebung währenddessen vorübergehend nicht verfügbar war.
  Es handelt sich um seit Jahren stabile, sehr verbreitete Standard-APIs, daher
  ist das Risiko gering — falls `dotnet build` dort dennoch etwas beanstandet,
  bitte die genaue Fehlermeldung schicken, dann korrigiere ich gezielt.
- **Runde 9, Playset-Auswahl:** Ich konnte über die EU5-Wiki (`https://eu5.paradoxwikis.com/Modding`)
  sowie mehrere Websuchen NICHT bestätigen, dass das Setzen von `"isActive": true` auf einen Eintrag
  in `playsets.json` tatsächlich das beim direkten Start von `eu5.exe` (ohne den Paradox-Launcher)
  geladene Playset bestimmt — dazu gibt es schlicht keine öffentliche Dokumentation. Die Annahme
  stützt sich ausschließlich auf die Struktur deiner bereitgestellten Datei (genau ein Playset trug
  `isActive: true`, alle anderen ließen das Feld weg) und ist plausibel, aber unbewiesen. Bitte beim
  ersten Test gezielt gegenprüfen, ob das im Programm gewählte Playset tatsächlich im Spiel landet
  — `PlaysetStore.SetActivePlayset` legt dafür vor jeder Änderung automatisch eine Sicherheitskopie
  unter `playsets.json.pmt-backup` an, falls etwas nicht wie erwartet funktioniert.
- **Runde 10, Verzeichnis-Übersicht:** `MainWindow.ComputeGameFilesGuess` schlägt für den „Ordner
  hinzufügen…"-Dialog den Elternordner des „binaries"-Ordners der Spiel-.exe vor (rein als
  Startordner-Vorauswahl, KEINE automatisch hinzugefügte Wurzel). Anders als beim Mod-Ordner-Pfad
  (`defaultModRootTemplate`, über die Wiki bestätigt) gibt es dafür keine öffentlich dokumentierte
  feste Konvention, wo die eigentlichen Spieldateien relativ zur .exe liegen — bitte beim ersten
  Test einmal „Ordner hinzufügen…" öffnen und prüfen, ob die Vorauswahl grob stimmt oder komplett
  daneben liegt (schadet in letzterem Fall nichts, der Nutzer navigiert dann einfach manuell weiter).
- **Runde 10, zweite Rückmeldung — `Microsoft.VisualBasic`:** für „Neuen Ordner anlegen…"
  (`ExplorerPanel`) sowie das Umbenennen der 4 frei konfigurierbaren Suchen-&-Ersetzen-
  Schnellzugriff-Buttons (`SearchReplaceWindow`) wird `Microsoft.VisualBasic.Interaction.InputBox`
  verwendet, für die neue Löschfunktion `Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile`/
  `.DeleteDirectory` (Papierkorb statt endgültigem Löschen). Beides ist Teil der .NET-Windows-
  Desktop-Runtime (automatisch verfügbar, sobald `UseWPF`/`UseWindowsForms` aktiv ist — kein
  zusätzliches NuGet-Paket, dieselbe „nur zertifizierte/unbedenkliche Bibliotheken"-Anforderung aus
  dem Konzept), eine seit Jahren gängige, weit verbreitete Technik in .NET-Core-WPF-Anwendungen —
  ich konnte das in dieser Umgebung aber mangels echtem Compiler nicht gegen einen tatsächlichen
  Build verifizieren. Falls `dotnet build` hier etwas beanstandet, bitte die genaue Fehlermeldung
  schicken.
- **Runde 10, zweite Rückmeldung — Werkzeugleisten-Button „Verzeichnis-Übersicht" angeblich nicht
  sichtbar:** der Code für diesen `ToggleButton` existierte bereits unverändert korrekt in
  `MainWindow.xaml` (zweiter Eintrag der Werkzeugleiste, `IsChecked="True"`) und konnte in dieser
  Cloud-Umgebung ohne echte Bildschirmanzeige nicht als tatsächlich fehlerhaft nachvollzogen
  werden. Eine denkbare Erklärung: WPFs `ToolBar`-Steuerelement blendet Einträge, die bei der
  aktuellen Fensterbreite nicht mehr passen, automatisch in ein Overflow-Menü (Chevron ganz
  rechts) aus — bei vielen Werkzeugleisten-Einträgen plus zwei breiten `ComboBox`en (260+220px)
  in diesem Fenster ist das bei geringerer Fensterbreite/höherer Skalierung durchaus möglich, auch
  wenn der Button als ZWEITER Eintrag eigentlich zu den zuerst sichtbar bleibenden gehören sollte.
  Da sich das ohne echten Test nicht abschließend klären ließ, gibt es jetzt zusätzlich die
  gleichwertige, garantiert erreichbare Checkbox „Verzeichnis-Übersicht anzeigen" im
  „Einstellungen"-Menü (siehe Runde-10-2.-Rückmeldung-Absatz oben) — bitte beim nächsten Test kurz
  prüfen, ob der Werkzeugleisten-Button bei voller Fensterbreite sichtbar ist, und falls nicht, ob
  ein Fenster-Breiter-Ziehen ihn wieder erscheinen lässt (würde die Overflow-Theorie bestätigen).

## Ordnerstruktur

```
profiles/                 Spielprofile (JSON) — community-editierbar, siehe Spezifikation Abschnitt 4
syntax-templates/          AvalonEdit-Highlighting-Definitionen (XSHD) — community-editierbar, Abschnitt 15
locales/                    Übersetzungsdateien (JSON), bisher nur en/de als Beispiel angelegt
samples/                    Beispieldateien zum Ausprobieren (keine echten Mod-Inhalte)
src/                        Alle Fachmodule, siehe Tabelle oben
tests/                      Unit-Tests (bisher nur für ParadoxModTool.ErrorLog)
```

## Release erstellen (Selbst-Update)

PMT aktualisiert sich selbst aus GitHub-Releases von `flogi1/Paradox-Modding-Tool`. Jedes Release enthält die ZIP, `update-manifest.json`
und `update-manifest.json.sig`; PMT installiert nur Pakete mit gültiger Signatur.

**Einmalig:**

1. Schlüssel erzeugen: `dotnet run --project tools/ParadoxModTool.ReleaseSigner -- keygen`
2. Den ausgegebenen öffentlichen Schlüssel in `src/ParadoxModTool.Updates/UpdateKeys.cs` eintragen und committen.
3. **Sichern:** `%AppData%\ParadoxModTool-Release\release-key.p8` und das Passwort offline aufbewahren (USB-Stick, Passwortmanager).
   Nie ins Repository. Geht der Schlüssel verloren, bekommen bestehende Installationen keine automatischen Updates mehr.
4. `gh auth login` und `git remote add origin https://github.com/flogi1/Paradox-Modding-Tool.git`.

**Pro Release:** `release.bat` doppelklicken. Das Skript fragt Version, Notizen und das Schlüsselpasswort, baut, testet, signiert und
fragt vor Commit, Tag, Push und Upload noch einmal nach.

**Öffentlicher Verlauf:** Der lokale `master` wird nie gepusht. GitHub bekommt pro Release genau einen Commit "Release v<Version>" (lokaler
Branch `public`, auf GitHub `main`) mit dem Dateistand ohne `docs/` und der verborgenen GitHub-Adresse als Autor.

**Lokal testen ohne GitHub:** `release.bat -LocalFeed C:\pmt-feed` baut ein Debug-Paket in einen lokalen Feed. Ein Debug-Build von PMT mit
der Umgebungsvariable `PMT_UPDATE_FEED=C:\pmt-feed` holt Updates dann von dort. `PMT_UPDATE_SKIP_MARKER=1` lässt die neue Version kein
Startsignal schreiben, damit sich der Rückfall prüfen lässt.
