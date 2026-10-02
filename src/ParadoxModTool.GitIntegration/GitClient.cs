using System.ComponentModel;

namespace ParadoxModTool.GitIntegration;

public sealed record GitChange(string Status, string Path);

/// <param name="Branch"><see langword="null"/> bei losgelöstem HEAD.</param>
/// <param name="HeadPushed">Ein Remote-Branch enthält den aktuellen Commit.</param>
public sealed record GitState(
    string TopLevel,
    string Head,
    string HeadSubject,
    string? Branch,
    string? Origin,
    string? OriginRepo,
    string? Upstream,
    int Ahead,
    int Behind,
    bool HeadPushed,
    IReadOnlyList<GitChange> Changes)
{
    public bool IsClean => Changes.Count == 0;

    public string ShortHead => Head.Length > 8 ? Head[..8] : Head;
}

public sealed record GitOperationResult(bool Success, string Message, IReadOnlyList<string> Output);

/// <summary>
/// Ruft <c>git</c> auf: der Stand des Mod-Ordners wird nur gelesen; Committen (<c>add -A</c>, <c>commit</c>) und Pushen (nie erzwungen) sind eigene, ausdrückliche Aufrufe.
/// Git fragt nie nach Eingaben (Standardeingabe geschlossen, <c>GIT_TERMINAL_PROMPT=0</c>).
/// </summary>
public sealed class GitClient
{
    private static readonly IReadOnlyDictionary<string, string> Environment = new Dictionary<string, string>
    {
        ["GIT_TERMINAL_PROMPT"] = "0",
        ["GCM_INTERACTIVE"] = "never",
    };

    private readonly IProcessRunner _runner;
    private readonly string _git;

    public GitClient(IProcessRunner runner, string gitPath = "git")
    {
        _runner = runner;
        _git = gitPath;
    }

    public async Task<GitState?> InspectAsync(string folder, CancellationToken cancellation)
    {
        var top = await ReadAsync(folder, cancellation, "rev-parse", "--show-toplevel");
        var head = await ReadAsync(folder, cancellation, "rev-parse", "HEAD");
        if (top is not { Count: > 0 } || head is not { Count: > 0 })
        {
            return null;
        }

        var origin = (await ReadAsync(folder, cancellation, "remote", "get-url", "origin"))?.FirstOrDefault();
        var branch = (await ReadAsync(folder, cancellation, "rev-parse", "--abbrev-ref", "HEAD"))?.FirstOrDefault();
        var subject = (await ReadAsync(folder, cancellation, "log", "-1", "--format=%s"))?.FirstOrDefault() ?? string.Empty;
        var upstream = (await ReadAsync(folder, cancellation, "rev-parse", "--abbrev-ref", "@{u}"))?.FirstOrDefault();
        var ahead = 0;
        var behind = 0;
        if (upstream is not null && (await ReadAsync(folder, cancellation, "rev-list", "--left-right", "--count", "HEAD...@{u}"))?.FirstOrDefault() is { } counts)
        {
            var parts = counts.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                int.TryParse(parts[0], out ahead);
                int.TryParse(parts[1], out behind);
            }
        }

        var contains = await ReadAsync(folder, cancellation, "branch", "-r", "--contains", "HEAD");
        var status = await ReadAsync(folder, cancellation, "status", "--porcelain") ?? new List<string>();
        var changes = status.Where(l => l.Length >= 4).Select(ParseChange).ToList();

        return new GitState(
            top[0].Trim(),
            head[0].Trim(),
            subject,
            branch is null or "HEAD" ? null : branch,
            origin,
            GitRemoteUrl.ToRepo(origin),
            upstream,
            ahead,
            behind,
            contains is { Count: > 0 },
            changes);
    }

    public async Task<GitOperationResult> CommitAllAsync(string folder, string message, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new GitOperationResult(false, GitTexts.CommitMessageMissing, Array.Empty<string>());
        }

        var add = await RunAsync(folder, TimeSpan.FromMinutes(2), cancellation, "add", "-A");
        if (add.ExitCode != 0 || add.TimedOut)
        {
            return Failure(GitTexts.StageFailed, add);
        }

        var commit = await RunAsync(folder, TimeSpan.FromMinutes(2), cancellation, "commit", "-m", message.Trim());
        return commit.ExitCode == 0 && !commit.TimedOut
            ? new GitOperationResult(true, GitTexts.Committed, commit.Output)
            : Failure(GitTexts.CommitFailed, commit);
    }

    public async Task<GitOperationResult> PushAsync(string folder, GitState state, CancellationToken cancellation)
    {
        if (state.Branch is null)
        {
            return new GitOperationResult(false, GitTexts.PushDetached, Array.Empty<string>());
        }

        if (state.Behind > 0)
        {
            return new GitOperationResult(false, GitTexts.PushBehind(state.Behind, state.Upstream), Array.Empty<string>());
        }

        var args = state.Upstream is null ? new[] { "push", "-u", "origin", state.Branch } : new[] { "push" };
        var result = await RunAsync(folder, TimeSpan.FromMinutes(5), cancellation, args);
        if (result.ExitCode == 0 && !result.TimedOut)
        {
            return new GitOperationResult(true, GitTexts.Pushed, result.Output.Concat(result.Error).ToList());
        }

        var text = string.Join(" ", result.Error.Concat(result.Output));
        var hint = text.Contains("could not read Username", StringComparison.OrdinalIgnoreCase) || text.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase)
            ? GitTexts.PushNoCredentials
            : string.Empty;
        return Failure(GitTexts.PushFailed + hint, result);
    }

    private static GitOperationResult Failure(string message, ProcessResult result)
    {
        var details = result.TimedOut
            ? GitTexts.Timeout
            : string.Join(" ", result.Error.Concat(result.Output).Where(l => !string.IsNullOrWhiteSpace(l)).TakeLast(3));
        return new GitOperationResult(false, details.Length == 0 ? message : message + " " + details, result.Output.Concat(result.Error).ToList());
    }

    private static GitChange ParseChange(string line)
    {
        var path = line[3..].Trim();
        if (path.Length >= 2 && path[0] == '"' && path[^1] == '"')
        {
            path = path[1..^1];
        }

        return new GitChange(line[..2].Trim(), path);
    }

    private Task<ProcessResult> RunAsync(string folder, TimeSpan timeout, CancellationToken cancellation, params string[] args) =>
        _runner.RunAsync(new ProcessSpec(_git, new[] { "-C", folder }.Concat(args).ToList(), null, timeout, Environment), null, cancellation);

    /// <summary>Die nicht leeren Ausgabezeilen; <see langword="null"/>, wenn der Aufruf scheitert oder <c>git</c> fehlt.</summary>
    private async Task<List<string>?> ReadAsync(string folder, CancellationToken cancellation, params string[] args)
    {
        try
        {
            var result = await RunAsync(folder, TimeSpan.FromSeconds(30), cancellation, args);
            return result.ExitCode == 0 && !result.TimedOut ? result.Output.Where(l => !string.IsNullOrWhiteSpace(l)).ToList() : null;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }
}
