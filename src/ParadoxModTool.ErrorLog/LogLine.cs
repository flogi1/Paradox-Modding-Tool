namespace ParadoxModTool.ErrorLog;

/// <summary>
/// Eine Meldung aus dem Log. <c>Message</c> ist der Text ohne Hülle <c>[Zeit][quelle.cpp:N]:</c>; Folgezeilen ohne Hülle
/// (Block "Script system error!") sind mit "\n" angehängt. <c>IsContinuation</c> markiert eine Folgezeile ohne Vorgänger in derselben
/// Leseportion (siehe <see cref="LogReader.Append"/>).
/// </summary>
/// <remarks>
/// <c>LineNumber</c> zählt pro Datei. <c>Part</c> ist die Datei im Lauf (0 = älteste), weil das Spiel <c>error.log</c> mitten im Lauf
/// wechselt; <see cref="Order"/> ordnet Zeilen über alle Dateien eines Laufs. <c>GameDate</c> ist das Spieldatum, das Hearts of Iron IV
/// zwischen Uhrzeit und Quelle schreibt (<c>no_game_date</c> oder z. B. <c>1936.01.01.12</c>); <see langword="null"/> bei Zeilen ohne dieses Feld.
/// </remarks>
public sealed record LogLine(string Time, string Source, int? SourceLine, string Message, int LineNumber, bool IsContinuation = false, int Part = 0, string? GameDate = null)
{
    public long Order => ((long)Part << 32) | (uint)LineNumber;
}

/// <summary>Leseposition im Log: Byte-Offset, nächste Zeilennummer und die ersten Bytes der Datei (zur Rotationserkennung).</summary>
public sealed record LogPosition(long Offset, int NextLineNumber, string Head)
{
    public static LogPosition Start { get; } = new(0, 1, string.Empty);
}

/// <summary>
/// Ergebnis eines inkrementellen Lesevorgangs. <c>Restarted</c>: neuer Spiellauf, gelesen wurde ab Dateianfang. <c>Continued</c>: Das Spiel
/// hat <c>error.log</c> mitten im Lauf gewechselt; die Zeilen gehören zum selben Lauf (neuer <see cref="LogLine.Part"/>).
/// </summary>
public sealed record LogChunk(IReadOnlyList<LogLine> Lines, LogPosition Next, bool Restarted, bool Continued = false);
