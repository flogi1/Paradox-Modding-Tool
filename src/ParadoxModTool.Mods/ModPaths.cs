namespace ParadoxModTool.Mods;

/// <summary>
/// Vergleich von Ordnerpfaden: voller Pfad ohne abschließenden Trenner, ohne Beachtung der Groß-/Kleinschreibung (wie unter Windows üblich)
/// und am Verzeichnisrand, damit <c>...\mod</c> nicht auf <c>...\mod2</c> passt.
/// </summary>
public static class ModPaths
{
    /// <summary>Voller Pfad ohne Schlusstrenner. Wirft <see cref="ArgumentException"/> bei leerem oder ungültigem Pfad.</summary>
    public static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    /// <summary>Wie <see cref="Normalize"/>, aber ohne Ausnahme: <see langword="false"/> bei leerem oder ungültigem Pfad.</summary>
    public static bool TryNormalize(string? path, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            normalized = Normalize(path);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    public static bool AreEqual(string a, string b) =>
        string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>Liegt <paramref name="path"/> in <paramref name="root"/> oder ist gleich <paramref name="root"/>?</summary>
    public static bool IsUnder(string path, string root)
    {
        var normalizedPath = Normalize(path);
        var normalizedRoot = Normalize(root);
        if (string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar) ? normalizedRoot : normalizedRoot + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
