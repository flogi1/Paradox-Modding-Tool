using System.Text.Json;
using ParadoxModTool.FlagTracker;

namespace ParadoxModTool.ScriptCheck;

/// <summary>
/// Ausgeblendete Funde einer Mod. Gespeichert werden die Schlüssel (<see cref="Finding.Key"/>, <c>Art|Name</c>) als JSON-Liste von
/// Zeichenketten in <c>&lt;Ordner&gt;/&lt;Schlüssel der Mod&gt;.json</c>, Schlüssel aus <see cref="FlagHideList.KeyFor"/>. Nicht threadsicher;
/// nur vom UI-Thread benutzen.
/// </summary>
public sealed class ScriptCheckHideList
{
    private readonly string _directory;

    /// <summary>Zieldatei; null, wenn sich aus dem Mod-Ordner kein Schlüssel bilden ließ (dann scheitert <see cref="Save"/>).</summary>
    private readonly string? _path;

    private readonly List<string> _keys = new();
    private readonly HashSet<string> _set = new(StringComparer.Ordinal);

    private ScriptCheckHideList(string directory, string? path)
    {
        _directory = directory;
        _path = path;
    }

    /// <summary>Die Schlüssel in der Reihenfolge, in der sie angelegt wurden.</summary>
    public IReadOnlyCollection<string> Keys => _keys;

    /// <summary>Die Datei gab es, ließ sich aber nicht lesen (IO- oder Rechtefehler, nicht kaputtes JSON): <see cref="Save"/> wirft dann, statt sie zu überschreiben.</summary>
    public bool ReadFailed { get; private set; }

    /// <summary>
    /// Liest die Schlüssel der Mod; wirft nie. Eine fehlende oder kaputte Datei ergibt eine leere Liste; eine kaputte Datei wird nach
    /// Möglichkeit in <c>.bad</c> umbenannt, damit das nächste Speichern sie nicht stillschweigend überschreibt.
    /// </summary>
    public static ScriptCheckHideList Load(string directory, string modDirectory)
    {
        string path;
        try
        {
            path = Path.Combine(directory, FlagHideList.KeyFor(modDirectory) + ".json");
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return new ScriptCheckHideList(directory, null);
        }

        var list = new ScriptCheckHideList(directory, path);
        try
        {
            if (!File.Exists(path))
            {
                return list;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                MoveAside(path);
                return list;
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    list.Add(item.GetString() ?? string.Empty);
                }
            }
        }
        catch (JsonException)
        {
            list._keys.Clear();
            list._set.Clear();
            MoveAside(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            list._keys.Clear();
            list._set.Clear();
            list.ReadFailed = true;
        }

        return list;
    }

    /// <summary>Ist der Fund ausgeblendet (sein <see cref="Finding.Key"/> steht in der Liste)?</summary>
    public bool IsHidden(Finding finding) => _set.Contains(finding.Key);

    /// <summary>Steht der Schlüssel in der Liste (für andere Prüfungen, die ihre eigenen Schlüssel <c>Art|Name</c> bilden)?</summary>
    public bool IsHidden(string key) => _set.Contains(key);

    /// <summary>Blendet den Schlüssel aus; false, wenn er leer ist oder schon besteht.</summary>
    public bool Add(string key)
    {
        key = (key ?? string.Empty).Trim();
        if (key.Length == 0 || !_set.Add(key))
        {
            return false;
        }

        _keys.Add(key);
        return true;
    }

    /// <summary>Blendet den Schlüssel wieder ein; false, wenn es ihn nicht gibt.</summary>
    public bool Remove(string key)
    {
        if (!_set.Remove(key))
        {
            return false;
        }

        _keys.Remove(key);
        return true;
    }

    /// <summary>Schreibt die Schlüssel (Zwischendatei im selben Ordner, dann Umbenennen). Wirft bei Schreibfehlern (IOException u. a.), bei <see cref="ReadFailed"/> InvalidOperationException.</summary>
    public void Save()
    {
        if (ReadFailed)
        {
            throw new InvalidOperationException("Die Ausblendliste war nicht lesbar und wird nicht überschrieben.");
        }

        if (_path is null)
        {
            throw new IOException("Kein gültiger Ordner der aktiven Mod.");
        }

        Directory.CreateDirectory(_directory);
        var temp = Path.Combine(_directory, Path.GetFileName(_path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(_keys, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    /// <summary>Kaputte Datei beiseitelegen (<c>.bad</c>); Fehler dabei werden verschluckt.</summary>
    private static void MoveAside(string path)
    {
        try
        {
            File.Move(path, path + ".bad", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
