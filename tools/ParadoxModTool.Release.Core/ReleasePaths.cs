using ParadoxModTool.Updates;

namespace ParadoxModTool.Release;

/// <summary>Pfade im Repo (Hauptordner mit <c>ParadoxModTool.slnx</c>) und die Schlüsseldatei.</summary>
public sealed record ReleasePaths(string Root)
{
    public const string SolutionName = "ParadoxModTool.slnx";

    public const string AppProjectRelative = @"src\ParadoxModTool.App\ParadoxModTool.App.csproj";

    public static string KeyFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ParadoxModTool-Release", "release-key.p8");

    public string Solution => Path.Combine(Root, SolutionName);

    public string AppProject => Path.Combine(Root, AppProjectRelative);

    public string Changelog => Path.Combine(Root, ReadmeFiles.Folder, ReadmeFiles.ChangelogName);

    /// <summary>Pfad des Changelogs relativ zum Repo mit "/", für git.</summary>
    public static string ChangelogRelative => ReadmeFiles.Folder + "/" + ReadmeFiles.ChangelogName;

    public string OutputFor(AppVersion version) => Path.Combine(Root, "release-output", version.ToString());

    public string AppOutputFor(AppVersion version) => Path.Combine(OutputFor(version), "app");

    /// <summary>Sucht vom Ordner aufwärts den Ordner mit <c>ParadoxModTool.slnx</c>; null ohne Treffer.</summary>
    public static string? FindRoot(string startFolder)
    {
        for (var folder = new DirectoryInfo(Path.GetFullPath(startFolder)); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, SolutionName)))
            {
                return folder.FullName;
            }
        }

        return null;
    }

    /// <summary>Ist eine Quelle des Werkzeugs (<c>*.cs</c>, <c>*.csproj</c> unter <c>tools\ParadoxModTool.Release*</c>, ohne bin/obj) neuer als die exe?</summary>
    public bool ToolSourcesNewerThan(DateTime exeWriteUtc)
    {
        var tools = Path.Combine(Root, "tools");
        if (!Directory.Exists(tools))
        {
            return false;
        }

        return Directory.EnumerateDirectories(tools, "ParadoxModTool.Release*")
            .Where(folder => !Path.GetFileName(folder).Equals("ParadoxModTool.ReleaseSigner", StringComparison.OrdinalIgnoreCase))
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            .Where(file => file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Where(file => !IsBuildOutput(file))
            .Any(file => File.GetLastWriteTimeUtc(file) > exeWriteUtc);
    }

    private static bool IsBuildOutput(string file) =>
        file.Split(Path.DirectorySeparatorChar).Any(part => part.Equals("bin", StringComparison.OrdinalIgnoreCase) || part.Equals("obj", StringComparison.OrdinalIgnoreCase));
}
