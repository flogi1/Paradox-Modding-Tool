using System.Text.RegularExpressions;

namespace ParadoxModTool.Patches;

public enum CheckSeverity
{
    Error,
    Warning,
}

/// <param name="Where"><c>recipe</c>, <c>column &lt;name&gt;</c>, <c>row &lt;key&gt;</c> oder <c>rule &lt;id&gt;</c>.</param>
public sealed record RecipeIssue(CheckSeverity Severity, string Where, string Message);

/// <summary>
/// Zusätzliche Angaben der Prüfung: Spiel- und Mod-Ordner (für Dateimuster), das bisherige Rezept samt Gedächtnis (für Id und entfallene Regeln)
/// und die unbekannten Eigenschaften des Einlesens.
/// </summary>
public sealed record RecipeCheckOptions(
    string? GameRoot = null,
    string? ModRoot = null,
    PatchRecipe? Previous = null,
    PatchMemory? PreviousMemory = null,
    IReadOnlyList<string>? UnknownProperties = null);

/// <summary>
/// Strukturprüfung eines Rezepts (Import, Fenster, Kommandozeile <c>pmt-patch check</c>). Fehler machen es unbrauchbar oder gefährden die Mod,
/// Warnungen sind Hinweise.
/// </summary>
public static class RecipeChecker
{
    private static readonly Regex RecipeId = new(@"\A[a-z0-9_]+\z", RegexOptions.CultureInvariant);

    private static readonly Regex RuleId = new(@"\A[A-Za-z0-9_]+\z", RegexOptions.CultureInvariant);

    private static readonly string[] RegionExtensions = { ".txt", ".gui", ".yml", ".gfx", ".asset" };

    public static IReadOnlyList<RecipeIssue> Check(PatchRecipe recipe, RecipeCheckOptions? options = null)
    {
        options ??= new RecipeCheckOptions();
        var issues = new Issues();
        var columns = (recipe.Columns ?? new List<PatchColumn>()).Where(c => c is not null).ToList();
        var rows = (recipe.Rows ?? new List<PatchRow>()).Where(r => r is not null).ToList();
        var rules = (recipe.Rules ?? new List<PatchRule>()).Where(r => r is not null).ToList();

        CheckRecipe(recipe, options, issues);
        CheckColumns(recipe, columns, issues);
        CheckRows(columns, rows, issues);

        // RuleWarnings liest recipe.Columns; fehlende Listen (nur im Code möglich, das Einlesen macht sie leer) dürfen es nicht zum Absturz bringen.
        var normalized = new PatchRecipe { Id = recipe.Id ?? string.Empty, Name = recipe.Name ?? string.Empty, GameId = recipe.GameId ?? string.Empty, Columns = columns, Rows = rows, Rules = rules };
        CheckRules(normalized, issues);

        // Entfallene Regeln mit Ausgabe
        if (options.Previous is not null)
        {
            foreach (var id in DroppedRulesWithOutput(recipe, options.Previous, options.PreviousMemory, options.ModRoot))
            {
                issues.Error("rule " + id, PatchTexts.CheckDroppedRule(id));
            }
        }

        CheckPatternsMatch(columns, rules, options, issues);
        return issues.Items;
    }

    /// <summary>Rezept: Id, Vorlage, bisherige Fassung, unbekannte Eigenschaften.</summary>
    private static void CheckRecipe(PatchRecipe recipe, RecipeCheckOptions options, Issues issues)
    {
        var recipeId = recipe.Id ?? string.Empty;
        if (!RecipeId.IsMatch(recipeId))
        {
            issues.Error("recipe", PatchTexts.CheckRecipeIdInvalid(recipeId));
        }

        if (recipe.Preset is not null && recipe.Preset != SpecialBuildingsPreset.Id && recipe.Preset != SpecialResourcesPreset.Id)
        {
            issues.Error("recipe", PatchTexts.CheckPresetUnknown(recipe.Preset));
        }

        if (options.Previous is not null && !string.Equals(recipeId, options.Previous.Id, StringComparison.Ordinal))
        {
            issues.Error("recipe", PatchTexts.CheckIdChanged(recipeId, options.Previous.Id ?? string.Empty));
        }

        foreach (var property in options.UnknownProperties ?? Array.Empty<string>())
        {
            issues.Warning("recipe", PatchTexts.CheckUnknownProperty(property));
        }
    }

    private static void CheckColumns(PatchRecipe recipe, List<PatchColumn> columns, Issues issues)
    {
        var seenColumns = new HashSet<string>(StringComparer.Ordinal);
        foreach (var column in columns)
        {
            var name = column.Name ?? string.Empty;
            var where = "column " + name;
            if (RecipeEditing.ColumnNameProblem(recipe, name, name) is { } problem)
            {
                issues.Error(where, PatchTexts.CheckColumnName(name, problem));
            }
            else if (!seenColumns.Add(name))
            {
                issues.Error(where, PatchTexts.CheckColumnName(name, PatchTexts.ColumnNameTaken));
            }

            if (column.Lookup is { } lookup)
            {
                if (RecipePaths.IsMissing(lookup.Files))
                {
                    issues.Error(where, PatchTexts.CheckLookupFilesEmpty);
                }

                if (string.IsNullOrWhiteSpace(lookup.Match))
                {
                    issues.Error(where, PatchTexts.CheckLookupMatchEmpty);
                }
            }
        }

        foreach (var (column, files) in RecipePaths.UnsafeLookups(recipe))
        {
            issues.Error("column " + column, PatchTexts.UnsafePath(files));
        }
    }

    private static void CheckRows(List<PatchColumn> columns, List<PatchRow> rows, Issues issues)
    {
        var ownColumns = columns.Where(c => c.Lookup is null).Select(c => c.Name ?? string.Empty).ToHashSet(StringComparer.Ordinal);
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var key = row.Key ?? string.Empty;
            var where = "row " + key;
            if (key.Trim().Length == 0)
            {
                issues.Error(where, PatchTexts.CheckRowKeyEmpty);
            }
            else if (!seenKeys.Add(key))
            {
                issues.Error(where, PatchTexts.CheckRowKeyDuplicate(key));
            }

            foreach (var column in (row.Values ?? new Dictionary<string, string>()).Keys.Where(k => !ownColumns.Contains(k)))
            {
                issues.Error(where, PatchTexts.CheckRowUnknownColumn(column));
            }
        }
    }

    /// <param name="recipe">Mit vorhandenen Listen (Spalten, Zeilen, Regeln ohne null).</param>
    private static void CheckRules(PatchRecipe recipe, Issues issues)
    {
        var seenRules = new HashSet<string>(StringComparer.Ordinal);
        var columnNames = recipe.Columns.Select(c => c.Name ?? string.Empty).ToList();
        foreach (var rule in recipe.Rules)
        {
            var id = rule.Id ?? string.Empty;
            var where = "rule " + id;
            if (!RuleId.IsMatch(id))
            {
                issues.Error(where, PatchTexts.CheckRuleIdInvalid(id));
            }
            else if (!seenRules.Add(id))
            {
                issues.Error(where, PatchTexts.CheckRuleIdDuplicate(id));
            }

            var fields = RuleFields.Of(rule);
            CheckRequiredFields(rule, fields, where, issues);

            var regexBroken = false;
            if (rule is LineRule { MatchKind: MatchKind.Regex } lineRule && !string.IsNullOrWhiteSpace(lineRule.Match))
            {
                if (RegexProblem(lineRule.Match, columnNames) is { } message)
                {
                    regexBroken = true;
                    issues.Error(where, PatchTexts.CheckRegexInvalid("match", message));
                }
            }

            foreach (var field in fields.Where(f => f.Value is not null))
            {
                if (field.Value!.Split('\n').Any(line => line.Trim().StartsWith("# PMT-", StringComparison.Ordinal)))
                {
                    issues.Error(where, PatchTexts.CheckMarkerLine(field.Name));
                }

                foreach (var section in TemplateRenderer.SectionProblems(field.Value))
                {
                    issues.Error(where, PatchTexts.CheckSectionProblem(field.Name, section));
                }
            }

            foreach (var path in RecipePaths.UnsafePaths(rule))
            {
                issues.Error(where, PatchTexts.UnsafePath(path));
            }

            if (RecipePaths.TargetOf(rule) is { } target && !RecipePaths.IsUnsafe(target) && RecipePaths.IsNotSingleFile(target))
            {
                issues.Error(where, PatchTexts.TargetNotSingleFile(target));
            }

            if (!regexBroken)
            {
                foreach (var warning in RecipeEditing.RuleWarnings(recipe, rule))
                {
                    issues.Warning(where, warning);
                }
            }
        }
    }

    /// <summary>
    /// Pfade und Muster fehlen auch, wenn sie nur aus leeren Teilen bestehen (" ; "). Leere Dateien einer lines- oder blockEdit-Regel sind nur
    /// eine Warnung (Ruling 11): die Regel tut nichts, bis Dateien eingetragen sind (z. B. die Iterator-Regel der Vorlage).
    /// </summary>
    private static void CheckRequiredFields(PatchRule rule, IReadOnlyList<RuleField> fields, string where, Issues issues)
    {
        foreach (var field in fields.Where(f => f.Required && (f.IsPath ? RecipePaths.IsMissing(f.Value) : string.IsNullOrWhiteSpace(f.Value))))
        {
            if (field.Name == "files" && rule is LineRule or BlockEditRule)
            {
                issues.Warning(where, PatchTexts.CheckFilesEmpty);
            }
            else
            {
                issues.Error(where, PatchTexts.CheckFieldEmpty(field.Name));
            }
        }
    }

    /// <summary>Dateimuster, die in Spiel und Mod nichts treffen (nur mit mindestens einem dieser Ordner).</summary>
    private static void CheckPatternsMatch(List<PatchColumn> columns, List<PatchRule> rules, RecipeCheckOptions options, Issues issues)
    {
        if (string.IsNullOrWhiteSpace(options.GameRoot) && string.IsNullOrWhiteSpace(options.ModRoot))
        {
            return;
        }

        var roots = new[] { options.GameRoot, options.ModRoot }.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r!).ToList();
        foreach (var column in columns.Where(c => c.Lookup is not null))
        {
            if (MatchesNothing(roots, column.Lookup!.Files))
            {
                issues.Warning("column " + column.Name, PatchTexts.CheckGlobNoMatch(column.Lookup.Files));
            }
        }

        foreach (var rule in rules)
        {
            var pattern = RecipePaths.ReadPatternOf(rule);
            if (MatchesNothing(roots, pattern))
            {
                issues.Warning("rule " + rule.Id, PatchTexts.CheckGlobNoMatch(pattern!));
            }
        }
    }

    private sealed class Issues
    {
        public List<RecipeIssue> Items { get; } = new();

        public void Error(string where, string message) => Items.Add(new RecipeIssue(CheckSeverity.Error, where, message));

        public void Warning(string where, string message) => Items.Add(new RecipeIssue(CheckSeverity.Warning, where, message));
    }

    /// <summary>
    /// Ids der Regeln von <paramref name="previous"/>, die in <paramref name="recipe"/> fehlen, aber schon Ausgabe haben: ein Baustein im Gedächtnis
    /// (<c>Id|…</c>), Listeneinträge oder ein erzeugter Bereich (<c># PMT-Patch &lt;rezept&gt;/&lt;regel&gt; begin</c>) in einer Textdatei der Mod.
    /// </summary>
    public static IReadOnlyList<string> DroppedRulesWithOutput(PatchRecipe recipe, PatchRecipe previous, PatchMemory? previousMemory, string? modRoot)
    {
        var kept = (recipe.Rules ?? new List<PatchRule>()).Where(r => r is not null).Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var dropped = (previous.Rules ?? new List<PatchRule>()).Where(r => r is not null).Select(r => r.Id ?? string.Empty)
            .Where(id => !kept.Contains(id)).Distinct(StringComparer.Ordinal).ToList();
        var result = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in dropped)
        {
            var prefix = PatchMemory.UnitId(id, string.Empty);
            if (previousMemory is not null
                && (previousMemory.Units.Keys.Any(key => key.StartsWith(prefix, StringComparison.Ordinal))
                    || (previousMemory.ListEntries.TryGetValue(id, out var entries) && entries.Count > 0)))
            {
                result.Add(id);
            }
        }

        var open = dropped.Where(id => !result.Contains(id)).ToDictionary(id => GeneratedRegion.BeginMarker(previous.Id ?? string.Empty, id), id => id, StringComparer.Ordinal);
        if (open.Count > 0 && !string.IsNullOrWhiteSpace(modRoot) && Directory.Exists(modRoot))
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
            foreach (var file in Directory.EnumerateFiles(modRoot, "*", options).Where(f => RegionExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)))
            {
                if (open.Count == 0)
                {
                    break;
                }

                try
                {
                    foreach (var line in File.ReadLines(file))
                    {
                        if (line.Length > 0 && open.Remove(line.Trim(), out var id))
                        {
                            result.Add(id);
                        }
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        return dropped.Where(result.Contains).ToList();
    }

    /// <summary>Null, wenn der Ausdruck (mit eingesetzten Platzhaltern) übersetzt werden kann.</summary>
    private static string? RegexProblem(string pattern, IEnumerable<string> columnNames)
    {
        var values = columnNames.Distinct(StringComparer.Ordinal).ToDictionary(n => n, _ => "v", StringComparer.Ordinal);
        values["key"] = "k";
        // Zweite Einsetzung wie in LineRuleValidation (nur {key} leer), damit RuleWarnings das Muster sicher übersetzen kann.
        var emptyKey = new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = string.Empty };
        try
        {
            _ = new Regex(TemplateRenderer.Render(pattern, values), RegexOptions.CultureInvariant);
            _ = new Regex(TemplateRenderer.Render(pattern, emptyKey), RegexOptions.CultureInvariant);
            return null;
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }
    }

    private static bool MatchesNothing(IReadOnlyList<string> roots, string? pattern)
    {
        if (RecipePaths.IsMissing(pattern) || pattern!.Contains('{') || RecipePaths.IsUnsafe(pattern))
        {
            return false;
        }

        return !roots.Any(root => Directory.Exists(root) && GlobMatcher.Enumerate(root, pattern).Any());
    }
}
