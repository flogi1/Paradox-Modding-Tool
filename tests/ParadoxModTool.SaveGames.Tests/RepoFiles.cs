namespace ParadoxModTool.SaveGames.Tests;

/// <summary>Dateien im Repository (Quellcode, Locale-Dateien), gefunden über den Ordner mit <c>locales/de.json</c> und <c>src</c>.</summary>
internal static class RepoFiles
{
    public static string Root { get; } = FindRoot();

    public static string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "locales", "de.json")) && Directory.Exists(Path.Combine(dir.FullName, "src")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository-Wurzel mit locales/de.json und src nicht gefunden.");
    }
}
