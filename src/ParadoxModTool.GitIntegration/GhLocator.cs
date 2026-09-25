namespace ParadoxModTool.GitIntegration;

/// <summary>Findet die <c>gh.exe</c>: gespeicherter Pfad, <c>PATH</c>, <c>&lt;ProgramFiles&gt;\GitHub CLI</c>, zuletzt der Programmordner des Programms (heruntergeladen). Ein vorhandenes <c>gh</c> hat also Vorrang.</summary>
public static class GhLocator
{
    public static string? Find(GhSettings settings, string? pathVariable = null, string? programFiles = null, string? defaultExecutable = null)
    {
        if (!string.IsNullOrWhiteSpace(settings.GhPath) && File.Exists(settings.GhPath))
        {
            return settings.GhPath;
        }

        foreach (var directory in (pathVariable ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim('"'), GhInstaller.ExecutableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                // Ein ungültiger Eintrag im PATH wird übersprungen.
            }
        }

        var installed = Path.Combine(programFiles ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GitHub CLI", GhInstaller.ExecutableName);
        if (File.Exists(installed))
        {
            return installed;
        }

        var own = defaultExecutable ?? GhInstaller.DefaultExecutable;
        return File.Exists(own) ? own : null;
    }
}
