using System.IO;
using System.Windows;
using ParadoxModTool.Generators.Ai;
using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Localization;

namespace ParadoxModTool.App;

/// <summary>
/// "Paket mit KI erstellen" (Spec 2026-10-05-generator-packages-ai-guide, Abschnitt 2): Auftrag aus Anleitung, Beispielpaket, Vorbild-Dateien und Beschreibung;
/// die Antwort (Einfügen oder Ordner) geht als .pmtgen durch den Import des Paket-Editors, danach "Prüfen" auf dem neuen Paket.
/// </summary>
internal sealed class GeneratorAiFlavor : IAiTaskFlavor
{
    private static readonly string GuideFolder = Path.Combine(AppContext.BaseDirectory, "ai-guide", "generator-packages");
    private static readonly string CliPath = Path.Combine(AppContext.BaseDirectory, "pmt-generator.exe");

    private readonly PackageEditorHost _host;
    private readonly string? _packageFolder;
    private readonly Func<string, Action<string>, string?> _import;

    public GeneratorAiFlavor(PackageEditorHost host, string? packageFolder, Func<string, Action<string>, string?> importArchive)
    {
        _host = host;
        _packageFolder = packageFolder;
        _import = importArchive;
    }

    /// <summary>Fensterdaten aus dem Paket-Editor; der Spielordner wird wie bisher gesucht und fehlt er, darf der Nutzer die Spiel-Exe wählen.</summary>
    public static AiTaskHost Host(PackageEditorHost host)
    {
        string? Find() => host.GameRoots().Where(Directory.Exists).LastOrDefault();
        return new AiTaskHost(host.GameId(), host.KnownMods, host.ActiveModRoot, () => Find() ?? (host.ChooseGameFolder() ? Find() : null));
    }

    public string Title => AiPackageTexts.Title;

    public string TaskIntro => AiPackageTexts.TaskIntro;

    public string ResultIntro => AiPackageTexts.ResultIntro;

    public string DescriptionTip => AiPackageTexts.DescriptionTip;

    public string WithOpenText => AiPackageTexts.WithPackage;

    public string WithOpenTip => AiPackageTexts.WithPackageTip;

    public string SaveFileName => "pmt-package-task.md";

    public string ModTip => AiPackageTexts.ModTip;

    public string ChooseModMessage => AiPackageTexts.ChooseModMessage;

    public string FilesTip => AiPackageTexts.FilesTip;

    public string CopyTextTip => AiPackageTexts.CopyTextTip;

    public string SaveFolderTip => AiPackageTexts.SaveFolderTip;

    public string AnswerTip => AiPackageTexts.AnswerTip;

    public string ApplyTip => AiPackageTexts.ApplyTip;

    public string ImportFolderTip => AiPackageTexts.ImportFolderTip;

    public string SnippetsIntro => AiPackageTexts.SnippetsIntro;

    public string SnippetWholeFileTip => AiPackageTexts.SnippetWholeFileTip;

    public string SnippetExcerptTip => AiPackageTexts.SnippetExcerptTip;

    public string SnippetNoteTip => AiPackageTexts.SnippetNoteTip;

    public string NoGameFolder => AiPackageTexts.NoGameFolder;

    public bool OffersGameFiles => false;

    // Das offene Paket hängt nicht an der Mod
    public bool CanHandOver(string? modRoot) => _packageFolder is not null;

    public void ShowHelp(Window owner) => GeneratorPackageHelpWindow.Show(owner, "generatorhelp.ai");

    public string BuildText(AiTaskInput input) => AiTaskBuilder.BuildText(Request(input, forFolder: false), GuideFolder);

    public void BuildFolder(AiTaskInput input, string targetFolder) => AiTaskBuilder.BuildFolder(Request(input, forFolder: true), GuideFolder, targetFolder);

    public string Feedback(IEnumerable<string> messages) => AiTaskBuilder.Feedback(messages);

    // withOpen spielt hier keine Rolle: welches Paket ersetzt wird, klärt der Import des Paket-Editors über die Kennung
    public Task<AiTaskOutcome> ApplyAnswerAsync(Window owner, string answer, string? modRoot, bool withOpen)
    {
        var parsed = AiResponseParser.Parse(answer);
        if (parsed.Problems.Count > 0)
        {
            return Task.FromResult(Problems(parsed.Problems));
        }

        return Task.FromResult(Install(parsed.Files));
    }

    public Task<AiTaskOutcome> ImportFolderAsync(Window owner, string folder, string? modRoot, bool withOpen)
    {
        try
        {
            return Task.FromResult(Install(PackageFileSet.FromFolder(folder)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Generators.GeneratorPackageException)
        {
            return Task.FromResult(Problems(new[] { ex.Message }));
        }
    }

    private AiTaskRequest Request(AiTaskInput input, bool forFolder) => new(
        input.Description,
        _host.GameId(),
        input.ModRoot,
        input.ModFiles,
        input.WithOpenItem ? _packageFolder : null,
        forFolder && File.Exists(CliPath) ? CliPath : null)
    {
        Snippets = input.Snippets,
    };

    /// <summary>Packt die Dateien als .pmtgen und schickt sie durch den Import des Paket-Editors; danach "Prüfen" auf dem neuen Paket.</summary>
    private AiTaskOutcome Install(IReadOnlyDictionary<string, string> files)
    {
        var archive = Path.Combine(Path.GetTempPath(), "pmt-ai-" + Path.GetRandomFileName() + GeneratorPackageArchive.Extension);
        var errors = new List<string>();
        try
        {
            PackageFileSet.WriteArchive(PackageFileSet.WithGameId(files, _host.GameId()), archive);
            if (_import(archive, errors.Add) is not { } folder)
            {
                return errors.Count > 0 ? Problems(errors) : new AiTaskOutcome(false, new List<string> { AiPackageTexts.NotImported }, string.Empty);
            }

            var issues = PackageChecker.Check(PackageDocument.Load(folder), new PackageFiles(folder), UiText.Current.Language)
                .Select(i => i.Line is { } line ? $"{i.Where}:{line}: {i.Message}" : $"{i.Where}: {i.Message}")
                .ToList();
            var lines = new List<string> { AiPackageTexts.Imported(Path.GetFileName(folder)) };
            lines.AddRange(issues.Count == 0 ? new[] { AiPackageTexts.CheckOk } : issues);
            return new AiTaskOutcome(true, lines, string.Empty) { Problem = issues.Count > 0, Feedback = issues };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Generators.GeneratorPackageException)
        {
            return Problems(new[] { ex.Message });
        }
        finally
        {
            File.Delete(archive);
        }
    }

    private static AiTaskOutcome Problems(IReadOnlyList<string> lines) => new(false, lines, string.Empty) { Problem = true };
}
