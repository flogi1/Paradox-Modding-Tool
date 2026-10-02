using System.Text;

namespace ParadoxModTool.SearchReplace.ReplaceLists;

/// <summary>Eingelesene Datei: Text ohne BOM, unverändert (Zeilenenden bleiben).</summary>
public sealed record ReplaceSourceFile(string Path, string Text, bool HasBom);

/// <summary>Ergebnis des Einlesens eines Zielordners; <see cref="Skipped"/> sind Dateien mit ungültigem UTF-8 oder ohne Lesezugriff.</summary>
public sealed record ReplaceFolderRead(IReadOnlyList<ReplaceSourceFile> Files, IReadOnlyList<string> Skipped, bool FolderExists);

/// <summary>Eine geänderte Datei; <see cref="OldText"/> ist der eingelesene Stand (für Vorschau und <see cref="ReplaceListRunner.StillCurrent"/>).</summary>
public sealed record ReplaceFileChange(string Path, string NewText, int Count, bool HasBom, string OldText);

/// <summary>Ergebnis eines Laufs: Treffer je Regel (Index wie in der Regelliste), geänderte Dateien, übersprungene Dateien.</summary>
public sealed record ReplaceListRun(IReadOnlyList<int> RuleCounts, IReadOnlyList<ReplaceFileChange> Changes, IReadOnlyList<string> Skipped);

/// <summary>
/// Führt eine Ersetzungsliste über einen Ordner aus (Spec 2026-09-30-replace-lists, Abschnitt 2). Einlesen und Rechnen sind getrennt; geschrieben
/// wird hier nichts.
/// </summary>
public static class ReplaceListRunner
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static ReplaceFolderRead ReadFolder(string folder, CancellationToken cancel = default)
    {
        if (!Directory.Exists(folder))
        {
            return new ReplaceFolderRead(Array.Empty<ReplaceSourceFile>(), Array.Empty<string>(), false);
        }

        var files = new List<ReplaceSourceFile>();
        var skipped = new List<string>();
        foreach (var path in FileSearchService.EnumerateSearchableFiles(folder).Order(StringComparer.OrdinalIgnoreCase))
        {
            cancel.ThrowIfCancellationRequested();
            if (TryRead(path) is { } file)
            {
                files.Add(file);
            }
            else
            {
                skipped.Add(path);
            }
        }

        return new ReplaceFolderRead(files, skipped, true);
    }

    public static ReplaceListRun Run(IReadOnlyList<ReplaceRule> rules, ReplaceFolderRead read, CancellationToken cancel = default)
    {
        var problems = RuleMatcher.Validate(rules);
        var counts = new int[rules.Count];
        var changes = new List<ReplaceFileChange>();
        foreach (var file in read.Files)
        {
            cancel.ThrowIfCancellationRequested();
            var text = file.Text;
            var fileCount = 0;
            for (var i = 0; i < rules.Count; i++)
            {
                if (!rules[i].Enabled || problems[i] != RuleProblem.None)
                {
                    continue;
                }

                text = RuleMatcher.Replace(text, rules[i], out var n);
                counts[i] += n;
                fileCount += n;
            }

            if (!string.Equals(text, file.Text, StringComparison.Ordinal))
            {
                changes.Add(new ReplaceFileChange(file.Path, text, fileCount, file.HasBom, file.Text));
            }
        }

        return new ReplaceListRun(counts, changes, read.Skipped);
    }

    /// <summary>Hat die Datei auf der Platte noch den Stand, auf dem der Lauf gerechnet hat?</summary>
    public static bool StillCurrent(ReplaceFileChange change) =>
        TryRead(change.Path) is { } file && file.HasBom == change.HasBom && string.Equals(file.Text, change.OldText, StringComparison.Ordinal);

    private static ReplaceSourceFile? TryRead(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var start = hasBom ? 3 : 0;
            return new ReplaceSourceFile(path, StrictUtf8.GetString(bytes, start, bytes.Length - start), hasBom);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return null;
        }
    }
}
