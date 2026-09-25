using System.Collections.Generic;
using System.Linq;
using ParadoxModTool.Categories;

namespace ParadoxModTool.Editor;

/// <summary>
/// Welche Häkchen (Herkunft und Kategorien) die Filterleiste zeigt; gilt für alle Arbeitsbereiche. Standard: alle sichtbar.
/// Ein aus der Leiste ausgeblendetes Häkchen filtert nicht mehr - seine Dateien sind immer sichtbar.
/// Schlüssel siehe <see cref="FilterEntryKeys"/>.
/// </summary>
public static class FilterBarSettings
{
    private static readonly HashSet<string> Hidden = new();

    /// <summary>Wird ausgelöst, wenn sich die Sichtbarkeit eines Häkchens ändert.</summary>
    public static event EventHandler? Changed;

    public static bool IsShown(string key) => !Hidden.Contains(key);

    public static void SetShown(string key, bool shown)
    {
        var changed = shown ? Hidden.Remove(key) : Hidden.Add(key);
        if (changed)
        {
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>Ausgeblendete Schlüssel für die Session; Einträge zu gelöschten Kategorien entfallen.</summary>
    public static List<string> HiddenKeys() =>
        FilterEntryKeys.Prune(Hidden.OrderBy(k => k), FileCategoryClassifier.Categories.Select(c => c.Id));

    public static void LoadHidden(IEnumerable<string>? keys)
    {
        Hidden.Clear();
        foreach (var key in FilterEntryKeys.Prune(keys, FileCategoryClassifier.Categories.Select(c => c.Id)))
        {
            Hidden.Add(key);
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }
}
