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
            hints.Add(new PublishHint("gh (GitHub CLI) ist nicht eingerichtet.", true));
        }
        else if (!input.LoggedIn)
        {
            hints.Add(new PublishHint("Bei GitHub ist keine Anmeldung aktiv.", true));
        }

        if (!input.VersionValid)
        {
            hints.Add(new PublishHint("metadata.json enthält keine gültige Version.", true));
        }

        if (input.SelectedRepoCount == 0)
        {
            hints.Add(new PublishHint("Es ist kein Repository angehakt.", true));
        }

        if (string.IsNullOrWhiteSpace(input.Title))
        {
            hints.Add(new PublishHint("Der Titel des Releases fehlt.", true));
        }

        if (ReleaseNotesFormatter.IsTooLong(input.Notes))
        {
            hints.Add(new PublishHint($"Die Release-Notes sind zu lang ({input.Notes.Length:N0} von {ReleaseNotesFormatter.MaxLength:N0} Zeichen).", true));
        }

        if (input.ChangelogExists && !input.ChangelogHasVersion)
        {
            hints.Add(new PublishHint("Im Changelog gibt es keinen Abschnitt für diese Version; die Release-Notes stammen nur aus deinem Text.", false));
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
                hints.Add(new PublishHint($"Du hast kein Schreibrecht in {check.Repo}.", true));
            }

            if (check.TagExists == true)
            {
                hints.Add(input.UpdateExisting
                    ? new PublishHint($"Das vorhandene Release {check.Tag} in {check.Repo} wird aktualisiert (Notes und ZIP-Dateien werden ersetzt).", false)
                    : new PublishHint($"Der Tag {check.Tag} existiert schon in {check.Repo}. Schalte \"Vorhandenes Release aktualisieren\" ein oder ändere das Tag-Format.", true));
            }
        }

        hints.AddRange(input.ZipProblems.Select(p => new PublishHint(p, true)));
        hints.AddRange(input.GitWarnings.Select(w => new PublishHint(w, false)));

        return new PublishReadiness(hints);
    }
}
