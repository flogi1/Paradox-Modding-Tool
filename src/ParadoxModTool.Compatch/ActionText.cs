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

    /// <summary>
    /// Zeilen für eine ganze Datei: Inhalt in A | Inhalt der gewählten Seite | Inhalt in B (| Vanilla), zeilenweise ausgerichtet (siehe <see cref="WholeFileText"/>).
    /// </summary>
    public static IReadOnlyList<AlignedRow> WholeFileRows(AddressInfo info, MergeDecision decision, bool withVanilla)
    {
        var a = WholeFileText.Of(info.A);
        var b = WholeFileText.Of(info.B);
        var vanilla = WholeFileText.Of(info.Vanilla);
        var result = decision.Kind switch
        {
            DecisionKind.TakeA => a,
            DecisionKind.TakeB => b,
            DecisionKind.TakeVanilla => vanilla,
            _ => string.Empty,
        };
        return ColumnAligner.Align(a, result, b, withVanilla ? vanilla : null);
    }

    /// <summary>
    /// Zeilen für einen Schlüssel mit Form "Ganze Datei": die Datei des Schlüssels in A | die Patch-Datei, wie sie geschrieben wird (aus <paramref name="prepared"/>;
    /// ohne Vorschau die Datei, die das Spiel ohne Patch lädt) | die Datei in B (| Vanilla).
    /// </summary>
    public static IReadOnlyList<AlignedRow> KeyFileRows(AddressInfo info, PreparedWrite? prepared, bool withVanilla)
    {
        var path = PatchWriter.Norm(PatchPlanner.WholeFilePath(info));
        var change = prepared?.Changes.FirstOrDefault(c => c.Kind != FileChangeKind.Delete && PatchWriter.Norm(c.RelativePath) == path);
        var result = change?.PreviewText ?? WholeFileText.Of(info.NoPatch);
        return ColumnAligner.Align(WholeFileText.Of(info.A), result, WholeFileText.Of(info.B), withVanilla ? WholeFileText.Of(info.Vanilla) : null);
    }

    /// <summary>Mini-Diff für Karten: höchstens <paramref name="max"/> Zeilen, dazu die Zahl der weggelassenen.</summary>
    public static (IReadOnlyList<AlignedRow> Rows, int Hidden) MiniRows(MergeAction action, MergeDecision decision, int max, bool hideComments = false)
    {
        var rows = Rows(action, decision, withVanilla: false, hideComments);
        return rows.Count <= max ? (rows, 0) : (rows.Take(max).ToList(), rows.Count - max);
    }
}
