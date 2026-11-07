namespace ParadoxModTool.Performance;

/// <summary>
/// Ein gespeicherter Messlauf: die Metadaten aus <c>run.json</c>; der Dump liegt als <see cref="DumpFileName"/> im selben Ordner
/// (<see cref="PerformanceRunStore.FolderOf"/>).
/// </summary>
/// <param name="Id">Ordnername des Laufs: <c>yyyyMMdd-HHmmss</c> plus vier zufällige Hex-Zeichen.</param>
/// <param name="Name">Vom Nutzer vergebener Name.</param>
/// <param name="GameId">Spielprofil-Id (z. B. <c>eu5</c>).</param>
/// <param name="ModKey">Mod-Id, oder <c>mod-</c> plus 8 Hex-Zeichen eines stabilen Hashes des Mod-Pfads (<see cref="PerformanceRunStore.ModKeyFor"/>).</param>
/// <param name="ModRoot">Wurzelordner der gemessenen Mod.</param>
/// <param name="PlaysetName">Name des verwendeten Playsets, falls bekannt.</param>
/// <param name="SaveName">Name des Spielstands ohne Endung, <c>null</c> wenn unbekannt.</param>
/// <param name="PlannedMonths">Geplante Messdauer in Monaten.</param>
/// <param name="CountedMonths">Tatsächlich gezählte Monate (wird nach dem Einlesen des Dumps nachgetragen).</param>
/// <param name="ModFilesHash">Hash der Moddateien zum Zeitpunkt der Messung (wird nachgetragen).</param>
/// <param name="LaunchArguments">Die Startparameter der Messung.</param>
/// <param name="CreatedUtc">Zeitpunkt des Anlegens (UTC).</param>
/// <param name="Note">Freie Notiz.</param>
public sealed record PerformanceRun(
    string Id,
    string Name,
    string GameId,
    string ModKey,
    string ModRoot,
    string? PlaysetName,
    string? SaveName,
    int? PlannedMonths,
    int? CountedMonths,
    string? ModFilesHash,
    string LaunchArguments,
    DateTime CreatedUtc,
    string Note)
{
    /// <summary>Dateiname der Dump-Kopie im Ordner des Laufs.</summary>
    public string DumpFileName => "dump.csv";
}
