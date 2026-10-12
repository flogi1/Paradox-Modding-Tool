using System.Text;
using System.Text.RegularExpressions;
using ParadoxModTool.Patches;
using Scriban.Runtime;

namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>Ein Treffer eines Musters: Pfad bzw. Schlüssel und die gefangenen Gruppen (z. B. <c>number=3, suffix=_1</c>).</summary>
public sealed record ProbeMatch(string Text, string Captures);

/// <summary>Treffer eines Musters im Probelauf; <see cref="Where"/> wie bei "Prüfen" (<c>read.keys</c>, <c>read.flags[1].block</c> …).</summary>
public sealed record ProbePatternReport(string Where, int Count, IReadOnlyList<ProbeMatch> Examples, string? Problem);

/// <summary>Ergebnis des Probelaufs: Fehler (Laden, Einlesen) oder Treffer, eingelesener Stand als Text und Hinweise des Einlesers.</summary>
public sealed record ReadProbeResult(string? Error, IReadOnlyList<ProbePatternReport> Patterns, string StateText, IReadOnlyList<string> Notes);

/// <summary>
/// "Probelauf" (Spec Teil 5b, Abschnitt 1): lädt den Editor-Stand aus einer Temp-Kopie und lässt den echten Einleser laufen; dazu je Muster die Treffer
/// in der Mod. Schreibt nichts in Mod oder Paket.
/// </summary>
public static class ReadProbe
{
    private const int MaxExamples = 10;
    private const string UiLanguage = "en";

    /// <summary>Schlüssel, die <c>read.keys</c> in der Mod findet; bei einem Ladefehler die Meldung.</summary>
    public static (IReadOnlyList<string> Keys, string? Error) Keys(PackageDocument document, PackageFiles files, IReadOnlyDictionary<string, string>? pending,
        string modRoot, string? dataFolder = null)
    {
        try
        {
            using var copy = PackageTempCopy.Create(document, files, pending);
            var package = copy.Load(UiLanguage, dataFolder);
            return package.Read is null
                ? (Array.Empty<string>(), GeneratorsTexts.FieldMissing("read"))
                : (GeneratorStateReader.Keys(package, modRoot), null);
        }
        catch (Exception ex) when (ex is GeneratorPackageException or GeneratorTemplateException or ArgumentException)
        {
            return (Array.Empty<string>(), ex.Message);
        }
    }

    public static ReadProbeResult Run(PackageDocument document, PackageFiles files, IReadOnlyDictionary<string, string>? pending, string modRoot, string key,
        IReadOnlyList<string> languages, string? dataFolder = null)
    {
        using var copy = PackageTempCopy.Create(document, files, pending);
        GeneratorPackage package;
        try
        {
            package = copy.Load(UiLanguage, dataFolder);
        }
        catch (Exception ex) when (ex is GeneratorPackageException or GeneratorTemplateException)
        {
            return new ReadProbeResult(ex.Message, Array.Empty<ProbePatternReport>(), string.Empty, Array.Empty<string>());
        }

        if (package.Read is not { } read)
        {
            return new ReadProbeResult(GeneratorsTexts.FieldMissing("read"), Array.Empty<ProbePatternReport>(), string.Empty, Array.Empty<string>());
        }

        var patterns = Reports(package, read, modRoot, key);
        try
        {
            if (GeneratorStateReader.Read(package, modRoot, key, languages) is not { } result)
            {
                return new ReadProbeResult(GeneratorsTexts.ProbeNoElementsFile(Render(package, key, read.Elements.File, null)), patterns, string.Empty, Array.Empty<string>());
            }

            return new ReadProbeResult(null, patterns, Describe(package, result.State), result.Notes);
        }
        catch (Exception ex) when (ex is GeneratorOutputException or GeneratorTemplateException or ArgumentException)
        {
            return new ReadProbeResult(ex.Message, patterns, string.Empty, Array.Empty<string>());
        }
    }

    /// <summary>Eingelesener Stand als eingerückter Text: Kopf-Felder, dann jede Ebene mit "Ebene Nummer" und ihren Feldern (zwei Leerzeichen je Tiefe).</summary>
    public static string Describe(GeneratorPackage package, GeneratorState state)
    {
        var builder = new StringBuilder();
        Values(builder, state.Head, package.Head, 0);
        if (package.Root is { } root && state.Head.Children.TryGetValue(root.Id, out var nodes))
        {
            Level(builder, root, nodes, 0);
        }

        return builder.ToString().TrimEnd('\n');
    }

    private static void Level(StringBuilder builder, LevelDef level, IReadOnlyList<FormNode> nodes, int depth)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            builder.Append(Indent(depth)).Append(level.Id).Append(' ').Append(i + 1).Append('\n');
            Values(builder, nodes[i], level.Fields, depth + 1);
            if (level.Child is { } child && nodes[i].Children.TryGetValue(child.Id, out var children))
            {
                Level(builder, child, children, depth + 1);
            }
        }
    }

    private static void Values(StringBuilder builder, FormNode node, IReadOnlyList<FieldDef> fields, int depth)
    {
        foreach (var field in fields)
        {
            if (node.Values.TryGetValue(field.Id, out var value))
            {
                Line(builder, depth, field.Id, value);
            }

            if (node.Translations.TryGetValue(field.Id, out var translations))
            {
                foreach (var (language, text) in translations)
                {
                    Line(builder, depth, $"{field.Id} [{language}]", text);
                }
            }
        }
    }

    private static void Line(StringBuilder builder, int depth, string name, string value)
    {
        var lines = value.Replace("\r\n", "\n").Split('\n');
        builder.Append(Indent(depth)).Append(name).Append(" = ").Append(lines[0]).Append('\n');
        foreach (var line in lines.Skip(1))
        {
            builder.Append(Indent(depth + 1)).Append(line).Append('\n');
        }
    }

    private static string Indent(int depth) => new(' ', depth * 2);

    private static IReadOnlyList<ProbePatternReport> Reports(GeneratorPackage package, ReadDef read, string modRoot, string key)
    {
        var reports = new List<ProbePatternReport> { KeysReport(package, modRoot) };
        reports.Add(BlockReport(package, key, modRoot, "read.elements.block", read.Elements.File, read.Elements.Block));
        for (var i = 0; i < read.Localization.Count; i++)
        {
            var where = $"read.localization[{i + 1}].key";
            reports.Add(read.LocalizationFor(package, read.Localization[i]) is { } output
                ? LocalizationReport(package, key, modRoot, where, Render(package, key, output.Path, "english"), read.Localization[i].Key)
                : new ProbePatternReport(where, 0, Array.Empty<ProbeMatch>(), GeneratorsTexts.ProbeNoLocalizationOutput));
        }

        for (var i = 0; i < read.Flags.Count; i++)
        {
            reports.Add(BlockReport(package, key, modRoot, $"read.flags[{i + 1}].block", read.Flags[i].File, read.Flags[i].Block));
        }

        var values = read.Values ?? Array.Empty<ReadValueDef>();
        for (var i = 0; i < values.Count; i++)
        {
            reports.Add(BlockReport(package, key, modRoot, $"read.values[{i + 1}].block", values[i].File, values[i].Block));
        }

        for (var i = 0; i < read.Assets.Count; i++)
        {
            reports.Add(Guarded($"read.assets[{i + 1}].file", () =>
            {
                var path = Render(package, key, read.Assets[i].File, null);
                return Exists(modRoot, path) is { } problem
                    ? new ProbePatternReport($"read.assets[{i + 1}].file", 0, Array.Empty<ProbeMatch>(), problem)
                    : new ProbePatternReport($"read.assets[{i + 1}].file", 1, new[] { new ProbeMatch(path, string.Empty) }, null);
            }));
        }

        return reports;
    }

    private static ProbePatternReport KeysReport(GeneratorPackage package, string modRoot) => Guarded("read.keys", () =>
    {
        // Gleiche Suche wie der Einleser: aus Pfaden oder aus Blockschlüsseln
        var matches = GeneratorStateReader.KeyMatches(package, modRoot).Select(m => new ProbeMatch(m.Path, "key=" + m.Key)).ToList();
        return new ProbePatternReport("read.keys", matches.Count, matches.Take(MaxExamples).ToList(), null);
    });

    private static ProbePatternReport BlockReport(GeneratorPackage package, string key, string modRoot, string where, string fileTemplate, string blockTemplate) => Guarded(where, () =>
    {
        var path = Render(package, key, fileTemplate, null);
        if (Exists(modRoot, path) is { } problem)
        {
            return new ProbePatternReport(where, 0, Array.Empty<ProbeMatch>(), problem);
        }

        var pattern = GeneratorStateReader.Whole(Render(package, key, blockTemplate, null));
        var matches = GeneratorStateReader.Parse(modRoot, path).Statements
            .Select(s => pattern.Match(s.Key))
            .Where(m => m.Success)
            .Select(m => new ProbeMatch(m.Value, Captures(m)))
            .ToList();
        return new ProbePatternReport(where, matches.Count, matches.Take(MaxExamples).ToList(), null);
    });

    private static ProbePatternReport LocalizationReport(GeneratorPackage package, string key, string modRoot, string where, string path, string keyTemplate) => Guarded(where, () =>
    {
        if (Exists(modRoot, path) is { } problem)
        {
            return new ProbePatternReport(where, 0, Array.Empty<ProbeMatch>(), problem);
        }

        var pattern = GeneratorStateReader.Whole(Render(package, key, keyTemplate, "english"));
        var matches = YmlReader.Values(TextFile.Read(GeneratorStateReader.Full(modRoot, path)).Text).Keys
            .Select(k => pattern.Match(k))
            .Where(m => m.Success)
            .Select(m => new ProbeMatch(m.Value, Captures(m)))
            .ToList();
        return new ProbePatternReport(where, matches.Count, matches.Take(MaxExamples).ToList(), null);
    });

    /// <summary>Fehler beim Rendern, im Muster oder beim Lesen der Datei landen als Problem der Zeile statt als Ausnahme.</summary>
    private static ProbePatternReport Guarded(string where, Func<ProbePatternReport> build)
    {
        try
        {
            return build();
        }
        catch (Exception ex) when (ex is ArgumentException or GeneratorOutputException or GeneratorTemplateException or IOException)
        {
            return new ProbePatternReport(where, 0, Array.Empty<ProbeMatch>(), ex.Message);
        }
    }

    private static string? Exists(string modRoot, string path) =>
        !SafeRelativePath.IsSafe(path.Trim()) ? GeneratorsTexts.ReadUnsafePath(path)
        : File.Exists(GeneratorStateReader.Full(modRoot, path)) ? null
        : GeneratorsTexts.ProbeFileMissing(path);

    private static string Captures(Match match) =>
        string.Join(", ", new[] { "key", "number", "suffix" }.Where(g => match.Groups[g].Success).Select(g => $"{g}={match.Groups[g].Value}"));

    /// <summary>Rendert eine Vorlage wie der Einleser: Schlüsselfeld = Schlüssel, Sprache für Lokalisierungspfade.</summary>
    private static string Render(GeneratorPackage package, string key, string template, string? language)
    {
        var keyState = new GeneratorState { PackageId = package.Id, Key = key };
        PackageKey.Apply(package, keyState, key);
        ScriptObject model = TemplateModel.Build(package, keyState, language);
        return new GeneratorRenderer(package).RenderInline(template, model);
    }
}
