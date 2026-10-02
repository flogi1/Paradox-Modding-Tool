using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.SaveGames;

/// <summary>Ordner der Spielstände: neben dem Mod-Ordner des Spiels (wie der Ordner <c>logs</c> beim Fehlerprotokoll).</summary>
public static class SaveGameFolder
{
    /// <summary>Elternordner des Mod-Ordners plus <see cref="SaveGameSettings.Folder"/>; <see langword="null"/> ohne Vorlage.</summary>
    public static string? For(string? modRootTemplate, SaveGameSettings settings)
    {
        if (string.IsNullOrWhiteSpace(modRootTemplate))
        {
            return null;
        }

        try
        {
            var modRoot = Environment.ExpandEnvironmentVariables(modRootTemplate.Trim()).TrimEnd('/', '\\');
            var userFolder = Path.GetDirectoryName(Path.GetFullPath(modRoot));
            return string.IsNullOrEmpty(userFolder) ? null : Path.Combine(userFolder, settings.Folder);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return null;
        }
    }
}
