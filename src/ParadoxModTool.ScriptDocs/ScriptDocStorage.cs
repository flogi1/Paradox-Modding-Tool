using System.Text.Json;
using System.Text.Json.Serialization;
using ParadoxModTool.Storage;

namespace ParadoxModTool.ScriptDocs;

/// <summary>
/// Speichert die Script-Datenbank eines Spiels in einem Ordner (<c>games\&lt;id&gt;\script-docs</c>): <c>entries.json</c> und <c>imports.json</c>.
/// Eine fehlende oder beschädigte Datei ergibt eine leere Datenbank, nie einen Fehler - beim nächsten Import wird sie neu aufgebaut.
/// </summary>
public sealed class ScriptDocStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _directory;

    public ScriptDocStorage(string directory)
    {
        _directory = directory;
    }

    public string FolderPath => _directory;

    private string EntriesPath => Path.Combine(_directory, "entries.json");

    private string ImportsPath => Path.Combine(_directory, "imports.json");

    public ScriptDocDatabase Load()
    {
        var database = new ScriptDocDatabase
        {
            Entries = Read<List<ScriptDocEntry>>(EntriesPath) ?? new(),
            Imports = Read<List<ImportRecord>>(ImportsPath) ?? new(),
        };

        // Einträge ohne Namen (beschädigte Datei) würden die Anzeige stören.
        database.Entries.RemoveAll(e => string.IsNullOrEmpty(e.Name));
        return database;
    }

    public void Save(ScriptDocDatabase database)
    {
        SharedFile.WriteAllTextAtomic(EntriesPath, JsonSerializer.Serialize(database.Entries, Options));
        SharedFile.WriteAllTextAtomic(ImportsPath, JsonSerializer.Serialize(database.Imports, Options));
    }

    private static T? Read<T>(string path)
        where T : class
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
