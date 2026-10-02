using System.ComponentModel;
using System.Diagnostics;

namespace ParadoxModTool.Updates;

/// <summary>Echte Prozesse unter Windows.</summary>
public sealed class WindowsProcessControl : IProcessControl
{
    /// <summary>Prozesse namens ParadoxModTool mit Programmdatei im Ordner. Ist der Pfad nicht lesbar, zählt der Prozess vorsichtshalber mit.</summary>
    public IReadOnlyList<int> FindInFolder(string folder, int ownPid)
    {
        var result = new List<int>();
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(UpdatePaths.ExecutableName)))
        {
            using (process)
            {
                if (process.Id == ownPid)
                {
                    continue;
                }

                string? path;
                try
                {
                    path = process.MainModule?.FileName;
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    path = null;
                }

                if (path is null || IsInstalledIn(path, folder))
                {
                    result.Add(process.Id);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Liegt die Programmdatei direkt im Programmordner? PMT liegt immer oben im Ordner; ein Vergleich per Präfix verfehlte eine Installation im
    /// Laufwerk selbst (<c>D:\</c> wurde zu <c>D:\\</c>) und zählte bei Unterordnern fremde Kopien mit.
    /// </summary>
    public static bool IsInstalledIn(string exePath, string folder) =>
        Path.GetDirectoryName(Path.GetFullPath(exePath)) is { } directory
        && string.Equals(
            Path.TrimEndingDirectorySeparator(directory),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)),
            StringComparison.OrdinalIgnoreCase);

    public bool HasExited(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.HasExited;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return true;
        }
    }

    public void Kill(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
        }
    }

    public int Start(string exe, IReadOnlyList<string> args)
    {
        var info = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Prozess wurde nicht gestartet.");
        return process.Id;
    }
}
