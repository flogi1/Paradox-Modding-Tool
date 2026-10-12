using Scriban.Runtime;

namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>
/// Probe-Erzeugen für "Prüfen": rendert Pfade, Vorlagen, Inline-Texte und Bedingungen aller Ausgaben mit einem Beispielsatz im Speicher, ohne Mod und
/// ohne zu schreiben. So fallen unbekannte Namen auf, die Scriban erst beim Rendern meldet. Jede zählbare Ebene bekommt mindestens ein Element,
/// damit auch Schleifen über anfangs leere Ebenen laufen.
/// </summary>
internal static class PackageSampleRender
{
    private const string SampleLanguage = "english";

    public static IEnumerable<PackageCheckIssue> Run(GeneratorPackage package)
    {
        var issues = new List<PackageCheckIssue>();
        var seen = new HashSet<(string, int?, string)>();
        var renderer = new GeneratorRenderer(package);
        var state = SampleState(package);
        var model = TemplateModel.Build(package, state, null);
        var languageModel = TemplateModel.Build(package, state, SampleLanguage);

        void Try(string where, bool isTemplate, Action render)
        {
            try
            {
                render();
            }
            catch (GeneratorTemplateException ex)
            {
                // Fehler in functions.sbn gehören dorthin, auch wenn ein Inline-Text sie auslöst.
                var templateName = isTemplate || ex.TemplateName == "functions.sbn" ? ex.TemplateName : null;
                var at = templateName ?? where;
                if (seen.Add((at, ex.Line, ex.Detail)))
                {
                    issues.Add(new PackageCheckIssue(at, ex.Line, GeneratorsTexts.CheckSampleRender(ex.Detail), templateName));
                }
            }
        }

        foreach (var output in package.Outputs)
        {
            var outputModel = output.Kind == OutputKind.Localization ? languageModel : model;
            foreach (var item in Items(package, output, outputModel))
            {
                var scope = item is null ? null : new ScriptObject { ["item"] = item };
                foreach (var (key, text) in Inline(output))
                {
                    var where = $"outputs[{output.Id}].{key}";
                    Try(where, false, () =>
                    {
                        var rendered = renderer.RenderInline(text, outputModel, scope);
                        if (key is "match" or "keys" or "insertAfter")
                        {
                            // Treffer-, Schlüssel- und Einfüge-Muster (insertAfter) sind reguläre Ausdrücke; ein Fehler fiele sonst erst beim Erzeugen auf.
                            try
                            {
                                _ = new System.Text.RegularExpressions.Regex(rendered, System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                            }
                            catch (ArgumentException ex)
                            {
                                if (seen.Add((where, null, ex.Message)))
                                {
                                    issues.Add(new PackageCheckIssue(where, null, GeneratorsTexts.MatchInvalid(output.Id, ex.Message), null));
                                }
                            }
                        }
                    });
                }

                if (output.Kind == OutputKind.Check)
                {
                    SampleCheck(output, outputModel, scope);
                    continue;
                }

                foreach (var name in new[] { output.Template, output.Skeleton }.OfType<string>())
                {
                    Try(name, true, () => renderer.Render(name, package.TemplateText(name), outputModel, scope));
                }
            }
        }

        Conditions(package.Warnings, "warnings");
        Conditions(package.Errors, "errors");
        return issues;

        // Prüfung: jede Zeile der Vorlage ist ein regulärer Ausdruck, die Meldung kennt zusätzlich "arg"
        void SampleCheck(OutputDef output, ScriptObject outputModel, ScriptObject? scope)
        {
            var name = output.Template!;
            string? rendered = null;
            Try(name, true, () => rendered = renderer.Render(name, package.TemplateText(name), outputModel, scope));
            foreach (var (pattern, _) in rendered is null ? Array.Empty<(string, string)>() : CheckLines.Parse(rendered))
            {
                try
                {
                    _ = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                }
                catch (ArgumentException ex)
                {
                    var where = $"outputs[{output.Id}].template";
                    if (seen.Add((where, null, ex.Message)))
                    {
                        issues.Add(new PackageCheckIssue(where, null, GeneratorsTexts.MatchInvalid(output.Id, ex.Message), null));
                    }
                }
            }

            if (output.Message is { } message)
            {
                var messageScope = new ScriptObject { ["arg"] = "sample" };
                if (scope?["item"] is { } item)
                {
                    messageScope["item"] = item;
                }

                Try($"outputs[{output.Id}].message", false, () => renderer.RenderInline(message, outputModel, messageScope));
            }
        }

        void Conditions(IReadOnlyList<WarningDef> conditions, string section)
        {
            for (var i = 0; i < conditions.Count; i++)
            {
                var when = conditions[i].When;
                Try($"{section}[{i + 1}]", false, () => renderer.Evaluate(when, model));
            }
        }
    }

    /// <summary>Kurze Vorlagen einer Ausgabe (Pfad, Block, Markierungen, Zeile, Schlüsselmuster, Quelle, Alt-Muster) mit ihrem Schlüssel.</summary>
    private static IEnumerable<(string Key, string Text)> Inline(OutputDef output)
    {
        var texts = new (string Key, string? Text)[]
        {
            ("path", output.Path), ("block", output.Block), ("legacyBegin", output.LegacyBegin), ("legacyEnd", output.LegacyEnd),
            ("line", output.Line), ("keyPattern", output.KeyPattern), ("source", output.Source), ("match", output.Match), ("keys", output.Keys), ("insertAfter", output.InsertAfter), ("ifLimit", output.IfLimit),
            ("jsonPath", output.JsonPath), ("entryKey", output.EntryKey),
        };
        foreach (var (key, text) in texts.Where(t => !string.IsNullOrEmpty(t.Text)))
        {
            yield return (key, text!);
        }

        var patterns = output.LegacyPatterns ?? Array.Empty<string>();
        for (var i = 0; i < patterns.Count; i++)
        {
            yield return ($"legacyPatterns[{i + 1}]", patterns[i]);
        }
    }

    /// <summary>Die Elemente einer ForEach-Ebene (wie der Planer) oder ein Lauf ohne Element.</summary>
    private static IEnumerable<ScriptObject?> Items(GeneratorPackage package, OutputDef output, ScriptObject model)
    {
        if (output.ForEach is null)
        {
            return new ScriptObject?[] { null };
        }

        IEnumerable<ScriptObject> current = new[] { model };
        for (var level = package.Root; level is not null; level = level.Child)
        {
            current = current.SelectMany(o => o[level.Id] is ScriptArray array ? array.OfType<ScriptObject>() : Enumerable.Empty<ScriptObject>()).ToList();
            if (level.Id == output.ForEach)
            {
                return current;
            }
        }

        // Unbekannte Ebene meldet schon der Loader; hier nichts zu rendern.
        return Array.Empty<ScriptObject?>();
    }

    /// <summary>Ein Satz mit Vorgabe- oder Beispielwerten je Feld; zählbare Ebenen mit mindestens einem Element.</summary>
    private static GeneratorState SampleState(GeneratorPackage package)
    {
        var state = GeneratorState.CreateEmpty(package, new[] { SampleLanguage });
        state.Key = "TST";
        Fill(state.Head, package.Head, package.Root, package);
        return state;
    }

    private static void Fill(FormNode node, IReadOnlyList<FieldDef> fields, LevelDef? child, GeneratorPackage package)
    {
        foreach (var field in fields.Where(f => !node.Values.ContainsKey(f.Id)))
        {
            node.Values[field.Id] = SampleValue(field);
        }

        if (child is null)
        {
            return;
        }

        if (!node.Children.TryGetValue(child.Id, out var elements))
        {
            elements = new List<FormNode>();
            node.Children[child.Id] = elements;
        }

        if (elements.Count == 0 && child.ConstantsTable is null)
        {
            elements.Add(FormNode.Create(child.Fields, child.Child, package.Constants));
        }

        foreach (var element in elements)
        {
            Fill(element, child.Fields, child.Child, package);
        }
    }

    private static string SampleValue(FieldDef field) => field.Type switch
    {
        FieldType.Number => "1",
        FieldType.Bool => "no",
        FieldType.Choice => field.Choices is [var first, ..] ? first : "x",
        FieldType.Image or FieldType.Modifiers => string.Empty,
        _ => "x",
    };
}
