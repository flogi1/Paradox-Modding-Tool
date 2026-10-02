// Siehe Kommentar in FolderScanner.cs: System.IO muss in diesem WPF-Projekt explizit
// importiert werden (Lesson Learned Punkt 2).
using System.IO;

namespace ParadoxModTool.Explorer;

/// <summary>
/// Normalisiert Dateipfade zu einem einzelnen kanonischen Schlüssel für Dictionary-/Mengen-
/// Vergleiche (Farbzuordnungen, Favoriten) — dieselbe Lehre wie
/// <c>ParadoxModTool.Editor.EditorPane.PathsEqual</c> (Lessons Learned Punkt 11 der
/// Architektur-Dokumentation: ein reiner String-Vergleich erkennt z. B. einen abschließenden
/// Backslash oder doppelte Trennzeichen nicht als dieselbe Datei). Dort wird paarweise
/// verglichen; hier wird stattdessen EIN kanonischer Schlüssel gebraucht (für
/// Dictionary-Schlüssel/HashSet-Einträge), daher zusätzlich <see cref="string.ToUpperInvariant"/>
/// für einen groß-/kleinschreibungsunabhängigen Schlüssel statt eines paarweisen
/// <see cref="StringComparison.OrdinalIgnoreCase"/>-Vergleichs.
/// </summary>
public static class PathNormalization
{
    /// <summary>
    /// Bugfix (vom Nutzer per fehlgeschlagenem Test <c>PathNormalizationTests.
    /// NormalizeKey_AbschliessenderBackslash_ErgibtDenselbenSchluesselWieOhne</c> aufgedeckt):
    /// <see cref="Path.GetFullPath(string)"/> entfernt einen abschließenden Trennzeichen NICHT
    /// von sich aus — "C:\Mods\MeinMod\" und "C:\Mods\MeinMod" ergaben dadurch bisher zwei
    /// UNTERSCHIEDLICHE normalisierte Schlüssel, obwohl beide denselben Ordner bezeichnen (genau
    /// der Fall, den dieser Bugfix laut Klassenkommentar eigentlich abdecken sollte). Behoben über
    /// <see cref="Path.TrimEndingDirectorySeparator(string)"/> NACH <c>GetFullPath</c> — entfernt
    /// gezielt genau ein abschließendes Trennzeichen, lässt einen Laufwerks-/UNC-Wurzelpfad (z. B.
    /// "C:\") aber bewusst unangetastet, da "C:" (ohne Backslash) in .NET etwas anderes bedeutet
    /// (aktuelles Verzeichnis auf Laufwerk C) als "C:\" (Laufwerkswurzel).
    /// </summary>
    public static string NormalizeKey(string path)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)).ToUpperInvariant();
        }
        catch (ArgumentException)
        {
            return Path.TrimEndingDirectorySeparator(path).ToUpperInvariant();
        }
        catch (NotSupportedException)
        {
            return Path.TrimEndingDirectorySeparator(path).ToUpperInvariant();
        }
        catch (PathTooLongException)
        {
            return Path.TrimEndingDirectorySeparator(path).ToUpperInvariant();
        }
    }

    /// <summary>
    /// Der Pfad mit einheitlichen Backslashes (<see cref="Path.GetFullPath(string)"/>). Ein gemischter Pfad wie <c>…\Crusader Kings III/mod\Name</c> (Profilvorlage
    /// mit Schrägstrichen plus Backslash-Anhang) erreicht den Windows-Explorer so nicht: <c>explorer.exe</c> öffnet dann stillschweigend "Dokumente". Ein ungültiger
    /// Pfad bleibt unverändert.
    /// </summary>
    public static string ToWindowsPath(string path)
    {
        try
        {
            return path.Length == 0 ? path : Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    /// <summary>Liegt <paramref name="path"/> im Ordner <paramref name="root"/> oder ist selbst dieser Ordner?</summary>
    public static bool IsSameOrUnder(string root, string path)
    {
        var rootKey = NormalizeKey(root);
        var pathKey = NormalizeKey(path);
        return pathKey == rootKey
            || pathKey.StartsWith(rootKey + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
