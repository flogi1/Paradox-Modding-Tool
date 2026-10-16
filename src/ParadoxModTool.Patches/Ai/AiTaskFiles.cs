using System.Text;

namespace ParadoxModTool.Patches.Ai;

/// <summary>
/// Ein Codeblock des Modders: Pfad in der Mod (leer = unbekannt), ganze Datei oder nur ein Ausschnitt, und der Code.
/// <see cref="Note"/>: Erläuterung zu diesem Block; <see cref="FromGame"/>: Datei des Spiels (Pfad relativ zum Spielordner) statt der Mod.
/// </summary>
public sealed record AiTaskSnippet(string Path, bool WholeFile, string Code)
{
    public string Note { get; init; } = string.Empty;

    public bool FromGame { get; init; }
}

/// <summary>Eine Vorbild-Datei: Text (gekürzt bei <see cref="Truncated"/>) oder <see langword="null"/> bei Binärdateien; <see cref="Bytes"/> = Originalgröße.</summary>
public sealed record AiTaskFile(string RelativePath, string? Text, bool Truncated, long Bytes);

/// <summary>
/// Gemeinsame Helfer für Aufträge an eine KI (Generator-Pakete und Patch-Rezepte): Vorbild-Dateien lesen, als Textblöcke ausgeben, Codeblöcke des
/// Modders ausgeben, Dateien in einen Auftragsordner kopieren. Liest nur; schreibt nur in den angegebenen Zielordner.
/// </summary>
public static class AiTaskFiles
{
    public const long MaxFileBytes = 204800;

    public const long WarnTotalBytes = 512000;

    private const int BinaryProbe = 8192;

    /// <summary>Liest die Vorbild-Dateien; eine Datei mit NUL-Byte in den ersten 8 KB gilt als binär, große Texte werden auf 200 KB gekürzt.</summary>
    public static IReadOnlyList<AiTaskFile> ReadFiles(string root, IReadOnlyList<string> relative)
    {
        var files = new List<AiTaskFile>();
        foreach (var path in relative)
        {
            var full = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            if (!PathSafety.IsSafeRelative(path) || !File.Exists(full))
            {
                continue;
            }

            var bytes = File.ReadAllBytes(full);
            if (bytes.AsSpan(0, Math.Min(bytes.Length, BinaryProbe)).Contains((byte)0))
            {
                files.Add(new AiTaskFile(path, null, false, bytes.Length));
                continue;
            }

            var text = Encoding.UTF8.GetString(bytes).TrimStart('﻿');
            var truncated = text.Length > MaxFileBytes;
            files.Add(new AiTaskFile(path, truncated ? text[..(int)MaxFileBytes] : text, truncated, bytes.Length));
        }

        return files;
    }

    public static long TotalBytes(IReadOnlyList<AiTaskFile> files) => files.Sum(f => f.Text is null ? 0 : Math.Min(f.Bytes, MaxFileBytes));

    /// <summary>Größe der Codeblöcke im Auftrag (UTF-8).</summary>
    public static long SnippetBytes(IEnumerable<AiTaskSnippet> snippets) => snippets.Sum(s => (long)Encoding.UTF8.GetByteCount(s.Code) + Encoding.UTF8.GetByteCount(s.Note));

    /// <summary>
    /// Die Zeilen, die eine Markierung berührt, ohne den letzten Zeilenumbruch. Endet die Markierung direkt nach einem Zeilenumbruch, zählt die
    /// folgende Zeile nicht mit. Leere Markierung: leerer Text.
    /// </summary>
    public static string WholeLines(string text, int start, int length)
    {
        if (length <= 0 || start < 0 || start >= text.Length)
        {
            return string.Empty;
        }

        var end = Math.Min(text.Length, start + length);
        if (end - 1 > start && text[end - 1] == '\n')
        {
            end--;
        }

        var first = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
        var stop = text.IndexOf('\n', end - 1);
        return text[first..(stop < 0 ? text.Length : stop)].TrimEnd('\r', '\n');
    }

    /// <summary>
    /// Hängt die Dateien als Blöcke <c>----- label: pfad -----</c> bis <c>----- end -----</c> an (LF); Binärdateien nur als Kopfzeile mit Hinweis,
    /// gekürzte Dateien mit einer Zeile "truncated".
    /// </summary>
    public static void AppendFiles(StringBuilder text, string label, IReadOnlyList<AiTaskFile> files)
    {
        foreach (var file in files)
        {
            if (file.Text is null)
            {
                text.Append($"----- {label}: {file.RelativePath} (binary, not included) -----\n");
                continue;
            }

            text.Append($"----- {label}: {file.RelativePath} -----\n");
            if (file.Truncated)
            {
                text.Append($"[truncated: first {MaxFileBytes / 1024} KB of {file.Bytes / 1024} KB]\n");
            }

            text.Append(file.Text.Replace("\r\n", "\n").TrimEnd('\n')).Append("\n----- end -----\n");
        }
    }

    /// <summary>
    /// Codeblöcke mit Pfad und Umfang; ein Ausschnitt ist nur ein Teil der Datei, der Rest bleibt. <paramref name="writeHint"/> setzt den Satz
    /// nach "must stay" fort (beginnt mit ", " und endet mit einem Punkt): wie die KI einen Ausschnitt schreiben soll, ist Sache der Funktion.
    /// <paramref name="gameFileHint"/> folgt auf die Erklärung von "game file" (ein oder mehrere ganze Sätze): was ein Spiel-Block der Funktion nützt
    /// und was die KI tut, wenn unklar ist, wie er gemeint ist.
    /// </summary>
    public static void AppendSnippets(StringBuilder text, IReadOnlyList<AiTaskSnippet> snippets, string writeHint, string gameFileHint)
    {
        text.Append("\n## Code blocks from the modder\n\n");
        text.Append("Instead of whole files the modder pasted these code blocks, each with its path in the mod. ")
            .Append("\"whole file\" means the block is the complete file. \"excerpt\" means it is only part of that file: the rest of the file ")
            .Append("belongs to other content and must stay").Append(writeHint)
            .Append(" If a path is unknown, ask the modder for it.\n");
        if (snippets.Any(s => s.Note.Trim().Length > 0))
        {
            text.Append("A line `Note: ...` right under a block header is the modder's explanation of that block, not part of the code. ")
                .Append("Follow it for that block.\n");
        }

        if (snippets.Any(s => s.FromGame))
        {
            text.Append("\"game file\" marks a block from the base game (path relative to the game folder, mods use the same paths). ")
                .Append(gameFileHint).Append('\n');
        }

        text.Append('\n');
        foreach (var snippet in snippets)
        {
            var path = snippet.Path.Trim().Length > 0 ? snippet.Path.Trim().Replace('\\', '/') : "(path unknown, ask the modder)";
            text.Append($"----- code block: {(snippet.FromGame ? "game file " : string.Empty)}{path} ({(snippet.WholeFile ? "whole file" : "excerpt")}) -----\n");
            foreach (var line in snippet.Note.Replace("\r\n", "\n").Trim('\n').Split('\n').Where(l => l.Trim().Length > 0))
            {
                text.Append("Note: ").Append(line.Trim()).Append('\n');
            }

            text.Append(snippet.Code.Replace("\r\n", "\n").TrimEnd('\n')).Append("\n----- end -----\n");
        }
    }

    /// <summary>Kopiert die Dateien (ganz, auch binär) unter ihrem relativen Pfad in den Zielordner; unsichere und fehlende Pfade werden übergangen.</summary>
    public static void CopyFiles(string root, IReadOnlyList<string> relative, string targetFolder)
    {
        foreach (var path in relative.Where(p => PathSafety.IsSafeRelative(p)))
        {
            var source = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(source))
            {
                continue;
            }

            var target = Path.Combine(targetFolder, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
        }
    }

    /// <summary>Text für "Fehler für die KI kopieren": <paramref name="intro"/> (ohne Zeilenumbruch am Ende), eine Leerzeile, dann PMTs Meldungen als Liste.</summary>
    public static string Feedback(string intro, IEnumerable<string> messages)
    {
        var text = new StringBuilder(intro).Append("\n\n");
        foreach (var message in messages)
        {
            text.Append("- ").Append(message).Append('\n');
        }

        return text.ToString();
    }
}
