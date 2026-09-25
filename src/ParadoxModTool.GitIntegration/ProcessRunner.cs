using System.Diagnostics;
using System.Text;

namespace ParadoxModTool.GitIntegration;

/// <summary>Ein Programmaufruf. Die Argumente sind eine Liste (keine Shell, kein Quoting).</summary>
public sealed record ProcessSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    TimeSpan? Timeout = null,
    IReadOnlyDictionary<string, string>? Environment = null);

/// <param name="TimedOut">Die Zeitgrenze war erreicht; der Prozessbaum wurde beendet (<paramref name="ExitCode"/> ist dann <c>-1</c>).</param>
public sealed record ProcessResult(int ExitCode, IReadOnlyList<string> Output, IReadOnlyList<string> Error, bool TimedOut);

/// <summary>Startet Programme (<c>git</c>, <c>gh</c>). Die Abstraktion erlaubt Tests ohne echte Programme.</summary>
public interface IProcessRunner
{
    /// <exception cref="System.ComponentModel.Win32Exception">Das Programm ließ sich nicht starten.</exception>
    /// <exception cref="OperationCanceledException">Abbruch; der Prozessbaum wurde vorher beendet.</exception>
    Task<ProcessResult> RunAsync(ProcessSpec spec, IProgress<string>? lines, CancellationToken cancellation);
}

/// <summary>Der echte Prozess: ohne Fenster, Ausgabe umgeleitet, Standardeingabe sofort geschlossen (fragt ein Programm nach etwas, bekommt es kein Zeichen).</summary>
public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessSpec spec, IProgress<string>? lines, CancellationToken cancellation)
    {
        var info = new ProcessStartInfo(spec.FileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in spec.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        if (!string.IsNullOrEmpty(spec.WorkingDirectory))
        {
            info.WorkingDirectory = spec.WorkingDirectory;
        }

        if (spec.Environment is not null)
        {
            foreach (var (key, value) in spec.Environment)
            {
                info.Environment[key] = value;
            }
        }

        var output = new List<string>();
        var error = new List<string>();
        var gate = new object();
        using var process = new Process { StartInfo = info };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            lock (gate)
            {
                output.Add(e.Data);
            }

            lines?.Report(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            lock (gate)
            {
                error.Add(e.Data);
            }

            lines?.Report(e.Data);
        };

        process.Start();
        process.StandardInput.Close();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = new CancellationTokenSource(spec.Timeout ?? Timeout.InfiniteTimeSpan);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, timeout.Token);
        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Der Prozess ist schon beendet.
            }

            process.WaitForExit();
            if (cancellation.IsCancellationRequested)
            {
                throw;
            }

            timedOut = true;
        }

        process.WaitForExit();
        lock (gate)
        {
            return new ProcessResult(timedOut ? -1 : process.ExitCode, output.ToList(), error.ToList(), timedOut);
        }
    }
}
