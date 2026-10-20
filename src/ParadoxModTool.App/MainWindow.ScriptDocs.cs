using System.IO;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using ParadoxModTool.Editor;
using ParadoxModTool.Explorer;
using ParadoxModTool.Scopes;
using ParadoxModTool.ScriptDocs;
using ParadoxModTool.Settings;

namespace ParadoxModTool.App;

/// <summary>
/// Menüpunkt "Script-Datenbank…": Nachschlagefenster für Modifier, Trigger, Effects und mehr, gelesen aus den Doku-Logs des Spiels
/// (siehe <see cref="ScriptDocsWindow"/>). Die Datenbank liegt pro Spiel unter <c>games\&lt;id&gt;\script-docs</c>.
/// </summary>
public partial class MainWindow
{
    private ScriptDocsService? _scriptDocsService;
    private ScriptDocsWindow? _scriptDocsWindow;

    /// <summary>Vom Nutzer gewählter Ordner der Doku-Logs; <see langword="null"/> = aus dem Spielprofil abgeleitet.</summary>
    private string? _scriptDocsFolderOverride;

    private ScriptDocsService ScriptDocs => _scriptDocsService ??= new ScriptDocsService(
        Path.Combine(GameContext.Repository.GameDirectory(GameContext.GameId), "script-docs"),
        ResolveScriptDocsFolder,
        folder => _scriptDocsFolderOverride = folder,
        () => _activeProfile);

    /// <summary>
    /// Der Ordner mit den Doku-Logs des Spiels: der vom Nutzer gewählte, sonst der Ordner des Spielprofils neben dem Mod-Ordner
    /// (Documents/Paradox Interactive/&lt;Spiel&gt;/docs bzw. /logs bei CK III).
    /// </summary>
    private string? ResolveScriptDocsFolder()
    {
        if (!string.IsNullOrWhiteSpace(_scriptDocsFolderOverride))
        {
            return _scriptDocsFolderOverride;
        }

        // Hearts of Iron IV: Doku im Installationsordner; bei flacher Installation ist der erste Vanilla-Ordner die Installation.
        if (_activeProfile is { ScriptDocsBase: "install" } installProfile)
        {
            return ScriptDocsLocation.ForInstall(CollectOriginRoots().Vanilla.FirstOrDefault(), installProfile.ScriptDocsFolder);
        }

        return _activeProfile is { DefaultModRootTemplate: { Length: > 0 } template } profile
            ? ScriptDocsLocation.Default(template, profile.ScriptDocsFolder)
            : null;
    }

    private readonly ScriptHintIndex _scriptHintIndex = new();

    /// <summary>
    /// Verbindet den Editor mit der Script-Datenbank (Hover-Tipps, Autovervollständigung) und lädt sie im Hintergrund, gleich nach dem
    /// Start: Ist ein Log neuer als der letzte Import, wird er dabei mit eingelesen. Fehler dabei stören den Start nicht.
    /// </summary>
    private async void InitializeScriptHints()
    {
        ScriptHintSettings.Provider = _scriptHintIndex;
        ModifierValueSettings.ReloadRequested += (_, _) => RefreshModifierFormats(report: true);
        ScriptDocs.DatabaseChanged += async (_, _) =>
        {
            _scriptHintIndex.SetDatabase(ScriptDocs.Database);
            RefreshModifierFormats();

            // Neue Logs können neue Scopes bedeuten, geänderte Spieldateien neue Felder: Regeln abgleichen (lernt nur bei Bedarf).
            await UpdateScopeRulesAsync(force: false);
            _ = UpdateLocProfilesAsync(force: false);
        };

        try
        {
            await ScriptDocs.RefreshAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Text.Json.JsonException)
        {
            // Ohne Datenbank gibt es einfach keine Hinweise im Editor; das Fenster "Script-Datenbank" zeigt den Zustand.
        }

        RefreshModifierFormats();
    }

    // Werkzeugleisten-Schalter und Einstellungsfenster schreiben dieselbe Einstellung; SyncScriptHintControls zieht die Schalter nach.
    private ScopeService? _scopeService;

    private ScopeService ScopeRuleService => _scopeService ??= new ScopeService(
        Path.Combine(GameContext.Repository.GameDirectory(GameContext.GameId), "scopes", "rules.json"),
        () => CollectOriginRoots().Vanilla,
        () => _activeProfile?.DefaultEventType,
        () => _activeProfile?.EventBlockScopes,
        () => _activeProfile?.NumericKeysChangeScope == true);

    /// <summary>Gleicht die Scope-Regeln mit Script-Datenbank und Vanilla-Dateien ab und lernt im Hintergrund neu, falls nötig.</summary>
    private async Task<ScopeUpdateResult?> UpdateScopeRulesAsync(bool force, CancellationToken cancellation = default, IProgress<(int Done, int Total)>? progress = null)
    {
        try
        {
            return await ScopeRuleService.UpdateAsync(ScriptDocs.Database, force, cancellation, progress);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Ohne Regeln zeigt der Editor "?" statt eines Scopes; der Start wird dadurch nicht gestört.
            return null;
        }
    }

    /// <summary>Lernt die Scope-Regeln mit Fortschrittsfenster neu; nach einem Abbruch bleibt der bisherige Stand, ohne Meldung.</summary>
    private async Task RelearnScopeRulesAsync()
    {
        ScopeUpdateResult? result = null;
        if (!await RunWithProgressAsync(SettingsTexts.RelearnScopeRules, async (cancellation, progress) => result = await UpdateScopeRulesAsync(force: true, cancellation, progress)))
        {
            return;
        }

        MessageBox.Show(
            this,
            result is null ? MiscWindowTexts.ScopeRulesFailed : result.Summary + (result.Learned ? "\n\n" + MiscWindowTexts.ScopeRulesDuration(result.Duration.TotalSeconds) : string.Empty),
            MiscWindowTexts.ScopeRulesTitle,
            MessageBoxButton.OK,
            result is null ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }

    private ScopePathFinder? _pathFinder;
    private ScopeEngine? _pathFinderEngine;

    private void ScopePaths_Click(object sender, RoutedEventArgs e) => OpenScopePathDialog();

    /// <summary>
    /// Öffnet den Scope Finder ("Pfade vorschlagen") für den aktiven Arbeitsbereich: Startpunkte sind der Scope am Cursor, root und die bekannten gespeicherten Scopes, danach alle Scopes zur freien Wahl.
    /// Der gewählte Weg wird an der Cursorstelle eingefügt.
    /// </summary>
    private void OpenScopePathDialog()
    {
        if (ParadoxModTool.Editor.ScopeSettings.Engine is not { } engine)
        {
            MessageBox.Show(this, ScopeFinderTexts.DatabaseMissing, ScopeFinderTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!ReferenceEquals(_pathFinderEngine, engine))
        {
            _pathFinder = new ScopePathFinder(engine.Db);
            _pathFinderEngine = engine;
        }

        var pane = ActivePane;
        var context = pane.ScopeInsight.Context;
        var starts = new List<ScopePathWindow.StartOption>();
        if (context?.CurrentScope is { } current)
        {
            var label = context.Confident ? ScopeFinderTexts.StartHere(current) : ScopeFinderTexts.StartHereUnsure(current);
            starts.Add(new ScopePathWindow.StartOption(label, current, string.Empty));
        }

        if (context?.StartScope is { } root)
        {
            starts.Add(new ScopePathWindow.StartOption(ScopeFinderTexts.StartRoot(root), root, "root"));
        }

        if (context is not null)
        {
            foreach (var (name, scope) in context.SavedScopes.Where(s => s.Value is not null).Select(s => (s.Key, s.Value!)))
            {
                starts.Add(new ScopePathWindow.StartOption(ScopeFinderTexts.StartSaved(name, scope), scope, $"scope:{name}"));
            }

            foreach (var (name, scope) in context.LearnedSavedScopes.Where(s => !context.SavedScopes.ContainsKey(s.Key)).OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => (s.Key, s.Value)))
            {
                starts.Add(new ScopePathWindow.StartOption(ScopeFinderTexts.StartSaved(name, scope), scope, $"scope:{name}"));
            }
        }

        starts.AddRange(_pathFinder!.Scopes.Select(s => new ScopePathWindow.StartOption(ScopeFinderTexts.StartFree(s), s, string.Empty)));

        // Script-Values: die der Mod werden bei jedem Öffnen frisch gelesen (eigene neue Werte sind sofort da), die aus Vanilla kommen mit den Scope-Regeln.
        var modRoot = ScriptValueModRoot(pane.CurrentFilePath);
        var modScriptValues = modRoot is null ? Array.Empty<string>() : ScriptValueNames.Read(new[] { modRoot });
        var dialog = new ScopePathWindow(
            _pathFinder,
            starts,
            context is not null && ScopePathFinder.LooksLikeTriggerContext(context),
            modScriptValues,
            ScopeRuleService.ScriptValues) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.ChosenChain is { } chain)
        {
            pane.InsertScopeChain(chain, dialog.PreferredForm, dialog.ChainOnly);
        }
    }

    /// <summary>
    /// Der Mod-Ordner für die Script-Value-Vorschläge: bei aktiver Mod ihr Pfad, der die Datei enthält, sonst ihr erster Pfad; ohne aktive Mod der Ordner
    /// ungebundener Dateien, wenn die Datei darin liegt. Anders als beim GUI-Werkzeug ohne Merken der zuletzt erkannten Mod.
    /// </summary>
    private string? ScriptValueModRoot(string? file)
    {
        if (_mods?.ActiveMod is { } mod)
        {
            return mod.Paths.FirstOrDefault(p => file is not null && IsInsideFolder(file, p)) ?? mod.Paths.FirstOrDefault();
        }

        return _mods?.UnmanagedFolder is { } unmanaged && file is not null && IsInsideFolder(file, unmanaged) ? unmanaged : null;
    }

    private void GoToMatchingBracket_Click(object sender, RoutedEventArgs e) => ActivePane.GoToMatchingBracket();

    private void ScriptHoverToggle_Click(object sender, RoutedEventArgs e) => ScriptHintSettings.SetHoverEnabled(ScriptHoverToggle.IsChecked == true);

    private void ScriptAutoCompleteToggle_Click(object sender, RoutedEventArgs e) => ScriptHintSettings.SetAutoCompleteEnabled(ScriptAutoCompleteToggle.IsChecked == true);

    private void SyncScriptHintControls()
    {
        ScriptHoverToggle.IsChecked = ScriptHintSettings.HoverEnabled;
        ScriptAutoCompleteToggle.IsChecked = ScriptHintSettings.AutoCompleteEnabled;
    }

    private void OpenScriptDocsWindow_Click(object sender, RoutedEventArgs e)
    {
        if (_scriptDocsWindow is null)
        {
            _scriptDocsWindow = new ScriptDocsWindow(
                ScriptDocs,
                GetUsageSearchRoots,
                name => ActivePane.InsertAtCaret(name),
                (path, line) => JumpToBookmarkInFile(path, line))
            {
                Owner = this,
            };
            _scriptDocsWindow.Closed += (_, _) => _scriptDocsWindow = null;
        }

        if (_scriptDocsWindow.IsVisible)
        {
            _scriptDocsWindow.Activate();
        }
        else
        {
            _scriptDocsWindow.Show();
        }
    }

    /// <summary>Alle Ordner, in denen Beispiele gesucht werden: Vanilla- und Mod-Wurzeln (siehe <see cref="CollectOriginRoots"/>).</summary>
    private IReadOnlyList<string> GetUsageSearchRoots()
    {
        var (vanilla, mod) = CollectOriginRoots();
        return vanilla.Concat(mod).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Vanilla-Wurzeln (Spielordner aus dem Exe-Pfad, Explorer-Wurzeln der Art Spieldateien) und Mod-Wurzeln (Explorer-Mod-Ordner, Vanilla-Diff-Mod-Ordner).</summary>
    private (List<string> Vanilla, List<string> Mod) CollectOriginRoots()
    {
        var roots = ExplorerPanelControl.CaptureRoots();
        var vanillaRoots = roots.Where(r => r.Kind == ExplorerRootKind.GameFiles).Select(r => r.FullPath).ToList();
        if (ComputeGameContentRoot(_gameExecutablePath) is { } gameRoot)
        {
            vanillaRoots.Add(gameRoot);
        }

        var modRoots = roots.Where(r => r.Kind == ExplorerRootKind.ModFolder).Select(r => r.FullPath).ToList();
        if (!string.IsNullOrWhiteSpace(_vanillaDiffModRootOverride))
        {
            modRoots.Add(_vanillaDiffModRootOverride);
        }

        return (vanillaRoots, modRoots);
    }
}
