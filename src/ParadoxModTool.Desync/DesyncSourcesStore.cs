using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParadoxModTool.Desync;

/// <summary>
/// Die vom Nutzer hinzugefügten Dump-Pfade (Zips oder Ordner anderer Spieler) in <c>&lt;GameDirectory&gt;/desync/sources.json</c>, Form
/// <c>{ "version": 1, "paths": [ … ] }</c>. Jeder Pfad steht einmal (Groß-/Kleinschreibung zählt nicht). Geschrieben wird über eine temporäre
/// Datei, die die alte ersetzt; eine fehlende oder unlesbare Datei gilt als leer.
/// </summary>
public sealed class DesyncSourcesStore
{
    private const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _path;

    /// <param name="path">Pfad der Datei, <c>&lt;GameDirectory&gt;/desync/sources.json</c>.</param>
    public DesyncSourcesStore(string path) => _path = path;

    /// <summary>Die gespeicherten Pfade in der Reihenfolge des Hinzufügens; leer, wenn die Datei fehlt oder unlesbar ist.</summary>
    public IReadOnlyList<string> Load()
    {
        try
        {
            return Read();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Wie <see cref="Load"/>, aber nur eine fehlende oder kaputte Datei gilt als leer. Kann die vorhandene Datei nicht gelesen werden (gesperrt,
    /// kein Recht), wirft es: sonst würde <see cref="Add"/> bzw. <see cref="Remove"/> die gespeicherten Pfade überschreiben.
    /// </summary>
    private IReadOnlyList<string> Read()
    {
        try
        {
            using var stream = File.OpenRead(_path);
            var file = JsonSerializer.Deserialize<SourcesFile>(stream, Options);
            if (file?.Paths is null)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>();
            foreach (var path in file.Paths)
            {
                if (!string.IsNullOrWhiteSpace(path) && !result.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(path);
                }
            }

            return result;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or FileNotFoundException or DirectoryNotFoundException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Hängt den Pfad an, sofern er (ohne Rücksicht auf Groß-/Kleinschreibung) noch nicht gespeichert ist.</summary>
    /// <exception cref="IOException">Die vorhandene Datei lässt sich nicht lesen, oder Ordner bzw. Datei lassen sich nicht schreiben.</exception>
    /// <exception cref="UnauthorizedAccessException">Kein Lese- oder Schreibrecht.</exception>
    public void Add(string path)
    {
        var paths = Read().ToList();
        if (paths.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        paths.Add(path);
        Write(paths);
    }

    /// <summary>Entfernt den Pfad (ohne Rücksicht auf Groß-/Kleinschreibung); ein nicht gespeicherter Pfad ändert nichts.</summary>
    /// <exception cref="IOException">Die vorhandene Datei lässt sich nicht lesen, oder Ordner bzw. Datei lassen sich nicht schreiben.</exception>
    /// <exception cref="UnauthorizedAccessException">Kein Lese- oder Schreibrecht.</exception>
    public void Remove(string path)
    {
        var paths = Read().ToList();
        if (paths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)) == 0)
        {
            return;
        }

        Write(paths);
    }

    private void Write(IReadOnlyList<string> paths)
    {
        if (Path.GetDirectoryName(_path) is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = File.Create(temporary))
            {
                JsonSerializer.Serialize(stream, new SourcesFile(FormatVersion, new List<string?>(paths)), Options);
            }

            File.Move(temporary, _path, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Form der Datei.</summary>
    private sealed record SourcesFile(
        [property: JsonPropertyName("version")] int Version,
        [property: JsonPropertyName("paths")] List<string?>? Paths);
}
