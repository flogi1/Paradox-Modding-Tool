using ParadoxModTool.Mods;

namespace ParadoxModTool.GitIntegration;

/// <summary>Das Ergebnis der Prüfung eines Repositories für den Tag; <see langword="null"/> = noch nicht geprüft.</summary>
public sealed record GitHubRepoCheck(string Repo, string Tag, string? Error, bool? CanWrite, bool? TagExists);

public sealed record GitHubReadinessInput(
    bool GhFound,
    bool LoggedIn,
    bool VersionValid,
    bool ChangelogExists,
    bool ChangelogHasVersion,
    string Title,
    string Notes,
    int SelectedRepoCount,
    IReadOnlyList<GitHubRepoCheck> Checks,
    bool UpdateExisting,
    IReadOnlyList<string> ZipProblems,
    IReadOnlyList<string> GitWarnings);

/// <summary>Die Hinweise vor dem Veröffentlichen auf GitHub und ob es erlaubt ist (Gegenstück zu <see cref="PublishReadiness"/> für Steam).</summary>
public static class GitHubReadiness
{
    public static PublishReadiness Evaluate(GitHubReadinessInput input)
    {
        var hints = new List<PublishHint>();

        if (!input.GhFound)
        {
            hints.Add(new PublishHint(GitTexts.CheckGhMissing, true));
        }
        else if (!input.LoggedIn)
        {
            hints.Add(new PublishHint(GitTexts.CheckNotLoggedIn, true));
        }

        if (!input.VersionValid)
        {
            hints.Add(new PublishHint(GitTexts.CheckVersionInvalid, true));
        }

        if (input.SelectedRepoCount == 0)
        {
            hints.Add(new PublishHint(GitTexts.CheckNoRepo, true));
        }

        if (string.IsNullOrWhiteSpace(input.Title))
        {
            hints.Add(new PublishHint(GitTexts.CheckTitleMissing, true));
        }

        if (ReleaseNotesFormatter.IsTooLong(input.Notes))
        {
            hints.Add(new PublishHint(GitTexts.CheckNotesTooLong(input.Notes.Length, ReleaseNotesFormatter.MaxLength), true));
        }

        if (input.ChangelogExists && !input.ChangelogHasVersion)
        {
            hints.Add(new PublishHint(GitTexts.CheckNoChangelogSection, false));
        }

        foreach (var check in input.Checks)
        {
            if (check.Error is not null)
            {
                hints.Add(new PublishHint($"{check.Repo}: {check.Error}", true));
                continue;
            }

            if (check.CanWrite == false)
            {
                hints.Add(new PublishHint(GitTexts.NoWriteAccess(check.Repo), true));
            }

            if (check.TagExists == true)
            {
                hints.Add(input.UpdateExisting
                    ? new PublishHint(GitTexts.CheckWillUpdate(check.Tag, check.Repo), false)
                    : new PublishHint(GitTexts.CheckTagExists(check.Tag, check.Repo), true));
            }
        }

        hints.AddRange(input.ZipProblems.Select(p => new PublishHint(p, true)));
        hints.AddRange(input.GitWarnings.Select(w => new PublishHint(w, false)));

        return new PublishReadiness(hints);
    }
}
