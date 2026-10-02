using System.IO.Compression;

namespace ParadoxModTool.SteamIntegration;

/// <summary>Packt das von Valve heruntergeladene <c>steamcmd.zip</c> aus. Den Download selbst macht die App nach Rückfrage beim Nutzer.</summary>
public static class SteamCmdInstaller
{
    public const string DownloadUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";

    public const string ExecutableName = "steamcmd.exe";

    /// <summary>Der Standardordner im Programmordner des Nutzers: <c>%APPDATA%\ParadoxModTool\tools\steamcmd</c>.</summary>
    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ParadoxModTool", "tools", "steamcmd");

    /// <summary>
    /// Entpackt das Archiv in einen Nachbarordner, prüft, dass es <c>steamcmd.exe</c> enthält, und legt die Dateien dann in <paramref name="folder"/> ab. Andere Dateien im Zielordner
    /// (die gespeicherte Anmeldung von SteamCMD) bleiben erhalten. Scheitert etwas, bleibt nichts Halbes zurück. Gibt den Pfad der <c>steamcmd.exe</c> zurück;
    /// wirft <see cref="InvalidDataException"/> bei einem ungültigen Archiv oder wenn die Exe fehlt.
    /// </summary>
    public static string Install(Stream zip, string folder)
    {
        var target = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(target) ?? throw new ArgumentException(SteamTexts.InstallNoParent, nameof(folder));
        Directory.CreateDirectory(parent);
        var temp = Path.Combine(parent, Path.GetFileName(target) + ".partial-" + Guid.NewGuid().ToString("N")[..8]);

        try
        {
            try
            {
                using var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
                archive.ExtractToDirectory(temp);
            }
            catch (InvalidDataException)
            {
                throw new InvalidDataException(SteamTexts.InstallNotZip);
            }

            if (!File.Exists(Path.Combine(temp, ExecutableName)))
            {
                throw new InvalidDataException(SteamTexts.InstallNoExe);
            }

            foreach (var file in Directory.EnumerateFiles(temp, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(target, Path.GetRelativePath(temp, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Move(file, destination, overwrite: true);
            }

            return Path.Combine(target, ExecutableName);
        }
        finally
        {
            if (Directory.Exists(temp))
            {
                try
                {
                    Directory.Delete(temp, recursive: true);
                }
                catch (IOException)
                {
                    // Aufräumen ist nur ein Versuch.
                }
            }
        }
    }
}

/// <summary>Findet die <c>steamcmd.exe</c>: erst den gespeicherten Pfad, dann den Standardordner.</summary>
public static class SteamCmdLocator
{
    public static string? Find(SteamCmdSettings settings, string? defaultFolder = null)
    {
        if (!string.IsNullOrWhiteSpace(settings.SteamCmdPath) && File.Exists(settings.SteamCmdPath))
        {
            return settings.SteamCmdPath;
        }

        var fallback = Path.Combine(defaultFolder ?? SteamCmdInstaller.DefaultFolder, SteamCmdInstaller.ExecutableName);
        return File.Exists(fallback) ? fallback : null;
    }
}
