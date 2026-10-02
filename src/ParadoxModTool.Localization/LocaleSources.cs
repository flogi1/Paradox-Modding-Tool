namespace ParadoxModTool.Localization;

/// <summary>Woher eine Sprachdatei kommt: mitgeliefert (Programmordner, vom Update ersetzt) oder vom Nutzer abgelegt.</summary>
public enum LocaleOrigin
{
    Program,
    User,
}

/// <summary>
/// Die beiden Ordner mit Sprachdateien. <see cref="Program"/> liegt neben der exe und wird vom Update ersetzt; <see cref="User"/> liegt unter
/// %APPDATA% und überlebt Updates. Eine Datei im User-Ordner schlägt die gleichnamige im Programmordner.
/// </summary>
public sealed record LocaleSources(string Program, string? User)
{
    public static LocaleSources Default() => new(
        Path.Combine(AppContext.BaseDirectory, "locales"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ParadoxModTool", "locales"));

    public string ProgramFile(string code) => Path.Combine(Program, code + ".json");

    /// <summary>Die Datei einer Sprache: aus dem User-Ordner, sonst aus dem Programmordner; <see langword="null"/>, wenn es keine gibt.</summary>
    public (string Path, LocaleOrigin Origin)? Resolve(string code)
    {
        if (User is not null)
        {
            var user = Path.Combine(User, code + ".json");
            if (File.Exists(user))
            {
                return (user, LocaleOrigin.User);
            }
        }

        var program = ProgramFile(code);
        return File.Exists(program) ? (program, LocaleOrigin.Program) : null;
    }
}
