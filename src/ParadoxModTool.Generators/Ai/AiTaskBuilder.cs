using System.Text;
using ParadoxModTool.Generators.Editing.Package;

namespace ParadoxModTool.Generators.Ai;

/// <summary>
/// Auftrag an eine KI: Beschreibung, Spiel, Mod mit den angehakten Vorbild-Dateien (relativ zur Mod), optional das offene Paket und der Pfad von
/// pmt-generator.exe (nur wenn die KI selbst prüfen kann). <see cref="Snippets"/>: von Hand eingefügte Codeblöcke statt ganzer Dateien.
/// </summary>
public sealed record AiTaskRequest(string Description, string GameId, string? ModRoot, IReadOnlyList<string> ExampleFiles, string? PackageFolder, string? CliPath)
{
    public IReadOnlyList<AiTaskSnippet> Snippets { get; init; } = Array.Empty<AiTaskSnippet>();
}

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
/// "Paket mit KI erstellen", Reiter "Auftrag" (Spec 2026-10-05-generator-packages-ai-guide, Abschnitt 2): baut aus Anleitung, Beispielpaket, Beschreibung
/// und Vorbild-Dateien einen Text für Chat-KIs oder einen Ordner für KIs mit Dateizugriff. Liest nur, schreibt nur in den Zielordner.
/// </summary>
public static class AiTaskBuilder
{
    public const long MaxFileBytes = 204800;

    public const long WarnTotalBytes = 512000;

    private const int BinaryProbe = 8192;
    private const string ExamplePackage = "examples/stadtwappen";

    /// <summary>Liest die Vorbild-Dateien; eine Datei mit NUL-Byte in den ersten 8 KB gilt als binär, große Texte werden auf 200 KB gekürzt.</summary>
    public static IReadOnlyList<AiTaskFile> ReadFiles(string root, IReadOnlyList<string> relative)
    {
        var files = new List<AiTaskFile>();
        foreach (var path in relative)
        {
            var full = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            if (!SafeRelativePath.IsSafe(path) || !File.Exists(full))
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

    /// <summary>Ein Text: SKILL.md, reference.md, das Beispielpaket in Dateiblöcken, dann Auftrag, Vorbild-Dateien und das offene Paket.</summary>
    public static string BuildText(AiTaskRequest request, string guideFolder)
    {
        var text = new StringBuilder();
        text.Append(File.ReadAllText(Path.Combine(guideFolder, "SKILL.md"))).Append("\n\n");
        text.Append(File.ReadAllText(Path.Combine(guideFolder, "reference.md"))).Append("\n\n");
        text.Append("# Example package (for reference, do not copy its id)\n\n");
        var example = Path.Combine(guideFolder, ExamplePackage.Replace('/', Path.DirectorySeparatorChar));
        foreach (var file in PackageTextFiles(example))
        {
            text.Append($"<<<FILE {ExamplePackage}/{file}>>>\n").Append(Read(example, file)).Append("\n<<<END>>>\n");
        }

        text.Append('\n').Append(TaskMarkdown(request, folderMode: false)).Append('\n');
        if (request.ModRoot is { } mod)
        {
            foreach (var file in ReadFiles(mod, request.ExampleFiles))
            {
                if (file.Text is null)
                {
                    text.Append($"----- example: {file.RelativePath} (binary, not included) -----\n");
                    continue;
                }

                text.Append($"----- example: {file.RelativePath} -----\n");
                if (file.Truncated)
                {
                    text.Append($"[truncated: first {MaxFileBytes / 1024} KB of {file.Bytes / 1024} KB]\n");
                }

                text.Append(file.Text.Replace("\r\n", "\n").TrimEnd('\n')).Append("\n----- end -----\n");
            }
        }

        if (request.Snippets.Count > 0)
        {
            AppendSnippets(text, request.Snippets);
        }

        if (request.PackageFolder is { } package)
        {
            foreach (var file in PackageTextFiles(package))
            {
                text.Append($"----- package: {file} -----\n").Append(Read(package, file)).Append("\n----- end -----\n");
            }
        }

        return text.ToString();
    }

    /// <summary>Ordner für KIs mit Dateizugriff: Anleitung, task.md, example/ mit den Vorbild-Dateien (ganz, auch binär), package/ mit dem offenen Paket.</summary>
    public static void BuildFolder(AiTaskRequest request, string guideFolder, string targetFolder)
    {
        PackageStore.CopyTree(guideFolder, targetFolder);
        File.WriteAllText(Path.Combine(targetFolder, "task.md"), TaskMarkdown(request, folderMode: true), new UTF8Encoding(false));
        if (request.ModRoot is { } mod)
        {
            foreach (var path in request.ExampleFiles.Where(p => SafeRelativePath.IsSafe(p)))
            {
                var source = Path.Combine(mod, path.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(source))
                {
                    var target = Path.Combine(targetFolder, "example", path.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(source, target, overwrite: true);
                }
            }
        }

        if (request.PackageFolder is { } package)
        {
            foreach (var file in PackageTextFiles(package))
            {
                var target = Path.Combine(targetFolder, "package", file.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(package, file.Replace('/', Path.DirectorySeparatorChar)), target, overwrite: true);
            }
        }
    }

    /// <summary>Text für "Fehler für die KI kopieren": PMTs Meldungen als Liste, auf Englisch eingeleitet.</summary>
    public static string Feedback(IEnumerable<string> messages)
    {
        var text = new StringBuilder("PMT reported these problems with your package. Please fix them and answer again with all changed files:\n\n");
        foreach (var message in messages)
        {
            text.Append("- ").Append(message).Append('\n');
        }

        return text.ToString();
    }

    private static string TaskMarkdown(AiTaskRequest request, bool folderMode)
    {
        var text = new StringBuilder("# Task\n\n");
        text.Append($"Game: {request.GameId}\n\n");
        text.Append("## What the modder wants\n\n").Append(request.Description.Trim()).Append("\n\n");
        var source = request.Snippets.Count > 0 ? "the example files and code blocks below" : "the example files below";
        text.Append(request.PackageFolder is null
            ? $"Build a new package from {source}.\n\n"
            : $"Change the existing package below; keep its `id` and `markerId`. Use {source}.\n\n");
        text.Append("## How to check\n\n");
        if (request.CliPath is { } cli)
        {
            text.Append("You can run PMT's check program. Save your package to a folder, then run:\n\n");
            text.Append($"    \"{cli}\" check <package folder>\n");
            if (request.ModRoot is { } mod)
            {
                text.Append($"    \"{cli}\" probe <package folder> \"{mod}\" [<key>]\n");
                text.Append($"    \"{cli}\" roundtrip <package folder> \"{mod}\" <key>\n");
            }

            text.Append('\n');
        }
        else
        {
            text.Append("You cannot run PMT yourself. Give the modder your files; they import them in PMT (\"Paket mit KI erstellen\", tab \"Ergebnis\"), ")
                .Append("press \"Prüfen\" and run \"Probelauf\" in the \"Einlesen\" mask, and send the messages back to you.\n\n");
        }

        if (request.ExampleFiles.Count > 0 || request.Snippets.Count == 0)
        {
            text.Append("## Example files from the mod\n\n");
            text.Append(folderMode
                ? "The example files are in the folder `example/` (paths relative to the mod)" + (request.PackageFolder is null ? ".\n" : "; the current package is in `package/`.\n")
                : "Each file follows below between `----- example: <path> -----` and `----- end -----`.\n");
        }
        else if (folderMode && request.PackageFolder is not null)
        {
            text.Append("The current package is in `package/`.\n");
        }

        // Im Text-Modus folgen die Codeblöcke erst nach den Vorbild-Dateien (BuildText), damit jede Überschrift über ihrem Inhalt steht.
        if (folderMode && request.Snippets.Count > 0)
        {
            AppendSnippets(text, request.Snippets);
        }

        return text.ToString();
    }

    /// <summary>Codeblöcke mit Pfad und Umfang; ein Ausschnitt ist nur ein Teil der Datei, der Rest bleibt.</summary>
    private static void AppendSnippets(StringBuilder text, IReadOnlyList<AiTaskSnippet> snippets)
    {
        text.Append("\n## Code blocks from the modder\n\n");
        text.Append("Instead of whole files the modder pasted these code blocks, each with its path in the mod. ")
            .Append("\"whole file\" means the block is the complete file. \"excerpt\" means it is only part of that file: the rest of the file ")
            .Append("belongs to other content and must stay, so write it with `insertSection`, `syncBlocks` or `ensureLine`, never `ownFile`. ")
            .Append("A `.yml` block is always written with `localization`. If a path is unknown, ask the modder for it.\n");
        if (snippets.Any(s => s.Note.Trim().Length > 0))
        {
            text.Append("A line `Note: ...` right under a block header is the modder's explanation of that block, not part of the code. ")
                .Append("Follow it for that block.\n");
        }

        if (snippets.Any(s => s.FromGame))
        {
            text.Append("\"game file\" marks a block from the base game (path relative to the game folder, mods use the same paths). ")
                .Append("It shows how the game does it; the package never writes game files. If it is unclear whether the mod should ")
                .Append("override, extend or only imitate it, ask the modder.\n");
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

    /// <summary>package.json, functions.sbn, Vorlagen und Text-Assets eines Pakets (relativ, mit "/").</summary>
    private static IEnumerable<string> PackageTextFiles(string folder)
    {
        foreach (var name in new[] { "package.json", "functions.sbn" }.Where(n => File.Exists(Path.Combine(folder, n))))
        {
            yield return name;
        }

        foreach (var sub in new[] { "templates", "assets" })
        {
            var dir = Path.Combine(folder, sub);
            if (!Directory.Exists(dir))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(dir).Where(f => sub == "templates" || f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                yield return sub + "/" + Path.GetFileName(file);
            }
        }
    }

    private static string Read(string folder, string relative) =>
        File.ReadAllText(Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar))).Replace("\r\n", "\n").TrimEnd('\n');
}
