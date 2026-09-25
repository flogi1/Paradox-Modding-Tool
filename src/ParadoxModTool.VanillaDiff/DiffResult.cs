namespace ParadoxModTool.VanillaDiff;

public enum DiffKind
{
    Unchanged,
    Added,
    Removed,
    Modified
}

/// <summary>
/// Optionen für <see cref="ScriptDiffer"/>. Der strukturelle Diff arbeitet grundsätzlich
/// auf dem AST und vergleicht daher nie Whitespace, Tabs oder Absätze — das entspricht der
/// Vorgabe aus Abschnitt 7, dass diese standardmäßig nicht eingefärbt werden. Die Optionen
/// hier steuern das Verhalten der UI-Anzeige und der Merge-Funktion, nicht den strukturellen
/// Vergleich selbst.
/// </summary>
public sealed class DiffOptions
{
    /// <summary>
    /// Wenn true, kann die UI zusätzlich eine informative Roh-Text-Whitespace-Überlagerung
    /// einblenden (z. B. um zu zeigen, wo sich nur die Formatierung geändert hat). Der
    /// eigentliche strukturelle Diff bleibt davon unberührt. Standard: aus, wie in Abschnitt 7
    /// gefordert ("Whitespace/Tabs/Absätze sind standardmäßig nicht eingefärbt").
    /// </summary>
    public bool HighlightWhitespace { get; init; } = false;

    /// <summary>
    /// Grundregel aus Abschnitt 7: Beim Vereinheitlichen/Schreiben wird die Vanilla-
    /// Formatierung übernommen, sofern kein triftiger Grund für die Mod-Formatierung vorliegt.
    /// Diese erste Fassung von <see cref="ScriptWriter"/> schreibt ohnehin immer einheitlich
    /// neu — das Flag ist für spätere, formatierungserhaltende Schreibvarianten vorgesehen.
    /// </summary>
    public bool PreferVanillaFormatting { get; init; } = true;
}

/// <summary>
/// Das Diff-Ergebnis für einen einzelnen Eintrag (Anweisung oder Listenelement) innerhalb
/// eines Blocks. Bei <see cref="DiffKind.Modified"/> und wenn beide Seiten Blockwerte mit
/// demselben Schlüssel sind, enthält <see cref="Children"/> den rekursiven Diff der
/// verschachtelten Ebene — das bildet die "nebeneinander gelegte Dateiarchitektur" aus
/// Abschnitt 7 als Baum ab, nicht nur als Zeile-für-Zeile-Vergleich.
/// </summary>
public sealed class EntryDiff
{
    public required DiffKind Kind { get; init; }

    /// <summary>Der Eintrag auf der linken/alten Seite. Null bei <see cref="DiffKind.Added"/>.</summary>
    public ScriptEntry? Left { get; init; }

    /// <summary>Der Eintrag auf der rechten/neuen Seite. Null bei <see cref="DiffKind.Removed"/>.</summary>
    public ScriptEntry? Right { get; init; }

    /// <summary>Nur gesetzt bei <see cref="DiffKind.Modified"/> mit beidseitig verschachtelten Blöcken.</summary>
    public IReadOnlyList<EntryDiff>? Children { get; init; }
}
