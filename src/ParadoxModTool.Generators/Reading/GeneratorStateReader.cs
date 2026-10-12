using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ParadoxModTool.Patches;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Generators;

/// <summary>Ein gelesener Arbeitsstand und Hinweise (übersprungene Nummern o. ä.).</summary>
public sealed record ReadResult(GeneratorState State, IReadOnlyList<string> Notes);

/// <summary>Gelesene Stände beim Übernehmen aus einer anderen Mod und die Hinweise dazu.</summary>
public sealed record TakeOverResult(IReadOnlyList<GeneratorState> States, IReadOnlyList<string> Notes);

/// <summary>
/// Der Einleser eines Pakets (Spec Teil 4, Abschnitt <c>read</c>): macht aus den Dateien einer Mod wieder einen Arbeitsstand. Muster sind
/// Scriban-Vorlagen mit dem Schlüssel (IV2: <c>{{ tag }}</c>) und Regex mit den Gruppen <c>number</c> und <c>suffix</c>. Schreibt nie in die Mod.
/// </summary>
public static class GeneratorStateReader
{
    /// <summary>Die Schlüssel einer Mod: <c>read.keys</c> auf die relativen Pfade (mit "/"), ohne Doppelte, sortiert.</summary>
    public static IReadOnlyList<string> Keys(GeneratorPackage package, string modRoot)
    {
        if (package.Read is not { } read || !Directory.Exists(modRoot))
        {
            return Array.Empty<string>();
        }

        return KeyMatches(package, modRoot)
            .Select(m => m.Key)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Jeder Fund eines Schlüssels mit seiner Datei: aus dem Pfad (Gruppe <c>key</c>) oder, mit <see cref="ReadDef.KeysBlock"/>, aus den obersten
    /// Blockschlüsseln der passenden Dateien (Gruppe <c>key</c> oder Gruppen nach den Schlüsselfeldern).
    /// </summary>
    internal static IEnumerable<(string Path, string Key)> KeyMatches(GeneratorPackage package, string modRoot)
    {
        if (package.Read is not { } read || !Directory.Exists(modRoot))
        {
            yield break;
        }

        var pattern = new Regex(read.Keys, RegexOptions.CultureInvariant);
        var block = read.KeysBlock is { Length: > 0 } b ? Whole(b) : null;
        foreach (var path in Directory.EnumerateFiles(modRoot, "*", SearchOption.AllDirectories)
                     .Select(f => Path.GetRelativePath(modRoot, f).Replace(Path.DirectorySeparatorChar, '/'))
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var match = pattern.Match(path);
            if (!match.Success)
            {
                continue;
            }

            if (block is null)
            {
                if (match.Groups["key"].Success)
                {
                    yield return (path, match.Groups["key"].Value);
                }

                continue;
            }

            foreach (var statement in Parse(modRoot, path).Statements)
            {
                if (block.Match(statement.Key) is { Success: true } hit && KeyOf(package, hit) is { } key)
                {
                    yield return (path, key);
                }
            }
        }
    }

    private static string? KeyOf(GeneratorPackage package, Match match) =>
        match.Groups["key"].Success
            ? match.Groups["key"].Value
            : PackageKey.Compose(package, package.KeyFields.Where(f => match.Groups[f].Success).ToDictionary(f => f, f => match.Groups[f].Value, StringComparer.Ordinal));

    /// <summary>
    /// Liest den Stand eines Schlüssels; <see langword="null"/>, wenn die Datei der Elemente fehlt. <paramref name="languages"/>: die Sprachen des Spielprofils.
    /// <paramref name="externalPaths"/>: gewählte Pfade der externen Dateien des Pakets (Kennung → Pfad, siehe <see cref="ExternalPathStore"/>) für <c>read.json</c>.
    /// </summary>
    public static ReadResult? Read(GeneratorPackage package, string modRoot, string key, IReadOnlyList<string> languages,
        IReadOnlyDictionary<string, string>? externalPaths = null)
    {
        var read = package.Read ?? throw new GeneratorPackageException(GeneratorsTexts.FieldMissing("read"));
        var renderer = new GeneratorRenderer(package);
        var keyState = new GeneratorState { PackageId = package.Id, Key = key };
        PackageKey.Apply(package, keyState, key);
        var model = TemplateModel.Build(package, keyState, null);
        string Render(string text) => renderer.RenderInline(text, model);

        var elementsPath = Render(read.Elements.File);
        // Ein importiertes Paket darf keine Dateien außerhalb der Mod einlesen.
        if (!SafeRelativePath.IsSafe(elementsPath.Trim()))
        {
            throw new GeneratorOutputException(GeneratorsTexts.ReadUnsafePath(elementsPath));
        }

        if (!File.Exists(Full(modRoot, elementsPath)))
        {
            return null;
        }

        var notes = new List<string>();
        var state = GeneratorState.CreateEmpty(package, languages);
        state.Key = key;
        PackageKey.Apply(package, state, key);
        state.ModRoot = modRoot;
        if (package.Root is { ConstantsTable: null } countable)
        {
            // Zählbare oberste Ebene: die Elemente entstehen aus den Blöcken.
            state.Head.Children[countable.Id] = new List<FormNode>();
        }
        else if (package.Root is { Child: { } numbered } root)
        {
            foreach (var group in state.Head.Children[root.Id])
            {
                group.Children[numbered.Id] = new List<FormNode>();
            }
        }

        var (elementsText, elements) = Parse(modRoot, elementsPath);
        var blockPattern = Whole(Render(read.Elements.Block));
        foreach (var statement in elements)
        {
            if (blockPattern.Match(statement.Key) is not { Success: true } match
                || Element(package, state, blockPattern, match, read.Elements.Level, create: true, notes) is not { } node)
            {
                continue;
            }

            foreach (var (field, definition) in read.Elements.Fields)
            {
                if (definition.Body)
                {
                    node.Values[field] = BlockText.Dedent(BlockText.BodyLines(elementsText, statement, definition.Except.ToList()));
                }
            }
        }

        ElementAddress.FillToMinimum(package, state);
        ReadLocalization(package, renderer, keyState, modRoot, languages, state, notes);
        ReadFlags(package, read, modRoot, state, Render, notes);
        ReadValues(package, read, modRoot, state, Render, notes);
        ReadAssets(package, read, modRoot, state, Render, notes);
        ReadJson(package, read, externalPaths, state, Render, notes);
        return new ReadResult(state, notes);
    }

    /// <summary>Liest mehrere Schlüssel aus <paramref name="sourceModRoot"/>; die Stände bekommen <paramref name="targetModRoot"/> als Basis-Mod.</summary>
    public static TakeOverResult TakeOver(GeneratorPackage package, string sourceModRoot, IReadOnlyList<string> keys, string? targetModRoot, IReadOnlyList<string> languages,
        IReadOnlyDictionary<string, string>? externalPaths = null)
    {
        var states = new List<GeneratorState>();
        var notes = new List<string>();
        foreach (var key in keys)
        {
            if (Read(package, sourceModRoot, key, languages, externalPaths) is not { } result)
            {
                notes.Add(GeneratorsTexts.ReadKeyMissing(key));
                continue;
            }

            result.State.ModRoot = targetModRoot;
            states.Add(result.State);
            notes.AddRange(result.Notes.Where(n => !notes.Contains(n)));
        }

        return new TakeOverResult(states, notes);
    }

    /// <summary>
    /// Lokalisierung: je Sprache die Datei am Pfad der Lokalisierungs-Ausgabe. Englisch füllt das Feld, andere Sprachen werden Übersetzungen (nur
    /// wenn sie vom Englischen abweichen und nicht leer sind). Ist in einer anderen Sprache ein Text leer, der englisch gefüllt ist, war "Englisch
    /// kopieren" aus.
    /// </summary>
    private static void ReadLocalization(GeneratorPackage package, GeneratorRenderer renderer, GeneratorState keyState, string modRoot,
        IReadOnlyList<string> languages, GeneratorState state, List<string> notes)
    {
        if (package.Read is not { } read)
        {
            return;
        }

        var found = new List<string>();
        var blanks = false;
        var ordered = languages.Contains(LanguageFill.English) ? languages.Where(l => l == LanguageFill.English).Concat(languages.Where(l => l != LanguageFill.English)) : languages;
        // Einträge nach ihrer Lokalisierungs-Ausgabe gruppiert: jede Ausgabe hat eigene Dateien je Sprache
        var groups = read.Localization
            .Select(d => (Output: read.LocalizationFor(package, d), Definition: d))
            .Where(p => p.Output is not null)
            .GroupBy(p => p.Output!.Id, StringComparer.Ordinal)
            .ToList();
        foreach (var language in ordered)
        foreach (var group in groups)
        {
            var output = group.First().Output!;
            var languageModel = TemplateModel.Build(package, keyState, language);
            var relative = renderer.RenderInline(output.Path, languageModel);
            if (!SafeRelativePath.IsSafe(relative.Trim()))
            {
                notes.Add(GeneratorsTexts.ReadUnsafePath(relative));
                continue;
            }

            var path = Full(modRoot, relative);
            if (!File.Exists(path))
            {
                continue;
            }

            if (!found.Contains(language))
            {
                found.Add(language);
            }

            var values = YmlReader.Values(TextFile.Read(path).Text);
            foreach (var definition in group.Select(p => p.Definition))
            {
                var pattern = Whole(renderer.RenderInline(definition.Key, languageModel));
                var (level, field) = Split(definition.Field);
                foreach (var (key, raw) in values)
                {
                    if (pattern.Match(key) is not { Success: true } match
                        || (level.Length == 0 ? state.Head : Element(package, state, pattern, match, level, create: false, notes)) is not { } node)
                    {
                        continue;
                    }

                    var text = YmlReader.Unescape(raw);
                    if (language == LanguageFill.English)
                    {
                        node.Values[field] = text;
                        continue;
                    }

                    var english = node.Values.GetValueOrDefault(field) ?? string.Empty;
                    if (text.Length == 0)
                    {
                        blanks |= english.Length > 0;
                    }
                    else if (text != english)
                    {
                        if (!node.Translations.TryGetValue(field, out var translations))
                        {
                            translations = new Dictionary<string, string>(StringComparer.Ordinal);
                            node.Translations[field] = translations;
                        }

                        translations[language] = text;
                    }
                }
            }
        }

        if (found.Count > 0)
        {
            state.Languages = found;
        }

        if (blanks)
        {
            state.LanguageRules = state.LanguageRules with { CopyEnglish = false };
        }
    }

    private static void ReadFlags(GeneratorPackage package, ReadDef read, string modRoot, GeneratorState state, Func<string, string> render, List<string> notes)
    {
        foreach (var flag in read.Flags)
        {
            var path = render(flag.File);
            if (!SafeRelativePath.IsSafe(path.Trim()))
            {
                notes.Add(GeneratorsTexts.ReadUnsafePath(path));
                continue;
            }

            if (!File.Exists(Full(modRoot, path)))
            {
                continue;
            }

            var (text, statements) = Parse(modRoot, path);
            var pattern = Whole(render(flag.Block));
            var (level, field) = Split(flag.Field);
            foreach (var statement in statements)
            {
                if (pattern.Match(statement.Key) is { Success: true } match
                    && (level.Length == 0 ? state.Head : Element(package, state, pattern, match, level, create: false, notes)) is { } node)
                {
                    var start = statement.SourceStart ?? 0;
                    var end = statement.SourceEnd ?? text.Length;
                    var body = text[start..end];
                    node.Values[field] = (flag.Pattern is { Length: > 0 } flagPattern ? Regex.IsMatch(body, render(flagPattern), RegexOptions.CultureInvariant) : body.Contains(flag.Contains, StringComparison.Ordinal)) ? "yes" : "no";
                }
            }
        }
    }

    /// <summary>Gesammelte Werte (<c>read.values</c>): Gruppe <c>value</c> jedes Treffers im passenden Block, ohne Doppelte in Fundreihenfolge.</summary>
    private static void ReadValues(GeneratorPackage package, ReadDef read, string modRoot, GeneratorState state, Func<string, string> render, List<string> notes)
    {
        foreach (var definition in read.Values ?? Array.Empty<ReadValueDef>())
        {
            var path = render(definition.File);
            if (!SafeRelativePath.IsSafe(path.Trim()))
            {
                notes.Add(GeneratorsTexts.ReadUnsafePath(path));
                continue;
            }

            if (!File.Exists(Full(modRoot, path)))
            {
                continue;
            }

            var (text, statements) = Parse(modRoot, path);
            var block = Whole(render(definition.Block));
            var value = definition.Body is null ? new Regex(render(definition.Pattern), RegexOptions.CultureInvariant) : null;
            var (level, field) = Split(definition.Field);
            foreach (var statement in statements)
            {
                if (block.Match(statement.Key) is not { Success: true } match
                    || (level.Length == 0 ? state.Head : Element(package, state, block, match, level, create: false, notes)) is not { } node)
                {
                    continue;
                }

                if (definition.Body is { } bodyPath)
                {
                    // Inhalt eines Unterblocks (z. B. allow) als Text, gemeinsame Einrückung entfernt
                    if (SubBlock(statement, bodyPath) is { } sub)
                    {
                        node.Values[field] = BlockText.Dedent(BlockText.BodyLines(text, sub, Array.Empty<string>()));
                    }

                    continue;
                }

                var body = text[(statement.SourceStart ?? 0)..(statement.SourceEnd ?? text.Length)];
                var found = value!.Matches(body).Where(m => m.Groups["value"].Success).Select(m => m.Groups["value"].Value).Distinct(StringComparer.Ordinal).ToList();
                if (found.Count > 0)
                {
                    node.Values[field] = string.Join(definition.Join, found);
                }
            }
        }
    }

    /// <summary>Unterblock eines Blocks über einen Punkt-Pfad aus Schlüsseln (je erster Treffer); <c>.</c> ist der Block selbst.</summary>
    private static ScriptStatement? SubBlock(ScriptStatement statement, string path)
    {
        if (path.Trim() == ".")
        {
            // der Block selbst
            return statement;
        }

        var current = statement;
        foreach (var part in path.Split('.'))
        {
            if (current.Value is not ScriptBlock block || block.Entries.OfType<ScriptStatement>().FirstOrDefault(s => s.Key == part.Trim() && s.Value is ScriptBlock) is not { } next)
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    /// <summary>
    /// Werte aus einer externen JSON-Datei (<c>read.json</c>): der Eintrag unter <c>jsonPath</c> mit dem gerenderten <c>entryKey</c>, darin die
    /// <c>property</c>; ein Array wird mit <c>join</c> verbunden. Fehlt Pfad, Datei, Eintrag oder Eigenschaft, bleibt das Feld und es gibt einen Hinweis.
    /// </summary>
    private static void ReadJson(GeneratorPackage package, ReadDef read, IReadOnlyDictionary<string, string>? externalPaths, GeneratorState state,
        Func<string, string> render, List<string> notes)
    {
        foreach (var definition in read.Json ?? Array.Empty<ReadJsonDef>())
        {
            var label = package.Externals.TryGetValue(definition.External, out var external) ? external.Label : definition.External;
            if (externalPaths is null || !externalPaths.TryGetValue(definition.External, out var path) || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                AddNote(notes, GeneratorsTexts.ReadJsonNoFile(label));
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(File.ReadAllText(path));
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                AddNote(notes, GeneratorsTexts.ReadJsonInvalid(label, ex.Message));
                continue;
            }

            using (document)
            {
                var entryKey = render(definition.EntryKey).Trim();
                var entry = document.RootElement;
                foreach (var segment in definition.JsonPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
                {
                    entry = entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty(segment, out var next) ? next : default;
                }

                if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty(entryKey, out entry) || entry.ValueKind != JsonValueKind.Object)
                {
                    AddNote(notes, GeneratorsTexts.ReadJsonNoEntry(label, entryKey));
                    continue;
                }

                if (!entry.TryGetProperty(definition.Property, out var value) || value.ValueKind is JsonValueKind.Null)
                {
                    AddNote(notes, GeneratorsTexts.ReadJsonNoProperty(label, entryKey, definition.Property));
                    continue;
                }

                state.Head.Values[definition.Field] = value.ValueKind == JsonValueKind.Array
                    ? string.Join(definition.Join, value.EnumerateArray().Select(JsonText))
                    : JsonText(value);
            }
        }
    }

    private static string JsonText(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();

    private static void AddNote(List<string> notes, string note)
    {
        if (!notes.Contains(note))
        {
            notes.Add(note);
        }
    }

    private static void ReadAssets(GeneratorPackage package, ReadDef read, string modRoot, GeneratorState state, Func<string, string> render, List<string> notes)
    {
        foreach (var asset in read.Assets)
        {
            var relative = render(asset.File);
            if (!SafeRelativePath.IsSafe(relative.Trim()))
            {
                notes.Add(GeneratorsTexts.ReadUnsafePath(relative));
                continue;
            }

            var path = Full(modRoot, relative);
            var field = package.Head.FirstOrDefault(f => f.Id == asset.Field);
            if (!File.Exists(path) || field?.Asset is not { } library || !package.Assets.ContainsKey(library))
            {
                continue;
            }

            var libraryPath = package.AssetPath(library);
            if ((File.Exists(libraryPath) || Directory.Exists(libraryPath)) && AssetLibrary.Open(libraryPath).FindEqual(File.ReadAllBytes(path)) is { } entry)
            {
                state.Head.Values[asset.Field] = entry;
            }
        }
    }

    /// <summary>
    /// Datei lesen und ihre obersten Blöcke liefern (Text mit LF); unlesbar = Fehler mit dem Dateinamen. GUI-Köpfe ohne <c>=</c>
    /// (<c>types name { }</c>, <c>template name { }</c>) werden Blöcke mit dem Kopfzeilentext als Schlüssel (<c>types name</c>).
    /// </summary>
    internal static (string Text, IReadOnlyList<ScriptStatement> Statements) Parse(string modRoot, string relativePath)
    {
        try
        {
            var text = TextFile.Read(Full(modRoot, relativePath)).Text.Replace("\r\n", "\n");
            var statements = new List<ScriptStatement>();
            foreach (var unit in EntryUnits.Build(ScriptDocument.Parse(text).Entries))
            {
                if (unit.Entry is ScriptStatement { Value: ScriptBlock } statement)
                {
                    statements.Add(statement);
                }
                else if (unit is { Kind: EntryUnit.UnitKind.Body, Header.Count: > 0, Entry: ScriptListItem { Value: ScriptBlock body } item })
                {
                    statements.Add(new ScriptStatement
                    {
                        Key = EntryUnits.HeaderText(unit.Header),
                        Operator = ScriptOperator.Equals,
                        Value = body,
                        SourceText = text,
                        SourceStart = unit.Header[0].SourceStart,
                        SourceEnd = item.SourceEnd,
                    });
                }
            }

            return (text, statements);
        }
        catch (Exception ex) when (ex is not GeneratorOutputException)
        {
            throw new GeneratorOutputException(GeneratorsTexts.InFile(relativePath, GeneratorsTexts.ParseFailed(ex.Message)));
        }
    }

    internal static Regex Whole(string pattern) => new("^(?:" + pattern + ")$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Das Element zu einem Treffer. Hat das Muster keine Gruppe <c>number</c>, aber <c>suffix</c>, wählt allein die Endung das Element der
    /// zählbaren obersten Ebene (siehe <see cref="ElementAddress"/>).
    /// </summary>
    private static FormNode? Element(GeneratorPackage package, GeneratorState state, Regex pattern, Match match, string level, bool create, List<string> notes)
    {
        var groups = pattern.GetGroupNames();
        return ElementAddress.Element(package, state, Number(match), Suffix(match), level, create, notes, bySuffix: !groups.Contains("number") && groups.Contains("suffix"));
    }

    private static int Number(Match match) =>
        match.Groups["number"].Success && int.TryParse(match.Groups["number"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : -1;

    private static string? Suffix(Match match) => match.Groups["suffix"].Success ? match.Groups["suffix"].Value : null;

    private static (string Level, string Field) Split(string path)
    {
        var dot = path.LastIndexOf('.');
        return dot < 0 ? (string.Empty, path) : (path[..dot], path[(dot + 1)..]);
    }

    internal static string Full(string root, string relativePath) => Path.Combine(root, relativePath.Trim().Replace('/', Path.DirectorySeparatorChar));
}
