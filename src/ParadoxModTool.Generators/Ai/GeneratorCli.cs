using ParadoxModTool.Generators.Editing.Package;

namespace ParadoxModTool.Generators.Ai;

/// <summary>Ergebnis eines Befehls von pmt-generator.exe: Rückgabewert (0 in Ordnung, 1 Befunde, 2 falscher Aufruf) und Ausgabezeilen.</summary>
public sealed record CliResult(int ExitCode, IReadOnlyList<string> Lines);

/// <summary>
/// Befehle von pmt-generator.exe (Spec 2026-10-05-generator-packages-ai-guide, Abschnitt 3): <c>check</c>, <c>probe</c> und <c>roundtrip</c>. Die Ausgabe
/// ist für KIs gedacht: feste Zeilenformen, Texte in der geladenen UI-Sprache (das Konsolenprogramm lädt Englisch). Schreibt nie in Mod oder Paket.
/// </summary>
public static class GeneratorCli
{
    private const int MaxExamples = 10;

    public static readonly IReadOnlyList<string> Usage = new[]
    {
        "usage:",
        "  pmt-generator check <package folder>",
        "  pmt-generator probe <package folder> <mod folder> [<key>]",
        "  pmt-generator roundtrip <package folder> <mod folder> <key> [--keep <folder>] [--languages a,b]",
    };

    public static CliResult Run(IReadOnlyList<string> args) => args switch
    {
        ["check", var package] => Check(package),
        ["probe", var package, var mod] => Probe(package, mod, null),
        ["probe", var package, var mod, var key] => Probe(package, mod, key),
        ["roundtrip", ..] when args.Count >= 4 => RoundtripWithOptions(args[1], args[2], args[3], args.Skip(4).ToList()),
        _ => new CliResult(2, Usage),
    };

    /// <summary>
    /// Liest den Satz <paramref name="key"/> aus der Mod, plant ihn neu (ohne zu schreiben) und vergleicht jede Datei: <c>same</c>, <c>differs</c> mit den
    /// ersten abweichenden Zeilen oder <c>new</c>. Rückgabe 0 nur, wenn alles gleich ist. <paramref name="keepFolder"/>: erzeugte Dateien dorthin schreiben.
    /// </summary>
    public static CliResult Roundtrip(string packageFolder, string modFolder, string key, string? keepFolder, IReadOnlyList<string>? languages)
    {
        GeneratorPlan plan;
        try
        {
            var package = GeneratorPackageLoader.Load(packageFolder, uiLanguage: "en");
            if (package.Read is null)
            {
                return new CliResult(1, new[] { GeneratorsTexts.FieldMissing("read") });
            }

            var all = languages ?? ModLanguages(modFolder);
            if (GeneratorStateReader.Read(package, modFolder, key, all) is not { } read)
            {
                return new CliResult(1, new[] { GeneratorsTexts.ReadKeyMissing(key) });
            }

            plan = GeneratorPlanner.Plan(package, read.State, new GeneratorTarget(modFolder, null, all));
        }
        catch (Exception ex) when (ex is GeneratorPackageException or GeneratorTemplateException or GeneratorOutputException or ArgumentException or IOException)
        {
            return new CliResult(1, new[] { ex.Message });
        }

        var lines = new List<string>();
        int same = 0, differ = 0, created = 0;
        foreach (var file in plan.Files.OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            switch (file.Status)
            {
                case ParadoxModTool.Patches.PlannedFileStatus.Unchanged:
                    same++;
                    lines.Add("same " + file.RelativePath);
                    break;
                case ParadoxModTool.Patches.PlannedFileStatus.New:
                    created++;
                    lines.Add("new " + file.RelativePath);
                    break;
                default:
                    differ++;
                    lines.Add("differs " + file.RelativePath);
                    lines.AddRange(FirstDifference(file.OriginalText, file.NewText));
                    break;
            }

            if (keepFolder is not null)
            {
                Keep(keepFolder, file);
            }
        }

        lines.Add($"{same} same, {differ} differ, {created} new");
        return new CliResult(differ + created == 0 ? 0 : 1, lines);
    }

    private static CliResult RoundtripWithOptions(string package, string mod, string key, IReadOnlyList<string> options)
    {
        string? keep = null;
        IReadOnlyList<string>? languages = null;
        for (var i = 0; i < options.Count; i++)
        {
            switch (options[i])
            {
                case "--keep" when i + 1 < options.Count:
                    keep = options[++i];
                    break;
                case "--languages" when i + 1 < options.Count:
                    languages = options[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    break;
                default:
                    return new CliResult(2, Usage);
            }
        }

        return Roundtrip(package, mod, key, keep, languages);
    }

    /// <summary>Ab der ersten abweichenden Zeile bis zu 5 Paare <c>- alt</c> / <c>+ neu</c>.</summary>
    private static IEnumerable<string> FirstDifference(string? original, string? updated)
    {
        if (original is null || updated is null)
        {
            yield break;
        }

        var before = original.Replace("\r\n", "\n").Split('\n');
        var after = updated.Replace("\r\n", "\n").Split('\n');
        var start = 0;
        while (start < before.Length && start < after.Length && before[start] == after[start])
        {
            start++;
        }

        for (var i = start; i < start + 5 && (i < before.Length || i < after.Length); i++)
        {
            if (i < before.Length)
            {
                yield return "  - " + before[i];
            }

            if (i < after.Length)
            {
                yield return "  + " + after[i];
            }
        }
    }

    private static void Keep(string folder, GeneratorPlannedFile file)
    {
        if (!SafeRelativePath.IsSafe(file.RelativePath))
        {
            return;
        }

        var path = Path.Combine(folder, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (file.Bytes is { } bytes)
        {
            File.WriteAllBytes(path, bytes);
        }
        else if (file.NewText is { } text)
        {
            File.WriteAllText(path, text, new System.Text.UTF8Encoding(file.HasBom));
        }
    }

    /// <summary>Echter Loader plus alles, was "Prüfen" im Paket-Editor tut; je Befund eine Zeile <c>Ort[:Zeile]: Meldung</c>.</summary>
    public static CliResult Check(string packageFolder)
    {
        PackageDocument document;
        try
        {
            if (!File.Exists(Path.Combine(packageFolder, "package.json")))
            {
                return new CliResult(1, new[] { GeneratorsTexts.PackageJsonMissing(packageFolder) });
            }

            document = PackageDocument.Load(packageFolder);
        }
        catch (Exception ex) when (ex is GeneratorPackageException or IOException or UnauthorizedAccessException)
        {
            return new CliResult(1, new[] { ex.Message });
        }

        var issues = PackageChecker.Check(document, new PackageFiles(packageFolder), "en");
        var lines = issues.Select(i => i.Line is { } line ? $"{i.Where}:{line}: {i.Message}" : $"{i.Where}: {i.Message}").ToList();
        lines.Add(issues.Count == 0 ? "OK" : $"{issues.Count} issue(s)");
        return new CliResult(issues.Count == 0 ? 0 : 1, lines);
    }

    /// <summary>Ohne Schlüssel die Schlüssel der Mod; mit Schlüssel der Probelauf wie in der Maske "Einlesen".</summary>
    public static CliResult Probe(string packageFolder, string modFolder, string? key)
    {
        if (Open(packageFolder) is not { } opened)
        {
            return new CliResult(1, new[] { GeneratorsTexts.PackageJsonMissing(packageFolder) });
        }

        var (document, files) = opened;
        if (key is null)
        {
            var (keys, error) = ReadProbe.Keys(document, files, null, modFolder);
            return error is null ? new CliResult(0, keys) : new CliResult(1, new[] { error });
        }

        var result = ReadProbe.Run(document, files, null, modFolder, key, ModLanguages(modFolder));
        var lines = new List<string>();
        if (result.Error is { } problem)
        {
            lines.Add("error: " + problem);
        }

        foreach (var report in result.Patterns)
        {
            lines.Add($"{report.Where}: {report.Count} match(es)");
            if (report.Problem is { } reason)
            {
                lines.Add("  ! " + reason);
            }

            lines.AddRange(report.Examples.Take(MaxExamples).Select(e => e.Captures.Length == 0 ? "  " + e.Text : $"  {e.Text}  ({e.Captures})"));
        }

        lines.Add("--- state ---");
        lines.AddRange(result.StateText.Split('\n'));
        lines.Add("--- notes ---");
        lines.AddRange(result.Notes);
        var failed = result.Error is not null || result.Patterns.Any(p => p.Count == 0);
        return new CliResult(failed ? 1 : 0, lines);
    }

    /// <summary>Sprachen der Mod: Ordnernamen unter einem Ordner <c>localization</c>; ohne Fund Englisch.</summary>
    public static IReadOnlyList<string> ModLanguages(string modFolder)
    {
        var found = Directory.Exists(modFolder)
            ? Directory.EnumerateDirectories(modFolder, "*", SearchOption.AllDirectories)
                .Where(d => ParadoxModTool.GameProfiles.LocalizationFolders.IsName(Path.GetFileName(Path.GetDirectoryName(d))))
                .Select(Path.GetFileName)
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();
        return found.Count > 0 ? found : new[] { "english" };
    }

    private static (PackageDocument Document, PackageFiles Files)? Open(string packageFolder)
    {
        try
        {
            return File.Exists(Path.Combine(packageFolder, "package.json")) ? (PackageDocument.Load(packageFolder), new PackageFiles(packageFolder)) : null;
        }
        catch (Exception ex) when (ex is GeneratorPackageException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
