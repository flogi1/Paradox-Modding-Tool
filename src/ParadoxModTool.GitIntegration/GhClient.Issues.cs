using System.Globalization;
using System.Text.Json;

namespace ParadoxModTool.GitIntegration;

/// <param name="StateReason">Grund bei geschlossenen Issues (<c>completed</c>, <c>not_planned</c>), sonst leer.</param>
public sealed record GhIssue(int Number, string Title, string Body, string Author, bool IsOpen, string StateReason, string Url, DateTime CreatedUtc, DateTime UpdatedUtc, DateTime? ClosedUtc, IReadOnlyList<string> Labels);

public sealed record GhComment(string Author, DateTime CreatedUtc, string Body);

/// <summary>Issues für den Bugtracker, alles über die REST-API (<c>gh api</c>), seitenweise; <c>--jq</c> liefert eine Zeile JSON je Eintrag.</summary>
public sealed partial class GhClient
{
    private static readonly TimeSpan ListTimeout = TimeSpan.FromMinutes(5);

    private const string IssueJq =
        ".[] | select(.pull_request == null) | {number, title, body: (.body // \"\"), author: (.user.login // \"\"), state, reason: (.state_reason // \"\"), url: .html_url, created: .created_at, updated: .updated_at, closed: .closed_at, labels: [.labels[].name]}";

    private const string CommentJq = ".[] | {author: (.user.login // \"\"), created: .created_at, body: (.body // \"\")}";

    /// <summary>Alle Issues des Repositories (offen und geschlossen), ohne Pull Requests.</summary>
    public async Task<IReadOnlyList<GhIssue>> ListIssuesAsync(string repo, CancellationToken cancellation)
    {
        var output = await OkAsync(new[] { "api", $"repos/{repo}/issues?state=all&per_page=100", "--paginate", "--jq", IssueJq }, ListTimeout, null, cancellation);
        return ParseLines(output, GitTexts.IssueListUnreadable, e => new GhIssue(
            e.GetProperty("number").GetInt32(),
            Text(e, "title"),
            Text(e, "body"),
            Text(e, "author"),
            Text(e, "state").Equals("open", StringComparison.OrdinalIgnoreCase),
            Text(e, "reason"),
            Text(e, "url"),
            Date(e, "created") ?? DateTime.MinValue,
            Date(e, "updated") ?? DateTime.MinValue,
            Date(e, "closed"),
            e.TryGetProperty("labels", out var labels) && labels.ValueKind == JsonValueKind.Array
                ? labels.EnumerateArray().Select(l => l.GetString() ?? string.Empty).Where(l => l.Length > 0).ToList()
                : new List<string>()));
    }

    public async Task<IReadOnlyList<GhComment>> ListIssueCommentsAsync(string repo, int number, CancellationToken cancellation)
    {
        var output = await OkAsync(new[] { "api", $"repos/{repo}/issues/{Number(number)}/comments?per_page=100", "--paginate", "--jq", CommentJq }, ListTimeout, null, cancellation);
        return ParseLines(output, GitTexts.CommentsUnreadable, e => new GhComment(Text(e, "author"), Date(e, "created") ?? DateTime.MinValue, Text(e, "body")));
    }

    public async Task<IReadOnlyList<string>> ListLabelsAsync(string repo, CancellationToken cancellation) =>
        (await OkAsync(new[] { "api", $"repos/{repo}/labels?per_page=100", "--paginate", "--jq", ".[].name" }, Short, null, cancellation))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Trim())
            .ToList();

    /// <summary>Schließt das Issue als erledigt oder (<paramref name="notPlanned"/>) als "not planned"; ändert auch den Grund eines schon geschlossenen Issues.</summary>
    public Task CloseIssueAsync(string repo, int number, bool notPlanned, CancellationToken cancellation) =>
        OkAsync(new[] { "api", "-X", "PATCH", $"repos/{repo}/issues/{Number(number)}", "-f", "state=closed", "-f", notPlanned ? "state_reason=not_planned" : "state_reason=completed" }, Short, null, cancellation);

    public Task ReopenIssueAsync(string repo, int number, CancellationToken cancellation) =>
        OkAsync(new[] { "api", "-X", "PATCH", $"repos/{repo}/issues/{Number(number)}", "-f", "state=open" }, Short, null, cancellation);

    private static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static DateTime? Date(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date.UtcDateTime
            : null;

    private static IReadOnlyList<T> ParseLines<T>(IReadOnlyList<string> output, string error, Func<JsonElement, T> read)
    {
        var result = new List<T>();
        try
        {
            foreach (var line in output.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                using var document = JsonDocument.Parse(line);
                result.Add(read(document.RootElement));
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new GhException(error, output);
        }

        return result;
    }
}
