namespace ParadoxModTool.Mods;

/// <summary>Ein GitHub-Repository einer Mod. <see cref="Repo"/> ist <c>owner/name</c>; <see cref="TagFormat"/> kennt <c>{version}</c> und <c>{mod}</c>.</summary>
public sealed class GitHubRepoEntry
{
    public string Repo { get; set; } = string.Empty;

    public string TagFormat { get; set; } = GitHubNames.DefaultTagFormat;

    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Eine ZIP-Variante des Release-Anhangs. Die Ausschlüsse kommen zu den Standardregeln von <see cref="ModStaging"/> dazu; <see cref="OnlyFolders"/> (nicht leer) beschränkt auf Dateien unter diesen Ordnern;
/// <see cref="RootFolder"/> ist ein optionaler Ordnername als Wurzel im Archiv.
/// </summary>
public sealed class GitHubZipVariant
{
    public string Name { get; set; } = string.Empty;

    public string FileNameFormat { get; set; } = GitHubNames.DefaultFileNameFormat;

    public List<string> ExcludedFolders { get; set; } = new();

    public List<string> ExcludedFilePatterns { get; set; } = new();

    public List<string> OnlyFolders { get; set; } = new();

    public string? RootFolder { get; set; }

    public bool Enabled { get; set; } = true;
}

/// <summary>Die Veröffentlichung einer Version als GitHub-Release. <see cref="PublishedAt"/> ist <c>yyyy-MM-ddTHH:mm:ss</c> (Ortszeit).</summary>
public sealed class GitHubPublication
{
    public string Repo { get; set; } = string.Empty;

    public string Tag { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string PublishedAt { get; set; } = string.Empty;
}
