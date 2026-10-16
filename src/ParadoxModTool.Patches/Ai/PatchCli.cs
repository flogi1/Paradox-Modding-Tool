using System.Text.Json;

namespace ParadoxModTool.Patches.Ai;

/// <summary>Ergebnis eines Befehls von pmt-patch.exe: Rückgabewert (0 in Ordnung, 1 Befunde, 2 falscher Aufruf) und Ausgabezeilen.</summary>
public sealed record PatchCliResult(int ExitCode, IReadOnlyList<string> Lines);

/// <summary>
/// Befehle von pmt-patch.exe (Spec 2026-10-12-patch-recipes-ai-guide, Abschnitt 5): <c>check</c> prüft ein Rezept, <c>plan</c> rechnet es als Probelauf
/// gegen Spiel und Mod. Die Ausgabe ist für KIs gedacht: feste Zeilenformen, Englisch. Schreibt nie in Mod, Spiel oder Rezeptordner
/// (nur <c>--keep</c> schreibt, in einen eigenen Ordner). <c>replace_path</c> und die Spielversion liest die Kommandozeile nicht.
/// </summary>
public static class PatchCli
{
    public static readonly IReadOnlyList<string> Usage = new[]
    {
        "usage:",
        "  pmt-patch check <recipe.json> [--game <folder>] [--mod <folder>] [--previous <recipe.json>]",
        "  pmt-patch plan <recipe.json> --game <folder> --mod <folder> [--rule <id>] [--row <key>] [--diff] [--keep <folder>]",
    };

    private static readonly PatchCliResult UsageError = new(2, Usage);

    public static PatchCliResult Run(IReadOnlyList<string> args)
    {
        if (args.Count < 2)
        {
            return UsageError;
        }

        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var diff = false;
        for (var i = 2; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--diff" when args[0] == "plan":
                    diff = true;
                    break;
                case "--game" or "--mod" or "--previous" or "--rule" or "--row" or "--keep" when i + 1 < args.Count && !options.ContainsKey(args[i]):
                    options[args[i]] = args[++i];
                    break;
                default:
                    return UsageError;
            }
        }

        string? Option(string name) => options.TryGetValue(name, out var value) ? value : null;
        switch (args[0])
        {
            case "check" when options.Keys.All(k => k is "--game" or "--mod" or "--previous"):
                return Check(args[1], Option("--game"), Option("--mod"), Option("--previous"));
            case "plan" when Option("--game") is { } game && Option("--mod") is { } mod && options.Keys.All(k => k is not "--previous"):
                return Plan(args[1], game, mod, Option("--rule"), Option("--row"), diff, Option("--keep"));
            default:
                return UsageError;
        }
    }

    /// <summary>
    /// Strukturprüfung. Je Befund eine Zeile <c>Ort: Meldung</c> (Warnungen mit <c>warning: </c> davor), dann <c>OK</c> oder <c>N issue(s)</c>;
    /// Rückgabe 1 nur bei einem Fehler. Mit Spiel- und Mod-Ordner prüft sie auch Dateimuster, mit <paramref name="previousPath"/> auch entfallene Regeln.
    /// </summary>
    public static PatchCliResult Check(string recipePath, string? gameRoot, string? modRoot, string? previousPath) =>
        Guard(() => CheckInner(recipePath, gameRoot, modRoot, previousPath));

    private static PatchCliResult CheckInner(string recipePath, string? gameRoot, string? modRoot, string? previousPath)
    {
        if (MissingFolder(gameRoot, modRoot) is { } missing)
        {
            return missing;
        }

        var (_, issues, failed) = CheckRecipe(recipePath, gameRoot, modRoot, previousPath);
        if (failed is not null)
        {
            return failed;
        }

        return new PatchCliResult(HasError(issues) ? 1 : 0, IssueLines(issues));
    }

    /// <summary>
    /// Probelauf wie im Fenster (<see cref="PatchRunner.Plan"/>): erst <see cref="Check"/> (Fehler beenden den Lauf), dann Tabelle, Dateien (mit
    /// <paramref name="diff"/> samt Unified-Diff), übersprungene Zeilen, Warnungen und eine Schlusszeile. Rückgabe 0 nur ohne übersprungene Zeilen,
    /// Konflikte und Zeilenfehler. <paramref name="keepFolder"/>: neue und geänderte Dateien dorthin schreiben.
    /// </summary>
    public static PatchCliResult Plan(string recipePath, string gameRoot, string modRoot, string? ruleId, string? rowKey, bool diff, string? keepFolder) =>
        Guard(() => PlanInner(recipePath, gameRoot, modRoot, ruleId, rowKey, diff, keepFolder));

    private static PatchCliResult PlanInner(string recipePath, string gameRoot, string modRoot, string? ruleId, string? rowKey, bool diff, string? keepFolder)
    {
        if (MissingFolder(gameRoot, modRoot) is { } missing)
        {
            return missing;
        }

        var (recipe, issues, failed) = CheckRecipe(recipePath, gameRoot, modRoot, null);
        if (failed is not null)
        {
            return failed;
        }

        if (HasError(issues))
        {
            return new PatchCliResult(1, IssueLines(issues));
        }

        if (ruleId is not null && !recipe!.Rules.Any(r => r.Id == ruleId))
        {
            return One(1, $"rule {ruleId}: not found in the recipe");
        }

        if (rowKey is not null && !recipe!.Rows.Any(r => r.Key == rowKey))
        {
            return One(1, $"row {rowKey}: not found in the recipe");
        }

        // --keep schreibt nur in einen eigenen Ordner: nie in Mod, Spiel oder den Ordner des Rezepts (dort könnten recipe.json oder das Gedächtnis liegen).
        if (keepFolder is not null && (IsInside(modRoot, keepFolder) || IsInside(gameRoot, keepFolder) || IsInside(FolderOf(recipePath), keepFolder)))
        {
            return One(1, "--keep: the folder must not lie inside the mod, game or recipe folder");
        }

        var narrowed = Narrow(recipe!, ruleId, rowKey);
        var context = new PatchContext(modRoot, gameRoot, null, null, RecipesFolder: FolderOf(recipePath));
        var table = ColumnResolver.Resolve(narrowed, new PatchWorkspace(modRoot, gameRoot));
        var plan = PatchRunner.Plan(context, narrowed);
        return Report(narrowed, table, plan, issues, diff, keepFolder);
    }

    /// <summary>
    /// Fängt jede Ausnahme eines Befehls: eine lesbare Zeile (Meldung ohne Stapelverfolgung), Rückgabe 1. So liegt der Rückgabewert immer in 0 bis 2,
    /// auch bei unerwarteten Fehlern (ungültige Pfade, gesperrte Dateien, Fehler in der Engine).
    /// </summary>
    private static PatchCliResult Guard(Func<PatchCliResult> command)
    {
        try
        {
            return command();
        }
        catch (Exception ex)
        {
            return One(1, "error: " + Flat(ex.Message));
        }
    }

    private static PatchCliResult Report(PatchRecipe recipe, ResolvedTable table, PatchPlan plan, IReadOnlyList<RecipeIssue> issues, bool diff, string? keepFolder)
    {
        var rows = recipe.OnlyRowKey is { } only ? table.Rows.Where(r => r.Key == only).ToList() : table.Rows.ToList();
        var lines = new List<string> { $"table: {rows.Count} row(s)" };
        foreach (var row in rows)
        {
            var values = string.Join(", ", recipe.Columns.Select(c => $"{c.Name}={Flat(row.Values.GetValueOrDefault(c.Name, string.Empty))}"));
            lines.Add(values.Length == 0 ? $"  {row.Key}:" : $"  {row.Key}: {values}");
            lines.AddRange(row.Errors.Select(e => $"  {row.Key}: ERROR {Flat(e)}"));
        }

        var shown = plan.Files.Where(f => f.Status != PlannedFileStatus.Unchanged).ToList();
        if (shown.Count > 0)
        {
            lines.Add("files:");
            foreach (var file in shown)
            {
                var status = file.Status.ToString().ToLowerInvariant();
                lines.Add(file.Status == PlannedFileStatus.Conflict
                    ? $"  {status} {file.RelativePath}: {string.Join("; ", file.Conflicts.Select(Flat))}"
                    : $"  {status} {file.RelativePath}");
            }

            if (diff)
            {
                foreach (var file in shown.Where(f => f.Status is PlannedFileStatus.New or PlannedFileStatus.Changed))
                {
                    lines.AddRange(UnifiedDiff.Build(file.RelativePath, file.OriginalText, file.NewText));
                }
            }
        }

        if (plan.Skipped.Count > 0)
        {
            lines.Add("skipped:");
            lines.AddRange(plan.Skipped.Select(s => $"  {s.RuleId} {s.RowKey}: {Flat(s.Reason)}"));
        }

        var warnings = plan.Warnings
            .Concat(issues.Where(i => i.Severity == CheckSeverity.Warning).Select(i => $"{i.Where}: {i.Message}"))
            .Select(Flat)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (warnings.Count > 0)
        {
            lines.Add("warnings:");
            lines.AddRange(warnings.Select(w => "  " + w));
        }

        if (keepFolder is not null)
        {
            foreach (var file in shown.Where(f => f.Status is PlannedFileStatus.New or PlannedFileStatus.Changed))
            {
                Keep(keepFolder, file);
            }
        }

        var changed = plan.Files.Count(f => f.Status is PlannedFileStatus.New or PlannedFileStatus.Changed);
        var conflicts = plan.Files.Count(f => f.Status == PlannedFileStatus.Conflict);
        var rowErrors = rows.Count(r => r.HasErrors);
        lines.Add($"{changed} file(s) changed, {plan.Skipped.Count} skipped, {conflicts} conflict(s), {rowErrors} row error(s)");
        return new PatchCliResult(plan.Skipped.Count + conflicts + rowErrors == 0 ? 0 : 1, lines);
    }

    /// <summary>Kopie mit nur der gewählten Regel (aktiv) und/oder nur der gewählten Zeile; ohne Angabe das Rezept selbst.</summary>
    private static PatchRecipe Narrow(PatchRecipe recipe, string? ruleId, string? rowKey)
    {
        if (ruleId is not null && rowKey is not null)
        {
            return RecipeEditing.SingleRulePreview(recipe, ruleId, rowKey);
        }

        if (ruleId is null && rowKey is null)
        {
            return recipe;
        }

        var copy = PatchRecipeStore.Clone(recipe);
        if (rowKey is not null)
        {
            copy.OnlyRowKey = rowKey;
        }
        else
        {
            foreach (var rule in copy.Rules)
            {
                rule.Enabled = rule.Id == ruleId;
            }
        }

        return copy;
    }

    private static void Keep(string folder, PlannedFile file)
    {
        if (!PathSafety.IsSafeRelative(file.RelativePath) || !PathSafety.IsInside(folder, file.RelativePath))
        {
            return;
        }

        var path = Path.Combine(folder, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, file.NewText, TextFile.EncodingFor(file.HasBom, file.EncodingName));
    }

    /// <summary>Liest und prüft das Rezept; <c>Failed</c> ist gesetzt, wenn eine Datei fehlt oder das JSON nicht lesbar ist (eine Zeile, Rückgabe 1).</summary>
    private static (PatchRecipe? Recipe, IReadOnlyList<RecipeIssue> Issues, PatchCliResult? Failed) CheckRecipe(string recipePath, string? gameRoot, string? modRoot, string? previousPath)
    {
        var none = Array.Empty<RecipeIssue>();
        if (ReadFile(recipePath) is not { } text)
        {
            return (null, none, One(1, $"{recipePath}: cannot read the recipe file"));
        }

        var answer = RecipeAnswerReader.FromJson(text);
        if (answer.Recipe is null)
        {
            return (null, none, new PatchCliResult(1, answer.Problems));
        }

        PatchRecipe? previous = null;
        PatchMemory? previousMemory = null;
        if (previousPath is not null)
        {
            if (ReadFile(previousPath) is not { } previousText)
            {
                return (null, none, One(1, $"{previousPath}: cannot read the previous recipe file"));
            }

            try
            {
                previous = PatchRecipeStore.Deserialize(previousText);
            }
            catch (JsonException ex)
            {
                return (null, none, One(1, $"{previousPath}: {ex.Message}"));
            }

            var id = previous.Id ?? string.Empty;
            if (id.Length > 0 && id.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
            {
                previousMemory = PatchRecipeStore.LoadMemory(FolderOf(previousPath), id);
            }
        }

        var issues = RecipeChecker.Check(answer.Recipe, new RecipeCheckOptions(
            NullIfEmpty(gameRoot), NullIfEmpty(modRoot), previous, previousMemory, answer.UnknownProperties));
        return (answer.Recipe, issues, null);
    }

    /// <summary>Ein angegebener Spiel- oder Mod-Ordner, den es nicht gibt: eine Zeile, Rückgabe 1 (ein Tippfehler darf nicht als "alles in Ordnung" durchgehen).</summary>
    private static PatchCliResult? MissingFolder(string? gameRoot, string? modRoot)
    {
        if (NullIfEmpty(gameRoot) is { } game && !Directory.Exists(game))
        {
            return One(1, $"--game: folder not found: {game}");
        }

        if (NullIfEmpty(modRoot) is { } mod && !Directory.Exists(mod))
        {
            return One(1, $"--mod: folder not found: {mod}");
        }

        return null;
    }

    private static string? ReadFile(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> IssueLines(IReadOnlyList<RecipeIssue> issues)
    {
        var lines = issues.Select(i => (i.Severity == CheckSeverity.Warning ? "warning: " : string.Empty) + $"{i.Where}: {Flat(i.Message)}").ToList();
        lines.Add(issues.Count == 0 ? "OK" : $"{issues.Count} issue(s)");
        return lines;
    }

    private static bool HasError(IReadOnlyList<RecipeIssue> issues) => issues.Any(i => i.Severity == CheckSeverity.Error);

    private static string FolderOf(string path) => Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";

    private static bool IsInside(string root, string folder)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return full.Equals(fullRoot, comparison) || full.StartsWith(fullRoot + Path.DirectorySeparatorChar, comparison);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static PatchCliResult One(int exitCode, string line) => new(exitCode, new[] { line });

    /// <summary>Eine Ausgabezeile darf keinen Zeilenumbruch enthalten.</summary>
    private static string Flat(string text) => text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
}
