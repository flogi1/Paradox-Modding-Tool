using System.Net;
using System.Text;

namespace ParadoxModTool.BugTracking.Tests;

/// <summary>Ersetzt das Netz im Test: hält Anfragen fest und antwortet nach Regeln (die zuletzt registrierte passende zählt, sonst 404).</summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly List<(string Method, string Path, Func<HttpResponseMessage> Respond)> _rules = new();

    public List<(string Method, string Url, string? Body, string? Auth)> Requests { get; } = new();

    public static HttpResponseMessage Json(int status, string json) =>
        new((HttpStatusCode)status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary><paramref name="path"/> wird als Teil der URL (ohne Basis) gesucht.</summary>
    public void On(string method, string path, int status, string json) => _rules.Add((method, path, () => Json(status, json)));

    public void On(string method, string path, Func<HttpResponseMessage> respond) => _rules.Add((method, path, respond));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var url = request.RequestUri!.AbsoluteUri;
        Requests.Add((request.Method.Method, url, body, request.Headers.Authorization?.ToString()));
        foreach (var (method, path, respond) in Enumerable.Reverse(_rules))
        {
            if (method == request.Method.Method && url.Contains(path, StringComparison.Ordinal))
            {
                return respond();
            }
        }

        return Json(404, """{"message":"Unknown","code":10003}""");
    }
}
