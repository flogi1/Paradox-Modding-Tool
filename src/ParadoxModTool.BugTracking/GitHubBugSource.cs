using System.Globalization;
using ParadoxModTool.GitIntegration;
using ParadoxModTool.Mods;
using ParadoxModTool.Notes;

namespace ParadoxModTool.BugTracking;

/// <summary>
/// GitHub-Issues als Meldungen, über <c>gh</c>. <c>client</c> liefert den gh-Client oder <see langword="null"/>, wenn gh nicht eingerichtet ist
/// (wird bei jedem Aufruf neu gefragt, damit ein inzwischen eingerichtetes gh sofort zählt).
/// </summary>
public sealed class GitHubBugSource : IBugSource
{
    private readonly Func<GhClient?> _client;

    public GitHubBugSource(Func<GhClient?> client) => _client = client;

    public string Kind => BugSourceKinds.GitHub;

    public BugSource Source => BugSource.GitHub;

    /// <summary>Offen = Offen; geschlossen mit Grund "not planned" = Verworfen; sonst geschlossen = Erledigt.</summary>
    public static BugStatus StatusOf(GhIssue issue) =>
        issue.IsOpen ? BugStatus.Open
        : issue.StateReason.Equals("not_planned", StringComparison.OrdinalIgnoreCase) ? BugStatus.Rejected
        : BugStatus.Done;

    public Task<IReadOnlyList<RemoteBug>> FetchAsync(BugSourceEntry entry, CancellationToken cancellation) =>
        RunAsync<IReadOnlyList<RemoteBug>>(async gh => (await gh.ListIssuesAsync(entry.Key, cancellation))
            .Where(issue => entry.Labels.Count == 0 || issue.Labels.Any(label => entry.Labels.Contains(label, StringComparer.OrdinalIgnoreCase)))
            .Select(issue => new RemoteBug(
                issue.Number.ToString(CultureInfo.InvariantCulture),
                issue.Url,
                issue.Title,
                issue.Body,
                issue.Author,
                StatusOf(issue),
                issue.CreatedUtc,
                issue.UpdatedUtc,
                issue.ClosedUtc,
                BugImageLinks.Extract(issue.Body)))
            .ToList());

    public Task SetStatusAsync(BugSourceEntry entry, BugReport report, BugStatus status, CancellationToken cancellation) =>
        RunAsync(async gh =>
        {
            var number = NumberOf(report);
            if (status == BugStatus.Open)
            {
                await gh.ReopenIssueAsync(entry.Key, number, cancellation);
            }
            else
            {
                await gh.CloseIssueAsync(entry.Key, number, status == BugStatus.Rejected, cancellation);
            }

            return true;
        });

    public Task<IReadOnlyList<BugComment>> LoadDiscussionAsync(BugSourceEntry entry, BugReport report, CancellationToken cancellation) =>
        RunAsync<IReadOnlyList<BugComment>>(async gh => (await gh.ListIssueCommentsAsync(entry.Key, NumberOf(report), cancellation))
            .Select(comment => new BugComment(comment.Author, comment.CreatedUtc, comment.Body, BugImageLinks.Extract(comment.Body)))
            .ToList());

    /// <summary>Labels des Repositories (für die Auswahl im Quellen-Dialog).</summary>
    public Task<IReadOnlyList<string>> LoadLabelsAsync(string repo, CancellationToken cancellation) =>
        RunAsync(gh => gh.ListLabelsAsync(repo, cancellation));

    private static int NumberOf(BugReport report) =>
        int.TryParse(report.ExternalId, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : throw new BugSourceException(BugTexts.SourceUnknown);

    private async Task<T> RunAsync<T>(Func<GhClient, Task<T>> action)
    {
        var gh = _client() ?? throw new BugSourceException(BugTexts.GhMissing);
        try
        {
            return await action(gh);
        }
        catch (GhException ex)
        {
            throw new BugSourceException(ex.Message, ex);
        }
    }
}
