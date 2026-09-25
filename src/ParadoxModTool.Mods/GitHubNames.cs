using System.Text.RegularExpressions;

namespace ParadoxModTool.Mods;

/// <summary>Namen rund um GitHub-Releases: Repository-Namen prüfen, Tags und ZIP-Dateinamen aus Vorlagen bilden.</summary>
public static class GitHubNames
{
    public const string DefaultTagFormat = "v{version}";
    public const string DefaultFileNameFormat = "{mod}-{version}.zip";

    private static readonly Regex RepoPattern = new(@"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})/[A-Za-z0-9._-]{1,100}$", RegexOptions.Compiled);
    private static readonly Regex TagUnsafe = new(@"[^A-Za-z0-9._-]+", RegexOptions.Compiled);

    public static bool IsValidRepo(string? text) =>
        text is not null && RepoPattern.IsMatch(text) && !text.EndsWith("/.", StringComparison.Ordinal) && !text.EndsWith("/..", StringComparison.Ordinal);

    public static string ExpandTag(string? format, string version, string modName)
    {
        var template = string.IsNullOrWhiteSpace(format) ? DefaultTagFormat : format.Trim();
        var mod = TagUnsafe.Replace(modName.Trim(), "-").Trim('-');
        return template.Replace("{version}", version.Trim()).Replace("{mod}", mod);
    }

    public static bool IsValidTag(string? tag)
    {
        if (string.IsNullOrEmpty(tag) || tag.Length > 200 || tag.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || "~^:?*[\\".Contains(c)))
        {
            return false;
        }

        return !tag.Contains("..") && !tag.Contains("@{") && !tag.Contains("//")
            && !tag.StartsWith('-') && !tag.StartsWith('/') && !tag.StartsWith('.')
            && !tag.EndsWith('/') && !tag.EndsWith('.') && !tag.EndsWith(".lock", StringComparison.OrdinalIgnoreCase);
    }

    public static string ExpandFileName(string? format, string modName, string version, string tag, string variant)
    {
        var template = string.IsNullOrWhiteSpace(format) ? DefaultFileNameFormat : format.Trim();
        var name = template
            .Replace("{mod}", modName.Trim())
            .Replace("{version}", version.Trim())
            .Replace("{tag}", tag)
            .Replace("{variant}", variant.Trim());
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        name = name.Trim().TrimEnd('.');
        return name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? name : name + ".zip";
    }
}
