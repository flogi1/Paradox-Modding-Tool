namespace ParadoxModTool.SearchReplace;

/// <summary>Ein einzelner Suchtreffer innerhalb eines Textes (Abschnitt 5b).</summary>
public sealed class TextMatch
{
    public TextMatch(int offset, int length, int lineNumber, string lineText, string matchedText)
    {
        Offset = offset;
        Length = length;
        LineNumber = lineNumber;
        LineText = lineText;
        MatchedText = matchedText;
    }

    /// <summary>Zeichenposition des Treffers im Gesamttext (0-basiert) — Grundlage für Markieren/Springen im Editor.</summary>
    public int Offset { get; }

    public int Length { get; }

    /// <summary>1-basierte Zeilennummer, in der der Treffer BEGINNT — bei einem mehrzeiligen Treffer (Blockersetzung) nur die erste Zeile.</summary>
    public int LineNumber { get; }

    /// <summary>Text der Zeile, in der der Treffer beginnt — für die Trefferbeschreibung im Suchen-Fenster.</summary>
    public string LineText { get; }

    /// <summary>
    /// Der exakt gefundene Text (Nutzer-Feedback: "nur den Code, der tatsächlich gefunden
    /// wurde, anzeigen ... damit man es rauskopieren und verwenden kann") — bei einem
    /// mehrzeiligen Suchbegriff der komplette gefundene Block inkl. Zeilenumbrüchen, NICHT
    /// nur die erste Zeile wie <see cref="LineText"/>.
    /// </summary>
    public string MatchedText { get; }
}
