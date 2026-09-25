namespace ParadoxModTool.SteamIntegration.Tests;

/// <summary>Ersetzt SteamCMD im Test: <see cref="Behavior"/> bekommt den Start und darf z. B. die VDF-Datei umschreiben.</summary>
internal sealed class FakeSteamCmdProcess : ISteamCmdProcess
{
    public Func<SteamCmdStart, CancellationToken, Task<SteamCmdRun>> Behavior { get; set; } =
        (_, _) => Task.FromResult(new SteamCmdRun(0, new[] { "Success." }, false));

    public List<SteamCmdStart> Starts { get; } = new();

    public Task<SteamCmdRun> RunAsync(SteamCmdStart start, IProgress<string>? lines, CancellationToken cancellation)
    {
        Starts.Add(start);
        return Behavior(start, cancellation);
    }

    /// <summary>Der Pfad der VDF-Datei aus den Argumenten (<c>+login name +workshop_build_item vdf +quit</c>).</summary>
    public static string VdfPath(SteamCmdStart start) => start.Arguments[3];
}
