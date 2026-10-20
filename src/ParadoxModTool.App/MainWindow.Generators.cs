using System.IO;
using System.Windows;
using ParadoxModTool.Editor;
using ParadoxModTool.Generators;
using ParadoxModTool.Generators.Candidates;
using ParadoxModTool.Modifiers;
using ParadoxModTool.ScriptDocs;
using ParadoxModTool.UndoRedo;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using RadioButton = System.Windows.Controls.RadioButton;
using TextBox = System.Windows.Controls.TextBox;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>Fenster "Generator-Pakete" (Spec 2026-10-03-generator-packages-core): Öffnen und Schreiben eines Plans als ein Rückgängig-Schritt.</summary>
public partial class MainWindow
{
    private GeneratorWindow? _generatorWindow;
    private PackageEditorWindow? _packageEditor;

    private void OpenGeneratorPackages_Click(object sender, RoutedEventArgs e) => OpenGeneratorPackages();

    private void OpenPackageEditor_Click(object sender, RoutedEventArgs e) => OpenPackageEditor(null, createNew: false);

    /// <summary>Paket-Editor öffnen (einmal je Hauptfenster), auf <paramref name="packageId"/> oder mit "Neu".</summary>
    internal void OpenPackageEditor(string? packageId, bool createNew)
    {
        if (_packageEditor is null)
        {
            _packageEditor = new PackageEditorWindow(new PackageEditorHost(
                () => GameContext.GameId,
                () => GameContext.Repository.GameDirectory(GameContext.GameId),
                id => GameContext.Repository.GameDirectory(id),
                () => GameContext.Profiles.Select(e => (e.Profile.Id, e.Profile.DisplayName)).ToList(),
                id => OpenGeneratorPackages(id),
                AppInfo.Version.ToString(),
                GeneratorKnownMods,
                () => _mods?.ActiveMod?.Paths.FirstOrDefault(Directory.Exists),
                () => CollectOriginRoots().Vanilla,
                () => _activeProfile?.LocalizationLanguages ?? new List<string>(),
                PromptForGameExecutablePath)) { Owner = this };
            _packageEditor.Closed += (_, _) => _packageEditor = null;
        }

        if (_packageEditor.IsVisible)
        {
            _packageEditor.Activate();
        }
        else
        {
            _packageEditor.Show();
        }

        _packageEditor.Open(packageId);
        if (createNew)
        {
            _packageEditor.StartNewPackage();
        }
    }

    /// <summary>"Mit KI erstellen" aus dem Generator-Fenster: Paket-Editor öffnen und darin das KI-Fenster.</summary>
    private void OpenPackageEditorAi()
    {
        OpenPackageEditor(null, createNew: false);
        _packageEditor?.OpenAiWindow();
    }

    /// <summary>Bekannte Mods mit vorhandenem Ordner, nach Namen sortiert (Generator-Fenster und Paket-Editor).</summary>
    private IReadOnlyList<(string Name, string Path)> GeneratorKnownMods() =>
        _mods?.Registry.Mods
            .Select(m => (m.Name, Path: m.Paths.FirstOrDefault(Directory.Exists)))
            .Where(m => m.Path is not null)
            .Select(m => (m.Name, m.Path!))
            .OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList() ?? new List<(string, string)>();

    /// <summary>Öffnet das Fenster (nur einmal je Hauptfenster); mit <paramref name="packageId"/> lädt es die Paketliste neu und wählt dieses Paket.</summary>
    internal void OpenGeneratorPackages(string? packageId = null)
    {
        if (_generatorWindow is null)
        {
            _generatorWindow = new GeneratorWindow(new GeneratorWindowHost(
                () => _mods?.ActiveMod?.Paths.FirstOrDefault(Directory.Exists),
                () => GameContext.GameId,
                () => GameContext.Repository.GameDirectory(GameContext.GameId),
                () => _activeProfile?.LocalizationLanguages ?? new List<string>(),
                GeneratorKnownMods,
                ApplyGeneratorPlan,
                () => CollectOriginRoots().Vanilla,
                () => GameContext.Session.State,
                PromptForGameExecutablePath,
                OpenPackageEditor,
                OpenPackageEditorAi,
                GeneratorCandidateSources,
                GeneratorModifierNames,
                extension => ParadoxModTool.GameProfiles.ModFileEncoding.NewFileHasBom(WriteProfile, extension))) { Owner = this };
            _generatorWindow.Closed += (_, _) => _generatorWindow = null;
        }

        if (_generatorWindow.IsVisible)
        {
            _generatorWindow.Activate();
        }
        else
        {
            _generatorWindow.Show();
        }

        if (packageId is not null)
        {
            _generatorWindow.ReloadPackages(packageId);
        }
    }

    /// <summary>
    /// Quellen der Kandidatenliste (Fenster "Modifier ohne Idee"): die Definitionen des Modifier-Index und die Modifier der Script-Datenbank, die nicht
    /// veraltet sind, mit ihrem Scope und der Marke "neu". Läuft im UI-Thread.
    /// </summary>
    private IReadOnlyList<CandidateSource> GeneratorCandidateSources(string category)
    {
        var database = ScriptDocs.Database;
        return CandidateSources.Merge(
            category,
            ModifierValueSettings.Index.Traits.Select(t => (t.Key, t.Value.Category, t.Value.Boolean)),
            database.Entries
                .Where(e => e.Kind == ScriptDocKind.Modifier && !database.IsOutdated(e))
                .Select(e => (e.Name, (IReadOnlyList<string>)e.Scopes, database.IsNew(e))));
    }

    /// <summary>
    /// Offizielle Namen der Modifier in genau einer Spielsprache (ohne englischen Rückfall); nur gefundene Namen sind im Ergebnis. Der Aufruf kommt aus einem
    /// Hintergrund-Thread: die Ordner (Explorer-Zustand) und das Profil werden deshalb über den UI-Thread gelesen.
    /// </summary>
    private IReadOnlyDictionary<string, string> GeneratorModifierNames(IReadOnlyList<string> modifiers, string language)
    {
        var (settings, gameRoots, modRoots) = Dispatcher.Invoke(() =>
        {
            var (vanilla, mod) = CollectOriginRoots();
            if (_mods?.ActiveMod?.Paths.FirstOrDefault(Directory.Exists) is { } active)
            {
                mod.Add(active);
            }

            return (_activeProfile?.ModifierFormats, vanilla, mod.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        });
        var index = ModifierNameIndex.Build(settings, modifiers, gameRoots, modRoots, new[] { language });
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var modifier in modifiers)
        {
            if (index.Find(modifier) is { Length: > 0 } name)
            {
                names[modifier] = name;
            }
        }

        return names;
    }

    /// <summary>
    /// Schreibt einen Plan: Prüfung auf seit der Vorschau geänderte Dateien und auf ungespeicherte offene Dokumente, dann alle Schreibschritte; bricht
    /// einer ab, werden die schon geschriebenen zurückgenommen. Danach ein Schritt im Aktionsverlauf und offene Tabs neu geladen.
    /// </summary>
    private FixOutcome ApplyGeneratorPlan(GeneratorPlan plan, string description)
    {
        if (GeneratorPlanWriter.StaleFiles(plan) is { Count: > 0 } stale)
        {
            return new FixOutcome(false, GeneratorTexts.Stale(string.Join(", ", stale)));
        }

        var paths = GeneratorPlanWriter.WrittenPaths(plan);
        var unsaved = AllPanes.SelectMany(p => p.Documents)
            .FirstOrDefault(d => d.IsModified && paths.Any(f => EditorPane.PathsEqual(d.FilePath, f)));
        if (unsaved?.FilePath is { } unsavedPath)
        {
            return new FixOutcome(false, GeneratorTexts.Unsaved(Path.GetFileName(unsavedPath)));
        }

        var action = GeneratorPlanWriter.CreateAction(plan, description);
        var written = new List<IUndoableAction>();
        try
        {
            foreach (var write in action.Actions)
            {
                write.Do();
                written.Add(write);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            for (var i = written.Count - 1; i >= 0; i--)
            {
                try
                {
                    written[i].Undo();
                }
                catch (Exception undoError) when (undoError is IOException or UnauthorizedAccessException)
                {
                    // Weiter mit den übrigen Schritten.
                }
            }

            ReloadOpenTabsAffectedBy(new CompositeUndoableAction(string.Empty, written));
            return new FixOutcome(false, GeneratorTexts.WriteFailed(ex.Message));
        }

        _history.Record(action);
        ReloadOpenTabsAffectedBy(action);
        // Die Sicherung einer externen Datei zählt nicht als geschriebene Datei
        return new FixOutcome(true, GeneratorTexts.Written(written.Count(w => w is not ExternalBackupAction)));
    }
}
