using System.IO;
using System.Linq;

namespace ParadoxModTool.Explorer;

/// <summary>
/// Die aufgeklappten Ordner der Verzeichnis-Übersicht, damit sie einen Neustart überdauern. Schlüssel ist der normalisierte Pfad
/// (Groß-/Kleinschreibung und abschließender Trenner spielen keine Rolle); gespeichert wird die Schreibweise des ersten Eintrags.
/// </summary>
public sealed class ExpandedPathSet
{
    private readonly Dictionary<string, string> _paths = new();

    public bool Contains(string path) => _paths.ContainsKey(PathNormalization.NormalizeKey(path));

    public void Add(string path) => _paths.TryAdd(PathNormalization.NormalizeKey(path), path);

    public void Remove(string path) => _paths.Remove(PathNormalization.NormalizeKey(path));

    /// <summary>Ersetzt den Inhalt durch die übergebenen Pfade; leere Einträge entfallen.</summary>
    public void Load(IEnumerable<string> paths)
    {
        _paths.Clear();
        foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            Add(path);
        }
    }

    /// <summary>Die Pfade zum Speichern; Ordner, die es nicht mehr gibt, entfallen.</summary>
    public IReadOnlyList<string> Capture() => _paths.Values.Where(Directory.Exists).ToList();
}
