namespace ParadoxModTool.Patches;

/// <summary>Ein Pfad aus einem Rezept liegt außerhalb von Mod- bzw. Spielordner (oder ist nicht relativ).</summary>
public sealed class UnsafePathException(string path) : InvalidOperationException(PatchTexts.UnsafePath(path))
{
    public string Path { get; } = path;
}

/// <summary>
/// Pfadschutz der Patch-Engine: ein Pfad aus einem Rezept muss relativ zur Mod- bzw. Spielwurzel sein und darf sie nicht verlassen:
/// nicht leer, kein Laufwerk oder <c>:</c>, kein führendes <c>/</c> oder <c>\</c>, kein Teil <c>..</c>.
/// </summary>
public static class PathSafety
{
    /// <summary>Beide Trennzeichen (<c>/</c> und <c>\</c>) zählen; Platzhalter wie <c>*</c> sind erlaubt.</summary>
    public static bool IsSafeRelative(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains(':') || path[0] is '/' or '\\')
        {
            return false;
        }

        return path.Split('/', '\\').All(part => part != "..");
    }

    /// <summary>
    /// Ein Dateimuster mit mehreren Teilen (mit <c>;</c> getrennt): es gibt mindestens einen Teil, und jeder ist sicher. Leere Teile (z. B. ein
    /// <c>;</c> am Ende) zählen nicht, wie bei <see cref="GlobMatcher"/>; sie lesen nichts.
    /// </summary>
    public static bool IsSafeGlob(string? glob)
    {
        var parts = GlobParts(glob);
        return parts.Count > 0 && parts.All(IsSafeRelative);
    }

    /// <summary>Die nicht leeren Teile eines Dateimusters (mit <c>;</c> getrennt, gekürzt); leer, wenn das Muster fehlt.</summary>
    public static IReadOnlyList<string> GlobParts(string? glob) =>
        glob is null ? Array.Empty<string>() : glob.Split(';').Select(part => part.Trim()).Where(part => part.Length > 0).ToList();

    /// <summary>Liegt <paramref name="relative"/> unter <paramref name="root"/>, wenn man beide zu vollen Pfaden auflöst?</summary>
    public static bool IsInside(string root, string relative)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var full = Path.GetFullPath(Path.Combine(root, relative));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return full.StartsWith(fullRoot + Path.DirectorySeparatorChar, comparison);
    }
}
