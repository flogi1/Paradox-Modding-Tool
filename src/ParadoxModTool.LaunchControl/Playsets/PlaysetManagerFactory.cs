using System.Diagnostics;

namespace ParadoxModTool.LaunchControl;

/// <summary>Baut den <see cref="PlaysetManager"/> aus den Einstellungen des Hauptfensters (Pfad der Playset-Datei, Lade-Datei aus dem Spielprofil, Spiel-.exe).</summary>
public static class PlaysetManagerFactory
{
    /// <summary>
    /// <c>null</c>, wenn die Playset-Datei fehlt. <paramref name="playsetLoadFileName"/> ist <c>GameProfile.PlaysetLoadFile</c> (dieses Projekt kennt die Spielprofile
    /// bewusst nicht); bei playsets.json (EU V) wird sie ignoriert, das Spiel liest die Datei selbst.
    /// </summary>
    public static PlaysetManager? Create(string? playsetsFilePath, string? playsetLoadFileName, string? gameExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(playsetsFilePath) || !File.Exists(playsetsFilePath))
        {
            return null;
        }

        var gameFolder = Path.GetDirectoryName(Path.GetFullPath(playsetsFilePath))!;
        var modFolder = Path.Combine(gameFolder, "mod");
        var launcherGame = PlaysetStore.IsLauncherDatabase(playsetsFilePath);
        IPlaysetBackend backend = launcherGame ? new LauncherPlaysetBackend(playsetsFilePath, modFolder) : new JsonPlaysetBackend(playsetsFilePath);
        var loadFile = launcherGame && !string.IsNullOrWhiteSpace(playsetLoadFileName) ? Path.Combine(gameFolder, playsetLoadFileName) : null;
        var guard = new PlaysetGuard(() => GameProcess.IsRunning(gameExecutablePath), IsLauncherRunning);
        return new PlaysetManager(backend, guard, new PlaysetBackups(Path.Combine(gameFolder, "pmt-playset-backups")), loadFile, modFolder, Directory.Exists, DlcNameResolver.Load(gameExecutablePath));
    }

    private static bool IsLauncherRunning() =>
        PlaysetGuard.LauncherProcessNames.Any(name =>
        {
            var processes = Process.GetProcessesByName(name);
            foreach (var process in processes)
            {
                process.Dispose();
            }

            return processes.Length > 0;
        });
}
