using System.Security.Cryptography;
using System.Text;

namespace ParadoxModTool.Performance;

/// <summary>
/// Fingerabdruck der Dateien einer Mod (Pfad, Länge, Änderungszeit), damit ein Lauf mit „Mod seit dem letzten Lauf
/// geändert“ verglichen werden kann. Liest nur Metadaten, nichts wird geschrieben.
/// </summary>
public static class ModFilesHash
{
    /// <summary>
    /// 16 Hex-Zeichen eines SHA-256 über die sortierten Zeilen <c>relativ|länge|ticks</c>; <c>""</c> für einen fehlenden Ordner.
    /// </summary>
    /// <param name="modRoot">Wurzel der Mod.</param>
    /// <param name="enumerate">Dateiliste (relativer Pfad mit <c>/</c>, Länge, letzte Änderung UTC); Standard ist alles unter der Wurzel, rekursiv.</param>
    public static string Compute(
        string modRoot,
        Func<string, IEnumerable<(string Relative, long Length, DateTime LastWriteUtc)>>? enumerate = null)
    {
        if (string.IsNullOrWhiteSpace(modRoot) || !Directory.Exists(modRoot))
        {
            return "";
        }

        var lines = (enumerate ?? EnumerateFiles)(modRoot)
            .Select(f => $"{f.Relative}|{f.Length}|{f.LastWriteUtc.Ticks}")
            .OrderBy(l => l, StringComparer.Ordinal);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
        return Convert.ToHexString(bytes, 0, 8).ToLowerInvariant();
    }

    private static IEnumerable<(string Relative, long Length, DateTime LastWriteUtc)> EnumerateFiles(string root)
    {
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(path);
            yield return (Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'), info.Length, info.LastWriteTimeUtc);
        }
    }
}
