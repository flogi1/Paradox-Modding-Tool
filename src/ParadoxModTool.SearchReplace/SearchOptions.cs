namespace ParadoxModTool.SearchReplace;

/// <summary>
/// Optionen für Suchen/Ersetzen (Abschnitt 5b) — gemeinsam für "im aktuellen Dokument" und
/// "in Dateien" (Ordnerstruktur) verwendet, damit beide Modi über denselben Funktionskasten
/// laufen können ("Suchen und Ersetzen und Suchen in Dateien kann in einem Funktionskasten
/// vereinheitlicht werden").
/// </summary>
public sealed class SearchOptions
{
    /// <summary>Groß-/Kleinschreibung beachten. Bei false (Standard) wird ordinal, aber ohne Berücksichtigung der Groß-/Kleinschreibung verglichen.</summary>
    public bool MatchCase { get; set; }

    /// <summary>
    /// Nur ganze Wörter treffen (Zeichen vor/nach dem Treffer dürfen kein Buchstabe, keine Ziffer
    /// und kein Unterstrich sein). Sinnvoll vor allem für einzeilige Suchbegriffe — bei einem
    /// mehrzeiligen Suchbegriff (Codeblock-Ersetzung, siehe NPP-Toolbucket-Vorbild) bleibt die
    /// Prüfung an Anfang/Ende des gesamten Blocks trotzdem funktionsfähig.
    /// </summary>
    public bool WholeWord { get; set; }

    /// <summary>
    /// Ignoriert Unterschiede in Whitespace (Leerzeichen, Tabs, Zeilenumbrüche, Anzahl Leerzeilen)
    /// beim Suchen — Nutzer-Feedback: "Manchmal hat man [...] den Fall, dass einzelne Codeblöcke
    /// anders formatiert sind. [...] Wenn aber beide ersetzt werden sollen, muss man nach beiden
    /// suchen." Betrifft NUR das Finden von Treffern; eine Ersetzung setzt trotzdem exakt den in
    /// der jeweiligen Datei tatsächlich gefundenen (und damit ggf. unterschiedlich formatierten)
    /// Textbereich durch den Ersetzungstext, ändert also nicht die Formatierung des UMGEBENDEN
    /// Codes und "vereinheitlicht" nicht rückwirkend andere Treffer.
    /// </summary>
    public bool IgnoreFormatting { get; set; }
}
