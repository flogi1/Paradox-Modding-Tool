namespace ParadoxModTool.Categories;

/// <summary>
/// Ein Ordner-Filter der Filterleiste (z. B. "Idea Variation Hauptmod"): Ist sein Häkchen an, zeigt der Arbeitsbereich nur Tabs mit
/// Dateien aus diesem Ordnerbaum. Wird pro Spiel gespeichert.
/// </summary>
public sealed class FolderFilterDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Wurzel des Ordnerbaums (voller Pfad).</summary>
    public string FolderPath { get; set; } = string.Empty;

    /// <summary>Häkchen in der Filterleiste sichtbar (über "Filter ▾" ein- und ausblendbar).</summary>
    public bool ShowInBar { get; set; } = true;

    public FolderFilterDefinition Clone() => new() { Id = Id, Name = Name, FolderPath = FolderPath, ShowInBar = ShowInBar };
}
