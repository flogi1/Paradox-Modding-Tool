namespace ParadoxModTool.Categories;

/// <summary>Herkunft einer Datei (Masterkategorie über den Dateikategorien).</summary>
public enum FileOrigin
{
    /// <summary>Weder im Spielordner noch in einem Mod-Ordner (auch: ungespeicherte Dokumente).</summary>
    External,

    /// <summary>Datei des Basisspiels.</summary>
    Vanilla,

    /// <summary>Datei eines Mods.</summary>
    Mod,
}

/// <summary>Ordnet einen Dateipfad anhand von Vanilla- und Mod-Wurzelordnern einer <see cref="FileOrigin"/> zu.</summary>
public static class FileOriginClassifier
{
    /// <summary>
    /// Die tiefste (längste) passende Wurzel entscheidet; bei gleicher Wurzel in beiden Listen gewinnt Mod.
    /// Vergleich ohne Groß-/Kleinschreibung, "\" und "/" gelten gleich.
    /// </summary>
    public static FileOrigin Classify(string? filePath, IEnumerable<string> vanillaRoots, IEnumerable<string> modRoots)
    {
        var path = Normalize(filePath);
        if (path is null)
        {
            return FileOrigin.External;
        }

        var vanillaDepth = DeepestMatch(path, vanillaRoots);
        var modDepth = DeepestMatch(path, modRoots);

        if (vanillaDepth < 0 && modDepth < 0)
        {
            return FileOrigin.External;
        }

        return modDepth >= vanillaDepth ? FileOrigin.Mod : FileOrigin.Vanilla;
    }

    /// <summary>Länge der tiefsten passenden Wurzel, oder -1.</summary>
    private static int DeepestMatch(string path, IEnumerable<string> roots)
    {
        var best = -1;
        foreach (var root in roots)
        {
            var normalized = Normalize(root);
            if (normalized is null)
            {
                continue;
            }

            var isUnder = path.Length > normalized.Length
                && path.StartsWith(normalized, StringComparison.OrdinalIgnoreCase)
                && path[normalized.Length] == '/';
            if (isUnder && normalized.Length > best)
            {
                best = normalized.Length;
            }
        }

        return best;
    }

    private static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            path = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        return path.Replace('\\', '/').TrimEnd('/');
    }
}
