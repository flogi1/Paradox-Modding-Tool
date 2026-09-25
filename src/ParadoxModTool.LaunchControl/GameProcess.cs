using System.ComponentModel;
using System.Diagnostics;

namespace ParadoxModTool.LaunchControl;

/// <summary>Laufende Prozesse der Spiel-Exe (Name = Dateiname ohne Endung, wie bei <c>SteamClientStatus</c>).</summary>
public static class GameProcess
{
    /// <summary>Startzeiten (UTC) der laufenden Prozesse; ist die Startzeit nicht lesbar, zählt der Prozess mit <see cref="DateTime.MinValue"/>.</summary>
    public static IReadOnlyList<DateTime> RunningSince(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return Array.Empty<DateTime>();
        }

        var result = new List<DateTime>();
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executablePath)))
        {
            using (process)
            {
                try
                {
                    result.Add(process.StartTime.ToUniversalTime());
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    result.Add(DateTime.MinValue);
                }
            }
        }

        return result;
    }
}

/// <summary>
/// Merkt sich einen Start mit <c>-debug_mode</c> durch PMT (Spec: nur dann weiß PMT, dass das Spiel im Debug-Modus läuft; Plan 4 Ruling 8). Über
/// Prozessname und Zeit, nicht die PID, weil Steam das Spiel neu starten kann.
/// </summary>
public sealed class DebugLaunchTracker
{
    private string? _processName;
    private DateTime _launchedUtc;

    public void Launched(string executablePath, string arguments, DateTime utcNow)
    {
        _processName = HasDebugFlag(arguments) ? Path.GetFileNameWithoutExtension(executablePath) : null;
        _launchedUtc = utcNow;
    }

    public void Forget() => _processName = null;

    /// <summary>Ein laufender Prozess derselben Exe ist nach dem Debug-Start gestartet (5 s Spielraum).</summary>
    public bool IsDebugSession(string executablePath, IEnumerable<DateTime> runningSinceUtc) =>
        _processName is not null
        && string.Equals(Path.GetFileNameWithoutExtension(executablePath), _processName, StringComparison.OrdinalIgnoreCase)
        && runningSinceUtc.Any(started => started >= _launchedUtc.AddSeconds(-5));

    public static bool HasDebugFlag(string arguments) =>
        arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("-debug_mode", StringComparer.OrdinalIgnoreCase);

    /// <summary>Argumente der ersten Startoption des Profils mit <c>-debug_mode</c> (Plan 4 Ruling 7); null, wenn es keine gibt.</summary>
    public static string? DebugArguments(IEnumerable<string> optionArgs) => optionArgs.FirstOrDefault(HasDebugFlag);
}

/// <summary>
/// Von PMT neu angelegte Mod-Dateien (Review Plan 4, Spec-Testpunkt 1): Das Spiel lädt eine neue Datei im Debug-Modus nicht nach, erst nach einem
/// Neustart. Eine Datei braucht den Neustart, solange kein laufender Spielprozess nach ihrem Anlegen gestartet wurde.
/// </summary>
public sealed class NewFileTracker
{
    private readonly Dictionary<string, DateTime> _created = new(StringComparer.OrdinalIgnoreCase);

    public void Created(string path, DateTime utcNow) => _created[path] = utcNow;

    /// <summary>
    /// Noch vorhandene Dateien, die nach dem letzten Start des Spiels angelegt wurden, in der Reihenfolge des Anlegens. Eine nicht lesbare Startzeit
    /// (<see cref="DateTime.MinValue"/>) zählt als früher Start. Leer, wenn das Spiel nicht läuft.
    /// </summary>
    public IReadOnlyList<string> NeedRestart(IEnumerable<DateTime> runningSinceUtc, Func<string, bool> fileExists)
    {
        var starts = runningSinceUtc.ToList();
        if (starts.Count == 0)
        {
            return Array.Empty<string>();
        }

        var lastStart = starts.Max();
        return _created.Where(entry => entry.Value >= lastStart && fileExists(entry.Key)).Select(entry => entry.Key).ToList();
    }
}
