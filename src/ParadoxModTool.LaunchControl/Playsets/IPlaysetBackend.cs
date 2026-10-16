namespace ParadoxModTool.LaunchControl;

/// <summary>
/// Speicherart der Playsets eines Spiels: <see cref="JsonPlaysetBackend"/> (EU V, playsets.json) oder <see cref="LauncherPlaysetBackend"/>
/// (Launcher-Datenbank). Die Schutzprüfungen und Sicherungen macht der <see cref="PlaysetManager"/>, nicht das Backend.
/// </summary>
public interface IPlaysetBackend
{
    /// <summary>Dateien, die vor jeder Änderung gesichert werden.</summary>
    IReadOnlyList<string> FilesToBackUp { get; }

    bool UsesLauncherDatabase { get; }

    IReadOnlyList<PlaysetData> LoadAll();

    /// <summary>Legt das Playset an (unbekannte <see cref="PlaysetData.Id"/>) oder ersetzt Name, Mods und DLCs; andere Felder bleiben unberührt.</summary>
    PlaysetResult Save(PlaysetData playset);

    PlaysetResult Delete(string id);

    PlaysetResult SetActive(string id);

    /// <summary>DLC-Liste für ein neues, leeres Playset (alle bekannten DLCs, eingeschaltet).</summary>
    IReadOnlyList<PlaysetDlcEntry> DefaultDlcs();
}
