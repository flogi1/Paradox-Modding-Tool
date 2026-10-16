namespace ParadoxModTool.LaunchControl;

/// <summary>Eine Mod in einem Playset. <paramref name="LauncherModId"/> ist die UUID der Zeile in <c>mods</c> (nur Launcher-Spiele).</summary>
public sealed record PlaysetModEntry(string Path, string? DisplayName, bool Enabled, string? LauncherModId);

/// <summary>Ein DLC eines Playsets (EU V: <c>paradoxAppId</c>, Launcher-Spiele: <c>dlcId</c>).</summary>
public sealed record PlaysetDlcEntry(string Id, bool Enabled);

/// <summary>
/// Neutrales Abbild eines Playsets für beide Speicherarten. <see cref="Id"/> ist die UUID in der Launcher-Datenbank, bei playsets.json der Name.
/// </summary>
public sealed class PlaysetData
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Das Playset hängt am Paradox-Konto (<c>pdxId</c> gesetzt); Bearbeiten ist erlaubt, aber mit Hinweis.</summary>
    public bool IsCloud { get; set; }

    /// <summary>Nur EU V: <c>isAutomaticallySorted</c>.</summary>
    public bool IsAutomaticallySorted { get; set; }

    public List<PlaysetModEntry> Mods { get; set; } = new();

    public List<PlaysetDlcEntry> Dlcs { get; set; } = new();

    /// <summary>Unabhängige Kopie mit allen Feldern (für die Bearbeitung im Fenster, bis gespeichert wird).</summary>
    public PlaysetData Clone() => new()
    {
        Id = Id,
        Name = Name,
        IsActive = IsActive,
        IsCloud = IsCloud,
        IsAutomaticallySorted = IsAutomaticallySorted,
        Mods = Mods.ToList(),
        Dlcs = Dlcs.ToList(),
    };

    /// <summary>Tiefe Kopie mit neuer Id und neuem Namen (nicht aktiv, nicht Cloud).</summary>
    public PlaysetData CopyAs(string id, string name) => new()
    {
        Id = id,
        Name = name,
        IsActive = false,
        IsCloud = false,
        IsAutomaticallySorted = IsAutomaticallySorted,
        Mods = Mods.ToList(),
        Dlcs = Dlcs.ToList(),
    };
}
