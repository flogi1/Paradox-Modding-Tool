using Scriban;
using Scriban.Parsing;

namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>Ein Befund von "Prüfen": Ort (package.json, Vorlagenname oder <c>outputs[id].key</c>), Zeile (1-basiert) und Vorlage für den Sprung.</summary>
public sealed record PackageCheckIssue(string Where, int? Line, string Message, string? TemplateName);

/// <summary>
/// "Prüfen" im Paket-Editor (Spec Teil 5a, Abschnitt 1): lädt den aktuellen Stand mit dem echten Loader aus einer temporären Kopie und lässt Scriban
/// alle Vorlagen, Inline-Vorlagen der Ausgaben und Bedingungen lesen. Kein zweiter Prüfer; der Loader meldet nur seinen ersten Fehler. Ist alles
/// lesbar, folgt ein Probe-Erzeugen mit Beispielwerten (<see cref="PackageSampleRender"/>), das unbekannte Namen findet.
/// </summary>
public static class PackageChecker
{
    private const string FunctionsName = "functions.sbn";

    private static readonly string[] InlineKeys = { "path", "block", "legacyBegin", "legacyEnd", "line", "keyPattern", "source", "match", "keys", "insertAfter", "ifLimit", "jsonPath", "entryKey" };

    /// <param name="pendingTemplates">Noch nicht gespeicherte Vorlagentexte (Name → Text), statt der Dateien geprüft.</param>
    public static IReadOnlyList<PackageCheckIssue> Check(PackageDocument document, PackageFiles files, string uiLanguage, IReadOnlyDictionary<string, string>? pendingTemplates = null)
    {
        var (templates, functions) = PackageTempCopy.Texts(files, pendingTemplates);

        var issues = new List<PackageCheckIssue>();
        // Die Kopie bleibt bis zum Ende: Das Probe-Erzeugen liest die Vorlagen aus ihr.
        using var copy = PackageTempCopy.Write(document, templates, functions);
        var package = LoadCheck(copy, uiLanguage, issues);

        if (functions.Length > 0)
        {
            ParseInto(issues, FunctionsName, functions, FunctionsName, FunctionsName);
        }

        foreach (var (name, text) in templates.OrderBy(t => t.Key, StringComparer.OrdinalIgnoreCase))
        {
            ParseInto(issues, name, text, name, name);
        }

        foreach (var output in document.Outputs)
        {
            foreach (var key in InlineKeys)
            {
                var text = key == "path" ? output.Path : output.Get(key);
                if (!string.IsNullOrEmpty(text))
                {
                    ParseInto(issues, $"outputs[{output.Id}].{key}", text, text, null);
                }
            }

            var patterns = output.GetList("legacyPatterns");
            for (var i = 0; i < patterns.Count; i++)
            {
                ParseInto(issues, $"outputs[{output.Id}].legacyPatterns[{i + 1}]", patterns[i], patterns[i], null);
            }
        }

        Conditions(issues, "warnings", document.Warnings);
        Conditions(issues, "errors", document.Errors);

        // Probe-Erzeugen erst, wenn alles lädt und sich lesen lässt; sonst käme jeder Lesefehler ein zweites Mal.
        if (package is not null && issues.Count == 0)
        {
            issues.AddRange(PackageSampleRender.Run(package));
        }

        foreach (var output in document.Outputs.Where(o => o.Kind != OutputKind.Localization && o.Path.Trim().EndsWith(".yml", StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(new PackageCheckIssue($"outputs[{output.Id}].kind", null, GeneratorsTexts.CheckYmlNotLocalization(output.Id), null));
        }

        ReadIssues(document, issues);
        LookupIssues(document, issues);
        return issues;
    }

    /// <summary>Muster, Pfade und Feld-Verweise in <c>read</c> (Spec Teil 5b, Abschnitt 4 "Prüfen").</summary>
    private static void ReadIssues(PackageDocument document, List<PackageCheckIssue> issues)
    {
        if (document.Read is not { } read)
        {
            return;
        }

        void Add(string where, string? message)
        {
            if (message is not null)
            {
                issues.Add(new PackageCheckIssue(where, null, message, null));
            }
        }

        if (read.KeysBlock.Length > 0)
        {
            Add("read.keys.file", PatternCheck.KeysFile(read.Keys));
            Add("read.keys.block", PatternCheck.KeysBlock(read.KeysBlock, document.KeyFields));
        }
        else
        {
            Add("read.keys", PatternCheck.Keys(read.Keys));
        }
        Add("read.elements.file", PatternCheck.Path(read.ElementsFile));
        // Bei zählbarer oberster Ebene darf die Endung allein das Element wählen (Gruppe suffix ohne number)
        var suffixAlone = document.Root is { ConstantsTable: null };
        Add("read.elements.block", PatternCheck.Numbered(read.ElementsBlock, suffixAlone));
        var level = document.Levels.FirstOrDefault(l => l.Id == read.ElementsLevel);
        Add("read.elements.level", level is null ? GeneratorsTexts.CheckUnknownLevel(read.ElementsLevel) : null);
        foreach (var field in read.ElementFields.Keys.Where(f => level is not null && level.Fields.Items.All(x => x.Id != f)))
        {
            Add($"read.elements.fields.{field}", GeneratorsTexts.CheckUnknownField(field));
        }

        var localization = read.Localization.Items;
        for (var i = 0; i < localization.Count; i++)
        {
            // Kopf-Feld (ohne Punkt) braucht keine Gruppe number
            var head = !localization[i].Get("field").Contains('.');
            Add($"read.localization[{i + 1}].key", head ? PatternCheck.Matching(localization[i].Get("key")) : PatternCheck.Numbered(localization[i].Get("key"), suffixAlone));
            Add($"read.localization[{i + 1}].field", FieldProblem(document, localization[i].Get("field"),
                f => f.Type is FieldType.Text or FieldType.MultilineText && f.Multilingual, GeneratorsTexts.ExpectedMultilingual));
            var output = localization[i].Get("output").Trim();
            Add($"read.localization[{i + 1}].output", output.Length == 0 || document.Outputs.Any(o => o.Kind == OutputKind.Localization && o.Id == output)
                ? null
                : GeneratorsTexts.UnknownValue("read.localization.output", output));
        }

        var flags = read.Flags.Items;
        for (var i = 0; i < flags.Count; i++)
        {
            var head = !flags[i].Get("field").Contains('.');
            Add($"read.flags[{i + 1}].file", PatternCheck.Path(flags[i].Get("file")));
            Add($"read.flags[{i + 1}].block", head ? PatternCheck.Matching(flags[i].Get("block")) : PatternCheck.Numbered(flags[i].Get("block"), suffixAlone));
            Add($"read.flags[{i + 1}].field", FieldProblem(document, flags[i].Get("field"), f => f.Type == FieldType.Bool, GeneratorsTexts.ExpectedBool));
        }

        var values = read.Values.Items;
        for (var i = 0; i < values.Count; i++)
        {
            Add($"read.values[{i + 1}].file", PatternCheck.Path(values[i].Get("file")));
            Add($"read.values[{i + 1}].block", PatternCheck.Matching(values[i].Get("block")));
            Add($"read.values[{i + 1}].pattern", values[i].Get("body").Trim().Length > 0 ? null : PatternCheck.Value(values[i].Get("pattern")));
            Add($"read.values[{i + 1}].field", FieldProblem(document, values[i].Get("field"), f => f.Type is FieldType.Text or FieldType.MultilineText or FieldType.Number or FieldType.Choice,
                GeneratorsTexts.ExpectedTextOrNumber));
        }

        var assets = read.Assets.Items;
        for (var i = 0; i < assets.Count; i++)
        {
            var id = assets[i].Get("field");
            var field = document.Head.Items.FirstOrDefault(f => f.Id == id);
            Add($"read.assets[{i + 1}].field", field is null ? GeneratorsTexts.CheckUnknownField(id)
                : field.Type != FieldType.Image ? GeneratorsTexts.CheckFieldWrongType(id, GeneratorsTexts.ExpectedImage) : null);
            Add($"read.assets[{i + 1}].file", PatternCheck.Path(assets[i].Get("file")));
        }
    }

    /// <summary>Verweis auf ein Kopf-Feld (ohne Punkt) oder <c>Ebene.Feld</c>, jeweils mit passender Art.</summary>
    private static string? FieldProblem(PackageDocument document, string reference, Func<FieldNode, bool> fits, string expected)
    {
        if (reference.Contains('.'))
        {
            return LevelFieldProblem(document, reference, fits, expected);
        }

        return document.Head.Items.FirstOrDefault(f => f.Id == reference) is not { } field ? GeneratorsTexts.CheckUnknownField(reference)
            : fits(field) ? null
            : GeneratorsTexts.CheckFieldWrongType(reference, expected);
    }

    /// <summary>Verweis <c>Ebene.Feld</c>: Ebene und Feld gibt es, und das Feld hat die passende Art.</summary>
    private static string? LevelFieldProblem(PackageDocument document, string reference, Func<FieldNode, bool> fits, string expected)
    {
        var dot = reference.IndexOf('.', StringComparison.Ordinal);
        var levelId = dot < 0 ? reference : reference[..dot];
        if (document.Levels.FirstOrDefault(l => l.Id == levelId) is not { } level)
        {
            return GeneratorsTexts.CheckUnknownLevel(levelId);
        }

        var fieldId = dot < 0 ? string.Empty : reference[(dot + 1)..];
        return level.Fields.Items.FirstOrDefault(f => f.Id == fieldId) is not { } field ? GeneratorsTexts.CheckUnknownField(reference)
            : fits(field) ? null
            : GeneratorsTexts.CheckFieldWrongType(reference, expected);
    }

    /// <summary>Quellen in <c>lookups</c> und die Feld-Angaben <c>lookup</c>.</summary>
    private static void LookupIssues(PackageDocument document, List<PackageCheckIssue> issues)
    {
        void Add(string where, string? message)
        {
            if (message is not null)
            {
                issues.Add(new PackageCheckIssue(where, null, message, null));
            }
        }

        var assets = document.Assets.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var source in document.LookupSources)
        {
            var where = $"lookups[{source.Id}]";
            if (source.Kind == Lookups.LookupKind.GameKeys)
            {
                var folder = source.Get("folder") ?? string.Empty;
                Add(where + ".folder", folder.Trim().Length == 0 ? GeneratorsTexts.PatternEmpty
                    : SafeRelativePath.IsSafe(folder.Trim()) ? null : GeneratorsTexts.PathUnsafe(folder));
                if (source.Get("tagFromFile") is { Length: > 0 } tagFromFile)
                {
                    Add(where + ".tagFromFile", PatternCheck.TagFromFile(tagFromFile));
                }
            }
            else
            {
                var asset = source.Get("asset") ?? string.Empty;
                Add(where + ".asset", asset.Length == 0 ? GeneratorsTexts.PatternEmpty : assets.Contains(asset) ? null : GeneratorsTexts.LookupUnknownAsset(source.Id, asset));
                Add(where + ".items", string.IsNullOrWhiteSpace(source.Get("items")) ? GeneratorsTexts.PatternEmpty : null);
            }
        }

        var sources = document.LookupSources.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var (list, field) in document.AllFields())
        {
            if (field.Lookup is not { } lookup)
            {
                continue;
            }

            var where = $"fields[{list.LevelId ?? "head"}.{field.Id}].lookup";
            if (lookup.Sources.Count == 0)
            {
                Add(where, GeneratorsTexts.CheckLookupNoSource);
            }

            foreach (var source in lookup.Sources.Where(s => !sources.Contains(s)))
            {
                Add(where, GeneratorsTexts.LookupUnknownSource(field.Id, source));
            }

            if (lookup.Fill("name") is null && lookup.Fill("desc") is null)
            {
                Add(where, GeneratorsTexts.LookupFillsNeedName(field.Id));
            }

            foreach (var target in new[] { lookup.Fill("name"), lookup.Fill("desc") }.OfType<string>())
            {
                var targetField = list.Items.FirstOrDefault(f => f.Id == target);
                Add(where, targetField is null ? GeneratorsTexts.LookupUnknownFill(field.Id, target)
                    : targetField.Type is FieldType.Text or FieldType.MultilineText ? null
                    : GeneratorsTexts.CheckFieldWrongType(target, GeneratorsTexts.ExpectedText));
            }
        }
    }

    private static void Conditions(List<PackageCheckIssue> issues, string section, IReadOnlyList<(string When, LocalizedText Message)> conditions)
    {
        for (var i = 0; i < conditions.Count; i++)
        {
            // Wie GeneratorRenderer.Evaluate.
            ParseInto(issues, $"{section}[{i + 1}]", "{{ if (" + conditions[i].When + ") }}1{{ end }}", section, null);
        }
    }

    private static void ParseInto(List<PackageCheckIssue> issues, string where, string text, string sourceName, string? templateName)
    {
        // Nur der erste Fehler je Text (wie beim Erzeugen); Folgefehler desselben Fehlers wären nur Lärm.
        var template = Template.Parse(text, sourceName);
        if (template.Messages.FirstOrDefault(m => m.Type == ParserMessageType.Error) is { } message)
        {
            issues.Add(new PackageCheckIssue(where, message.Span.Start.Line + 1, message.Message, templateName));
        }
    }

    /// <summary>Lädt den aktuellen Stand aus der temporären Kopie; Ladefehler kommen als ein Befund "package.json".</summary>
    private static GeneratorPackage? LoadCheck(PackageTempCopy copy, string uiLanguage, List<PackageCheckIssue> issues)
    {
        try
        {
            return copy.Load(uiLanguage);
        }
        catch (GeneratorPackageException ex)
        {
            issues.Add(new PackageCheckIssue("package.json", null, ex.Message, null));
        }
        catch (GeneratorTemplateException ex)
        {
            issues.Add(new PackageCheckIssue(ex.TemplateName, ex.Line, ex.Message, ex.TemplateName));
        }

        return null;
    }
}
