using System.ComponentModel;
using System.Text.Json;

namespace ParadoxModTool.GitIntegration;

public sealed class GhException : Exception
{
    public GhException(string message, IReadOnlyList<string> output)
        : base(message)
    {
        Output = output;
    }

    public IReadOnlyList<string> Output { get; }
}

/// <param name="DefaultBranch">Leer, wenn das Repository noch keinen Commit hat.</param>
public sealed record GhRepoInfo(string DefaultBranch, string ViewerPermission, bool IsPrivate)
{
    public bool CanWrite => ViewerPermission is "WRITE" or "MAINTAIN" or "ADMIN";

    /// <summary>Das Repository hat noch keinen Commit (keinen Standard-Branch); ein Release ist dort nicht möglich.</summary>
    public bool IsEmpty => DefaultBranch.Length == 0;

    public static string EmptyMessage => GitTexts.RepoEmpty;
}

public sealed record GhReleaseInfo(string Tag, string Url, bool IsDraft);

public sealed record GhReleaseListItem(string Tag, string Name, bool IsDraft, bool IsPrerelease, string PublishedAt);

public sealed record GhBranch(string Name, bool Protected);

/// <param name="Target">Commit oder Branch, auf den der neue Tag zeigt; <see langword="null"/> = Standard-Branch von GitHub.</param>
public sealed record GhReleaseSpec(string Repo, string Tag, string Title, string NotesFile, string? Target, bool Draft, bool Prerelease);

/// <summary>Dünne Schicht über die Aufrufe von <c>gh</c>. Die Anmeldung gehört <c>gh</c>; das Programm sieht nie ein Token. Fehler kommen als <see cref="GhException"/> mit den letzten Zeilen der Ausgabe.</summary>
public sealed partial class GhClient
{
    private static readonly IReadOnlyDictionary<string, string> Environment = new Dictionary<string, string>
    {
        ["GH_PROMPT_DISABLED"] = "1",
        ["NO_COLOR"] = "1",
        ["GH_NO_UPDATE_NOTIFIER"] = "1",
        ["GH_SPINNER_DISABLED"] = "1",
    };

    private static readonly TimeSpan Short = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Long = TimeSpan.FromMinutes(30);

    private readonly IProcessRunner _runner;
    private readonly string _gh;

    public GhClient(IProcessRunner runner, string ghPath)
    {
        _runner = runner;
        _gh = ghPath;
    }

    public async Task<bool> IsLoggedInAsync(CancellationToken cancellation)
    {
        try
        {
            var result = await RunAsync(new[] { "auth", "status", "--hostname", "github.com" }, Short, null, cancellation);
            return result.ExitCode == 0 && !result.TimedOut;
        }
        catch (GhException)
        {
            return false;
        }
    }

    public async Task<GhRepoInfo> GetRepoAsync(string repo, CancellationToken cancellation)
    {
        var output = await OkAsync(new[] { "repo", "view", repo, "--json", "defaultBranchRef,viewerPermission,isPrivate" }, Short, null, cancellation);
        try
        {
            using var document = JsonDocument.Parse(string.Join("\n", output));
            var root = document.RootElement;
            var branchRef = root.TryGetProperty("defaultBranchRef", out var reference) && reference.ValueKind == JsonValueKind.Object
                && reference.TryGetProperty("name", out var branchName)
                    ? branchName.GetString() ?? string.Empty
                    : string.Empty;
            return new GhRepoInfo(
                branchRef,
                root.GetProperty("viewerPermission").GetString() ?? string.Empty,
                root.TryGetProperty("isPrivate", out var isPrivate) && isPrivate.GetBoolean());
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new GhException(GitTexts.RepoUnreadable, output);
        }
    }

    public async Task<GhReleaseInfo?> FindReleaseAsync(string repo, string tag, CancellationToken cancellation)
    {
        var result = await RunAsync(new[] { "release", "view", tag, "--repo", repo, "--json", "tagName,url,isDraft" }, Short, null, cancellation);
        if (result.ExitCode != 0 || result.TimedOut)
        {
            if (!result.TimedOut && string.Join(" ", result.Error.Concat(result.Output)).Contains("release not found", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            throw Failure(result);
        }

        try
        {
            using var document = JsonDocument.Parse(string.Join("\n", result.Output));
            var root = document.RootElement;
            return new GhReleaseInfo(
                root.GetProperty("tagName").GetString() ?? tag,
                root.GetProperty("url").GetString() ?? string.Empty,
                root.TryGetProperty("isDraft", out var draft) && draft.GetBoolean());
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new GhException(GitTexts.ReleaseUnreadable, result.Output);
        }
    }

    public async Task<string> CreateReleaseAsync(GhReleaseSpec spec, IReadOnlyList<string> files, IProgress<string>? progress, CancellationToken cancellation)
    {
        var args = new List<string> { "release", "create", spec.Tag };
        args.AddRange(files);
        args.AddRange(new[] { "--repo", spec.Repo, "--title", spec.Title, "--notes-file", spec.NotesFile });
        if (spec.Target is not null)
        {
            args.AddRange(new[] { "--target", spec.Target });
        }

        if (spec.Draft)
        {
            args.Add("--draft");
        }

        if (spec.Prerelease)
        {
            args.Add("--prerelease");
        }

        var output = await OkAsync(args, Long, progress, cancellation);
        return output.LastOrDefault(l => l.TrimStart().StartsWith("https://", StringComparison.OrdinalIgnoreCase))?.Trim() ?? string.Empty;
    }

    public Task EditReleaseAsync(GhReleaseSpec spec, CancellationToken cancellation) =>
        OkAsync(
            new[]
            {
                "release", "edit", spec.Tag, "--repo", spec.Repo, "--title", spec.Title, "--notes-file", spec.NotesFile,
                "--draft=" + (spec.Draft ? "true" : "false"), "--prerelease=" + (spec.Prerelease ? "true" : "false"),
            },
            Short,
            null,
            cancellation);

    public Task UploadAsync(string repo, string tag, IReadOnlyList<string> files, IProgress<string>? progress, CancellationToken cancellation)
    {
        var args = new List<string> { "release", "upload", tag };
        args.AddRange(files);
        args.AddRange(new[] { "--repo", repo, "--clobber" });
        return OkAsync(args, Long, progress, cancellation);
    }

    public async Task<IReadOnlyList<GhReleaseListItem>> ListReleasesAsync(string repo, CancellationToken cancellation)
    {
        var output = await OkAsync(new[] { "release", "list", "--repo", repo, "--limit", "100", "--json", "tagName,name,isDraft,isPrerelease,publishedAt" }, Short, null, cancellation);
        try
        {
            using var document = JsonDocument.Parse(string.Join("\n", output));
            return document.RootElement.EnumerateArray()
                .Select(e => new GhReleaseListItem(
                    e.GetProperty("tagName").GetString() ?? string.Empty,
                    e.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                    e.TryGetProperty("isDraft", out var draft) && draft.GetBoolean(),
                    e.TryGetProperty("isPrerelease", out var pre) && pre.GetBoolean(),
                    e.TryGetProperty("publishedAt", out var at) ? at.GetString() ?? string.Empty : string.Empty))
                .ToList();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new GhException(GitTexts.ReleaseListUnreadable, output);
        }
    }

    public Task DeleteReleaseAsync(string repo, string tag, bool cleanupTag, CancellationToken cancellation)
    {
        var args = new List<string> { "release", "delete", tag, "--repo", repo, "--yes" };
        if (cleanupTag)
        {
            args.Add("--cleanup-tag");
        }

        return OkAsync(args, Short, null, cancellation);
    }

    public async Task<IReadOnlyList<string>> ListTagsAsync(string repo, CancellationToken cancellation) =>
        (await OkAsync(new[] { "api", $"repos/{repo}/tags?per_page=100", "--paginate", "--jq", ".[].name" }, Short, null, cancellation))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Trim())
            .ToList();

    public Task DeleteTagAsync(string repo, string tag, CancellationToken cancellation) =>
        OkAsync(new[] { "api", "-X", "DELETE", $"repos/{repo}/git/refs/tags/{EscapePath(tag)}" }, Short, null, cancellation);

    public async Task<IReadOnlyList<GhBranch>> ListBranchesAsync(string repo, CancellationToken cancellation)
    {
        var output = await OkAsync(new[] { "api", $"repos/{repo}/branches?per_page=100", "--paginate", "--jq", ".[] | \"\\(.name)\\t\\(.protected)\"" }, Short, null, cancellation);
        return output
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Split('\t'))
            .Select(p => new GhBranch(p[0].Trim(), p.Length > 1 && p[1].Trim().Equals("true", StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public Task DeleteBranchAsync(string repo, string branch, CancellationToken cancellation) =>
        OkAsync(new[] { "api", "-X", "DELETE", $"repos/{repo}/git/refs/heads/{EscapePath(branch)}" }, Short, null, cancellation);

    private static string EscapePath(string value) => string.Join("/", value.Split('/').Select(Uri.EscapeDataString));

    private async Task<IReadOnlyList<string>> OkAsync(IReadOnlyList<string> args, TimeSpan timeout, IProgress<string>? progress, CancellationToken cancellation)
    {
        var result = await RunAsync(args, timeout, progress, cancellation);
        if (result.TimedOut)
        {
            throw new GhException(GitTexts.GhTimeout, result.Output);
        }

        return result.ExitCode == 0 ? result.Output : throw Failure(result);
    }

    private static GhException Failure(ProcessResult result)
    {
        var lines = result.Error.Concat(result.Output).Where(l => !string.IsNullOrWhiteSpace(l)).TakeLast(3).Select(l => l.Trim()).ToList();
        var message = result.TimedOut
            ? GitTexts.GhTimeout
            : lines.Count == 0 ? GitTexts.GhExitCode(result.ExitCode) : string.Join(" ", lines);
        return new GhException(message, result.Output);
    }

    private async Task<ProcessResult> RunAsync(IReadOnlyList<string> args, TimeSpan timeout, IProgress<string>? progress, CancellationToken cancellation)
    {
        try
        {
            return await _runner.RunAsync(new ProcessSpec(_gh, args, null, timeout, Environment), progress, cancellation);
        }
        catch (Win32Exception ex)
        {
            throw new GhException(GitTexts.GhStartFailed(ex.Message), Array.Empty<string>());
        }
    }
}
