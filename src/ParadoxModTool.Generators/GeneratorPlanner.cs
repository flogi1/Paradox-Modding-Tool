using System.Text.RegularExpressions;
using ParadoxModTool.Patches;
using Scriban.Runtime;

namespace ParadoxModTool.Generators;

/// <summary>
/// Macht aus Paket, Arbeitsstand und Ziel einen Plan (Spec Abschnitt 3): alle Ausgaben werden im Speicher gerendert und angewendet, geschrieben wird
/// nichts. Mehrere Ausgaben auf dieselbe Datei wirken in Paket-Reihenfolge auf denselben Text. Beim Ziel "Ausgabeordner" ist die Grundlage einer
/// Datei die Kopie dort, sonst die Datei der Mod.
/// </summary>
public static class GeneratorPlanner
{
    private sealed class Work
    {
        public required string RelativePath { get; init; }

        public string? OriginalText { get; set; }

        /// <summary>Grundlage aus der Mod, wenn im Ausgabeordner noch keine Kopie liegt.</summary>
        public string? ModBaseText { get; set; }

        public byte[]? OriginalBytes { get; set; }

        public string? Text { get; set; }

        public byte[]? Bytes { get; set; }

        public bool HasBom { get; set; } = true;

        public string EncodingName { get; set; } = TextFile.Utf8;

        public string NewLine { get; set; } = "\r\n";

        public List<string> Notes { get; } = new();

        public List<string> Removed { get; } = new();

        public int LegacyRemoved { get; set; }

        /// <summary>Voller Pfad einer externen Datei (nur "jsonEntry"); <see cref="RelativePath"/> ist dann nur der Anzeigename <c>extern/&lt;Name&gt;</c>.</summary>
        public string? ExternalPath { get; init; }
    }

    public static GeneratorPlan Plan(GeneratorPackage package, GeneratorState state, GeneratorTarget target)
    {
        var renderer = new GeneratorRenderer(package);
        var model = TemplateModel.Build(package, state, null);
        var writeRoot = target.OutputFolder ?? target.ModRoot;
        var files = new Dictionary<string, Work>(StringComparer.OrdinalIgnoreCase);
        var order = new List<Work>();
        var warnings = new List<string>();
        var checks = new List<(OutputDef Output, ScriptObject? Scope, string Path)>();

        Work Get(string rawPath, OutputDef output, bool binary)
        {
            // Pfadschutz: fremde Vorlagen (Import) dürfen nicht außerhalb von Mod oder Ausgabeordner schreiben.
            if (!SafeRelativePath.IsSafe(rawPath.Trim()))
            {
                throw new GeneratorOutputException(GeneratorsTexts.UnsafePath(output.Id, rawPath.Trim()));
            }

            var path = rawPath.Trim().Replace('\\', '/').TrimStart('/');
            if (!files.TryGetValue(path, out var work))
            {
                work = Open(path, output, binary, writeRoot, target);
                files[path] = work;
                order.Add(work);
            }
            else if (work.ExternalPath is not null)
            {
                // Nur "jsonEntry" darf außerhalb von Mod und Ausgabeordner schreiben
                throw new GeneratorOutputException(GeneratorsTexts.UnsafePath(output.Id, path));
            }

            return work;
        }

        // Externe Datei (außerhalb von Mod und Ausgabeordner): mehrere Einträge in dieselbe Datei wirken auf denselben Text
        Work GetExternal(string relativePath, string fullPath)
        {
            if (files.TryGetValue(relativePath, out var existing))
            {
                return string.Equals(existing.ExternalPath, fullPath, StringComparison.OrdinalIgnoreCase)
                    ? existing
                    : throw new GeneratorOutputException(GeneratorsTexts.ExternalNameClash(Path.GetFileName(fullPath)));
            }

            var content = TextFile.Read(fullPath);
            var work = new Work
            {
                RelativePath = relativePath,
                ExternalPath = fullPath,
                OriginalText = content.Text,
                Text = content.Text,
                // JSON ist UTF-8 ohne BOM (auch der Tag-Editor liest es so); gelesen wird wie beim Prüfen auf geänderte Dateien
                HasBom = false,
                EncodingName = TextFile.Utf8,
                NewLine = content.NewLine,
            };
            files[relativePath] = work;
            order.Add(work);
            return work;
        }

        foreach (var output in package.Outputs)
        {
            if (output.Kind == OutputKind.Localization)
            {
                foreach (var language in LanguageFill.LanguagesToWrite(state.Languages, target.AllLanguages ?? state.Languages, state.LanguageRules))
                {
                    var languageModel = TemplateModel.Build(package, state, language);
                    var work = Get(renderer.RenderInline(output.Path, languageModel), output, binary: false);
                    var content = TextNormalizer.Normalize(renderer.Render(output.Template!, package.TemplateText(output.Template!), languageModel), work.NewLine);
                    if (output.Keys is { } keys)
                    {
                        // Gemeinsame Datei: nur die Zeilen mit passenden Schlüsseln ersetzen, der Rest bleibt
                        Regex pattern;
                        try
                        {
                            pattern = new Regex(renderer.RenderInline(keys, languageModel), RegexOptions.CultureInvariant);
                        }
                        catch (ArgumentException ex)
                        {
                            throw new GeneratorOutputException(GeneratorsTexts.MatchInvalid(output.Id, ex.Message));
                        }

                        work.Text = LocalizationMerger.Apply(work.Text, language, pattern, content, work.NewLine, InsertAfterPattern(renderer, output, languageModel, null));
                        continue;
                    }

                    work.Text = WithHeader(content, language, work.NewLine);
                }

                continue;
            }

            foreach (var item in Items(package, output, model))
            {
                var scope = item is null ? null : new ScriptObject { ["item"] = item };
                var path = renderer.RenderInline(output.Path, model, scope);
                var errorFile = path;
                try
                {
                    switch (output.Kind)
                    {
                        case OutputKind.OwnFile:
                        {
                            var work = Get(path, output, binary: false);
                            work.Text = TextNormalizer.Normalize(renderer.Render(output.Template!, package.TemplateText(output.Template!), model, scope), work.NewLine);
                            break;
                        }

                        case OutputKind.InsertSection:
                        {
                            var work = Get(path, output, binary: false);
                            var marker = $"# PMT-Generator {package.MarkerId}/{output.Id}/{state.Key}" + (item is null ? string.Empty : "/" + ElementKey(item));
                            var content = TextNormalizer.Normalize(renderer.Render(output.Template!, package.TemplateText(output.Template!), model, scope), "\n");
                            var skeleton = output.Skeleton is { } s ? renderer.Render(s, package.TemplateText(s), model, scope) : null;
                            var patterns = output.LegacyPatterns?.Select(p => new Regex(renderer.RenderInline(p, model, scope), RegexOptions.CultureInvariant)).ToList();
                            var section = SectionInserter.Apply(
                                work.Text,
                                Inline(renderer, output.Block, model, scope),
                                marker + " begin",
                                marker + " end",
                                Inline(renderer, output.LegacyBegin, model, scope),
                                Inline(renderer, output.LegacyEnd, model, scope),
                                content,
                                skeleton,
                                work.NewLine,
                                patterns);
                            work.Text = section.Text;
                            work.Notes.AddRange(section.Notes);
                            work.LegacyRemoved += section.LegacyRemoved;
                            break;
                        }

                        case OutputKind.EnsureLine:
                        {
                            var work = Get(path, output, binary: false);
                            var existing = work.Text
                                ?? (output.Skeleton is { } s ? TextNormalizer.Normalize(renderer.Render(s, package.TemplateText(s), model, scope), work.NewLine) : null)
                                ?? throw new GeneratorOutputException(GeneratorsTexts.FileMissingNoSkeleton);
                            work.Text = LineEnsurer.Apply(existing, Inline(renderer, output.Block, model, scope)!, Inline(renderer, output.Line, model, scope)!, work.NewLine).Text;
                            break;
                        }

                        case OutputKind.SyncBlocks:
                        {
                            var work = Get(path, output, binary: false);
                            var rendered = TextNormalizer.Normalize(renderer.Render(output.Template!, package.TemplateText(output.Template!), model, scope), work.NewLine);
                            var result = BlockSyncer.Apply(work.Text, Inline(renderer, output.KeyPattern, model, scope)!, rendered, work.NewLine, output.Keep);
                            work.Text = result.Text;
                            work.Removed.AddRange(result.Removed);
                            if (result.Removed.Count > 0)
                            {
                                warnings.Add(GeneratorsTexts.BlocksRemoved(work.RelativePath, string.Join(", ", result.Removed)));
                            }

                            break;
                        }

                        case OutputKind.SyncMatching:
                        {
                            var work = Get(path, output, binary: false);
                            Regex match;
                            try
                            {
                                match = new Regex(Inline(renderer, output.Match, model, scope)!, RegexOptions.CultureInvariant);
                            }
                            catch (ArgumentException ex)
                            {
                                throw new GeneratorOutputException(GeneratorsTexts.MatchInvalid(output.Id, ex.Message));
                            }

                            var existing = work.Text
                                ?? (output.Skeleton is { } s ? TextNormalizer.Normalize(renderer.Render(s, package.TemplateText(s), model, scope), work.NewLine) : string.Empty);
                            var rendered = TextNormalizer.Normalize(renderer.Render(output.Template!, package.TemplateText(output.Template!), model, scope), work.NewLine);
                            var result = MatchSyncer.Apply(existing, Inline(renderer, output.Block, model, scope), match, rendered, work.NewLine, InsertAfterPattern(renderer, output, model, scope),
                                Inline(renderer, output.IfLimit, model, scope));
                            work.Text = result.Text;
                            work.Removed.AddRange(result.Removed);
                            if (result.Removed.Count > 0)
                            {
                                warnings.Add(GeneratorsTexts.BlocksRemoved(work.RelativePath, string.Join(", ", result.Removed)));
                            }

                            break;
                        }

                        case OutputKind.Check:
                            // Prüfungen erst nach allen Ausgaben: sie sehen den fertig geplanten Text
                            checks.Add((output, scope, path));
                            break;

                        case OutputKind.JsonEntry:
                        {
                            var external = package.Externals[output.External!];
                            if (target.ExternalPaths is not { } chosen || !chosen.TryGetValue(output.External!, out var externalPath)
                                || string.IsNullOrWhiteSpace(externalPath) || !File.Exists(externalPath))
                            {
                                var notChosen = GeneratorsTexts.ExternalNotChosen(external.Label);
                                if (!warnings.Contains(notChosen))
                                {
                                    warnings.Add(notChosen);
                                }

                                break;
                            }

                            errorFile = "extern/" + Path.GetFileName(externalPath);
                            var valueJson = renderer.Render(output.Template!, package.TemplateText(output.Template!), model, scope).Trim();
                            if (valueJson.Length == 0)
                            {
                                // Leerer Wert = kein Eintrag: die Datei wird nicht geplant, ein vorhandener Eintrag bleibt unberührt
                                break;
                            }

                            var work = GetExternal(errorFile, externalPath);
                            work.Text = JsonEntryWriter.Set(work.Text!, Inline(renderer, output.JsonPath, model, scope)?.Trim() ?? string.Empty, Inline(renderer, output.EntryKey, model, scope)!.Trim(), valueJson);
                            break;
                        }

                        case OutputKind.CopyBinary:
                        {
                            var source = Inline(renderer, output.Source, model, scope)!.Trim();
                            var colon = source.IndexOf(':');
                            var assetId = colon < 0 ? source : source[..colon];
                            var entry = colon < 0 ? string.Empty : source[(colon + 1)..].Trim();
                            if (entry.Length == 0)
                            {
                                break;
                            }

                            if (!package.Assets.ContainsKey(assetId))
                            {
                                throw new GeneratorOutputException(GeneratorsTexts.UnknownAsset(assetId));
                            }

                            var library = package.AssetPath(assetId);
                            if (!File.Exists(library) && !Directory.Exists(library))
                            {
                                // Bibliothek noch nicht bereitgestellt (z. B. Wappen-Zip): nur diese Kopie entfällt.
                                warnings.Add(GeneratorsTexts.AssetLibraryNotProvided(assetId));
                                break;
                            }

                            Get(path, output, binary: true).Bytes = AssetLibrary.Open(library).Read(entry);
                            break;
                        }
                    }
                }
                catch (GeneratorOutputException ex)
                {
                    // Fehler einer Ausgabe nennen die Datei, sonst weiß niemand, welche fehlt oder kaputt ist.
                    throw new GeneratorOutputException(GeneratorsTexts.InFile(errorFile.Trim(), ex.Message));
                }
            }
        }

        foreach (var (output, scope, rawPath) in checks)
        {
            try
            {
                RunCheck(output, scope, rawPath);
            }
            catch (GeneratorOutputException ex)
            {
                throw new GeneratorOutputException(GeneratorsTexts.InFile(rawPath.Trim(), ex.Message));
            }
        }

        void RunCheck(OutputDef output, ScriptObject? scope, string rawPath)
        {
            if (!SafeRelativePath.IsSafe(rawPath.Trim()))
            {
                throw new GeneratorOutputException(GeneratorsTexts.UnsafePath(output.Id, rawPath.Trim()));
            }

            // Geplanter Text dieser Datei, sonst Ausgabeordner, sonst Mod; ohne Datei gibt es nichts zu prüfen
            var path = rawPath.Trim().Replace('\\', '/').TrimStart('/');
            var text = files.TryGetValue(path, out var planned) ? planned.Text : Open(path, output, binary: false, writeRoot, target).Text;
            if (text is null)
            {
                return;
            }

            foreach (var (pattern, argument) in CheckLines.Parse(renderer.Render(output.Template!, package.TemplateText(output.Template!), model, scope)))
            {
                Regex regex;
                try
                {
                    regex = new Regex(pattern, RegexOptions.CultureInvariant);
                }
                catch (ArgumentException ex)
                {
                    throw new GeneratorOutputException(GeneratorsTexts.MatchInvalid(output.Id, ex.Message));
                }

                if (!regex.IsMatch(text))
                {
                    var messageScope = new ScriptObject { ["arg"] = argument };
                    if (scope?["item"] is { } item)
                    {
                        messageScope["item"] = item;
                    }

                    warnings.Add(renderer.RenderInline(output.Message!, model, messageScope).Trim());
                }
            }
        }

        return new GeneratorPlan
        {
            PackageId = package.Id,
            Key = state.Key,
            WriteRoot = writeRoot,
            Files = order.Select(ToPlannedFile).ToList(),
            Warnings = warnings,
        };
    }

    /// <summary>Die Elemente einer ForEach-Ebene (über alle Eltern hinweg) oder ein einzelner Lauf ohne Element.</summary>
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

        throw new GeneratorOutputException(GeneratorsTexts.UnknownValue("forEach", output.ForEach));
    }

    private static string ElementKey(ScriptObject item) =>
        Convert.ToString(item.ContainsKey("key") ? item["key"] : item["index"], System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

    private static string? Inline(GeneratorRenderer renderer, string? text, ScriptObject model, ScriptObject? scope) =>
        text is null ? null : renderer.RenderInline(text, model, scope);

    /// <summary>Das Muster <c>insertAfter</c> der Ausgabe, mit dem Arbeitsstand gerendert; ein ungültiger Regex ist ein Ausgabefehler. Ohne Angabe <see langword="null"/>.</summary>
    private static Regex? InsertAfterPattern(GeneratorRenderer renderer, OutputDef output, ScriptObject model, ScriptObject? scope)
    {
        if (output.InsertAfter is null)
        {
            return null;
        }

        try
        {
            return new Regex(renderer.RenderInline(output.InsertAfter, model, scope), RegexOptions.CultureInvariant);
        }
        catch (ArgumentException ex)
        {
            throw new GeneratorOutputException(GeneratorsTexts.MatchInvalid(output.Id, ex.Message));
        }
    }

    /// <summary>Lokalisierung bekommt die Kopfzeile <c>l_&lt;sprache&gt;:</c>, wenn die Vorlage sie nicht selbst schreibt.</summary>
    private static string WithHeader(string content, string language, string newLine)
    {
        var header = "l_" + language + ":";
        var firstLine = content.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        return firstLine == header ? content : header + newLine + content;
    }

    /// <summary>Stand einer Datei am Schreibort; beim Ausgabeordner ohne Kopie dort ist die Datei der Mod die Grundlage.</summary>
    private static Work Open(string relativePath, OutputDef output, bool binary, string writeRoot, GeneratorTarget target)
    {
        var work = new Work { RelativePath = relativePath, NewLine = DefaultNewLine(relativePath, output) };
        var atTarget = Full(writeRoot, relativePath);
        if (binary)
        {
            work.OriginalBytes = File.Exists(atTarget) ? File.ReadAllBytes(atTarget) : null;
            return work;
        }

        var source = File.Exists(atTarget) ? atTarget : target.OutputFolder is not null && File.Exists(Full(target.ModRoot, relativePath)) ? Full(target.ModRoot, relativePath) : null;
        if (source is not null)
        {
            var content = TextFile.Read(source);
            work.Text = content.Text;
            work.HasBom = content.HasBom;
            work.EncodingName = content.EncodingName;
            work.NewLine = content.NewLine;
            work.OriginalText = source == atTarget ? content.Text : null;
            work.ModBaseText = source == atTarget ? null : content.Text;
        }
        else
        {
            // Neue Datei: BOM nach dem Spielprofil (Stellaris liest Skriptdateien mit BOM falsch); Lokalisierung braucht immer eins
            var extension = Path.GetExtension(relativePath).ToLowerInvariant();
            work.HasBom = output.Kind == OutputKind.Localization || extension == ".yml" || (target.NewFileHasBom?.Invoke(extension) ?? true);
        }

        return work;
    }

    /// <summary>Zeilenende neuer Dateien: aus der Ausgabe (<c>crlf</c>/<c>lf</c>), sonst LF für .yml und CRLF für alles andere (wie der IV2-Generator).</summary>
    private static string DefaultNewLine(string relativePath, OutputDef output) => output.NewLine?.ToLowerInvariant() switch
    {
        "lf" => "\n",
        "crlf" => "\r\n",
        _ => relativePath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ? "\n" : "\r\n",
    };

    private static GeneratorPlannedFile ToPlannedFile(Work work)
    {
        PlannedFileStatus status;
        if (work.Bytes is { } bytes)
        {
            status = work.OriginalBytes is null ? PlannedFileStatus.New : work.OriginalBytes.AsSpan().SequenceEqual(bytes) ? PlannedFileStatus.Unchanged : PlannedFileStatus.Changed;
        }
        else
        {
            status = work.OriginalText is null ? PlannedFileStatus.New : work.OriginalText == work.Text ? PlannedFileStatus.Unchanged : PlannedFileStatus.Changed;
        }

        return new GeneratorPlannedFile(work.RelativePath, status, work.OriginalText, work.Bytes is null ? work.Text ?? string.Empty : null, work.Bytes, work.OriginalBytes,
            work.HasBom, work.EncodingName, work.Notes, work.Removed, work.LegacyRemoved, work.ModBaseText, work.ExternalPath);
    }

    private static string Full(string root, string relativePath) => Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
