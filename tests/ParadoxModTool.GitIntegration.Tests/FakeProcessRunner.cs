namespace ParadoxModTool.GitIntegration.Tests;

/// <summary>Ersetzt <see cref="IProcessRunner"/> im Test: hält Aufrufe fest und antwortet nach Vorgaben (die zuletzt registrierte passende zählt, sonst leerer Erfolg).</summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    private readonly List<(Func<ProcessSpec, bool> Match, ProcessResult Result)> _rules = new();

    public List<ProcessSpec> Calls { get; } = new();

    public Func<ProcessSpec, ProcessResult>? Handler { get; set; }

    public static string Cmd(ProcessSpec spec) => string.Join(" ", spec.Arguments);

    public static ProcessResult Ok(params string[] lines) => new(0, lines, Array.Empty<string>(), false);

    public static ProcessResult Fail(int code, string error) => new(code, Array.Empty<string>(), new[] { error }, false);

    public void On(string tail, ProcessResult result) => _rules.Add((s => Cmd(s).EndsWith(tail, StringComparison.Ordinal), result));

    public void OnContains(string text, ProcessResult result) => _rules.Add((s => Cmd(s).Contains(text, StringComparison.Ordinal), result));

    public Task<ProcessResult> RunAsync(ProcessSpec spec, IProgress<string>? lines, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        Calls.Add(spec);
        if (Handler is not null)
        {
            return Task.FromResult(Handler(spec));
        }

        foreach (var (match, result) in Enumerable.Reverse(_rules))
        {
            if (match(spec))
            {
                return Task.FromResult(result);
            }
        }

        return Task.FromResult(Ok());
    }
}
