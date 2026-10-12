using System.Text.Json;
using ParadoxModTool.Generators.Lookups;
using ParadoxModTool.Localization;

namespace ParadoxModTool.Generators;

/// <summary>Liest ein Paket (<c>package.json</c>, <c>functions.sbn</c>, <c>templates/</c>) und prüft es; Fehler nennen die Stelle.</summary>
public static class GeneratorPackageLoader
{
    private static readonly string[] KnownTargets = { "mod", "output" };

    /// <summary>
    /// Alle Schlüssel, die der Loader irgendwo in package.json liest (oben, Felder, Ebenen, Ausgaben, read, lookups, lookup, Assets, Meldungen,
    /// Sprachregeln). Die KI-Anleitung (ai-guide/generator-packages/reference.md) muss jeden davon nennen; ein Test prüft beides.
    /// </summary>
    public static IReadOnlyList<string> KnownKeys { get; } = new[]
    {
        "id", "name", "gameId", "keyField", "keyFields", "keySeparator", "markerId", "head", "root", "constants", "targets", "languages", "assets", "outputs", "warnings", "errors",
        "read", "lookups",
        "label", "type", "required", "pattern", "choices", "multilingual", "default", "asset", "lookup",
        "constantsTable", "min", "max", "initial", "numberBaseField", "fields", "child",
        "extendEnglish", "copyEnglish", "englishForMissing",
        "path", "origin",
        "when", "message",
        "kind", "template", "forEach", "block", "skeleton", "legacyBegin", "legacyEnd", "legacyPatterns", "line", "keyPattern", "keep",
        "checkUnreferenced", "source", "newLine", "match", "insertAfter", "ifLimit", "autoNumber",
        "keys", "elements", "file", "level", "body", "except", "localization", "key", "field", "flags", "contains", "localizationOutput", "values", "pattern", "join", "output",
        "json", "property",
        "folder", "depth", "namedOnly", "tagFromFile", "tagInBlock", "items",
        "sources", "fills",
        "externals", "hint", "external", "jsonPath", "entryKey", "afterGenerate",
        "candidates", "category", "used", "groups", "hidden", "create", "ask", "modifiers",
    };

    /// <summary>Ordner der Pakete eines Spiels im PMT-Datenordner.</summary>
    public static string PackagesRoot(string gameDirectory) => Path.Combine(gameDirectory, "generator-packages");

    /// <summary>Ordner der mitgelieferten Pakete neben der exe (aus <c>generator-packages/</c> im Repo).</summary>
    public static string ShippedRoot => Path.Combine(AppContext.BaseDirectory, "generator-packages");

    /// <summary>
    /// Mitgelieferte Pakete (Arbeitsstände und Assets im Datenordner unter gleichem Ordnernamen), dann eigene Pakete des Datenordners. Ein eigenes
    /// Paket mit der Kennung eines mitgelieferten wird nicht gelistet (Hinweis im Trace); kaputte Pakete kommen mit ihrem Fehler.
    /// </summary>
    /// <param name="gameId">Nur mitgelieferte Pakete dieses Spiels (und solche ohne Spielangabe); null = alle. Eigene Pakete liegen schon im Datenordner des Spiels.</param>
    public static IReadOnlyList<(string Folder, GeneratorPackage? Package, string? Error, bool Shipped)> ListAll(string shippedRoot, string packagesRoot, string? gameId = null)
    {
        var result = new List<(string Folder, GeneratorPackage? Package, string? Error, bool Shipped)>();
        if (Directory.Exists(shippedRoot))
        {
            foreach (var folder in Directory.EnumerateDirectories(shippedRoot).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var data = Path.Combine(packagesRoot, Path.GetFileName(folder));
                result.Add(TryLoad(folder, () => Load(folder, data, shipped: true), shipped: true));
            }
        }

        var shippedIds = result.Select(e => e.Package?.Id).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var shippedFolders = result.Select(e => Path.GetFileName(e.Folder)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (folder, package, error) in List(packagesRoot))
        {
            if (package is not null && shippedIds.Contains(package.Id))
            {
                System.Diagnostics.Trace.WriteLine($"Generator-Paket {folder}: Kennung {package.Id} ist mitgeliefert, das mitgelieferte gilt.");
                continue;
            }

            if (package is null && shippedFolders.Contains(Path.GetFileName(folder)))
            {
                // Datenordner eines mitgelieferten Pakets (nur Arbeitsstände und Assets), kein eigenes Paket.
                continue;
            }

            result.Add((folder, package, error, false));
        }

        // Erst hier filtern: Eine eigene Kopie mit der Kennung eines mitgelieferten Pakets bleibt auch dann verdeckt, wenn das mitgelieferte
        // zu einem anderen Spiel gehört.
        if (!string.IsNullOrEmpty(gameId))
        {
            result.RemoveAll(e => e.Shipped && e.Package is { GameId: { Length: > 0 } packageGame } && !string.Equals(packageGame, gameId, StringComparison.OrdinalIgnoreCase));
        }

        return result;
    }

    private static (string Folder, GeneratorPackage? Package, string? Error, bool Shipped) TryLoad(string folder, Func<GeneratorPackage> load, bool shipped)
    {
        try
        {
            return (folder, load(), null, shipped);
        }
        catch (Exception ex) when (ex is GeneratorPackageException or IOException or UnauthorizedAccessException)
        {
            return (folder, null, ex.Message, shipped);
        }
    }

    /// <summary>Alle Paketordner unter <paramref name="packagesRoot"/>; kaputte Pakete kommen mit ihrem Fehler statt einer Ausnahme.</summary>
    public static IReadOnlyList<(string Folder, GeneratorPackage? Package, string? Error)> List(string packagesRoot)
    {
        if (!Directory.Exists(packagesRoot))
        {
            return Array.Empty<(string, GeneratorPackage?, string?)>();
        }

        var result = new List<(string Folder, GeneratorPackage? Package, string? Error)>();
        foreach (var folder in Directory.EnumerateDirectories(packagesRoot).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                result.Add((folder, Load(folder), null));
            }
            catch (Exception ex) when (ex is GeneratorPackageException or IOException or UnauthorizedAccessException)
            {
                result.Add((folder, null, ex.Message));
            }
        }

        return result;
    }

    /// <summary>
    /// Liest ein Paket. <paramref name="dataFolder"/>: Ordner für Arbeitsstände und Assets (ohne Angabe der Paketordner); <paramref name="uiLanguage"/>:
    /// Sprache für Paket-Texte (ohne Angabe die aktuelle UI-Sprache).
    /// </summary>
    public static GeneratorPackage Load(string folder, string? dataFolder = null, bool shipped = false, string? uiLanguage = null)
    {
        var language = uiLanguage ?? UiText.Current.Language;
        var path = Path.Combine(folder, "package.json");
        if (!File.Exists(path))
        {
            throw new GeneratorPackageException(GeneratorsTexts.PackageJsonMissing(folder));
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException ex)
        {
            throw new GeneratorPackageException(GeneratorsTexts.PackageJsonInvalid(ex.Message));
        }

        using (document)
        {
            var root = document.RootElement;
            var head = Items(root, "head").Select(f => Field(f, language)).ToList();
            var keyFields = KeyFields(root, head, out var keySeparator);
            var keyField = keyFields[0];

            var targets = root.TryGetProperty("targets", out var t) ? t.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList() : KnownTargets.ToList();
            foreach (var target in targets.Where(x => !KnownTargets.Contains(x)))
            {
                throw new GeneratorPackageException(GeneratorsTexts.UnknownTarget(target));
            }

            var functionsPath = Path.Combine(folder, "functions.sbn");
            var assetDefs = AssetDefs(root);
            var package = new GeneratorPackage(
                Required(root, "id"),
                RequiredText(root, "name", language),
                Required(root, "gameId"),
                folder,
                head,
                keyField,
                root.TryGetProperty("root", out var level) ? Level(level, language) : null,
                Constants(root, language),
                File.Exists(functionsPath) ? File.ReadAllText(functionsPath) : string.Empty,
                Items(root, "outputs").Select(o => Output(o, language)).ToList(),
                targets,
                Languages(root),
                Items(root, "warnings").Select(w => new WarningDef(Required(w, "when"), RequiredText(w, "message", language))).ToList(),
                assetDefs.ToDictionary(p => p.Key, p => p.Value.Path),
                Items(root, "errors").Select(w => new WarningDef(Required(w, "when"), RequiredText(w, "message", language))).ToList(),
                dataFolder ?? folder,
                shipped,
                root.TryGetProperty("read", out var read) ? Read(read) : null,
                root.TryGetProperty("lookups", out var lookups) ? Items(root, "lookups").Select(l => LookupSource(l, language)).ToList() : null)
            {
                AssetDefs = assetDefs,
                MarkerId = MarkerId(root) ?? Required(root, "id"),
                KeyFields = keyFields,
                KeySeparator = keySeparator,
                Externals = Externals(root, language),
                AfterGenerate = Text(root, "afterGenerate", language) is { Length: > 0 } after ? after : null,
                Candidates = root.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Object ? Candidates(candidates, language) : null,
            };
            CheckTemplates(package);
            CheckLookups(package);
            CheckAutoNumber(package);
            CheckCandidates(package);
            if (package.Read is { LocalizationOutput: { } localizationOutput } readDef && readDef.LocalizationFrom(package) is null)
            {
                throw new GeneratorPackageException(GeneratorsTexts.UnknownValue("read.localizationOutput", localizationOutput));
            }

            if (package.Read?.Json?.FirstOrDefault(j => !package.Externals.ContainsKey(j.External)) is { } badJson)
            {
                throw new GeneratorPackageException(GeneratorsTexts.UnknownValue("read.json.external", badJson.External));
            }

            if (package.Read?.Localization.FirstOrDefault(l => l.Output is not null && package.Read.LocalizationFor(package, l) is null) is { } badEntry)
            {
                throw new GeneratorPackageException(GeneratorsTexts.UnknownValue("read.localization.output", badEntry.Output!));
            }

            return package;
        }
    }

    /// <summary>
    /// <c>keyFields</c> (Liste) oder <c>keyField</c>: jedes Feld muss im Kopf stehen; außer dem letzten braucht jedes ein Muster oder eine Auswahl, und
    /// kein Auswahlwert darf das Trennzeichen enthalten, sonst ließe sich der Schlüssel nicht eindeutig zerlegen.
    /// </summary>
    private static List<string> KeyFields(JsonElement root, IReadOnlyList<FieldDef> head, out string separator)
    {
        var sep = Optional(root, "keySeparator") is { Length: > 0 } s ? s : "_";
        separator = sep;
        var fields = Strings(root, "keyFields")?.Where(f => f.Length > 0).ToList() is { Count: > 0 } list ? list : new List<string> { Required(root, "keyField") };
        for (var i = 0; i < fields.Count; i++)
        {
            var field = head.FirstOrDefault(f => f.Id == fields[i]) ?? throw new GeneratorPackageException(GeneratorsTexts.KeyFieldNotInHead(fields[i]));
            if (i == fields.Count - 1)
            {
                continue;
            }

            if (field.Type != FieldType.Choice && string.IsNullOrEmpty(field.Pattern))
            {
                throw new GeneratorPackageException(GeneratorsTexts.KeyFieldNeedsPattern(field.Id));
            }

            if (field.Choices?.FirstOrDefault(c => c.Contains(sep, StringComparison.Ordinal)) is { } bad)
            {
                throw new GeneratorPackageException(GeneratorsTexts.KeyChoiceHasSeparator(field.Id, bad, sep));
            }
        }

        return fields;
    }

    private static void CheckTemplates(GeneratorPackage package)
    {
        foreach (var output in package.Outputs)
        {
            var needsTemplate = output.Kind is OutputKind.OwnFile or OutputKind.Localization or OutputKind.InsertSection or OutputKind.SyncBlocks or OutputKind.SyncMatching
                or OutputKind.Check or OutputKind.JsonEntry;
            if (output.Kind is OutputKind.Check or OutputKind.JsonEntry)
            {
                // Eine Prüfung schreibt nichts, ein JSON-Eintrag hat keinen Blockpfad: Blockpfad, Treffer, Schlüssel und Gerüst hätten keine Wirkung
                foreach (var (key, value) in new[] { ("block", output.Block), ("match", output.Match), ("keys", output.Keys), ("skeleton", output.Skeleton) })
                {
                    if (value is not null)
                    {
                        throw new GeneratorPackageException(GeneratorsTexts.KeyNotAllowed($"outputs[{output.Id}].{key}"));
                    }
                }

                if (output.Kind == OutputKind.Check && string.IsNullOrWhiteSpace(output.Message))
                {
                    throw new GeneratorPackageException(GeneratorsTexts.FieldMissing($"outputs[{output.Id}].message"));
                }
            }

            // external, jsonPath und entryKey gehören nur zu "jsonEntry"
            if (output.Kind != OutputKind.JsonEntry)
            {
                foreach (var (key, value) in new[] { ("external", output.External), ("jsonPath", output.JsonPath), ("entryKey", output.EntryKey) })
                {
                    if (value is not null)
                    {
                        throw new GeneratorPackageException(GeneratorsTexts.KeyNotAllowed($"outputs[{output.Id}].{key}"));
                    }
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(output.External))
                {
                    throw new GeneratorPackageException(GeneratorsTexts.FieldMissing($"outputs[{output.Id}].external"));
                }

                if (!package.Externals.ContainsKey(output.External))
                {
                    throw new GeneratorPackageException(GeneratorsTexts.UnknownValue($"outputs[{output.Id}].external", output.External));
                }

                if (string.IsNullOrWhiteSpace(output.EntryKey))
                {
                    throw new GeneratorPackageException(GeneratorsTexts.FieldMissing($"outputs[{output.Id}].entryKey"));
                }
            }

            // insertAfter ordnet nur neue Einheiten ein: bei "Abgleich nach Treffer" und bei Lokalisierung in gemeinsamen Dateien
            if (output.InsertAfter is not null && output.Kind != OutputKind.SyncMatching && !(output.Kind == OutputKind.Localization && output.Keys is not null))
            {
                throw new GeneratorPackageException(GeneratorsTexts.KeyNotAllowed($"outputs[{output.Id}].insertAfter"));
            }

            // ifLimit gruppiert Einheiten in if/limit-Blöcken: nur bei "Abgleich nach Treffer"
            if (output.IfLimit is not null && output.Kind != OutputKind.SyncMatching)
            {
                throw new GeneratorPackageException(GeneratorsTexts.KeyNotAllowed($"outputs[{output.Id}].ifLimit"));
            }

            if (needsTemplate && string.IsNullOrEmpty(output.Template))
            {
                throw new GeneratorPackageException(GeneratorsTexts.FieldMissing($"outputs[{output.Id}].template"));
            }

            foreach (var name in new[] { output.Template, output.Skeleton }.OfType<string>())
            {
                // Nur Dateinamen im Ordner templates: ein importiertes Paket darf keine Dateien außerhalb lesen und in die Mod schreiben.
                if (!Editing.Package.PackageFiles.IsValidTemplateName(name))
                {
                    throw new GeneratorPackageException(GeneratorsTexts.TemplateNameInvalid(output.Id, name));
                }

                if (!File.Exists(Path.Combine(package.Folder, "templates", name)))
                {
                    throw new GeneratorPackageException(GeneratorsTexts.TemplateMissing(name));
                }
            }

            if (output.Kind == OutputKind.EnsureLine && (output.Line is null || output.Block is null))
            {
                throw new GeneratorPackageException(GeneratorsTexts.FieldMissing($"outputs[{output.Id}].line/block"));
            }

            if (output.Kind == OutputKind.CopyBinary && output.Source is null)
            {
                throw new GeneratorPackageException(GeneratorsTexts.FieldMissing($"outputs[{output.Id}].source"));
            }

            if (output.Kind == OutputKind.SyncBlocks && output.KeyPattern is null)
            {
                throw new GeneratorPackageException(GeneratorsTexts.FieldMissing($"outputs[{output.Id}].keyPattern"));
            }

            if (output.Kind == OutputKind.SyncMatching && string.IsNullOrWhiteSpace(output.Match))
            {
                throw new GeneratorPackageException(GeneratorsTexts.FieldMissing($"outputs[{output.Id}].match"));
            }
        }
    }

    private static FieldDef Field(JsonElement e, string language) => new(
        Required(e, "id"),
        Text(e, "label", language) ?? Required(e, "id"),
        Enum<FieldType>(e, "type", FieldType.Text),
        Bool(e, "required"),
        Optional(e, "pattern"),
        e.TryGetProperty("choices", out var choices) ? choices.EnumerateArray().Select(c => c.GetString() ?? string.Empty).ToList() : null,
        Bool(e, "multilingual"),
        Optional(e, "default"),
        Optional(e, "asset"),
        e.TryGetProperty("lookup", out var lookup) && lookup.ValueKind == JsonValueKind.Object ? FieldLookup(lookup) : null,
        Bool(e, "autoNumber"));

    private static FieldLookupDef FieldLookup(JsonElement e) => new(
        Strings(e, "sources") ?? Array.Empty<string>(),
        e.TryGetProperty("fills", out var fills) && fills.ValueKind == JsonValueKind.Object
            ? fills.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal));

    private static LookupSourceDef LookupSource(JsonElement e, string language)
    {
        var id = Required(e, "id");
        var kind = Enum<LookupKind>(e, "kind", null);
        string Need(string name) => Optional(e, name) is { Length: > 0 } value ? value : throw new GeneratorPackageException(GeneratorsTexts.FieldMissing($"lookups[{id}].{name}"));
        return new LookupSourceDef(
            id,
            kind,
            Text(e, "label", language) ?? id,
            kind == LookupKind.GameKeys ? Need("folder") : Optional(e, "folder"),
            Int(e, "depth", 1),
            Bool(e, "namedOnly"),
            Optional(e, "tagFromFile"),
            Strings(e, "tagInBlock"),
            kind == LookupKind.Json ? Need("asset") : Optional(e, "asset"),
            kind == LookupKind.Json ? Need("items") : Optional(e, "items"),
            e.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Object
                ? fields.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty, StringComparer.Ordinal)
                : null);
    }

    /// <summary><c>autoNumber</c> gibt es nur am letzten Schlüsselfeld, und das muss vom Typ Zahl sein.</summary>
    private static void CheckAutoNumber(GeneratorPackage package)
    {
        var fieldLists = new List<IReadOnlyList<FieldDef>> { package.Head };
        for (var level = package.Root; level is not null; level = level.Child)
        {
            fieldLists.Add(level.Fields);
        }

        foreach (var field in fieldLists.SelectMany(fields => fields).Where(f => f.AutoNumber))
        {
            if (field.Type != FieldType.Number || !package.Head.Contains(field) || field.Id != package.KeyFields[^1])
            {
                throw new GeneratorPackageException(GeneratorsTexts.AutoNumberNotAllowed(field.Id));
            }
        }
    }

    /// <summary>Abschnitt <c>candidates</c>; <c>groups</c> darf fehlen, <c>create</c> und seine Angaben nicht.</summary>
    private static CandidatesDef Candidates(JsonElement e, string language)
    {
        var create = e.TryGetProperty("create", out var c) && c.ValueKind == JsonValueKind.Object ? c : throw new GeneratorPackageException(GeneratorsTexts.FieldMissing("candidates.create"));
        return new CandidatesDef(
            Required(e, "category"),
            Items(e, "used").Select(u => new CandidateUsedDef(Required(u, "file"), Required(u, "block"), Strings(u, "except") ?? Array.Empty<string>())).ToList(),
            Items(e, "groups").Select(g => new CandidateGroupDef(Optional(g, "id") ?? string.Empty, Text(g, "label", language) ?? Optional(g, "id") ?? string.Empty, Required(g, "pattern"), Bool(g, "hidden"))).ToList(),
            new CandidateCreateDef(Strings(create, "ask") ?? Array.Empty<string>(), Required(create, "name"), Required(create, "modifiers")));
    }

    /// <summary>
    /// Regeln für <c>candidates</c>: das Paket hat ein <c>autoNumber</c>-Feld (neue Stände bekommen ihre Nummer selbst), <c>used</c> ist nicht leer, alle Regex
    /// sind gültig, Gruppen-Kennungen sind nicht leer und eindeutig, <c>ask</c> nennt alle übrigen Schlüsselfelder (Kopf-Felder vom Typ Auswahl), <c>name</c> ist
    /// ein Kopf-Textfeld und <c>modifiers</c> ein Modifikator-Feld der Wurzel-Ebene.
    /// </summary>
    private static void CheckCandidates(GeneratorPackage package)
    {
        if (package.Candidates is not { } candidates)
        {
            return;
        }

        var autoNumber = package.Head.FirstOrDefault(f => f.AutoNumber);
        if (autoNumber is null)
        {
            throw new GeneratorPackageException(GeneratorsTexts.CandidatesNeedAutoNumber);
        }

        if (candidates.Used.Count == 0)
        {
            throw new GeneratorPackageException(GeneratorsTexts.CandidatesUsedEmpty);
        }

        foreach (var (where, pattern) in candidates.Used.SelectMany((u, i) => new[] { ($"candidates.used[{i}].file", u.File), ($"candidates.used[{i}].block", u.Block) })
                     .Concat(candidates.Groups.Select((g, i) => ($"candidates.groups[{i}].pattern", g.Pattern))))
        {
            try
            {
                _ = new System.Text.RegularExpressions.Regex(pattern);
            }
            catch (ArgumentException ex)
            {
                throw new GeneratorPackageException(GeneratorsTexts.CandidatesPatternInvalid(where, pattern, ex.Message));
            }
        }

        var groupIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in candidates.Groups.Where(g => g.Id.Length == 0 || !groupIds.Add(g.Id)))
        {
            throw new GeneratorPackageException(GeneratorsTexts.CandidatesGroupIdInvalid(group.Id));
        }

        foreach (var ask in candidates.Create.Ask)
        {
            if (package.Head.FirstOrDefault(f => f.Id == ask) is not { Type: FieldType.Choice } field || field.AutoNumber)
            {
                throw new GeneratorPackageException(GeneratorsTexts.CandidatesAskInvalid(ask));
            }
        }

        foreach (var key in package.KeyFields.Where(k => k != autoNumber.Id && !candidates.Create.Ask.Contains(k)))
        {
            throw new GeneratorPackageException(GeneratorsTexts.CandidatesAskMissing(key));
        }

        if (package.Head.All(f => f.Id != candidates.Create.Name || f.Type != FieldType.Text))
        {
            throw new GeneratorPackageException(GeneratorsTexts.CandidatesNameInvalid(candidates.Create.Name));
        }

        var parts = candidates.Create.Modifiers.Split('.');
        if (parts.Length != 2 || package.Root is not { } root || root.Id != parts[0] || root.Fields.All(f => f.Id != parts[1] || f.Type != FieldType.Modifiers))
        {
            throw new GeneratorPackageException(GeneratorsTexts.CandidatesModifiersInvalid(candidates.Create.Modifiers));
        }
    }

    /// <summary>Quellen der Felder gibt es, <c>fills</c> nennt <c>name</c>/<c>desc</c> und Felder derselben Ebene, JSON-Quellen nennen ein bekanntes Asset.</summary>
    private static void CheckLookups(GeneratorPackage package)
    {
        var sources = (package.Lookups ?? Array.Empty<LookupSourceDef>()).ToDictionary(s => s.Id, StringComparer.Ordinal);
        foreach (var source in sources.Values.Where(s => s.Kind == LookupKind.Json && !package.Assets.ContainsKey(s.Asset!)))
        {
            throw new GeneratorPackageException(GeneratorsTexts.LookupUnknownAsset(source.Id, source.Asset!));
        }

        var fieldLists = new List<IReadOnlyList<FieldDef>> { package.Head };
        for (var level = package.Root; level is not null; level = level.Child)
        {
            fieldLists.Add(level.Fields);
        }

        foreach (var fields in fieldLists)
        {
            foreach (var field in fields.Where(f => f.Lookup is not null))
            {
                var lookup = field.Lookup!;
                foreach (var source in lookup.Sources.Where(s => !sources.ContainsKey(s)))
                {
                    throw new GeneratorPackageException(GeneratorsTexts.LookupUnknownSource(field.Id, source));
                }

                if (!lookup.Fills.Keys.Any(k => k is "name" or "desc"))
                {
                    throw new GeneratorPackageException(GeneratorsTexts.LookupFillsNeedName(field.Id));
                }

                foreach (var target in lookup.Fills.Values.Where(t => fields.All(f => f.Id != t)))
                {
                    throw new GeneratorPackageException(GeneratorsTexts.LookupUnknownFill(field.Id, target));
                }
            }
        }
    }

    private static LevelDef Level(JsonElement e, string language) => new(
        Required(e, "id"),
        Text(e, "label", language) ?? Required(e, "id"),
        Optional(e, "constantsTable"),
        Int(e, "min", 0),
        Int(e, "max", int.MaxValue),
        Optional(e, "numberBaseField"),
        Items(e, "fields").Select(f => Field(f, language)).ToList(),
        e.TryGetProperty("child", out var child) ? Level(child, language) : null,
        e.TryGetProperty("initial", out var initial) && initial.TryGetInt32(out var count) ? count : null);

    private static OutputDef Output(JsonElement e, string language)
    {
        var kind = Enum<OutputKind>(e, "kind", null);
        return new OutputDef(
            Required(e, "id"),
            kind,
            // "jsonEntry" schreibt in eine externe Datei und braucht keinen Pfad ("path": "" ist erlaubt)
            kind == OutputKind.JsonEntry ? Optional(e, "path") ?? string.Empty : Required(e, "path"),
            Optional(e, "template"),
            Optional(e, "forEach"),
            Optional(e, "block"),
            Optional(e, "skeleton"),
            Optional(e, "legacyBegin"),
            Optional(e, "legacyEnd"),
            Optional(e, "line"),
            Optional(e, "keyPattern"),
            Optional(e, "source"),
            Optional(e, "newLine"),
            Strings(e, "legacyPatterns"),
            Strings(e, "keep"),
            Bool(e, "checkUnreferenced"),
            Optional(e, "match"),
            e.TryGetProperty("keys", out var keys) && keys.ValueKind == JsonValueKind.String ? keys.GetString() : null,
            Text(e, "message", language),
            Optional(e, "insertAfter"),
            Optional(e, "ifLimit"),
            Optional(e, "external"),
            Optional(e, "jsonPath"),
            Optional(e, "entryKey"));
    }

    /// <summary>Abschnitt <c>externals</c>: Kennung → <c>label</c> (ohne Angabe die Kennung) und <c>hint</c>.</summary>
    private static Dictionary<string, ExternalDef> Externals(JsonElement root, string language)
    {
        var result = new Dictionary<string, ExternalDef>(StringComparer.Ordinal);
        if (!root.TryGetProperty("externals", out var externals) || externals.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var entry in externals.EnumerateObject())
        {
            result[entry.Name] = new ExternalDef(Text(entry.Value, "label", language) ?? entry.Name, Text(entry.Value, "hint", language));
        }

        return result;
    }

    /// <summary>Abschnitt <c>read</c>; fehlt <c>keys</c> oder <c>elements</c>, ist das ein Fehler mit dem Feldnamen.</summary>
    private static ReadDef Read(JsonElement e)
    {
        if (!e.TryGetProperty("elements", out var elements) || elements.ValueKind != JsonValueKind.Object)
        {
            throw new GeneratorPackageException(GeneratorsTexts.FieldMissing("read.elements"));
        }

        var fields = elements.TryGetProperty("fields", out var f) && f.ValueKind == JsonValueKind.Object
            ? f.EnumerateObject().ToDictionary(p => p.Name, p => new ReadFieldDef(Bool(p.Value, "body"), Strings(p.Value, "except") ?? Array.Empty<string>()), StringComparer.Ordinal)
            : new Dictionary<string, ReadFieldDef>(StringComparer.Ordinal);
        return new ReadDef(
            ReadKeys(e, out var keysBlock),
            new ReadElementsDef(Required(elements, "file"), Required(elements, "block"), Required(elements, "level"), fields),
            Items(e, "localization").Select(l => new ReadLocalizationDef(Required(l, "key"), Required(l, "field"), Optional(l, "output") is { Length: > 0 } o ? o : null)).ToList(),
            Items(e, "flags").Select(l => new ReadFlagDef(Required(l, "file"), Required(l, "block"), Optional(l, "pattern") is { Length: > 0 } ? Optional(l, "contains") ?? string.Empty : Required(l, "contains"), Required(l, "field"), Optional(l, "pattern"))).ToList(),
            Items(e, "assets").Select(l => new ReadAssetDef(Required(l, "field"), Required(l, "file"))).ToList(),
            Optional(e, "localizationOutput"),
            keysBlock,
            Items(e, "values").Select(ReadValue).ToList(),
            Items(e, "json").Select(ReadJson).ToList());
    }

    /// <summary>Eine Zeile von <c>read.json</c>: Eintrag und Eigenschaft in einer externen JSON-Datei; <c>jsonPath</c> darf leer sein, <c>join</c> ist ohne Angabe ein Zeilenumbruch.</summary>
    private static ReadJsonDef ReadJson(JsonElement j) =>
        new(Required(j, "external"), Optional(j, "jsonPath") ?? string.Empty, Required(j, "entryKey"), Required(j, "property"), Required(j, "field"),
            Optional(j, "join") is { Length: > 0 } join ? join : "\n");

    /// <summary>Eine Zeile von <c>read.values</c>: <c>pattern</c> (Treffer sammeln) oder <c>body</c> (Inhalt eines Unterblocks), eins von beiden ist Pflicht.</summary>
    private static ReadValueDef ReadValue(JsonElement v)
    {
        var body = Optional(v, "body") is { Length: > 0 } b ? b : null;
        var pattern = body is null ? Required(v, "pattern") : Optional(v, "pattern") ?? string.Empty;
        return new ReadValueDef(Required(v, "file"), Required(v, "block"), pattern, Required(v, "field"), Optional(v, "join") is { Length: > 0 } join ? join : "\n", body);
    }

    /// <summary><c>read.keys</c>: Text (Regex auf Pfade) oder Objekt <c>{ file, block }</c> für Schlüssel aus Blockschlüsseln.</summary>
    private static string ReadKeys(JsonElement read, out string? block)
    {
        block = null;
        if (read.TryGetProperty("keys", out var keys) && keys.ValueKind == JsonValueKind.Object)
        {
            block = Required(keys, "block");
            return Required(keys, "file");
        }

        return Optional(read, "keys") ?? throw new GeneratorPackageException(GeneratorsTexts.FieldMissing("read.keys"));
    }

    private static IReadOnlyList<string>? Strings(JsonElement e, string name) =>
        e.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array ? array.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList() : null;

    /// <summary>Konstanten-Tabellen; ein Objekt als Wert ist ein Paket-Text (Sprache → Text).</summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> Constants(JsonElement root, string language)
    {
        var result = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>(StringComparer.Ordinal);
        if (!root.TryGetProperty("constants", out var constants))
        {
            return result;
        }

        foreach (var table in constants.EnumerateObject())
        {
            result[table.Name] = table.Value.EnumerateArray()
                .Select(row => (IReadOnlyDictionary<string, string>)row.EnumerateObject().ToDictionary(
                    p => p.Name,
                    p => PackageText.Resolve(p.Value, language),
                    StringComparer.Ordinal))
                .ToList();
        }

        return result;
    }

    private static LanguageDefaults Languages(JsonElement root) =>
        root.TryGetProperty("languages", out var l)
            ? new LanguageDefaults(Bool(l, "extendEnglish"), Bool(l, "copyEnglish"), Bool(l, "englishForMissing"))
            : new LanguageDefaults();

    /// <summary>
    /// Abschnitt <c>assets</c>: Text (Pfad, Herkunft Spiel) oder Objekt mit <c>path</c> und <c>origin</c> (<c>game</c>/<c>mod</c>, ohne Angabe Spiel).
    /// Jeder Pfad muss relativ im Datenordner bleiben.
    /// </summary>
    private static Dictionary<string, AssetDef> AssetDefs(JsonElement root)
    {
        var result = new Dictionary<string, AssetDef>();
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var entry in assets.EnumerateObject())
        {
            var asset = entry.Value.ValueKind == JsonValueKind.Object
                ? new AssetDef(Required(entry.Value, "path"), Enum(entry.Value, "origin", (AssetOrigin?)AssetOrigin.Game))
                : new AssetDef(entry.Value.GetString() ?? string.Empty, AssetOrigin.Game);
            if (!SafeRelativePath.IsSafe(asset.Path))
            {
                throw new GeneratorPackageException(GeneratorsTexts.UnsafeAssetPath(entry.Name, asset.Path));
            }

            result[entry.Name] = asset;
        }

        return result;
    }

    private static IEnumerable<JsonElement> Items(JsonElement e, string name) =>
        e.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array ? array.EnumerateArray().ToList() : Enumerable.Empty<JsonElement>();

    /// <summary><c>markerId</c> wie eine Paket-Kennung (steht in Markierungszeilen der Mod); ohne Angabe <see langword="null"/> (dann gilt die Paket-Kennung).</summary>
    private static string? MarkerId(JsonElement root)
    {
        var markerId = Optional(root, "markerId");
        return markerId is null || Editing.Package.PackageStore.IsValidPackageId(markerId)
            ? markerId
            : throw new GeneratorPackageException(GeneratorsTexts.UnknownValue("markerId", markerId));
    }

    private static string Required(JsonElement e, string name) =>
        Optional(e, name) is { Length: > 0 } value ? value : throw new GeneratorPackageException(GeneratorsTexts.FieldMissing(name));

    private static string? Optional(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Paket-Text (Text oder Sprachtabelle) in der UI-Sprache; fehlt er, <see langword="null"/>.</summary>
    private static string? Text(JsonElement e, string name, string language) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String or JsonValueKind.Object
            ? PackageText.Resolve(value, language)
            : null;

    private static string RequiredText(JsonElement e, string name, string language) =>
        Text(e, name, language) is { Length: > 0 } value ? value : throw new GeneratorPackageException(GeneratorsTexts.FieldMissing(name));

    private static bool Bool(JsonElement e, string name) => e.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static int Int(JsonElement e, string name, int fallback) =>
        e.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : fallback;

    /// <summary>camelCase-Name aus package.json (<c>ownFile</c>, <c>multilineText</c>) als Enum; unbekannte Werte sind ein Fehler mit Feld und Wert.</summary>
    private static T Enum<T>(JsonElement e, string name, T? fallback)
        where T : struct, System.Enum
    {
        var text = Optional(e, name);
        if (text is null)
        {
            return fallback ?? throw new GeneratorPackageException(GeneratorsTexts.FieldMissing(name));
        }

        return System.Enum.TryParse<T>(text, ignoreCase: true, out var value) && !int.TryParse(text, out _)
            ? value
            : throw new GeneratorPackageException(GeneratorsTexts.UnknownValue(name, text));
    }
}
