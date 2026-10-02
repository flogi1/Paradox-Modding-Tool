using ParadoxModTool.Updates;

namespace ParadoxModTool.Release;

/// <summary>Ergebnis: der neue öffentliche Commit oder ein Fehlertext (dann wird nicht gepusht).</summary>
public sealed record PublicCommitResult(string? Commit, string? Error);

/// <summary>
/// Öffentlicher Commit (Spec "PublicCommit", wie früher <c>release.ps1</c> Schritt 10): Dateistand von <c>HEAD</c> ohne <c>docs</c> in einem eigenen
/// Index, ein Commit "Release v&lt;version&gt;" mit der verborgenen GitHub-Adresse, Elternteil <c>refs/heads/public</c>; fehlt der (neuer Rechner,
/// frischer Clone), der zuletzt geholte Stand von GitHub (<c>refs/remotes/origin/main</c>), damit der Push nicht an vorhandener GitHub-Historie
/// scheitert. Arbeitsbaum und normaler Index bleiben unberührt. Vor der Rückgabe zwei Sicherheitsprüfungen.
/// </summary>
public static class PublicCommit
{
    public const string Name = "Florian";

    public const string Email = "152910095+flogi1@users.noreply.github.com";

    public static readonly IReadOnlyList<string> Excluded = ["docs"];

    public static async Task<PublicCommitResult> CreateAsync(ICommandRunner runner, string repoRoot, AppVersion version, Action<string> log, CancellationToken cancellation)
    {
        var index = Path.Combine(Path.GetTempPath(), $"pmt-public-index-{Guid.NewGuid():N}");
        var withIndex = new Dictionary<string, string> { ["GIT_INDEX_FILE"] = index };
        try
        {
            async Task<CommandResult> Git(IReadOnlyDictionary<string, string>? environment, params string[] arguments) =>
                await runner.RunAsync("git", arguments, repoRoot, environment, log, cancellation).ConfigureAwait(false);

            if ((await Git(withIndex, "read-tree", "HEAD")).ExitCode != 0)
            {
                return new PublicCommitResult(null, "read-tree fehlgeschlagen.");
            }

            foreach (var excluded in Excluded)
            {
                if ((await Git(withIndex, "rm", "-r", "--cached", "--quiet", "--ignore-unmatch", "--", excluded)).ExitCode != 0)
                {
                    return new PublicCommitResult(null, $"Ausschluss {excluded} fehlgeschlagen.");
                }
            }

            var tree = await Git(withIndex, "write-tree");
            if (tree.ExitCode != 0 || tree.Output.Count == 0)
            {
                return new PublicCommitResult(null, "write-tree fehlgeschlagen.");
            }

            var parent = await Git(null, "rev-parse", "--verify", "--quiet", "refs/heads/public");
            if (parent.ExitCode != 0 || parent.Output.Count == 0)
            {
                parent = await Git(null, "rev-parse", "--verify", "--quiet", "refs/remotes/origin/main");
            }

            var arguments = new List<string> { "commit-tree", tree.Output[0].Trim() };
            if (parent.ExitCode == 0 && parent.Output.Count > 0)
            {
                arguments.AddRange(["-p", parent.Output[0].Trim()]);
            }

            arguments.AddRange(["-m", $"Release v{version}"]);
            var identity = new Dictionary<string, string>
            {
                ["GIT_AUTHOR_NAME"] = Name,
                ["GIT_AUTHOR_EMAIL"] = Email,
                ["GIT_COMMITTER_NAME"] = Name,
                ["GIT_COMMITTER_EMAIL"] = Email,
            };
            var commit = await Git(identity, [.. arguments]);
            if (commit.ExitCode != 0 || commit.Output.Count == 0)
            {
                return new PublicCommitResult(null, "commit-tree fehlgeschlagen.");
            }

            var hash = commit.Output[0].Trim();

            // Letzte Sicherung vor dem Push: nichts Ausgeschlossenes, richtige Adresse.
            var files = await Git(null, "-c", "core.quotepath=off", "ls-tree", "-r", "--name-only", hash);
            var leaked = files.Output.Where(name => Excluded.Any(excluded => name.StartsWith(excluded + "/", StringComparison.Ordinal))).ToList();
            if (files.ExitCode != 0 || leaked.Count > 0)
            {
                return new PublicCommitResult(null, $"Öffentlicher Commit enthält ausgeschlossene Dateien: {string.Join(", ", leaked.Take(3))}");
            }

            var addresses = await Git(null, "log", "-1", "--format=%ae|%ce", hash);
            if (addresses.ExitCode != 0 || addresses.Output.FirstOrDefault()?.Trim() != $"{Email}|{Email}")
            {
                return new PublicCommitResult(null, "Öffentlicher Commit hat die falsche E-Mail-Adresse.");
            }

            return new PublicCommitResult(hash, null);
        }
        finally
        {
            try
            {
                File.Delete(index);
            }
            catch (IOException)
            {
                // Eine liegen gebliebene Temp-Datei ist harmlos.
            }
        }
    }
}
