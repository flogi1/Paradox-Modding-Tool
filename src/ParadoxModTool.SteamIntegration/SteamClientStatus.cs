using System.Diagnostics;

namespace ParadoxModTool.SteamIntegration;

public static class SteamClientStatus
{
    /// <summary>Läuft ein Prozess <c>steam</c> (der Steam-Client)?</summary>
    public static bool IsSteamRunning()
    {
        var processes = Process.GetProcessesByName("steam");
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}
