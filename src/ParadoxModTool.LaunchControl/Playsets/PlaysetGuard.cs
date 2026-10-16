namespace ParadoxModTool.LaunchControl;

/// <summary>
/// Schutz vor Schreibzugriffen: nie bei laufendem Spiel, bei Launcher-Datenbanken auch nie bei laufendem Launcher (beide schreiben die Dateien selbst und
/// würden die Änderung überschreiben oder beschädigen), und Prüfung des Playsets vor dem Speichern.
/// </summary>
public sealed class PlaysetGuard
{
    /// <summary>Prozessname des Paradox-Launchers (Datei "Paradox Launcher.exe", ohne Endung).</summary>
    public static readonly string[] LauncherProcessNames = { "Paradox Launcher" };

    private readonly Func<bool> _isGameRunning;
    private readonly Func<bool> _isLauncherRunning;

    public PlaysetGuard(Func<bool> isGameRunning, Func<bool> isLauncherRunning)
    {
        _isGameRunning = isGameRunning;
        _isLauncherRunning = isLauncherRunning;
    }

    /// <summary>Prüft, ob geschrieben werden darf; läuft das Spiel oder (bei Datenbank-Spielen) der Launcher, wird nichts geschrieben.</summary>
    public PlaysetResult CheckWritable(bool usesLauncherDatabase)
    {
        if (_isGameRunning())
        {
            return new PlaysetResult(PlaysetStatus.GameRunning);
        }

        if (usesLauncherDatabase && _isLauncherRunning())
        {
            return new PlaysetResult(PlaysetStatus.LauncherRunning);
        }

        return PlaysetResult.Ok;
    }

    /// <summary>
    /// Prüft Name und Mods: leerer oder doppelter Name (ohne Beachtung der Groß-/Kleinschreibung, das Playset selbst zählt nicht), ein fehlender Ordner einer
    /// eingeschalteten Mod, dieselbe Mod zweimal. Ausgeschaltete Mods mit fehlendem Ordner blockieren nicht.
    /// </summary>
    public static PlaysetResult Validate(PlaysetData playset, IEnumerable<PlaysetData> others, Func<string, bool> modFolderExists)
    {
        if (string.IsNullOrWhiteSpace(playset.Name))
        {
            return new PlaysetResult(PlaysetStatus.InvalidName);
        }

        if (others.Any(o => !ReferenceEquals(o, playset) && o.Id != playset.Id && string.Equals(o.Name, playset.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return new PlaysetResult(PlaysetStatus.DuplicateName, playset.Name);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in playset.Mods)
        {
            if (mod.Path.Length > 0 && !seen.Add(NormalizePath(mod.Path)))
            {
                return new PlaysetResult(PlaysetStatus.DuplicateMod, mod.Path);
            }

            if (mod.Enabled && !modFolderExists(mod.Path))
            {
                return new PlaysetResult(PlaysetStatus.ModMissing, ModLabel(mod));
            }
        }

        return PlaysetResult.Ok;
    }

    /// <summary>Wie die Mod in einer Meldung heißt: der Pfad, bei einer Mod ohne Ordner ihr Anzeigename.</summary>
    public static string ModLabel(PlaysetModEntry mod) => mod.Path.Length > 0 ? mod.Path : mod.DisplayName ?? mod.Path;

    /// <summary>Ordner in einheitlicher Schreibweise (Schrägstriche, ohne Schlusstrenner) zum Vergleichen.</summary>
    public static string NormalizePath(string path) => path.Replace('\\', '/').TrimEnd('/');
}
