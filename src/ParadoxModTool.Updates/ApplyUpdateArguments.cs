using System.Globalization;

namespace ParadoxModTool.Updates;

/// <summary>
/// Aufruf des Update-Modus: <c>ParadoxModTool.exe --apply-update --protocol 1 --target … --wait-pid … --from-version … --to-version …
/// [--game …] [--lang …]</c>. Diese Kommandozeile ist ein Vertrag über alle Versionen: Die alte Version ruft die neue auf. Parameter
/// dürfen dazukommen, vorhandene dürfen nie wegfallen oder ihre Bedeutung ändern; Unbekanntes wird ignoriert.
/// </summary>
public sealed record ApplyUpdateArguments(int Protocol, string Target, int WaitPid, AppVersion FromVersion, AppVersion ToVersion, string? GameId, string? Language)
{
    public const string Switch = "--apply-update";

    public const int CurrentProtocol = 1;

    public IReadOnlyList<string> ToArguments()
    {
        var args = new List<string>
        {
            Switch,
            "--protocol", Protocol.ToString(CultureInfo.InvariantCulture),
            "--target", Target,
            "--wait-pid", WaitPid.ToString(CultureInfo.InvariantCulture),
            "--from-version", FromVersion.ToString(),
            "--to-version", ToVersion.ToString(),
        };
        if (!string.IsNullOrEmpty(GameId))
        {
            args.AddRange(["--game", GameId]);
        }

        if (!string.IsNullOrEmpty(Language))
        {
            args.AddRange(["--lang", Language]);
        }

        return args;
    }

    public static ApplyUpdateArguments? Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || args[0] != Switch)
        {
            return null;
        }

        var options = CommandLine.ReadOptions(args, 1);
        if (!TryInt(options, "--protocol", out var protocol) || protocol < 1
            || !options.TryGetValue("--target", out var target) || !Path.IsPathFullyQualified(target)
            || !TryInt(options, "--wait-pid", out var pid)
            || !AppVersion.TryParse(options.GetValueOrDefault("--from-version"), out var from)
            || !AppVersion.TryParse(options.GetValueOrDefault("--to-version"), out var to))
        {
            return null;
        }

        return new ApplyUpdateArguments(protocol, Path.TrimEndingDirectorySeparator(target), pid, from, to, Empty(options, "--game"), Empty(options, "--lang"));
    }

    private static bool TryInt(Dictionary<string, string> options, string name, out int value)
    {
        value = 0;
        return options.TryGetValue(name, out var text) && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static string? Empty(Dictionary<string, string> options, string name) =>
        options.TryGetValue(name, out var value) && value.Length > 0 ? value : null;
}

/// <summary>Was die normale App beim Start über ein vorangegangenes Update erfährt.</summary>
public sealed record UpdateStartInfo(AppVersion? Applied, AppVersion? From, AppVersion? Failed, AppVersion? Cancelled)
{
    public bool IsUpdateStart => Applied is not null || Failed is not null || Cancelled is not null;
}

/// <summary>Start-Argumente, mit denen der Update-Modus die normale App wieder startet (immer mit <c>--game</c> davor).</summary>
public static class UpdateStartArguments
{
    public const string Applied = "--update-applied";

    public const string From = "--update-from";

    public const string Failed = "--update-failed";

    public const string Cancelled = "--update-cancelled";

    public static IReadOnlyList<string> ForApplied(string? gameId, AppVersion to, AppVersion from) =>
        [.. Game(gameId), Applied, to.ToString(), From, from.ToString()];

    public static IReadOnlyList<string> ForFailed(string? gameId, AppVersion to) => [.. Game(gameId), Failed, to.ToString()];

    public static IReadOnlyList<string> ForCancelled(string? gameId, AppVersion to) => [.. Game(gameId), Cancelled, to.ToString()];

    public static UpdateStartInfo Parse(IReadOnlyList<string> args)
    {
        var options = CommandLine.ReadOptions(args, 0);
        return new UpdateStartInfo(Version(options, Applied), Version(options, From), Version(options, Failed), Version(options, Cancelled));
    }

    /// <summary>
    /// Die Start-Argumente ohne die Update-Schalter und ihre Werte. Andere Auswertungen der Kommandozeile (z. B. der Aufruf als externer
    /// Editor, der jedes Argument ohne "--" für einen Dateipfad hält) dürfen "--update-applied 1.2.0" nicht zu sehen bekommen.
    /// </summary>
    public static string[] WithoutUpdateArguments(IReadOnlyList<string> args)
    {
        var result = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] is Applied or From or Failed or Cancelled)
            {
                if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    i++;
                }

                continue;
            }

            result.Add(args[i]);
        }

        return result.ToArray();
    }

    private static IEnumerable<string> Game(string? gameId) => string.IsNullOrEmpty(gameId) ? [] : ["--game", gameId];

    private static AppVersion? Version(Dictionary<string, string> options, string name) =>
        options.TryGetValue(name, out var text) && AppVersion.TryParse(text, out var version) ? version : null;
}
