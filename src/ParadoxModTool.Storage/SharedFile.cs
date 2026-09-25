using System.Security.Cryptography;
using System.Text;

namespace ParadoxModTool.Storage;

/// <summary>
/// Hilfen für Dateien, die mehrere gleichzeitig laufende Programmprozesse (z. B. ein Fenster je Spiel) benutzen:
/// ein prozessübergreifender Schreibschutz und atomares Schreiben. Zusammen ergeben sie das Muster
/// "unter Sperre frisch lesen, ändern, atomar schreiben", bei dem sich zwei Prozesse nicht gegenseitig Änderungen überschreiben.
/// </summary>
public static class SharedFile
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private const int MoveAttempts = 15;

    /// <summary>
    /// Führt <paramref name="action"/> aus, während dieser Prozess die benannte Sperre für <paramref name="path"/> hält.
    /// Andere Prozesse (und Threads), die dieselbe Datei sperren wollen, warten. Bekommt der Aufruf die Sperre nicht innerhalb
    /// des Zeitlimits (z. B. weil ein anderer Prozess hängt), läuft <paramref name="action"/> trotzdem - ein hängender Prozess
    /// darf das Speichern nie dauerhaft verhindern.
    /// </summary>
    public static T Locked<T>(string path, Func<T> action, TimeSpan? timeout = null)
    {
        using var mutex = new Mutex(false, MutexNameFor(path));
        var held = false;
        try
        {
            held = mutex.WaitOne(timeout ?? DefaultTimeout);
        }
        catch (AbandonedMutexException)
        {
            // Ein anderer Prozess ist mit gehaltener Sperre beendet worden - die Sperre gehört jetzt uns.
            held = true;
        }

        try
        {
            return action();
        }
        finally
        {
            if (held)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    public static void Locked(string path, Action action) =>
        Locked<object?>(path, () =>
        {
            action();
            return null;
        });

    /// <summary>
    /// Schreibt <paramref name="contents"/> zuerst in eine Nachbardatei und ersetzt dann <paramref name="path"/> in einem Schritt.
    /// Ein Absturz mitten im Schreiben hinterlässt so nie eine halbe Datei. Fehlende Ordner werden angelegt.
    /// </summary>
    public static void WriteAllTextAtomic(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            File.WriteAllText(temp, contents);
            MoveWithRetry(temp, path);
        }
        catch
        {
            try
            {
                File.Delete(temp);
            }
            catch (IOException)
            {
                // Aufräumen ist nur ein Versuch.
            }

            throw;
        }
    }

    /// <summary>
    /// Ersetzt <paramref name="destination"/> durch <paramref name="source"/>. Unter Windows scheitert das Ersetzen kurz mit
    /// <see cref="UnauthorizedAccessException"/> oder <see cref="IOException"/>, wenn ein fremder Prozess (Virenscanner, Indexer)
    /// die Zieldatei gerade ohne Löschfreigabe geöffnet hat; die Sperre von <see cref="Locked{T}"/> hält solche Prozesse nicht ab.
    /// Der Zustand ist nach Millisekunden vorbei, darum wird mit wachsender Pause wiederholt, bevor der Fehler durchgereicht wird.
    /// </summary>
    private static void MoveWithRetry(string source, string destination)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(source, destination, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException && attempt < MoveAttempts)
            {
                Thread.Sleep(10 * attempt);
            }
        }
    }

    private static string MutexNameFor(string path)
    {
        var normalized = Path.GetFullPath(path).Replace('\\', '/').ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return "Local\\ParadoxModTool_" + Convert.ToHexString(hash, 0, 12);
    }
}
