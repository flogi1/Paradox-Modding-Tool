namespace ParadoxModTool.ModCompat;

/// <summary>Legt bei der ersten Nutzung von Mod-Kompatibilität für ein Spiel ein Beispielset mit Erklärtext an.</summary>
public static class CompatStarterSet
{
    public const string Name = "Mod Kompatibilitätstest";

    public const string Description =
        "Füge hier die zwei oder mehr Mods hinzu, die du kompatibel machen willst, in Ladereihenfolge vom größten zum kleinsten Mod, um sie zu vergleichen.";

    /// <summary>
    /// Legt das Startset an, wenn der Set-Ordner noch nicht existiert (erste Nutzung für dieses Spiel).
    /// Existiert der Ordner schon (auch leer), wird nichts getan, damit ein gelöschtes Startset nicht zurückkommt.
    /// </summary>
    public static bool SeedIfFirstUse(string setsDirectory, CompatSetStore store)
    {
        if (Directory.Exists(setsDirectory))
        {
            return false;
        }

        store.Save(new CompatSet { Name = Name, Description = Description });
        return true;
    }
}
