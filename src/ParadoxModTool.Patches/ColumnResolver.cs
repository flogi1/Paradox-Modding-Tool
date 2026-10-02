using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Patches;

/// <summary>Eine Tabellenzeile mit allen Werten: <c>key</c>, eigene Spalten (Standardwert bei leerer Zelle) und abgeleitete Spalten.</summary>
public sealed class ResolvedRow
{
    public required string Key { get; init; }

    public required IReadOnlyDictionary<string, string> Values { get; init; }

    public List<string> Errors { get; } = new();

    public bool HasErrors => Errors.Count > 0;
}

public sealed class ResolvedTable
{
    public required IReadOnlyList<ResolvedRow> Rows { get; init; }

    public required IReadOnlySet<string> ListColumns { get; init; }

    public List<string> Warnings { get; } = new();
}

/// <summary>
/// Löst die Tabelle eines Rezepts auf. Abgeleitete Spalten schlagen eine Anweisung <c>schlüssel = wert</c> in Skriptdateien nach
/// und nehmen den Schlüssel des äußersten oder innersten umgebenden Blocks; Dateien werden ordinal sortiert gelesen, der erste Treffer zählt
/// (mit "Alle Treffer" alle als Liste). Mehrere Suchen stehen mit ";" getrennt.
/// </summary>
public static class ColumnResolver
{
    private const string LandedTitles = "common/landed_titles/**/*.txt";

    public static ResolvedTable Resolve(PatchRecipe recipe, PatchWorkspace workspace)
    {
        var warnings = new List<string>();
        var parsed = new Dictionary<string, ScriptBlock?>(StringComparer.Ordinal);
        var indexes = new Dictionary<DerivedLookup, Dictionary<(string, string), List<(string Top, string Enclosing, int Order)>>>();
        HashSet<string>? titles = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<ResolvedRow>();
        var listColumns = ListColumns(recipe);

        foreach (var row in recipe.Rows)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = row.Key };
            var errors = new List<string>();
            foreach (var column in recipe.Columns)
            {
                if (column.Lookup is null)
                {
                    var cell = row.Values.TryGetValue(column.Name, out var raw) ? raw.Trim() : string.Empty;
                    values[column.Name] = cell.Length > 0 ? cell : column.Default;
                }
                else
                {
                    if (!indexes.TryGetValue(column.Lookup, out var index))
                    {
                        index = BuildIndex(column.Lookup, workspace, parsed, warnings);
                        indexes[column.Lookup] = index;
                    }

                    // Listen-Spalten in der Suche ergeben eine Suche je Wert; mehrere Suchen stehen mit ";" getrennt.
                    var hits = TemplateRenderer.RenderEach(column.Lookup.Match, values, listColumns)
                        .SelectMany(instance => instance.Text.Split(';'))
                        .SelectMany(part => FindAll(index, part, column.Lookup.Take))
                        .OrderBy(hit => hit.Order)
                        .Select(hit => hit.Value)
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
                    var found = hits.Count == 0 ? null : column.Lookup.All ? string.Join(", ", hits) : hits[0];
                    values[column.Name] = found ?? string.Empty;
                    if (found is null && !column.Lookup.Optional)
                    {
                        errors.Add(PatchTexts.NotFound(column.Name));
                    }
                }

                if (column.TitleKey && values[column.Name].Length > 0)
                {
                    titles ??= CollectTitles(workspace, parsed, warnings);
                    foreach (var title in TemplateRenderer.SplitList(values[column.Name]).Where(t => !titles.Contains(t)))
                    {
                        errors.Add(PatchTexts.UnknownTitle(title));
                    }
                }
            }

            if (row.Key.Trim().Length == 0)
            {
                errors.Insert(0, PatchTexts.KeyMissing);
            }
            else if (!seen.Add(row.Key))
            {
                errors.Insert(0, PatchTexts.DuplicateKey);
            }

            var resolved = new ResolvedRow { Key = row.Key, Values = values };
            resolved.Errors.AddRange(errors);
            rows.Add(resolved);
        }

        var table = new ResolvedTable { Rows = rows, ListColumns = listColumns };
        table.Warnings.AddRange(warnings.Distinct(StringComparer.Ordinal));
        return table;
    }

    /// <summary>Eigene Listen-Spalten und abgeleitete Spalten mit "Alle Treffer".</summary>
    private static HashSet<string> ListColumns(PatchRecipe recipe) =>
        recipe.Columns.Where(c => c.Type == ColumnType.List || c.Lookup is { All: true }).Select(c => c.Name).ToHashSet(StringComparer.Ordinal);

    /// <summary>Alle Treffer einer Suche <c>schlüssel = wert</c> mit ihrer Fundnummer (Datei- und Fundreihenfolge über alle Dateien).</summary>
    private static IEnumerable<(string Value, int Order)> FindAll(Dictionary<(string, string), List<(string Top, string Enclosing, int Order)>> index, string match, LookupTake take)
    {
        var equals = match.IndexOf('=');
        if (equals < 0)
        {
            return Array.Empty<(string, int)>();
        }

        var key = match[..equals].Trim();
        var value = match[(equals + 1)..].Trim().Trim('"');
        return index.TryGetValue((key, value), out var hits)
            ? hits.Select(hit => (take == LookupTake.TopLevelKey ? hit.Top : hit.Enclosing, hit.Order))
            : Array.Empty<(string, int)>();
    }

    private static Dictionary<(string, string), List<(string Top, string Enclosing, int Order)>> BuildIndex(
        DerivedLookup lookup, PatchWorkspace workspace, Dictionary<string, ScriptBlock?> parsed, List<string> warnings)
    {
        var index = new Dictionary<(string, string), List<(string, string, int)>>();
        var counter = new int[1];
        foreach (var (label, text) in Sources(lookup, workspace))
        {
            var root = Parse(label, text, parsed, warnings);
            if (root is null)
            {
                continue;
            }

            foreach (var entry in root.Entries.OfType<ScriptStatement>())
            {
                if (entry.Value is ScriptBlock block)
                {
                    Walk(block, entry.Key, entry.Key, index, counter);
                }
            }
        }

        return index;
    }

    private static void Walk(ScriptBlock block, string top, string enclosing, Dictionary<(string, string), List<(string, string, int)>> index, int[] counter)
    {
        foreach (var statement in block.Entries.OfType<ScriptStatement>())
        {
            switch (statement.Value)
            {
                case ScriptScalar scalar:
                    if (!index.TryGetValue((statement.Key, scalar.Text), out var hits))
                    {
                        hits = new List<(string, string, int)>();
                        index[(statement.Key, scalar.Text)] = hits;
                    }

                    // Fundreihenfolge über alle Dateien: mehrere Suchformen (";") werden danach sortiert, nicht nach Suchform.
                    hits.Add((top, enclosing, counter[0]++));
                    break;
                case ScriptBlock inner:
                    Walk(inner, top, statement.Key, index, counter);
                    break;
            }
        }
    }

    private static IEnumerable<(string Label, string? Text)> Sources(DerivedLookup lookup, PatchWorkspace workspace)
    {
        switch (lookup.Source)
        {
            case LookupSource.Mod:
                foreach (var rel in workspace.ModFiles(lookup.Files))
                {
                    yield return ("mod:" + rel, workspace.Get(rel));
                }

                break;
            case LookupSource.ModOverVanilla:
                var mod = workspace.ModFiles(lookup.Files).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var rel in mod.Concat(workspace.VanillaFiles(lookup.Files)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(r => r, StringComparer.Ordinal))
                {
                    yield return mod.Contains(rel) ? ("mod:" + rel, workspace.Get(rel)) : ("vanilla:" + rel, workspace.ReadVanilla(rel));
                }

                break;
            default:
                foreach (var rel in workspace.VanillaFiles(lookup.Files))
                {
                    yield return ("vanilla:" + rel, workspace.ReadVanilla(rel));
                }

                break;
        }
    }

    private static HashSet<string> CollectTitles(PatchWorkspace workspace, Dictionary<string, ScriptBlock?> parsed, List<string> warnings)
    {
        var titles = new HashSet<string>(StringComparer.Ordinal);
        var sources = workspace.VanillaFiles(LandedTitles).Select(rel => ("vanilla:" + rel, workspace.ReadVanilla(rel)))
            .Concat(workspace.ModFiles(LandedTitles).Select(rel => ("mod:" + rel, workspace.Get(rel))));
        foreach (var (label, text) in sources)
        {
            if (Parse(label, text, parsed, warnings) is { } root)
            {
                CollectKeys(root, titles);
            }
        }

        return titles;
    }

    private static void CollectKeys(ScriptBlock block, HashSet<string> keys)
    {
        foreach (var statement in block.Entries.OfType<ScriptStatement>())
        {
            if (statement.Value is ScriptBlock inner)
            {
                keys.Add(statement.Key);
                CollectKeys(inner, keys);
            }
        }
    }

    private static ScriptBlock? Parse(string label, string? text, Dictionary<string, ScriptBlock?> parsed, List<string> warnings)
    {
        if (parsed.TryGetValue(label, out var cached))
        {
            return cached;
        }

        ScriptBlock? root = null;
        if (text is not null)
        {
            try
            {
                root = ScriptDocument.Parse(text);
            }
            catch (ScriptParseException ex)
            {
                warnings.Add(PatchTexts.FileSkipped(label[(label.IndexOf(':') + 1)..], ex.Message));
            }
        }

        parsed[label] = root;
        return root;
    }
}
