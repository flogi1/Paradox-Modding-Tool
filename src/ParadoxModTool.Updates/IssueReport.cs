using System.Text;

namespace ParadoxModTool.Updates;

/// <summary>
/// Baut die vorbefüllte "Neues Issue"-Adresse für GitHub. Enthält nur Version und Systemangaben, keine
/// Pfade, Modnamen oder sonstigen Nutzerdaten.
/// </summary>
public static class IssueReport
{
    public static string BuildUrl(string repository, string appVersion, string osDescription, string frameworkDescription)
    {
        var body = new StringBuilder()
            .AppendLine("## " + UpdateTexts.IssueWhatHappened)
            .AppendLine()
            .AppendLine("## " + UpdateTexts.IssueSteps)
            .AppendLine("1. ")
            .AppendLine()
            .AppendLine("## " + UpdateTexts.IssueExpectedActual)
            .AppendLine()
            .AppendLine("## " + UpdateTexts.IssueEnvironment)
            .AppendLine($"- Paradox Modding Tool: {appVersion}")
            .AppendLine($"- {UpdateTexts.IssueOs}: {osDescription}")
            .AppendLine($"- {UpdateTexts.IssueRuntime}: {frameworkDescription}")
            .ToString();

        return $"https://github.com/{repository}/issues/new?title={Uri.EscapeDataString("[Bug] ")}&body={Uri.EscapeDataString(body)}";
    }

    /// <summary>Repository-Kürzel wie "owner/name"? Genau ein Schrägstrich, beide Teile nicht leer und ohne Sonderzeichen.</summary>
    public static bool IsValidRepository(string? repository)
    {
        if (string.IsNullOrWhiteSpace(repository))
        {
            return false;
        }

        var parts = repository.Split('/');
        return parts.Length == 2 && parts.All(p => p.Length > 0 && p.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.'));
    }
}
