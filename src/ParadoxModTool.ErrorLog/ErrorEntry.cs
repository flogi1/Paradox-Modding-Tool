namespace ParadoxModTool.ErrorLog;

/// <summary>
/// Ein erkannter Fehler. <c>FilePath</c>/<c>FileLine</c> stammen aus dem Log (Pfad ohne Phasenordner und ohne Mod, siehe SourceResolver).
/// <c>Lines</c> enthält alle Log-Zeilen des Eintrags (mehrere bei zusammengeführten GUI-Meldungen).
/// </summary>
public sealed class ErrorEntry
{
    public const string UnmatchedCode = "???";

    public ErrorKind? Kind { get; init; }

    public string Code => Kind?.Code ?? UnmatchedCode;

    public ErrorSeverity Severity => Kind?.Severity ?? ErrorSeverity.Info;

    public required string Message { get; set; }

    public string? Name { get; init; }

    public string? FilePath { get; init; }

    public int? FileLine { get; init; }

    public string? Detail { get; init; }

    /// <summary>Kandidaten aus angehängten "Possible match"-Zeilen, in Log-Reihenfolge, ohne Doppelte.</summary>
    public List<string> Candidates { get; } = new();

    public required LogLine Line { get; init; }

    public List<LogLine> Lines { get; } = new();

    public bool HasFileReference => FilePath is not null && FileLine is not null;
}
