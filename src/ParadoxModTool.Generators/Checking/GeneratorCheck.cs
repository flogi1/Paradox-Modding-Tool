using System.Globalization;
using System.Text.RegularExpressions;
using ParadoxModTool.Patches;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Generators;

/// <summary>Ein Block, auf den keine andere Datei des Schlüssels verweist; <see cref="Removable"/>: sein Element liegt am Ende und kann weg.</summary>
public sealed record UnreferencedBlock(string BlockKey, bool Removable);

/// <summary>Eine Zeile von "Mod prüfen": Dateien fehlend/abweichend/gleich, Alt-Einträge, ungenutzte Blöcke, Fehler; <see cref="State"/> = der gelesene Stand.</summary>
public sealed record CheckRow(
    string Key,
    int Missing,
    int Changed,
    int Equal,
    int FormatOnly,
    int LegacyRemoved,
    IReadOnlyList<UnreferencedBlock> Unreferenced,
    string? Error,
    GeneratorState? State);

/// <summary>
/// "Mod prüfen" (Spec Teil 4): jeden Schlüssel der Mod frisch lesen und ohne Schreiben planen; Blöcke einer Ausgabe mit <c>checkUnreferenced</c>,
/// deren Schlüssel in keiner anderen vorhandenen Datei des Plans als ganzes Wort vorkommt, sind ungenutzt. Entfernbar sind nur Elemente am Ende
/// (letzte Stufen, letzte Ideen einer Gruppe), sonst verschöben sich die Nummern.
/// </summary>
public static class GeneratorCheck
{
    /// <param name="externalPaths">Gewählte Pfade der externen Dateien (Kennung → Pfad): ohne sie liest <c>read.json</c> nichts, und der Stand der Zeile verlöre z. B. die KI-Tags.</param>
    public static IReadOnlyList<CheckRow> Run(GeneratorPackage package, string modRoot, IReadOnlyList<string> languages,
        IReadOnlyDictionary<string, string>? externalPaths = null) =>
        GeneratorStateReader.Keys(package, modRoot).Select(key => Check(package, modRoot, key, languages, externalPaths)).ToList();

    public static CheckRow Check(GeneratorPackage package, string modRoot, string key, IReadOnlyList<string> languages,
        IReadOnlyDictionary<string, string>? externalPaths = null)
    {
        try
        {
            if (GeneratorStateReader.Read(package, modRoot, key, languages, externalPaths) is not { } read)
            {
                return new CheckRow(key, 0, 0, 0, 0, 0, Array.Empty<UnreferencedBlock>(), GeneratorsTexts.ReadKeyMissing(key), null);
            }

            var plan = GeneratorPlanner.Plan(package, read.State, new GeneratorTarget(modRoot, null, languages));
            var unreferenced = Unreferenced(package, modRoot, key, plan);
            var removable = RemovableBlocks(package, read.State, key, unreferenced);
            // Externe Dateien (jsonEntry) liegen nicht in der Mod und gehören nicht zu "Mod prüfen"
            var files = plan.Files.Where(f => f.ExternalPath is null).ToList();
            var changed = files.Where(f => f.Status == PlannedFileStatus.Changed).ToList();
            var formatOnly = changed.Count(f => f.Bytes is null && f.OriginalText is { } before && SameContent(f.RelativePath, before, f.NewText ?? string.Empty));
            return new CheckRow(
                key,
                files.Count(f => f.Status == PlannedFileStatus.New),
                changed.Count - formatOnly,
                files.Count(f => f.Status == PlannedFileStatus.Unchanged),
                formatOnly,
                files.Sum(f => f.LegacyRemoved),
                unreferenced.Select(b => new UnreferencedBlock(b, removable.Contains(b))).ToList(),
                null,
                read.State);
        }
        catch (Exception ex) when (ex is GeneratorOutputException or GeneratorTemplateException or GeneratorPackageException or IOException or UnauthorizedAccessException)
        {
            return new CheckRow(key, 0, 0, 0, 0, 0, Array.Empty<UnreferencedBlock>(), ex.Message, null);
        }
    }

    /// <summary>
    /// Gleicher Inhalt trotz anderem Text: bei .yml gleiche Schlüssel und Werte, sonst gleiche Token-Folge ohne Kommentare und Leerraum (Text in
    /// Anführungszeichen bleibt ein Token). So zählt eine nur anders formatierte Datei als "nur Format", nicht als "weicht ab".
    /// </summary>
    public static bool SameContent(string relativePath, string a, string b)
    {
        if (relativePath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
        {
            var left = YmlReader.Values(a);
            var right = YmlReader.Values(b);
            return left.Count == right.Count && left.All(p => right.TryGetValue(p.Key, out var v) && v == p.Value);
        }

        return Tokens(a).SequenceEqual(Tokens(b));
    }

    /// <summary>Token ohne Kommentare und Leerraum: <c>{ } =</c> einzeln, Text in Anführungszeichen ganz (auch mit <c>#</c> darin), sonst Wortfolgen.</summary>
    internal static IEnumerable<string> Tokens(string text)
    {
        var current = new System.Text.StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                var end = text.IndexOf('"', i + 1);
                end = end < 0 ? text.Length - 1 : end;
                yield return text[i..(end + 1)];
                i = end;
            }
            else if (c == '#' || char.IsWhiteSpace(c) || c is '{' or '}' or '=' || c == '﻿')
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                if (c == '#')
                {
                    var end = text.IndexOf('\n', i);
                    i = end < 0 ? text.Length : end;
                }
                else if (c is '{' or '}' or '=')
                {
                    yield return c.ToString();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    /// <summary>Der gelesene Stand ohne die entfernbaren ungenutzten Elemente (eine Kopie; der Stand der Zeile bleibt).</summary>
    public static GeneratorState WithoutUnreferenced(GeneratorPackage package, CheckRow row)
    {
        var state = GeneratorStateStore.Clone(row.State ?? throw new InvalidOperationException("Zeile ohne Stand"));
        RemoveTrailing(package, state, Elements(package, row.Key, row.Unreferenced.Select(b => b.BlockKey)));
        return state;
    }

    /// <summary>Blockschlüssel der geprüften Ausgaben, die in keiner anderen vorhandenen Textdatei des Plans als ganzes Wort vorkommen.</summary>
    private static List<string> Unreferenced(GeneratorPackage package, string modRoot, string key, GeneratorPlan plan)
    {
        var result = new List<string>();
        var renderer = new GeneratorRenderer(package);
        var keyState = new GeneratorState { PackageId = package.Id, Key = key };
        PackageKey.Apply(package, keyState, key);
        var model = TemplateModel.Build(package, keyState, null);
        foreach (var output in package.Outputs.Where(o => o.CheckUnreferenced && o.Kind == OutputKind.SyncBlocks))
        {
            var own = renderer.RenderInline(output.Path, model).Trim().Replace('\\', '/');
            var ownPath = Full(modRoot, own);
            if (!File.Exists(ownPath))
            {
                continue;
            }

            var pattern = new Regex("^" + Regex.Escape(renderer.RenderInline(output.KeyPattern!, model)).Replace("\\*", "[A-Za-z0-9_]+") + "$", RegexOptions.CultureInvariant);
            var blocks = ScriptDocument.Parse(TextFile.Read(ownPath).Text).Entries.OfType<ScriptStatement>()
                .Where(s => s.Value is ScriptBlock && pattern.IsMatch(s.Key)).Select(s => s.Key).Distinct(StringComparer.Ordinal).ToList();
            var others = plan.Files
                .Where(f => f.Bytes is null && f.ExternalPath is null && !string.Equals(f.RelativePath, own, StringComparison.OrdinalIgnoreCase))
                .Select(f => Full(modRoot, f.RelativePath))
                .Where(File.Exists)
                .Select(p => TextFile.Read(p).Text)
                .ToList();
            result.AddRange(blocks.Where(block =>
            {
                var word = new Regex("(?<![A-Za-z0-9_])" + Regex.Escape(block) + "(?![A-Za-z0-9_])", RegexOptions.CultureInvariant);
                return !others.Any(word.IsMatch);
            }));
        }

        return result;
    }

    /// <summary>Welche ungenutzten Blöcke wirklich wegfallen würden: auf einer Kopie entfernen und vergleichen.</summary>
    private static HashSet<string> RemovableBlocks(GeneratorPackage package, GeneratorState state, string key, IReadOnlyList<string> unreferenced)
    {
        var elements = Elements(package, key, unreferenced);
        var copy = GeneratorStateStore.Clone(state);
        var removed = RemoveTrailing(package, copy, elements);
        return elements.Where(e => removed.Contains((e.Number, e.Level))).Select(e => e.Block).ToHashSet(StringComparer.Ordinal);
    }

    private static List<(string Block, int Number, int Level)> Elements(GeneratorPackage package, string key, IEnumerable<string> blocks)
    {
        var list = new List<(string, int, int)>();
        if (package.Read is not { } read)
        {
            return list;
        }

        var keyState = new GeneratorState { PackageId = package.Id, Key = key };
        PackageKey.Apply(package, keyState, key);
        var pattern = new Regex("^(?:" + new GeneratorRenderer(package).RenderInline(read.Elements.Block, TemplateModel.Build(package, keyState, null)) + ")$", RegexOptions.CultureInvariant);
        foreach (var block in blocks)
        {
            if (pattern.Match(block) is { Success: true } match
                && int.TryParse(match.Groups["number"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                var suffix = match.Groups["suffix"].Success ? match.Groups["suffix"].Value : string.Empty;
                var level = suffix.Length == 0 ? 0 : int.TryParse(suffix.TrimStart('_'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : -1;
                list.Add((block, number, level));
            }
        }

        return list;
    }

    /// <summary>
    /// Entfernt ungenutzte Elemente von hinten: je Idee die letzten Stufen, solange sie ungenutzt sind (die erste bleibt, außer die ganze Idee ist
    /// ungenutzt und die letzte ihrer Gruppe). Liefert die entfernten (Nummer, Stufe).
    /// </summary>
    private static HashSet<(int Number, int Level)> RemoveTrailing(GeneratorPackage package, GeneratorState state, IReadOnlyList<(string Block, int Number, int Level)> elements)
    {
        var removed = new HashSet<(int, int)>();
        if (package.Root is not { ConstantsTable: { } table, Child: { NumberBaseField: { } baseField, Child: { } deeper } numbered } root
            || !package.Constants.TryGetValue(table, out var rows))
        {
            return removed;
        }

        var unused = elements.Select(e => (e.Number, e.Level)).ToHashSet();
        var groups = state.Head.Children[root.Id];
        for (var g = 0; g < rows.Count && g < groups.Count; g++)
        {
            if (!int.TryParse(rows[g].GetValueOrDefault(baseField), NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
                || !groups[g].Children.TryGetValue(numbered.Id, out var ideas))
            {
                continue;
            }

            var tail = true;
            for (var i = ideas.Count - 1; i >= 0; i--)
            {
                var number = start + i + 1;
                var levels = ideas[i].Children.GetValueOrDefault(deeper.Id) ?? new List<FormNode>();
                var whole = levels.Count > 0 && Enumerable.Range(0, levels.Count).All(l => unused.Contains((number, l)));
                if (tail && whole && ideas.Count > numbered.Min)
                {
                    removed.UnionWith(Enumerable.Range(0, levels.Count).Select(l => (number, l)));
                    ideas.RemoveAt(i);
                    continue;
                }

                tail = false;
                while (levels.Count > Math.Max(1, deeper.Min) && unused.Contains((number, levels.Count - 1)))
                {
                    removed.Add((number, levels.Count - 1));
                    levels.RemoveAt(levels.Count - 1);
                }
            }
        }

        return removed;
    }

    private static string Full(string root, string relativePath) => Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
