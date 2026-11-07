using System.IO;
using System.Windows;
using ParadoxModTool.Performance;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// "Mit KI analysieren" im Reiter Auswertung des Performance-Checkers (Plan 4): Auftrag aus Anleitung, Messlauf, Hotspots, Hinweisen und Codeblöcken
/// um die Hotspots, dazu angehakte Dateien und die Beschreibung des Modders. Die Antwort ist Text (wird als <see cref="PerformanceRunStore.AdviceFileName"/>
/// beim Lauf gespeichert) und optional ein Rezept, das nach Rückfrage an die Patch-Rezepte geht. In die Mod schreibt dieser Weg nichts.
/// <para>
/// Lauf des Auftrags: der zuletzt für "Auftrag kopieren", "Als Datei" oder "Als Ordner" verwendete Lauf; vorher der gewählte. So landet der Rat bei
/// dem Lauf, aus dem der Auftrag stammt, auch wenn im Performance-Fenster inzwischen ein anderer gewählt ist.
/// </para>
/// </summary>
internal sealed class PerformanceAiFlavor : IAiTaskFlavor
{
    /// <summary>So viele Zeilen des Rats zeigt die Meldungsliste; der Rest steht im gespeicherten Rat.</summary>
    private const int ShownAdviceLines = 60;

    private const string AnswerFile = "answer.md";

    private static readonly string GuideFolder = Path.Combine(AppContext.BaseDirectory, "ai-guide", "performance");
    private static readonly string RecipeGuideFolder = Path.Combine(AppContext.BaseDirectory, "ai-guide", "patch-recipes");

    /// <summary>Dateien, die <see cref="PerformanceTaskBuilder.BuildFolder"/> selbst in den Auftragsordner schreibt; keine Antwort.</summary>
    private static readonly HashSet<string> TaskFiles = new(StringComparer.OrdinalIgnoreCase) { "SKILL.md", "reference.md", "task.md", "snippets.md", "recipe-format.md" };

    private readonly PerformanceWindow _owner;
    private readonly PerformanceService _service;
    private readonly Func<string, string?, Task<IReadOnlyList<string>>> _importRecipe;
    private PerformanceRun? _taskRun;

    /// <param name="service">Dienst des Performance-Fensters; Sitzung, Auswahl und Speicher werden bei jedem Aufruf neu gelesen (die Sitzung wechselt mit der aktiven Mod).</param>
    /// <param name="importRecipe">Antworttext und Mod-Ordner an die Patch-Rezepte; gibt deren Meldungen zurück.</param>
    public PerformanceAiFlavor(PerformanceWindow owner, PerformanceService service, Func<string, string?, Task<IReadOnlyList<string>>> importRecipe)
    {
        _owner = owner;
        _service = service;
        _importRecipe = importRecipe;
    }

    public string Title => PerformanceTexts.AiTitle;

    public string TaskIntro => PerformanceTexts.AiTaskIntro;

    public string ResultIntro => PerformanceTexts.AiResultIntro;

    public string DescriptionTip => PerformanceTexts.AiDescriptionTip;

    public string WithOpenText => PerformanceTexts.AiWithRecipe;

    public string WithOpenTip => PerformanceTexts.AiWithRecipeTip;

    public string SaveFileName => "pmt-performance-task.md";

    public string ModTip => PerformanceTexts.AiModTip;

    public string ChooseModMessage => PerformanceTexts.AiChooseModMessage;

    public string FilesTip => PerformanceTexts.AiFilesTip;

    public string CopyTextTip => PerformanceTexts.AiCopyTextTip;

    public string SaveFolderTip => PerformanceTexts.AiSaveFolderTip;

    public string AnswerTip => PerformanceTexts.AiAnswerTip;

    public string ApplyTip => PerformanceTexts.AiApplyTip;

    public string ImportFolderTip => PerformanceTexts.AiImportFolderTip;

    public string SnippetsIntro => PerformanceTexts.AiSnippetsIntro;

    public string SnippetWholeFileTip => PerformanceTexts.AiSnippetWholeFileTip;

    public string SnippetExcerptTip => PerformanceTexts.AiSnippetExcerptTip;

    public string SnippetNoteTip => PerformanceTexts.AiSnippetNoteTip;

    public string NoGameFolder => PatchRecipeTexts.NoGameFolder;

    public bool OffersGameFiles => true;

    /// <summary>Der Haken heißt hier "Rezeptformat beilegen": möglich, sobald die Rezept-Anleitung da ist (unabhängig von der Mod).</summary>
    public bool CanHandOver(string? modRoot) => Directory.Exists(RecipeGuideFolder);

    public void ShowHelp(Window owner) => PerformanceHelpWindow.Show(owner, PerformanceHelpTexts.SectionAi);

    public string BuildText(AiTaskInput input) => PerformanceTaskBuilder.BuildText(Request(input), GuideFolder, RecipeGuideFolder);

    public void BuildFolder(AiTaskInput input, string targetFolder) => PerformanceTaskBuilder.BuildFolder(Request(input), GuideFolder, RecipeGuideFolder, targetFolder);

    public string Feedback(IEnumerable<string> messages) => PerformanceTaskBuilder.Feedback(messages);

    public Task<AiTaskOutcome> ApplyAnswerAsync(Window owner, string answer, string? modRoot, bool withOpen) => ApplyAsync(owner, answer ?? string.Empty, modRoot);

    /// <summary>Liest <c>answer.md</c> aus dem Ordner; fehlt sie, die einzige andere <c>.md</c>- oder <c>.txt</c>-Datei, die nicht zum Auftrag gehört.</summary>
    public Task<AiTaskOutcome> ImportFolderAsync(Window owner, string folder, string? modRoot, bool withOpen)
    {
        string text;
        try
        {
            if (AnswerPath(folder) is not { } path)
            {
                return Task.FromResult(Problems(new[] { PerformanceTexts.AiNoAnswerFile(folder) }));
            }

            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(Problems(new[] { ex.Message }));
        }

        return ApplyAsync(owner, text, modRoot);
    }

    private static string? AnswerPath(string folder)
    {
        var answer = Path.Combine(folder, AnswerFile);
        if (File.Exists(answer))
        {
            return answer;
        }

        var candidates = Directory.EnumerateFiles(folder)
            .Where(f => (f.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                && !TaskFiles.Contains(Path.GetFileName(f)))
            .Take(2)
            .ToList();
        return candidates.Count == 1 ? candidates[0] : null;
    }

    /// <summary>
    /// Der Auftrag zum gewählten Lauf, so wie er jetzt ist: Entwurf mit dem Dateistand von jetzt, Codeblöcke aus den aktuellen Moddateien. Merkt den
    /// Lauf für das Übernehmen der Antwort.
    /// </summary>
    private PerformanceTaskRequest Request(AiTaskInput input)
    {
        if (_service.Session is not { Selected: { } run, SelectedSummary: { } summary, SelectedDuration: { } duration, SelectedClassifier: { } classifier } session)
        {
            throw new InvalidOperationException(PerformanceTexts.AiNoRun);
        }

        // Der Fingerabdruck liest alle Moddateien: Sanduhr wie beim Auswerten
        AiTaskDraft? draft = null;
        PerformanceWindow.WithWaitCursor(() =>
        {
            var hash = PerformanceService.HashOf(run.ModRoot);
            draft = PerformanceAiTask.Build(run, summary, duration, session.SelectedHints, session.SelectedHintsNote, classifier, hash, new AiTaskLimits(), path => ReadModFile(classifier, path));
        });
        _taskRun = run;
        return new PerformanceTaskRequest(draft!, input.Description, run.GameId, input.ModRoot, input.GameRoot, input.ModFiles, input.GameFiles, input.Snippets, input.WithOpenItem);
    }

    /// <summary>Text der Moddatei zu einem Dump-Pfad; <c>null</c>, wenn sie nicht in der Mod liegt oder nicht lesbar ist.</summary>
    private static string? ReadModFile(OriginClassifier classifier, string dumpPath)
    {
        try
        {
            return classifier.ResolveModFile(dumpPath) is { } path ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Rat beim Lauf speichern und anzeigen (die ersten <see cref="ShownAdviceLines"/> Zeilen); ein Rezept nur nach Rückfrage an die Patch-Rezepte.
    /// Probleme beim Lesen der Dateiblöcke färben die Meldungen rot und gehen in "Fehler für die KI kopieren".
    /// </summary>
    private async Task<AiTaskOutcome> ApplyAsync(Window owner, string text, string? modRoot)
    {
        var answer = PerformanceAnswerReader.Read(text);
        var hasRecipe = !string.IsNullOrWhiteSpace(answer.RecipeJson);
        var hasAdvice = answer.Advice.Trim().Length > 0;
        if (!hasAdvice && !hasRecipe)
        {
            var lines = answer.Problems.Count > 0 ? answer.Problems : new[] { PerformanceTexts.AiEmptyAnswer };
            return Problems(lines);
        }

        var run = _taskRun ?? _service.Session?.Selected;
        if (run is null)
        {
            return Problems(new[] { PerformanceTexts.AiNoRun });
        }

        var messages = new List<string>();
        var status = string.Empty;
        if (hasAdvice)
        {
            try
            {
                _service.Store.SaveAttachment(run, PerformanceRunStore.AdviceFileName, answer.Advice + "\n");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // DirectoryNotFoundException (Lauf gelöscht) ist eine IOException
                var failed = new List<string> { PerformanceTexts.AiAdviceSaveFailed(ex.Message) };
                failed.AddRange(answer.Problems);
                return Problems(failed);
            }

            _owner.UpdateAiButtons();
            status = PerformanceTexts.AiAdviceSaved(run.Name);
            var lines = answer.Advice.Split('\n');
            messages.AddRange(lines.Take(ShownAdviceLines));
            if (lines.Length > ShownAdviceLines)
            {
                messages.Add(PerformanceTexts.AiAdviceMore(lines.Length - ShownAdviceLines));
            }
        }
        else
        {
            messages.Add(PerformanceTexts.AiNoAdvice);
        }

        messages.AddRange(answer.Problems);
        if (hasRecipe)
        {
            if (MessageBox.Show(owner, PerformanceTexts.AiRecipeOffer, Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var imported = await _importRecipe(text, modRoot);
                messages.Add(string.Empty);
                messages.Add(PerformanceTexts.AiRecipeImported);
                messages.AddRange(imported);
            }
            else
            {
                messages.Add(PerformanceTexts.AiRecipeNotImported);
            }
        }

        return new AiTaskOutcome(true, messages, status) { Problem = answer.Problems.Count > 0, Feedback = answer.Problems };
    }

    private static AiTaskOutcome Problems(IReadOnlyList<string> lines) => new(false, lines, string.Empty) { Problem = true };
}
