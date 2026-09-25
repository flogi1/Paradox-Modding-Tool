using System.Reflection;
using ParadoxModTool.Updates;

namespace ParadoxModTool.App;

/// <summary>Programmname, Version und Links an einer zentralen Stelle (Startbild, Über-Fenster, Update-Check, Fehlerbericht).</summary>
public static class AppInfo
{
    public const string Name = "Paradox Modding Tool";

    public const string Creator = "flogi";

    /// <summary>GitHub-Repository im Format "owner/name". Quelle für Update-Prüfung, Selbst-Update und Fehlerbericht.</summary>
    public const string GitHubRepository = "flogi1/Paradox-Modding-Tool";

    public static bool HasRepository => IssueReport.IsValidRepository(GitHubRepository);

    public static string RepositoryUrl => $"https://github.com/{GitHubRepository}";

    /// <summary>Version aus der Assembly (<c>&lt;Version&gt;</c> der .csproj); 0.0.0, falls sie sich nicht lesen lässt.</summary>
    public static AppVersion Version { get; } = ReadVersion();

    private static AppVersion ReadVersion()
    {
        var text = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        return AppVersion.TryParse(text, out var version) ? version : default;
    }
}
