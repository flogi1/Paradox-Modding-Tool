namespace ParadoxModTool.ErrorLog;

/// <summary>Eine Logdatei eines Laufs: <c>Index</c> 0 ist <c>error.log</c> (letzter Lauf), 1 bis 5 sind <c>error.N.log</c> (frühere Läufe).</summary>
public sealed record LogRunFile(string Path, int Index, DateTime LastWriteUtc)
{
    public string FileName => System.IO.Path.GetFileName(Path);
}

/// <summary>Wo das Spiel seine Logs ablegt und wo PMT die Unterdrückungen speichert (Plan-Ruling 6).</summary>
public static class ErrorLogLocations
{
    /// <summary>Das Spiel behält neben <c>error.log</c> fünf frühere Läufe.</summary>
    public const int MaxPreviousRuns = 5;

    /// <summary>Der Ordner <c>logs</c> neben dem Mod-Ordner des Spiels; <see langword="null"/> ohne Vorlage.</summary>
    public static string? LogFolderFor(string? modRootTemplate)
    {
        if (string.IsNullOrWhiteSpace(modRootTemplate))
        {
            return null;
        }

        try
        {
            var modRoot = Environment.ExpandEnvironmentVariables(modRootTemplate.Trim()).TrimEnd('/', '\\');
            var userFolder = Path.GetDirectoryName(modRoot);
            return string.IsNullOrEmpty(userFolder) ? null : Path.Combine(userFolder, "logs");
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException)
        {
            return null;
        }
    }

    public static string RunFileName(int index) => index == 0 ? "error.log" : $"error.{index}.log";

    /// <summary>Die Spielläufe im Ordner, neuester zuerst; ein Lauf kann mehrere Dateien umfassen (<see cref="LogRunChain"/>).</summary>
    public static IReadOnlyList<LogRun> Runs(string? folder) =>
        LogRunChain.Group(Files(folder).Select(LogFileTimes.For).ToList(), CurrentRunStart(folder));

    /// <summary><c>debug.log</c> im Logordner; das Spiel legt es bei jedem Start neu an und wechselt es nicht mitten im Lauf.</summary>
    public static string? DebugLogPath(string? folder) => folder is null ? null : Path.Combine(folder, "debug.log");

    /// <summary>Start des laufenden bzw. letzten Spiels: erste Zeile von <c>debug.log</c> (lokale Zeit); <c>null</c> ohne <c>debug.log</c>.</summary>
    public static DateTime? CurrentRunStart(string? folder)
    {
        var path = DebugLogPath(folder);
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return LogFileTimes.For(new LogRunFile(path, -1, File.GetLastWriteTimeUtc(path))).First;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Die vorhandenen Logdateien <c>error.log</c> bis <c>error.5.log</c> im Ordner, nach Index.</summary>
    public static IReadOnlyList<LogRunFile> Files(string? folder)
    {
        var result = new List<LogRunFile>();
        if (folder is null || !Directory.Exists(folder))
        {
            return result;
        }

        for (var index = 0; index <= MaxPreviousRuns; index++)
        {
            var path = Path.Combine(folder, RunFileName(index));
            if (File.Exists(path))
            {
                result.Add(new LogRunFile(path, index, File.GetLastWriteTimeUtc(path)));
            }
        }

        return result;
    }

    /// <summary>Unterdrückungen je Spiel: <c>&lt;Spielordner&gt;\errorlog\suppressions.json</c>.</summary>
    public static string SuppressionsPath(string gameDirectory) => Path.Combine(gameDirectory, "errorlog", "suppressions.json");
}
