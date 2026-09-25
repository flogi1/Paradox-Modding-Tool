using System.Text;
using ParadoxModTool.Mods;

namespace ParadoxModTool.GitIntegration;

/// <param name="Repos">Nur die Repositories, in die veröffentlicht werden soll.</param>
/// <param name="TagHead">Der Tag darf auf den gepushten HEAD zeigen (gilt nur für das Repo, das zu <c>origin</c> passt); sonst zeigt er auf den Standard-Branch.</param>
public sealed record GitHubPublishRequest(
    string ModName,
    string Version,
    string ModFolder,
    IReadOnlyList<GitHubRepoEntry> Repos,
    string Title,
    string Notes,
    bool Draft,
    bool Prerelease,
    bool UpdateExisting,
    IReadOnlyList<GitHubZipVariant> Variants,
    GitState? Git,
    bool TagHead,
    string? TempRoot = null);

public enum GitHubRepoOutcome
{
    Created,
    Updated,
    Skipped,
    Failed,
    Cancelled,
}

public sealed record GitHubRepoResult(string Repo, string Tag, GitHubRepoOutcome Outcome, string? Url, string Message)
{
    public bool Succeeded => Outcome is GitHubRepoOutcome.Created or GitHubRepoOutcome.Updated;
}

/// <param name="Problem">Ein Fehler vor der Repo-Schleife (die ZIP-Dateien ließen sich nicht erstellen); dann ist <paramref name="Repos"/> leer.</param>
public sealed record GitHubPublishResult(IReadOnlyList<GitHubRepoResult> Repos, string? Problem)
{
    public bool AllSucceeded => Problem is null && Repos.Count > 0 && Repos.All(r => r.Succeeded);
}

/// <summary>Veröffentlicht eine Version als GitHub-Release in einem oder mehreren Repositories. Die ZIP-Dateien und die Notes-Datei liegen im Temp-Ordner und werden immer gelöscht.</summary>
public sealed class GitHubPublisher
{
    private readonly GhClient _gh;

    public GitHubPublisher(GhClient gh)
    {
        _gh = gh;
    }

    public async Task<GitHubPublishResult> PublishAsync(GitHubPublishRequest request, IProgress<string>? progress, CancellationToken cancellation)
    {
        var work = Path.Combine(request.TempRoot ?? Path.Combine(Path.GetTempPath(), "ParadoxModTool", "github"), Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var targets = request.Repos
                .Select(r => (Entry: r, Tag: GitHubNames.ExpandTag(r.TagFormat, request.Version, request.ModName)))
                .ToList();
            var zipTag = targets.Count > 0 ? targets[0].Tag : GitHubNames.ExpandTag(null, request.Version, request.ModName);

            var problems = ZipPackager.Validate(request.ModFolder, request.ModName, request.Version, zipTag, request.Variants);
            if (problems.Count > 0)
            {
                return new GitHubPublishResult(Array.Empty<GitHubRepoResult>(), string.Join(" ", problems));
            }

            IReadOnlyList<PackagedZip> zips;
            try
            {
                progress?.Report("ZIP-Dateien werden erstellt …");
                zips = ZipPackager.Build(request.ModFolder, request.ModName, request.Version, zipTag, request.Variants, Path.Combine(work, "zips"));
                Directory.CreateDirectory(work);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new GitHubPublishResult(Array.Empty<GitHubRepoResult>(), "Die ZIP-Dateien konnten nicht erstellt werden: " + ex.Message);
            }

            var notesFile = Path.Combine(work, "notes.md");
            File.WriteAllText(notesFile, request.Notes, new UTF8Encoding(false));
            var files = zips.Select(z => z.Path).ToList();

            var results = new List<GitHubRepoResult>();
            foreach (var (entry, tag) in targets)
            {
                results.Add(cancellation.IsCancellationRequested
                    ? new GitHubRepoResult(entry.Repo, tag, GitHubRepoOutcome.Cancelled, null, "Abgebrochen.")
                    : await PublishOneAsync(request, entry.Repo, tag, notesFile, files, progress, cancellation));
            }

            return new GitHubPublishResult(results, null);
        }
        finally
        {
            try
            {
                if (Directory.Exists(work))
                {
                    Directory.Delete(work, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Aufräumen ist nur ein Versuch; der Ordner liegt im Temp-Ordner.
            }
        }
    }

    private async Task<GitHubRepoResult> PublishOneAsync(
        GitHubPublishRequest request,
        string repo,
        string tag,
        string notesFile,
        IReadOnlyList<string> files,
        IProgress<string>? progress,
        CancellationToken cancellation)
    {
        GitHubRepoResult Result(GitHubRepoOutcome outcome, string? url, string message) => new(repo, tag, outcome, url, message);

        try
        {
            if (!GitHubNames.IsValidTag(tag))
            {
                return Result(GitHubRepoOutcome.Failed, null, $"Der Tag „{tag}“ ist ungültig. Prüfe das Tag-Format.");
            }

            progress?.Report($"[{repo}] Repository wird geprüft …");
            var info = await _gh.GetRepoAsync(repo, cancellation);
            if (!info.CanWrite)
            {
                return Result(GitHubRepoOutcome.Failed, null, $"Du hast kein Schreibrecht in {repo}.");
            }

            if (info.IsEmpty)
            {
                return Result(GitHubRepoOutcome.Failed, null, GhRepoInfo.EmptyMessage);
            }

            var existing = await _gh.FindReleaseAsync(repo, tag, cancellation);
            if (existing is not null && !request.UpdateExisting)
            {
                return Result(GitHubRepoOutcome.Skipped, existing.Url, $"Der Tag {tag} hat in {repo} schon ein Release. Schalte \"Vorhandenes Release aktualisieren\" ein oder ändere das Tag-Format.");
            }

            var target = request.TagHead && request.Git is { HeadPushed: true } git && string.Equals(git.OriginRepo, repo, StringComparison.OrdinalIgnoreCase)
                ? git.Head
                : info.DefaultBranch;
            var spec = new GhReleaseSpec(repo, tag, request.Title, notesFile, target, request.Draft, request.Prerelease);
            if (existing is null)
            {
                progress?.Report($"[{repo}] Release {tag} wird angelegt …");
                var url = await _gh.CreateReleaseAsync(spec, files, progress, cancellation);
                return Result(GitHubRepoOutcome.Created, url, $"Das Release {tag} wurde in {repo} angelegt.");
            }

            progress?.Report($"[{repo}] Release {tag} wird aktualisiert …");
            await _gh.EditReleaseAsync(spec, cancellation);
            if (files.Count > 0)
            {
                await _gh.UploadAsync(repo, tag, files, progress, cancellation);
            }

            return Result(GitHubRepoOutcome.Updated, existing.Url, $"Das Release {tag} in {repo} wurde aktualisiert.");
        }
        catch (GhException ex)
        {
            return Result(GitHubRepoOutcome.Failed, null, ex.Message);
        }
        catch (OperationCanceledException)
        {
            return Result(GitHubRepoOutcome.Cancelled, null, "Abgebrochen. Ein Release kann in diesem Repo schon angelegt sein; unter \"Aufräumen\" lässt es sich löschen.");
        }
    }
}
