using System.IO;
using System.Windows;
using ParadoxModTool.Patches;
using ParadoxModTool.Patches.Ai;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// "Rezept mit KI erstellen" (Spec 2026-10-12-patch-recipes-ai-guide, Abschnitt 2): Auftrag aus Anleitung, Beispielrezept, Mod- und Spieldateien und
/// Beschreibung; die Antwort (Einfügen oder Ordner) wird geprüft, bei der gewählten Mod gespeichert und im Fenster Patch-Rezepte mit Erklärung und
/// Vorschau gezeigt. In die Mod schreibt erst "Anwenden" dort. Spielordner ist überall genau der, den das Rezepte-Fenster dem Motor gibt.
/// </summary>
internal sealed class RecipeAiFlavor : IAiTaskFlavor
{
    private static readonly string GuideFolder = Path.Combine(AppContext.BaseDirectory, "ai-guide", "patch-recipes");
    private static readonly string CliPath = Path.Combine(AppContext.BaseDirectory, "pmt-patch.exe");

    private readonly PatchRecipesWindow _window;
    private readonly PatchWindowHost _host;

    public RecipeAiFlavor(PatchRecipesWindow window, PatchWindowHost host)
    {
        _window = window;
        _host = host;
    }

    /// <summary>
    /// Fensterdaten aus dem Rezepte-Fenster: vorgewählt ist dessen Mod (dann geht "Offenes Rezept mitgeben" sofort); sie steht auch in der Auswahl,
    /// wenn sie nicht unter den bekannten Mods ist. Der Spielordner fragt nie nach: ohne ihn gibt es einfach keine Spieldateien.
    /// </summary>
    public static AiTaskHost Host(PatchRecipesWindow window, PatchWindowHost host)
    {
        IReadOnlyList<(string Name, string Path)> Mods()
        {
            var mods = host.KnownMods().ToList();
            if (window.ModRoot is { } current && !mods.Any(m => SamePath(m.Path, current)))
            {
                mods.Add((Path.GetFileName(current.TrimEnd('\\', '/')), current));
            }

            return mods;
        }

        return new AiTaskHost(host.GameId() ?? string.Empty, Mods, () => window.ModRoot ?? host.ActiveModRoot(), host.VanillaRoot) { GameRootMayAsk = false };
    }

    public string Title => PatchRecipeTexts.AiTitle;

    public string TaskIntro => PatchRecipeTexts.AiTaskIntro;

    public string ResultIntro => PatchRecipeTexts.AiResultIntro;

    public string DescriptionTip => PatchRecipeTexts.AiDescriptionTip;

    public string WithOpenText => PatchRecipeTexts.AiWithRecipe;

    public string WithOpenTip => PatchRecipeTexts.AiWithRecipeTip;

    public string SaveFileName => "pmt-recipe-task.md";

    public string ModTip => PatchRecipeTexts.AiModTip;

    public string ChooseModMessage => PatchRecipeTexts.AiChooseModMessage;

    public string FilesTip => PatchRecipeTexts.AiFilesTip;

    public string CopyTextTip => PatchRecipeTexts.AiCopyTextTip;

    public string SaveFolderTip => PatchRecipeTexts.AiSaveFolderTip;

    public string AnswerTip => PatchRecipeTexts.AiAnswerTip;

    public string ApplyTip => PatchRecipeTexts.AiApplyTip;

    public string ImportFolderTip => PatchRecipeTexts.AiImportFolderTip;

    public string SnippetsIntro => PatchRecipeTexts.AiSnippetsIntro;

    public string SnippetWholeFileTip => PatchRecipeTexts.AiSnippetWholeFileTip;

    public string SnippetExcerptTip => PatchRecipeTexts.AiSnippetExcerptTip;

    public string SnippetNoteTip => PatchRecipeTexts.AiSnippetNoteTip;

    public string NoGameFolder => PatchRecipeTexts.NoGameFolder;

    public bool OffersGameFiles => true;

    /// <summary>Nur das im Rezepte-Fenster gewählte Rezept, und nur für dessen Mod (Rezepte gehören zu einer Mod).</summary>
    public bool CanHandOver(string? modRoot) => _window.Recipe is not null && modRoot is not null && SamePath(modRoot, _window.ModRoot);

    public void ShowHelp(Window owner) => PatchRecipeHelpWindow.Show(owner, "patchhelp.ai");

    public string BuildText(AiTaskInput input) => RecipeTaskBuilder.BuildText(Request(input, forFolder: false), GuideFolder);

    public void BuildFolder(AiTaskInput input, string targetFolder) => RecipeTaskBuilder.BuildFolder(Request(input, forFolder: true), GuideFolder, targetFolder);

    public string Feedback(IEnumerable<string> messages) => RecipeTaskBuilder.Feedback(messages);

    public Task<AiTaskOutcome> ApplyAnswerAsync(Window owner, string answer, string? modRoot, bool withOpen) =>
        ImportAsync(owner, RecipeAnswerReader.FromText(answer), modRoot, withOpen);

    public Task<AiTaskOutcome> ImportFolderAsync(Window owner, string folder, string? modRoot, bool withOpen) =>
        ImportAsync(owner, RecipeAnswerReader.FromFolder(folder), modRoot, withOpen);

    private RecipeTaskRequest Request(AiTaskInput input, bool forFolder)
    {
        var open = input.WithOpenItem && CanHandOver(input.ModRoot) ? _window.Recipe : null;
        var memory = open is null ? null : PatchRecipeStore.MemoryPath(_window.RecipesFolderOfCurrentMod(), open.Id);
        return new RecipeTaskRequest(
            input.Description,
            _host.GameId() ?? string.Empty,
            input.ModRoot,
            input.GameRoot,
            input.ModFiles,
            input.GameFiles,
            open is null ? null : PatchRecipeStore.Clone(open),
            memory,
            forFolder && File.Exists(CliPath) ? CliPath : null)
        {
            Snippets = input.Snippets,
        };
    }

    /// <summary>
    /// Antwort lesen, prüfen, Kennung klären, entfallene Regeln bestätigen lassen, speichern, dann Erklärung und Vorschau im Rezepte-Fenster.
    /// Vor dem Speichern bleibt alles unverändert; Abbrechen speichert nichts.
    /// </summary>
    private async Task<AiTaskOutcome> ImportAsync(Window owner, RecipeAnswer answer, string? modRoot, bool withOpen)
    {
        if (answer.Recipe is null)
        {
            return Problems(answer.Problems);
        }

        if (modRoot is null)
        {
            return Problems(new[] { PatchRecipeTexts.AiNoMod });
        }

        // Mitgegeben ist, was beim Übernehmen gilt: Häkchen gesetzt und ein Rezept derselben Mod im Rezepte-Fenster gewählt (vor SelectMod
        // bestimmen). Nur dann schlägt der Import "Ersetzen" vor; so verwaist kein erzeugter Bereich durch eine umbenannte Kennung, auch nicht
        // in einem neuen KI-Fenster, und ein ganz neues Rezept übernimmt nie die Kennung eines anderen. Trägt die Antwort die Kennung eines anderen
        // Rezepts dieser Mod, ist dieses das offene (Ruling 12); nur eine unbekannte Kennung wird auf die des gewählten zurückgesetzt.
        var open = withOpen && CanHandOver(modRoot) ? RecipeImport.HandedOver(answer.Recipe, _window.Recipe, _window.FindRecipe) : null;

        // Die Mod des KI-Fensters bekommt das Rezept; das Rezepte-Fenster wechselt dorthin (SelectMod speichert vorher), sonst nur Ausstehendes speichern.
        if (!SamePath(modRoot, _window.ModRoot))
        {
            _window.SelectMod(modRoot);
        }
        else
        {
            _window.SaveNow();
        }

        var folder = _window.RecipesFolderOfCurrentMod();
        var ids = _window.RecipeIdsOfCurrentMod();
        var openMemory = open is null ? null : PatchRecipeStore.LoadMemory(folder, open.Id);
        var plan = RecipeImport.Prepare(answer.Recipe, _host.GameId() ?? string.Empty, ids, open, openMemory, modRoot, _host.VanillaRoot(), answer.UnknownProperties);

        var issues = plan.Issues.Select(Issue).ToList();
        if (plan.HasErrors)
        {
            var stopped = new List<string> { PatchRecipeTexts.AiStoppedErrors };
            stopped.AddRange(issues);
            return new AiTaskOutcome(false, stopped, string.Empty) { Problem = true, Feedback = issues };
        }

        var notes = new List<string>();
        var feedback = new List<string>(issues);
        if (plan.IdReset)
        {
            var reset = PatchRecipeTexts.AiIdReset(plan.Recipe.Id);
            notes.Add(reset);
            feedback.Add(reset);
        }

        var recipe = plan.Recipe;
        PatchRecipe? replaced = null;
        if (plan.IdConflict)
        {
            var existing = _window.FindRecipe(recipe.Id);
            var choice = MessageBox.Show(owner, PatchRecipeTexts.AiIdConflict(existing?.Name ?? recipe.Id, recipe.Id), Title,
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question, open is not null ? MessageBoxResult.Yes : MessageBoxResult.No);
            if (choice == MessageBoxResult.Cancel || choice == MessageBoxResult.None)
            {
                return new AiTaskOutcome(false, new List<string> { PatchRecipeTexts.AiNotSaved }, string.Empty);
            }

            if (choice == MessageBoxResult.No)
            {
                // Neues Rezept neben dem alten: die Vorlage gehört nur zum ersetzten Rezept
                recipe = RecipeImport.WithNewId(recipe, ids);
                recipe.Preset = null;
                notes.Add(PatchRecipeTexts.AiNewId(recipe.Id));
            }
            else
            {
                replaced = existing;
            }
        }

        // Ersetzen: Regeln des alten Rezepts, die fehlen und schon Text in der Mod erzeugt haben, verwaisen dort
        if (replaced is not null)
        {
            var dropped = open is not null && string.Equals(open.Id, replaced.Id, StringComparison.OrdinalIgnoreCase)
                ? plan.DroppedRules
                : RecipeChecker.DroppedRulesWithOutput(recipe, replaced, PatchRecipeStore.LoadMemory(folder, replaced.Id), modRoot);
            if (dropped.Count > 0)
            {
                var list = string.Join("\n", dropped.Select(id => "• " + DroppedRule(replaced, id)));
                var line = PatchRecipeTexts.AiDropped(string.Join(", ", dropped.Select(id => DroppedRule(replaced, id))));
                if (MessageBox.Show(owner, PatchRecipeTexts.AiDroppedConfirm(list), Title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                {
                    // Die KI soll alles erfahren, was an der Antwort nicht passte: Prüfwarnungen, zurückgesetzte Kennung und die entfallenen Regeln
                    var cancelled = new List<string> { PatchRecipeTexts.AiNotSaved };
                    cancelled.AddRange(notes);
                    cancelled.AddRange(issues);
                    cancelled.Add(line);
                    feedback.Add(line);
                    return new AiTaskOutcome(false, cancelled, string.Empty) { Problem = true, Feedback = feedback };
                }

                notes.Add(line);
                feedback.Add(line);
            }
        }

        // Rückfragen pumpen Nachrichten: eine Bearbeitung im Rezepte-Fenster darf das Gespeicherte danach nicht überschreiben
        _window.SaveNow();
        try
        {
            PatchRecipeStore.Save(folder, recipe);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Problems(new[] { PatchRecipeTexts.SaveFailed(ex.Message) });
        }

        var lines = new List<string> { PatchRecipeTexts.AiSaved(recipe.Name) };
        lines.AddRange(notes);
        lines.AddRange(issues);
        var planned = await _window.ShowImportedAsync(recipe.Id);
        if (planned is null)
        {
            lines.Add(PatchRecipeTexts.AiNoPlan(_window.StatusLine is { Length: > 0 } reason ? reason : _window.ContextMissingText));
        }
        else
        {
            lines.Add(PatchRecipeTexts.AiPlanReady(planned.Files.Count(f => f.Status is PlannedFileStatus.New or PlannedFileStatus.Changed or PlannedFileStatus.Conflict)));
            var findings = planned.Skipped.Select(s => PatchRecipeTexts.SkippedLine(s.RuleId, s.RowKey, s.Reason))
                .Concat(planned.Warnings)
                .Concat(planned.ConflictUnits.Select(c => PatchRecipeTexts.AiPlanConflict(c.File, c.UnitKey, string.Join("; ", c.Messages))))
                .ToList();
            lines.AddRange(findings);
            feedback.AddRange(findings);
        }

        // Wie beim Paket: gibt es etwas für die KI zu korrigieren, sind alle Zeilen rot und "Fehler für die KI kopieren" geht
        return new AiTaskOutcome(true, lines, PatchRecipeTexts.AiStatusHint) { Problem = feedback.Count > 0, Feedback = feedback };
    }

    private static string Issue(RecipeIssue issue) => issue.Severity == CheckSeverity.Error
        ? PatchRecipeTexts.AiIssueError(issue.Where, issue.Message)
        : PatchRecipeTexts.AiIssueWarning(issue.Where, issue.Message);

    /// <summary>Eine entfallene Regel in Worten: Name und Kennung aus dem alten Rezept.</summary>
    private static string DroppedRule(PatchRecipe previous, string id)
    {
        var name = previous.Rules.FirstOrDefault(r => r?.Id == id)?.Name;
        return PatchRecipeTexts.AiDroppedRule(string.IsNullOrWhiteSpace(name) ? id : name, id);
    }

    private static AiTaskOutcome Problems(IReadOnlyList<string> lines) => new(false, lines, string.Empty) { Problem = true };

    private static bool SamePath(string? a, string? b)
    {
        if (a is null || b is null)
        {
            return false;
        }

        try
        {
            return string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
