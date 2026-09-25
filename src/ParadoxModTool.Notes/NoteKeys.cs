namespace ParadoxModTool.Notes;

/// <summary>
/// Schlüssel der Notiz-, Lesezeichen- und Bug-Dateien. Eine Datei in einer geführten Mod bekommt den Schlüssel
/// <c>mod:&lt;id&gt;/&lt;relativer Pfad&gt;</c>, damit die Einträge einen verschobenen und neu verknüpften Mod-Ordner überleben.
/// Alle anderen Dateien (Vanilla, fremde Ordner) behalten den normalisierten absoluten Pfad. Die Bibliothek kennt das
/// Mod-Verzeichnis nicht: Die App setzt mit <see cref="SetResolver"/> eine Funktion, die zu einem Pfad den Mod-Schlüssel liefert.
/// </summary>
public static class NoteKeys
{
    public const string ModPrefix = "mod:";

    private static volatile Func<string, string?>? _resolver;

    /// <summary>
    /// Setzt die Funktion, die zu einem vollständigen Dateipfad den Mod-Schlüssel (siehe <see cref="ModKey"/>) liefert oder
    /// <see langword="null"/> für Dateien außerhalb jeder Mod. <see langword="null"/> schaltet die Übersetzung ab.
    /// </summary>
    public static void SetResolver(Func<string, string?>? resolver) => _resolver = resolver;

    /// <summary>Der Schlüssel einer Datei in allen drei Stores.</summary>
    public static string ForFile(string filePath)
    {
        string full;
        try
        {
            full = Path.GetFullPath(filePath);
        }
        catch (ArgumentException)
        {
            return filePath;
        }
        catch (NotSupportedException)
        {
            return filePath;
        }
        catch (PathTooLongException)
        {
            return filePath;
        }

        return _resolver?.Invoke(full) ?? full;
    }

    /// <summary>Baut den Schlüssel einer Mod-Datei. Der Pfadteil hat <c>/</c> als Trenner und ist klein geschrieben, weil Windows Pfade ohne Groß-/Kleinschreibung vergleicht.</summary>
    public static string ModKey(string modId, string relativePath) =>
        ModPrefix + modId + "/" + relativePath.Replace('\\', '/').ToLowerInvariant();

    public static bool IsModKey(string key) => TryParseModKey(key, out _, out _);

    public static bool TryParseModKey(string key, out string modId, out string relativePath)
    {
        modId = string.Empty;
        relativePath = string.Empty;
        if (!key.StartsWith(ModPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var slash = key.IndexOf('/', ModPrefix.Length);
        if (slash <= ModPrefix.Length)
        {
            return false;
        }

        modId = key[ModPrefix.Length..slash];
        relativePath = key[(slash + 1)..];
        return true;
    }
}
