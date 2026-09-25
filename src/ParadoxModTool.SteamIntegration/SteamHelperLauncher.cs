using System.Diagnostics;
using System.Text;

namespace ParadoxModTool.SteamIntegration;

/// <summary>
/// Startet das eigene Programm noch einmal als Hilfsprozess (<see cref="SteamUploadProtocol.Argument"/>, Pfad der Anfragedatei), ohne Fenster, mit umgeleiteter Ein- und Ausgabe.
/// Bei Abbruch schickt es <c>CANCEL</c> und beendet den Prozess erst nach einer Karenzzeit hart.
/// </summary>
public sealed class SteamHelperLauncher : ISteamHelperLauncher
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(20);

    public async Task<int> RunAsync(string requestFile, Action<string> onLine, CancellationToken cancellation)
    {
        var path = Environment.ProcessPath ?? throw new InvalidOperationException("Der Pfad des Programms ist unbekannt.");
        var info = new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            StandardInputEncoding = new UTF8Encoding(false),
        };

        // Läuft das Programm über "dotnet Programm.dll", ist der Prozess dotnet selbst; dann gehört die DLL als erstes Argument dazu.
        if (string.Equals(Path.GetFileNameWithoutExtension(path), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            info.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
        }

        info.ArgumentList.Add(SteamUploadProtocol.Argument);
        info.ArgumentList.Add(requestFile);

        using var process = new Process { StartInfo = info };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                onLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, _) =>
        {
        };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var registration = cancellation.Register(() => RequestCancel(process));
        await process.WaitForExitAsync(CancellationToken.None);
        process.WaitForExit();
        return process.ExitCode;
    }

    private static void RequestCancel(Process process)
    {
        try
        {
            process.StandardInput.WriteLine(SteamUploadProtocol.CancelLine);
            process.StandardInput.Flush();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException)
        {
            // Der Prozess ist schon weg oder die Eingabe geschlossen; das Beenden unten erledigt den Rest.
        }

        _ = Task.Delay(Grace).ContinueWith(_ =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                // Der Prozess endete inzwischen oder wurde schon freigegeben.
            }
        });
    }
}
