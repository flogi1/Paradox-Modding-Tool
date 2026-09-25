using System.Text.RegularExpressions;
using ParadoxModTool.Mods;

namespace ParadoxModTool.GitIntegration;

/// <summary>Rechnet die Adresse eines Git-Remotes in <c>owner/name</c> um; nur github.com wird erkannt.</summary>
public static class GitRemoteUrl
{
    private static readonly Regex[] Patterns =
    {
        new(@"^https?://(?:[^@/\s]+@)?github\.com/([^/\s]+)/([^/\s]+?)(?:\.git)?/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^(?:[^@\s]+@)?github\.com:([^/\s]+)/([^/\s]+?)(?:\.git)?/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^ssh://(?:[^@/\s]+@)?github\.com(?::\d+)?/([^/\s]+)/([^/\s]+?)(?:\.git)?/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    };

    public static string? ToRepo(string? url)
    {
        var text = url?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        foreach (var pattern in Patterns)
        {
            if (pattern.Match(text) is { Success: true } match)
            {
                var repo = match.Groups[1].Value + "/" + match.Groups[2].Value;
                return GitHubNames.IsValidRepo(repo) ? repo : null;
            }
        }

        return null;
    }
}
