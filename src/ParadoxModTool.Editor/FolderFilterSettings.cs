using System.Collections.Generic;
using System.Linq;
using ParadoxModTool.Categories;

namespace ParadoxModTool.Editor;

/// <summary>
/// Die Ordner-Filter dieses Spiels (<see cref="FolderFilterDefinition"/>); gelten für alle Arbeitsbereiche. Welche davon gerade an sind,
/// merkt sich jeder Arbeitsbereich selbst. Angelegt werden sie über die Verzeichnis-Übersicht ("Als Filter anlegen").
/// </summary>
public static class FolderFilterSettings
{
    private static readonly List<FolderFilterDefinition> Items = new();

    /// <summary>Ein Filter kam dazu, fiel weg, wurde umbenannt oder aus der Leiste aus- bzw. eingeblendet.</summary>
    public static event EventHandler? Changed;

    public static IReadOnlyList<FolderFilterDefinition> Filters => Items;

    public static FolderFilterDefinition? Find(string id) => Items.FirstOrDefault(f => f.Id == id);

    public static FolderFilterDefinition Add(string name, string folderPath)
    {
        var filter = new FolderFilterDefinition { Id = Guid.NewGuid().ToString("N"), Name = name.Trim(), FolderPath = folderPath };
        Items.Add(filter);
        Changed?.Invoke(null, EventArgs.Empty);
        return filter;
    }

    public static void Rename(string id, string name)
    {
        if (Find(id) is { } filter && !string.IsNullOrWhiteSpace(name) && filter.Name != name.Trim())
        {
            filter.Name = name.Trim();
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    public static void SetShown(string id, bool shown)
    {
        if (Find(id) is { } filter && filter.ShowInBar != shown)
        {
            filter.ShowInBar = shown;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    public static void Remove(string id)
    {
        if (Items.RemoveAll(f => f.Id == id) > 0)
        {
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>Filter aus der Session übernehmen; kaputte Einträge entfallen.</summary>
    public static void Load(IEnumerable<FolderFilterDefinition>? filters)
    {
        Items.Clear();
        Items.AddRange(FolderFilterMatcher.Prune(filters));
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Kopien für die Session.</summary>
    public static List<FolderFilterDefinition> Snapshot() => Items.Select(f => f.Clone()).ToList();
}
