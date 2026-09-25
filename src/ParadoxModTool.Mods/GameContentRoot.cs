namespace ParadoxModTool.Mods;

/// <summary>
/// Findet aus dem Pfad der Spiel-Exe den Ordner mit den Vanilla-Dateien. Zwei bekannte Aufbauten: EU V liegt die Exe in
/// <c>binaries</c> und die Dateien in dem Geschwisterordner <c>game</c>; bei EU IV liegen Exe und Dateien (<c>common</c>, <c>events</c> ...)
/// im selben Installationsordner. Liest nur.
/// </summary>
public static class GameContentRoot
{
    /// <summary>Der Ordner mit den Vanilla-Dateien; <see langword="null"/>, wenn der Aufbau nicht erkannt wird.</summary>
    public static string? Find(string? exePath)
    {
        if (ExeFolder(exePath) is not { } exeFolder)
        {
            return null;
        }

        var parent = Path.GetDirectoryName(exeFolder);
        var candidates = new List<string>();
        if (parent is not null)
        {
            candidates.Add(Path.Combine(parent, "game"));
        }

        candidates.Add(Path.Combine(exeFolder, "game"));
        var found = candidates.FirstOrDefault(Directory.Exists);
        if (found is not null)
        {
            return found;
        }

        return Directory.Exists(Path.Combine(exeFolder, "common")) ? exeFolder : null;
    }

    /// <summary>Der Installationsordner: bei EU IV der Exe-Ordner selbst, sonst der Elternordner von <c>binaries</c>.</summary>
    public static string? InstallRoot(string? exePath)
    {
        if (ExeFolder(exePath) is not { } exeFolder)
        {
            return null;
        }

        if (Directory.Exists(Path.Combine(exeFolder, "common")) && !Directory.Exists(Path.Combine(exeFolder, "game")))
        {
            return exeFolder;
        }

        var parent = Path.GetDirectoryName(exeFolder);
        return parent is not null && Directory.Exists(parent) ? parent : null;
    }

    private static string? ExeFolder(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return null;
        }

        try
        {
            var folder = Path.GetDirectoryName(exePath);
            return folder is not null && Directory.Exists(folder) ? folder : null;
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException)
        {
            return null;
        }
    }
}
