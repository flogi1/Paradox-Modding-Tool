using System.Text;
using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Patches;
using ParadoxModTool.Patches.Ai;

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
/// "Paket mit KI erstellen", Reiter "Auftrag" (Spec 2026-10-05-generator-packages-ai-guide, Abschnitt 2): baut aus Anleitung, Beispielpaket, Beschreibung
/// und Vorbild-Dateien einen Text für Chat-KIs oder einen Ordner für KIs mit Dateizugriff. Liest nur, schreibt nur in den Zielordner.
/// Die allgemeinen Helfer (Dateien lesen, Blöcke und Codeblöcke ausgeben) liegen in <see cref="AiTaskFiles"/>.
/// </summary>
public static class AiTaskBuilder
{
    private const string ExamplePackage = "examples/stadtwappen";

    /// <summary>Wie die KI einen Ausschnitt schreibt: setzt den Satz nach "must stay" fort (<see cref="AiTaskFiles.AppendSnippets"/>).</summary>
    private const string WriteHint = ", so write it with `insertSection`, `syncBlocks` or `ensureLine`, never `ownFile`. " +
        "A `.yml` block is always written with `localization`.";

    /// <summary>Was ein Spiel-Codeblock nützt (<see cref="AiTaskFiles.AppendSnippets"/>).</summary>
    private const string GameFileHint = "It shows how the game does it; the package never writes game files. " +
        "If it is unclear whether the mod should override, extend or only imitate it, ask the modder.";

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
            AiTaskFiles.AppendFiles(text, "example", AiTaskFiles.ReadFiles(mod, request.ExampleFiles));
        }

        if (request.Snippets.Count > 0)
        {
            AiTaskFiles.AppendSnippets(text, request.Snippets, WriteHint, GameFileHint);
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
        FolderCopy.CopyTree(guideFolder, targetFolder);
        File.WriteAllText(Path.Combine(targetFolder, "task.md"), TaskMarkdown(request, folderMode: true), new UTF8Encoding(false));
        if (request.ModRoot is { } mod)
        {
            AiTaskFiles.CopyFiles(mod, request.ExampleFiles, Path.Combine(targetFolder, "example"));
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
    public static string Feedback(IEnumerable<string> messages) =>
        AiTaskFiles.Feedback("PMT reported these problems with your package. Please fix them and answer again with all changed files:", messages);

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
            AiTaskFiles.AppendSnippets(text, request.Snippets, WriteHint, GameFileHint);
        }

        return text.ToString();
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
