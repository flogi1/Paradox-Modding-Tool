using System.Text;

namespace ParadoxModTool.VanillaDiff;

public enum FolderCompareStatus
{
    Different,
    OnlyLeft,
    OnlyRight,
    Same,
}

/// <summary>Eine Datei im Ordnervergleich: relativer Pfad mit "/", volle Pfade (fehlt auf einer Seite = <see langword="null"/>), Status.</summary>
/// <summary>Fortschritt des Ordnervergleichs; <see cref="Total"/> 0 = Dateien werden noch gesucht.</summary>
public sealed record FolderCompareProgress(int Done, int Total, string RelativePath);

public sealed record FolderCompareEntry(string RelativePath, string? LeftPath, string? RightPath, FolderCompareStatus Status, bool IsBinary);

/// <summary>
/// Ordnervergleich im Compare-Reiter des Compare und Update Tools (Nutzerwunsch 2026-09-30, wie WinMerge): alle Dateien beider Ordner
/// samt Unterordnern. Textdateien gelten als gleich, wenn sie sich nur in Zeilenenden oder BOM unterscheiden; Binärdateien (Nullbyte
/// in den ersten 8 KB) werden Byte für Byte verglichen. Ausgelassen werden .git und PMTs eigene Ordner (.pmt-…).
/// </summary>
public static class FolderComparer
{
    private const int BinaryProbeLength = 8192;

    /// <param name="progress">Erst ohne Gesamtzahl (Dateien werden gesucht), dann je verglichener Datei; die letzte Meldung hat Done == Total.</param>
    public static IReadOnlyList<FolderCompareEntry> Compare(string leftRoot, string rightRoot, CancellationToken cancellationToken, IProgress<FolderCompareProgress>? progress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new FolderCompareProgress(0, 0, string.Empty));
        var left = Files(leftRoot);
        var right = Files(rightRoot);
        var all = left.Keys.Union(right.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        var result = new List<FolderCompareEntry>(all.Count);
        foreach (var relative in all)
        {
            cancellationToken.ThrowIfCancellationRequested();
            left.TryGetValue(relative, out var leftPath);
            right.TryGetValue(relative, out var rightPath);
            result.Add(Entry(relative, leftPath, rightPath));
            progress?.Report(new FolderCompareProgress(result.Count, all.Count, relative));
        }

        return result;
    }

    /// <summary>Status einer Datei; nicht vorhandene Pfade (auch nach dem Speichern einer vorher fehlenden Datei neu geprüft) zählen als fehlend.</summary>
    public static FolderCompareEntry Entry(string relativePath, string? leftPath, string? rightPath)
    {
        leftPath = leftPath is not null && File.Exists(leftPath) ? leftPath : null;
        rightPath = rightPath is not null && File.Exists(rightPath) ? rightPath : null;
        if (leftPath is null || rightPath is null)
        {
            var existing = leftPath ?? rightPath;
            var status = leftPath is null ? FolderCompareStatus.OnlyRight : FolderCompareStatus.OnlyLeft;
            return new FolderCompareEntry(relativePath, leftPath, rightPath, status, existing is not null && IsBinary(existing));
        }

        var binary = IsBinary(leftPath) || IsBinary(rightPath);
        var same = binary ? SameBytes(leftPath, rightPath) : SameText(leftPath, rightPath);
        return new FolderCompareEntry(relativePath, leftPath, rightPath, same ? FolderCompareStatus.Same : FolderCompareStatus.Different, binary);
    }

    /// <summary>Filterfeld (Text im Pfad, Groß-/Kleinschreibung egal) und "Gleiche ausblenden".</summary>
    public static IReadOnlyList<FolderCompareEntry> Filter(IEnumerable<FolderCompareEntry> entries, string? text, bool hideSame)
    {
        var needle = text?.Trim();
        return entries
            .Where(e => !(hideSame && e.Status == FolderCompareStatus.Same))
            .Where(e => string.IsNullOrEmpty(needle) || e.RelativePath.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static Dictionary<string, string> Files(string root)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(root))
        {
            return files;
        }

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.Split('/').SkipLast(1).Any(folder => folder.Equals(".git", StringComparison.OrdinalIgnoreCase) || folder.StartsWith(".pmt", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            files[relative] = file;
        }

        return files;
    }

    private static bool IsBinary(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var buffer = new byte[BinaryProbeLength];
            var read = stream.Read(buffer, 0, buffer.Length);
            return Array.IndexOf(buffer, (byte)0, 0, read) >= 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool SameBytes(string left, string right)
    {
        try
        {
            return new FileInfo(left).Length == new FileInfo(right).Length && File.ReadAllBytes(left).AsSpan().SequenceEqual(File.ReadAllBytes(right));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Gleicher Text ohne Rücksicht auf BOM und Zeilenenden (ReadAllText entfernt den BOM).</summary>
    private static bool SameText(string left, string right)
    {
        try
        {
            return string.Equals(Normalize(File.ReadAllText(left, Encoding.UTF8)), Normalize(File.ReadAllText(right, Encoding.UTF8)), StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
