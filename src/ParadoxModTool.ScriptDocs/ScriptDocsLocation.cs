namespace ParadoxModTool.ScriptDocs;

/// <summary>Wo das Spiel seine Doku-Logs ablegt, wenn der Nutzer keinen eigenen Ordner gewählt hat.</summary>
public static class ScriptDocsLocation
{
    /// <summary>
    /// Der Ordner <paramref name="subfolder"/> (Spielprofil: "docs" bei EU V, "logs" bei CK III) im Spieldatenordner, also neben dem
    /// Mod-Ordner aus <paramref name="modRootTemplate"/> (Umgebungsvariablen werden aufgelöst). <see langword="null"/> bei ungültigem Pfad.
    /// </summary>
    public static string? Default(string modRootTemplate, string subfolder)
    {
        if (string.IsNullOrWhiteSpace(modRootTemplate))
        {
            return null;
        }

        try
        {
            var modFolder = Path.GetFullPath(Environment.ExpandEnvironmentVariables(modRootTemplate));
            var gameFolder = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(modFolder));
            return gameFolder is null ? null : Path.Combine(gameFolder, subfolder);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
