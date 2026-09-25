using System.Diagnostics;
using System.Text;

namespace ParadoxModTool.SteamIntegration;

/// <param name="Timeout">Nach dieser Zeit wird der Prozessbaum beendet und <see cref="SteamCmdRun.TimedOut"/> gesetzt.</param>
public sealed record SteamCmdStart(string ExecutablePath, IReadOnlyList<string> Arguments, TimeSpan Timeout);

/// <param name="Lines">Alle Ausgabezeilen (Standardausgabe und -fehler) in der Reihenfolge des Eintreffens.</param>
public sealed record SteamCmdRun(int ExitCode, IReadOnlyList<string> Lines, bool TimedOut);

/// <summary>Startet SteamCMD. Die Abstraktion erlaubt Tests ohne echtes Steam.</summary>
public interface ISteamCmdProcess
{
    /// <summary>Führt den Prozess aus und wartet. Bei Abbruch wird der Prozessbaum beendet und <see cref="OperationCanceledException"/> geworfen; bei Zeitüberschreitung kommt ein Lauf mit <see cref="SteamCmdRun.TimedOut"/> zurück.</summary>
    Task<SteamCmdRun> RunAsync(SteamCmdStart start, IProgress<string>? lines, CancellationToken cancellation);
}

/// <summary>
/// Der echte Prozess: ohne Fenster, Ausgabe umgeleitet, Standardeingabe sofort geschlossen. Fragt SteamCMD nach einem Passwort oder Steam-Guard-Code, bekommt es kein Zeichen und endet;
/// das Programm gibt so nie etwas an SteamCMD weiter.
/// </summary>
public sealed class SteamCmdProcess : ISteamCmdProcess
{
    public async Task<SteamCmdRun> RunAsync(SteamCmdStart start, IProgress<string>? lines, CancellationToken cancellation)
    {
        var info = new ProcessStartInfo(start.ExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (Path.GetDirectoryName(start.ExecutablePath) is { Length: > 0 } workingDirectory)
        {
            info.WorkingDirectory = workingDirectory;
        }

        foreach (var argument in start.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        var collected = new List<string>();
        void OnLine(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (collected)
            {
                collected.Add(line);
            }

            lines?.Report(line);
        }

        using var process = new Process { StartInfo = info };
        process.OutputDataReceived += (_, e) => OnLine(e.Data);
        process.ErrorDataReceived += (_, e) => OnLine(e.Data);
        process.Start();
        process.StandardInput.Close();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(start.Timeout);
        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            if (cancellation.IsCancellationRequested)
            {
                throw;
            }

            timedOut = true;
        }

        if (!timedOut)
        {
            process.WaitForExit();
        }

        List<string> snapshot;
        lock (collected)
        {
            snapshot = new List<string>(collected);
        }

        return new SteamCmdRun(timedOut ? -1 : process.ExitCode, snapshot, timedOut);
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Der Prozess war schon beendet.
        }
    }
}
