using System.Text;
using ParadoxModTool.Localization;
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
                progress?.Report(GitTexts.ZipsBuilding);
                zips = ZipPackager.Build(request.ModFolder, request.ModName, request.Version, zipTag, request.Variants, Path.Combine(work, "zips"));
                Directory.CreateDirectory(work);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new GitHubPublishResult(Array.Empty<GitHubRepoResult>(), GitTexts.ZipsFailed(ex.Message));
            }

            var notesFile = Path.Combine(work, "notes.md");
            File.WriteAllText(notesFile, request.Notes, new UTF8Encoding(false));
            var files = zips.Select(z => z.Path).ToList();

            var results = new List<GitHubRepoResult>();
            foreach (var (entry, tag) in targets)
            {
                results.Add(cancellation.IsCancellationRequested
                    ? new GitHubRepoResult(entry.Repo, tag, GitHubRepoOutcome.Cancelled, null, CommonTexts.Cancelled)
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
                return Result(GitHubRepoOutcome.Failed, null, GitTexts.TagInvalid(tag));
            }

            progress?.Report(GitTexts.CheckingRepo(repo));
            var info = await _gh.GetRepoAsync(repo, cancellation);
            if (!info.CanWrite)
            {
                return Result(GitHubRepoOutcome.Failed, null, GitTexts.NoWriteAccess(repo));
            }

            if (info.IsEmpty)
            {
                return Result(GitHubRepoOutcome.Failed, null, GhRepoInfo.EmptyMessage);
            }

            var existing = await _gh.FindReleaseAsync(repo, tag, cancellation);
            if (existing is not null && !request.UpdateExisting)
            {
                return Result(GitHubRepoOutcome.Skipped, existing.Url, GitTexts.ReleaseExists(tag, repo));
            }

            var target = request.TagHead && request.Git is { HeadPushed: true } git && string.Equals(git.OriginRepo, repo, StringComparison.OrdinalIgnoreCase)
                ? git.Head
                : info.DefaultBranch;
            var spec = new GhReleaseSpec(repo, tag, request.Title, notesFile, target, request.Draft, request.Prerelease);
            if (existing is null)
            {
                progress?.Report(GitTexts.CreatingRelease(repo, tag));
                var url = await _gh.CreateReleaseAsync(spec, files, progress, cancellation);
                return Result(GitHubRepoOutcome.Created, url, GitTexts.ReleaseCreated(tag, repo));
            }

            progress?.Report(GitTexts.UpdatingRelease(repo, tag));
            await _gh.EditReleaseAsync(spec, cancellation);
            if (files.Count > 0)
            {
                await _gh.UploadAsync(repo, tag, files, progress, cancellation);
            }

            return Result(GitHubRepoOutcome.Updated, existing.Url, GitTexts.ReleaseUpdated(tag, repo));
        }
        catch (GhException ex)
        {
            return Result(GitHubRepoOutcome.Failed, null, ex.Message);
        }
        catch (OperationCanceledException)
        {
            return Result(GitHubRepoOutcome.Cancelled, null, GitTexts.PublishCancelled);
        }
    }
}
