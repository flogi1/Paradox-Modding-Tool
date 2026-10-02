using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.ErrorLog;

/// <summary>Eine geänderte Zeile in der Vorschau: Zeilennummer (1-basiert), alter und neuer Text ohne Zeilenende.</summary>
public sealed record RenameLine(int Number, string Before, string After);

/// <summary>Umbenennen in einer Datei: neuer Text, Anzahl Ersetzungen, geänderte Zeilen und ob die Datei mit UTF-8-BOM beginnt.</summary>
public sealed record RenameFile(string Path, string NewText, int Count, IReadOnlyList<RenameLine> Lines, bool HasBom);

/// <summary>Umbenennen in einer eigenen Mod (Plan-3-Ruling 2): Name und Ordner der Mod, betroffene Dateien.</summary>
public sealed record ModRenamePlan(string ModName, string Folder, IReadOnlyList<RenameFile> Files);

/// <summary>Eine gelesene Datei, die den Namen enthält: Text ohne BOM und ob sie ein BOM hat.</summary>
public sealed record ScannedFile(string Path, string Text, bool HasBom);

/// <summary>Ergebnis des Lesens einer Mod für einen Namen; <c>Unreadable</c>: kein gültiges UTF-8, enthält den Namen (Plan-3-Ruling 9).</summary>
public sealed record NameScan(string Folder, IReadOnlyList<ScannedFile> Files, IReadOnlyList<string> Unreadable);

/// <summary>
/// Namen als ganze Bezeichner suchen und ersetzen (Fix-Bausteine <c>replaceWithCandidate</c> und <c>renameEverywhere</c>): Ein Buchstabe,
/// eine Ziffer oder <c>_</c> direkt davor oder dahinter gehört zu einem anderen Namen (Plan-3-Ruling 3). Umbenennen in zwei Schritten:
/// <see cref="Scan"/> liest die Dateien einmal, <see cref="Apply"/> rechnet für jeden neuen Namen ohne Plattenzugriff.
/// </summary>
public static class NameEdits
{
    public static readonly IReadOnlyList<string> Extensions = new[] { ".txt", ".yml", ".gui", ".gfx" };

    private static readonly Regex ValidName = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static bool IsValidName(string name) => ValidName.IsMatch(name);

    /// <summary>Spalte (0-basiert) des ersten Vorkommens als ganzer Bezeichner; <see langword="null"/>, wenn der Name in der Zeile fehlt.</summary>
    public static int? FindInLine(string line, string name)
    {
        var match = PatternFor(name).Match(line);
        return match.Success ? match.Index : null;
    }

    /// <summary>
    /// Liest alle Dateien mit den <see cref="Extensions"/> im Mod-Ordner, außer Punkt-Ordnern und -Dateien und den
    /// <paramref name="excludedDirectories"/> (Sicherungsordner), und behält die, die den Namen enthalten. Dateien, die kein gültiges UTF-8
    /// sind, würde ein Umschreiben beschädigen: Enthalten sie den Namen, stehen sie in <see cref="NameScan.Unreadable"/>.
    /// </summary>
    public static NameScan Scan(string folder, string name, IReadOnlyCollection<string> excludedDirectories)
    {
        var files = new List<ScannedFile>();
        var unreadable = new List<string>();
        if (!Directory.Exists(folder))
        {
            return new NameScan(folder, files, unreadable);
        }

        var pattern = PatternFor(name);
        var excluded = excludedDirectories.Select(d => ErrorLogSession.PathKey(d) + System.IO.Path.DirectorySeparatorChar).ToList();
        foreach (var path in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            if (!Extensions.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)
                || System.IO.Path.GetRelativePath(folder, path).Split('\\', '/').Any(part => part.StartsWith('.')))
            {
                continue;
            }

            var key = ErrorLogSession.PathKey(path);
            if (excluded.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var start = hasBom ? 3 : 0;
            string text;
            try
            {
                text = StrictUtf8.GetString(bytes, start, bytes.Length - start);
            }
            catch (DecoderFallbackException)
            {
                // Latin-1 liest jedes Byte verlustfrei; nur so lässt sich prüfen, ob der (ASCII-)Name darin steht.
                if (pattern.IsMatch(Encoding.Latin1.GetString(bytes)))
                {
                    unreadable.Add(path);
                }

                continue;
            }

            if (pattern.IsMatch(text))
            {
                files.Add(new ScannedFile(path, text, hasBom));
            }
        }

        return new NameScan(folder, files, unreadable);
    }

    /// <summary>Rechnet das Umbenennen für einen neuen Namen aus einem <see cref="Scan"/>; BOM und Zeilenenden bleiben, wie sie sind.</summary>
    public static IReadOnlyList<RenameFile> Apply(NameScan scan, string oldName, string newName)
    {
        var pattern = PatternFor(oldName);
        var result = new List<RenameFile>();
        foreach (var file in scan.Files)
        {
            var count = pattern.Matches(file.Text).Count;
            if (count == 0)
            {
                continue;
            }

            var newText = pattern.Replace(file.Text, _ => newName);
            result.Add(new RenameFile(file.Path, newText, count, ChangedLines(file.Text, newText), file.HasBom));
        }

        return result;
    }

    private static Regex PatternFor(string name) =>
        new(@"(?<![A-Za-z0-9_])" + Regex.Escape(name) + @"(?![A-Za-z0-9_])", RegexOptions.CultureInvariant);

    /// <summary>Geänderte Zeilen; Ersetzen und Auskommentieren ändern keine Zeilenumbrüche, beide Texte haben gleich viele Zeilen.</summary>
    internal static IReadOnlyList<RenameLine> ChangedLines(string before, string after)
    {
        var oldLines = before.Split('\n');
        var newLines = after.Split('\n');
        var lines = new List<RenameLine>();
        for (var i = 0; i < oldLines.Length && i < newLines.Length; i++)
        {
            if (oldLines[i] != newLines[i])
            {
                lines.Add(new RenameLine(i + 1, oldLines[i].TrimEnd('\r'), newLines[i].TrimEnd('\r')));
            }
        }

        return lines;
    }
}
