namespace ParadoxModTool.Session;

/// <summary>
/// Hält für die Lebensdauer eines Programmfensters die Sperre auf ein Spiel: Ein Spiel kann nur in einem Prozess geöffnet sein,
/// damit zwei Fenster nicht denselben Zustand schreiben. Umgesetzt als Datei, die der Prozess exklusiv geöffnet hält -
/// stürzt der Prozess ab, gibt Windows sie automatisch frei (keine verwaiste Sperre).
/// </summary>
public sealed class GameLock : IDisposable
{
    private readonly FileStream _stream;

    private GameLock(FileStream stream)
    {
        _stream = stream;
    }

    /// <summary>Versucht die Sperre zu erhalten; <see langword="null"/>, wenn ein anderer Prozess das Spiel offen hat.</summary>
    public static GameLock? TryAcquire(string lockFilePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(lockFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var stream = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            return new GameLock(stream);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // Die Datei wird gerade von einem anderen Prozess gelöscht/geschlossen - zählt als "belegt".
            return null;
        }
    }

    public void Dispose() => _stream.Dispose();
}
