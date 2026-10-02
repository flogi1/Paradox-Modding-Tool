using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

/// <summary>Text einer Aktion für alle Ansichten: Einträge als Skripttext, Ergebnis nach einer Entscheidung, ausgerichtete Zeilen.</summary>
public static class ActionText
{
    public static string Render(IReadOnlyList<ScriptEntry> entries) =>
        entries.Count == 0 ? string.Empty : ScriptWriter.Write(new ScriptBlock { Entries = entries.ToList() }, preferSourceVerbatim: true).Replace("\r\n", "\n");

    /// <summary>Was die Entscheidung an dieser Stelle einsetzt. Eigener Text, der nicht lesbar ist, ergibt nichts.</summary>
    public static IReadOnlyList<ScriptEntry> ResultEntries(MergeAction action, MergeDecision decision)
    {
        switch (decision.Kind)
        {
            case DecisionKind.TakeA:
                return action.A;
            case DecisionKind.TakeB:
                return action.B;
            case DecisionKind.TakeVanilla:
                return action.Vanilla;
            default:
                try
                {
                    return ScriptDocument.Parse(decision.ManualText ?? string.Empty).Entries;
                }
                catch (ScriptParseException)
                {
                    return Array.Empty<ScriptEntry>();
                }
        }
    }

    /// <summary>"A", "B" oder "Vanilla" für die Anzeige einer ganzen Datei.</summary>
    public static string SideName(DecisionKind kind) => kind switch
    {
        DecisionKind.TakeA => "A",
        DecisionKind.TakeB => "B",
        _ => CompatchTexts.ColumnVanilla,
    };

    /// <summary>Wie <see cref="Render"/>, auf Wunsch ohne Kommentarzeilen (<see cref="StripCommentLines"/>).</summary>
    public static string Render(IReadOnlyList<ScriptEntry> entries, bool hideComments) =>
        hideComments ? StripCommentLines(Render(entries)) : Render(entries);

    /// <summary>
    /// Entfernt Zeilen, die (nach Leerraum) mit <c>#</c> beginnen: auskommentierte Blöcke und Kommentarzeilen. Kommentare am Zeilenende
    /// bleiben, weil sie zu einer echten Zeile gehören. Nur für die Anzeige; Entscheidungen und Patch nutzen den vollen Text.
    /// </summary>
    public static string StripCommentLines(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }

        var kept = text.Replace("\r\n", "\n").Split('\n').Where(line => !line.TrimStart().StartsWith('#')).ToList();
        return string.Join("\n", kept);
    }

    /// <summary>Zeilen A | Ergebnis | B (| Vanilla) der Aktion selbst (nicht des ganzen Schlüssels).</summary>
    public static IReadOnlyList<AlignedRow> Rows(MergeAction action, MergeDecision decision, bool withVanilla, bool hideComments = false) =>
        EntryAligner.Align(action.A, ResultEntries(action, decision), action.B, withVanilla ? action.Vanilla : null, hideComments);

    /// <summary>Zeilen für eine ganze Datei: Pfad in A | gewählte Seite | Pfad in B.</summary>
    public static IReadOnlyList<AlignedRow> WholeFileRows(AddressInfo info, MergeDecision decision) =>
        ColumnAligner.Align(info.A.RelativePath ?? string.Empty, CompatchTexts.WholeFileChoice(SideName(decision.Kind)), info.B.RelativePath ?? string.Empty, null);

    /// <summary>Mini-Diff für Karten: höchstens <paramref name="max"/> Zeilen, dazu die Zahl der weggelassenen.</summary>
    public static (IReadOnlyList<AlignedRow> Rows, int Hidden) MiniRows(MergeAction action, MergeDecision decision, int max, bool hideComments = false)
    {
        var rows = Rows(action, decision, withVanilla: false, hideComments);
        return rows.Count <= max ? (rows, 0) : (rows.Take(max).ToList(), rows.Count - max);
    }
}
