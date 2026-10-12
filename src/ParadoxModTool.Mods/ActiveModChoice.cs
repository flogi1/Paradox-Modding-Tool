namespace ParadoxModTool.Mods;

/// <summary>Ergebnis der Wahl: die aktive Mod, ob sie automatisch erkannt ist und ob eine feste Wahl verloren ging (Mod entfernt oder Ordner fehlt).</summary>
public sealed record ActiveModResult(ModInfo? Mod, bool IsAutomatic, bool ChoiceLost);

/// <summary>
/// Die aktive Mod des Hauptfensters: fest gewählt (Mod-Id, je Spiel gespeichert) oder <see cref="Automatic"/> (Mod der offenen Datei, wie früher).
/// Die feste Wahl gilt, solange die Mod angelegt ist und einen vorhandenen Ordner hat; sonst gilt wieder die Erkennung.
/// </summary>
public static class ActiveModChoice
{
    public const string Automatic = "*auto*";

    public static ActiveModResult Resolve(ModRegistry registry, string? choice, ModInfo? detected)
    {
        if (string.IsNullOrEmpty(choice) || choice == Automatic)
        {
            return new ActiveModResult(detected, true, false);
        }

        return registry.Get(choice) is { } chosen && chosen.Paths.Any(Directory.Exists)
            ? new ActiveModResult(chosen, false, false)
            : new ActiveModResult(detected, true, true);
    }

    /// <summary>Die Wahl beim ersten Start mit dieser Funktion: die gerade erkannte Mod fest, ohne erkannte Mod Automatisch; eine vorhandene Wahl bleibt.</summary>
    public static string Initial(string? stored, ModInfo? detected) => stored ?? detected?.Id ?? Automatic;
}
