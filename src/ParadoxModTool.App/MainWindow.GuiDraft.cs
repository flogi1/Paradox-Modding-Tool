using System.IO;
using System.Text;
using System.Windows;
using ParadoxModTool.Editor;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.Mods;
using ParadoxModTool.UndoRedo;
using ParadoxModTool.VersionManager;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Vanilla-Entwürfe (Plan 3b): "Für Mod bearbeiten", "Entwurf verwerfen", "In Mod übernehmen" mit Übersicht, Sicherung und einem
/// Rückgängig-Schritt, "Im Designer öffnen" aus dem Inspektor und die GUI-Einstellungen der Mod.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// "Für Mod bearbeiten" (Plan-Ruling 4): Der Tab wird zum Entwurf. Definitionen, die die Mod schon überschreibt, stehen darin in ihrer
    /// Mod-Fassung; das ist der Ausgangsstand (kein Rückgängig dorthin, nicht "geändert"). Ersetzt die Mod die ganze Datei, öffnet sich die Mod-Datei.
    /// </summary>
    private void StartGuiDraft(EditorPane pane, OpenDocument document)
    {
        if (document.IsGuiDraft || document.FilePath is not { } path)
        {
            return;
        }

        // Ausgeschaltete GUI-Werkzeuge: kein Index, Entwürfe gehen erst nach dem Einschalten (Vanilla bleibt schreibgeschützt).
        if (!GuiSettings.ToolsEnabled)
        {
            MessageBox.Show(this, GuiTexts.IndexToolsOff, GuiTexts.DraftTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (GuiSettings.Workspace is not { } workspace)
        {
            MessageBox.Show(this, GuiTexts.DraftNoIndex, GuiTexts.DraftTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!GuiIndexFitsMod(workspace, CurrentGuiModRoots().ModRoot))
        {
            MessageBox.Show(this, GuiTexts.DraftNoIndex, GuiTexts.DraftTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (VanillaSourceOf(workspace, path) is not { } source)
        {
            MessageBox.Show(this, GuiTexts.DraftNotInIndex, GuiTexts.DraftTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Klassisch per GuiTextReader: EU IV hat Vanilla-Dateien in Windows-1252 (end_of_combat.gui); als UTF-8 gelesen würde "é" zu Ersatzzeichen.
        string vanillaText;
        try
        {
            vanillaText = ReadGuiSource(path).Text;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, GuiTexts.DraftTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var start = GuiDraft.Start(source, vanillaText, workspace.Index);
        if (start.ModCopy is { } copy)
        {
            MessageBox.Show(this, GuiTexts.DraftModCopy(copy.RelativePath), GuiTexts.DraftTitle, MessageBoxButton.OK, MessageBoxImage.Information);

            // Dasselbe Element in der Mod-Kopie statt derselben Zeilennummer (3b-Nacharbeit 8).
            var caretOffset = ReferenceEquals(pane.ActiveDocument, document) ? pane.CurrentCaretOffset : 0;
            OpenGuiInDesigner(copy.FullPath, GuiDraft.LineInCopy(GuiParser.Parse(vanillaText), caretOffset, workspace.Index.DocumentOf(copy), workspace.Index.Layout), startDraft: false);
            return;
        }

        if (start.DependencyCopy is { } parent)
        {
            MessageBox.Show(this, GuiTexts.DraftDependencyCopy(parent.Label), GuiTexts.DraftTitle, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        var caret = pane.CurrentCaretOffset;
        int? line = null;
        if (document.Document.Text != start.Text)
        {
            // Der Entwurf geht womöglich von der Abhängigkeits-Kopie aus und enthält eingesetzte Mod-Fassungen: dieselbe Stelle über ihr Element
            // suchen statt den Vanilla-Offset in den neuen Text zu übertragen (3c-Review).
            line = GuiDraft.LineInCopy(GuiParser.Parse(document.Document.Text), caret, GuiParser.Parse(start.Text), workspace.Index.Layout);
            document.Document.Text = start.Text;
        }

        // Die eingesetzten Mod-Fassungen sind die Grundlage des Vergleichs: Strg+Z darf nicht zu Vanilla zurückführen.
        document.Document.UndoStack.ClearAll();
        document.MarkSaved();
        document.GuiDraftBase = start.Text;
        document.IsGuiDraft = true;
        if (ReferenceEquals(pane.ActiveDocument, document))
        {
            pane.GoToLine(line ?? document.Document.GetLineByOffset(Math.Min(caret, document.Document.TextLength)).LineNumber);
        }
    }

    /// <summary>Entwurf verwerfen: Vanilla-Text von der Platte, kein Rückgängig dorthin, wieder schreibgeschützt.</summary>
    private void DiscardGuiDraft(OpenDocument document, bool ask)
    {
        if (!document.IsGuiDraft)
        {
            return;
        }

        if (ask && document.IsModified
            && MessageBox.Show(this, GuiTexts.DraftDiscardConfirm, GuiTexts.DraftTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            document.ReloadFromDisk();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, GuiTexts.DraftTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        document.Document.UndoStack.ClearAll();
        document.MarkSaved();
        document.GuiDraftBase = null;
        document.IsGuiDraft = false;
        SyncRecovery();
    }

    /// <summary>
    /// "In Mod übernehmen": Plan rechnen (<see cref="GuiOverridePlanner"/>), Übersicht zeigen und erst nach Bestätigung schreiben. Offene Zieldateien
    /// mit ungespeicherten Änderungen blockieren (der Plan liest die Dateien von der Platte).
    /// </summary>
    private void TakeOverGuiDraft(OpenDocument document)
    {
        if (!document.IsGuiDraft || document.FilePath is not { } path)
        {
            return;
        }

        // Ausgeschaltete GUI-Werkzeuge: kein Index, Entwürfe gehen erst nach dem Einschalten (Vanilla bleibt schreibgeschützt).
        if (!GuiSettings.ToolsEnabled)
        {
            MessageBox.Show(this, GuiTexts.IndexToolsOff, GuiTexts.DraftTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (GuiSettings.Workspace is not { } workspace)
        {
            MessageBox.Show(this, GuiTexts.DraftNoIndex, GuiTexts.DraftTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (VanillaSourceOf(workspace, path) is not { } source)
        {
            MessageBox.Show(this, GuiTexts.DraftNotInIndex, GuiTexts.DraftTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (CurrentGuiModRoots().ModRoot is not { } modRoot)
        {
            MessageBox.Show(this, GuiTexts.NewFileNoMod, GuiTexts.DraftTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!GuiIndexFitsMod(workspace, modRoot))
        {
            MessageBox.Show(this, GuiTexts.DraftNoIndex, GuiTexts.DraftTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Klassisch (HOI IV, EU IV): der klassische Planer mit dem Standardnamen aus ClassicOverrideTargets; ein gesetzter Name gilt wie angegeben.
        var layout = CurrentGuiLayout;
        var classic = layout.IsClassic;
        var forceCopy = false;
        while (true)
        {
            GuiOverridePlan plan;
            try
            {
                var mod = RegisteredGuiMod(modRoot);
                var targets = GuiModTargets.For(modRoot, mod?.GuiOverrideFile, mod?.GuiTypesFile, layout.FirstWins, classic: classic);
                var (vanillaText, encodingName) = ReadGuiSource(path);
                plan = GuiOverridePlanner.Plan(new GuiOverrideRequest(
                    source,
                    vanillaText,
                    document.Document.Text,
                    workspace.Index,
                    targets,
                    ReadGuiTarget,
                    document.GuiDraftBase)
                {
                    ForceCopyFile = forceCopy,
                    SourceEncoding = encodingName,
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, GuiTexts.TakeOverFailed(ex.Message), GuiTexts.TakeOverTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Ziehen Fenster aus einer verlierenden Mod-Datei um, fragt "Übernehmen" erst nach (Florian 2026-10-17); "Nein" bleibt in der Übersicht.
            var window = new GuiTakeOverWindow(plan, modRoot, OpenModifiedTargets(plan), classic, layout.FirstWins)
            {
                Owner = this,
                ConfirmMoves = (overview, moves) => MessageBox.Show(
                    overview,
                    GuiTexts.ClassicMoveConfirm(moves, layout.FirstWins),
                    GuiTexts.TakeOverTitle,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes,
            };
            if (window.ShowDialog() == true)
            {
                ApplyGuiPlan(plan, modRoot, GuiTexts.TakeOverAction(Path.GetFileName(path)), document);
                return;
            }

            // "Ganze Datei kopieren…": neu planen und die Übersicht noch einmal zeigen (dort gibt es den Knopf dann nicht mehr).
            if (!window.CopyFileRequested || forceCopy)
            {
                return;
            }

            forceCopy = true;
        }
    }

    /// <summary>Zieldateien des Plans, die in PMT offen sind und ungespeicherte Änderungen haben (sie blockieren; der Plan liest von der Platte).</summary>
    private List<string> OpenModifiedTargets(GuiOverridePlan plan) =>
        plan.Files
            .Where(f => AllPanes.SelectMany(p => p.Documents).Any(d => d.IsModified && EditorPane.PathsEqual(d.FilePath, f.FullPath)))
            .Select(f => f.FullPath)
            .ToList();

    /// <summary>
    /// Inspektor "Als neuen Typ in Mod übernehmen" (Plan 3c Ruling 7): Name abfragen, Plan rechnen, Übersicht zeigen und nach Bestätigung schreiben
    /// wie "In Mod übernehmen", nur ohne Entwurf.
    /// </summary>
    private void TakeOverAsNewType(GuiInspectTarget target)
    {
        // HOI IV und EU IV kennen keine Typen.
        if (!GuiSettings.TypeCopyOffered)
        {
            MessageBox.Show(this, GuiTexts.ClassicNoTypes, GuiTexts.NewTypeCopyTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (GuiSettings.Workspace is not { } workspace)
        {
            MessageBox.Show(this, GuiTexts.DraftNoIndex, GuiTexts.NewTypeCopyTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (CurrentGuiModRoots().ModRoot is not { } modRoot)
        {
            MessageBox.Show(this, GuiTexts.NewFileNoMod, GuiTexts.NewTypeCopyTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!GuiIndexFitsMod(workspace, modRoot))
        {
            MessageBox.Show(this, GuiTexts.DraftNoIndex, GuiTexts.NewTypeCopyTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (GuiTypeEdits.CopySource(workspace.Index, target, workspace.Rules) is not { } source
            || GuiTypeEdits.CopySnippet(source.Document, source.Node, "x") is not { } probe)
        {
            MessageBox.Show(this, GuiTexts.InspectorTakeOverAsTypeNotPossible, GuiTexts.NewTypeCopyTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var mod = RegisteredGuiMod(modRoot);
        var targets = GuiModTargets.For(modRoot, mod?.GuiOverrideFile, mod?.GuiTypesFile, CurrentGuiLayout.FirstWins, classic: CurrentGuiLayout.IsClassic);
        bool Taken(string name) => GuiTypeEdits.IsTaken(name, probe.Kind, workspace.Index, null, workspace.Catalog.IsKnownEngineWidget);
        var ask = new GuiNewTypeWindow(
            GuiNewTypeMode.Copy,
            GuiElementTree.LabelOf(source.Node),
            GuiTypeEdits.SuggestName(GuiTypeEdits.ModBase(modRoot), GuiTypeEdits.CopyStem(source.Node), Taken),
            Array.Empty<string>(),
            0,
            GuiTexts.NewTypeWhereCopy(targets.TypesFileName),
            Taken)
        {
            Owner = this,
        };
        if (ask.ShowDialog() != true)
        {
            return;
        }

        GuiOverridePlan plan;
        try
        {
            plan = GuiOverridePlanner.PlanNewTypeCopy(new GuiNewTypeCopyRequest(
                source,
                ask.TypeName,
                workspace.Index,
                targets,
                ReadGuiTarget));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, GuiTexts.TakeOverFailed(ex.Message), GuiTexts.TakeOverTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var overview = new GuiTakeOverWindow(plan, modRoot, OpenModifiedTargets(plan)) { Owner = this };
        if (overview.ShowDialog() == true)
        {
            ApplyGuiPlan(plan, modRoot, GuiTexts.TakeOverNewTypeAction(ask.TypeName), draft: null);
        }
    }

    /// <summary>
    /// Schreibt den Plan: Sicherung, falls eine bestehende Mod-Datei sich ändert; fehlende Ordner und alle Dateien als ein Rückgängig-Schritt; offene
    /// Tabs und GUI-Index nachziehen; einen Entwurf verwerfen und in der Mod-Datei an der ersten Definition weiterarbeiten.
    /// </summary>
    private void ApplyGuiPlan(GuiOverridePlan plan, string modRoot, string actionName, OpenDocument? draft)
    {
        if (plan.Files.Any(f => !f.IsNew))
        {
            try
            {
                new ModVersionManager(modRoot, StorageLocations.ResolveSnapshotsDirectory(modRoot, _storageSettings)).CreateSnapshot(GuiTexts.TakeOverSnapshotNote);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                MessageBox.Show(this, GuiTexts.TakeOverSnapshotFailed(ex.Message), GuiTexts.TakeOverTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        // Neue Dateien in der Kodierung aus dem Spielprofil, bestehende behalten ihr BOM (Spec "GuiOverridePlanner", 3b-Nacharbeit 6). Die
        // Ordner-Aktion steht vorn: Rückgängig entfernt die Dateien zuerst und danach die selbst angelegten, leeren Ordner (3b-Nacharbeit 3).
        // Klassisch (HOI IV, EU IV): bestehende Dateien behalten ihre Kodierung (auch Windows-1252), neue nach Profil oder Windows-1252-Quelle
        // (ClassicOverridePlanner.WriteEncoding). Die Kodierungen stehen hier fest, bevor die erste Datei geschrieben wird (ForWrite liest die Datei).
        var classic = CurrentGuiLayout.IsClassic;
        var encodingName = GuiFileEncodingName();
        var writes = new List<IUndoableAction>
        {
            new CreateFoldersUndoableAction(actionName, plan.Files.Select(f => Path.GetDirectoryName(f.FullPath)!)),
        };
        writes.AddRange(plan.Files.Select(file => (IUndoableAction)new FileWriteUndoableAction(
            GuiTexts.TakeOverFileAction(Path.GetFileName(file.FullPath)),
            file.FullPath,
            file.NewText,
            classic
                ? ClassicOverridePlanner.WriteEncoding(file, _activeProfile)
                : GuiFileEncoding.For(encodingName, file.IsNew, !file.IsNew && HasUtf8Bom(file.FullPath)))).ToList());

        // Alle Dateien oder keine: Scheitert eine, gleich aus welchem Grund, werden die schon geschriebenen zurückgenommen (3b-Nacharbeit 4).
        var done = new List<IUndoableAction>();
        try
        {
            foreach (var write in writes)
            {
                write.Do();
                done.Add(write);
            }
        }
        catch (Exception ex)
        {
            for (var i = done.Count - 1; i >= 0; i--)
            {
                try
                {
                    done[i].Undo();
                }
                catch (Exception)
                {
                    // Weiter mit den übrigen; die Sicherung von eben enthält den alten Stand.
                }
            }

            ReloadOpenTabsAffectedBy(new CompositeUndoableAction(string.Empty, done));
            MessageBox.Show(this, GuiTexts.TakeOverFailed(ex.Message), GuiTexts.TakeOverTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var action = new CompositeUndoableAction(actionName, writes);
        _history.Record(action);
        foreach (var file in plan.Files.Where(f => f.IsNew))
        {
            _newModFiles.Created(file.FullPath, DateTime.UtcNow);
        }

        // Lädt offene Tabs neu und meldet jede Datei dem GUI-Index; ein zweites Melden entfällt (3b-Nacharbeit 2).
        ReloadOpenTabsAffectedBy(action);
        foreach (var folder in plan.Files.Select(f => Path.GetDirectoryName(f.FullPath)!).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ExplorerPanelControl.NotifyFolderChanged(folder);
        }

        if (draft is not null)
        {
            DiscardGuiDraft(draft, ask: false);
        }

        var focus = plan.Entries.FirstOrDefault(e => e.Line > 0);
        OpenGuiInDesigner(focus?.TargetPath ?? plan.Files[0].FullPath, focus?.Line ?? 1, startDraft: false);
    }

    /// <summary>Öffnet eine .gui-Datei an der Zeile in "Geteilt"; eine Vanilla-Datei wird auf Wunsch gleich zum Entwurf ("Im Designer öffnen").</summary>
    private void OpenGuiInDesigner(string path, int line, bool startDraft)
    {
        JumpToBookmarkInFile(path, line);
        foreach (var pane in AllPanes)
        {
            if (pane.Documents.FirstOrDefault(d => EditorPane.PathsEqual(d.FilePath, path)) is not { } document)
            {
                continue;
            }

            pane.ShowGuiMode(document, GuiViewMode.Split);
            if (startDraft && GuiSettings.DraftsOffered && GuiSettings.IsVanillaGui(document))
            {
                StartGuiDraft(pane, document);
            }

            return;
        }
    }

    /// <summary>"GUI-Einstellungen der Mod": Zieldateien und Abhängigkeiten der aktiven, registrierten Mod.</summary>
    private void GuiModSettings_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentGuiModRoots().ModRoot is not { } modRoot || _mods is null || RegisteredGuiMod(modRoot) is not { } mod)
        {
            MessageBox.Show(this, GuiTexts.ModSettingsNoMod, GuiTexts.MenuModSettings, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Klassisch ist der Standardname der aus ClassicOverrideTargets (zz_ bzw. 00_); eine Typdatei gibt es dort nicht.
        var defaults = GuiModTargets.For(modRoot, firstWins: CurrentGuiLayout.FirstWins, classic: CurrentGuiLayout.IsClassic);
        var defaultDependencies = ModGuiDependencies.Defaults(_mods.Registry, mod);
        var window = new GuiModSettingsWindow(mod.Name, defaults, mod.GuiOverrideFile, mod.GuiTypesFile, ModGuiDependencies.For(_mods.Registry, mod), defaultDependencies, CurrentGuiLayout)
        {
            Owner = this,
        };
        if (window.ShowDialog() != true)
        {
            return;
        }

        // Standardwerte bleiben "nicht gesetzt", damit sie dem Ordnernamen bzw. den Beziehungen folgen.
        var dependencies = window.Dependencies.SequenceEqual(defaultDependencies, StringComparer.OrdinalIgnoreCase) ? null : window.Dependencies;
        _mods.Registry.SetGuiSettings(
            mod.Id,
            window.OverrideFile == defaults.OverrideFileName ? null : window.OverrideFile,
            window.TypesFile == defaults.TypesFileName ? null : window.TypesFile,
            dependencies);
        _ = UpdateGuiWorkspaceAsync(force: false);
    }

    /// <summary>Die Mod aus dem Register, zu der der Ordner gehört (sonst gelten Standardnamen und -abhängigkeiten).</summary>
    private ModInfo? RegisteredGuiMod(string modRoot) => _mods?.Registry.Resolve(modRoot);

    /// <summary>Kodierung für neue .gui-Dateien aus dem Spielprofil (wie beim Kompatch), Standard "utf-8-bom".</summary>
    private string GuiFileEncodingName() =>
        ParadoxModTool.GameProfiles.ModFileEncoding.NewFileEncodingName(_activeProfile, ".gui");

    /// <summary>
    /// Gehört der Index zur aktiven Mod? Nach einem Mod-Wechsel baut er noch (oder der Neuaufbau ist gescheitert); dann stammten Mod-Fassungen und
    /// Ersetzungen aus der alten Mod, Zieldateien und Sicherung aber aus der neuen. Stößt in dem Fall den Neuaufbau an.
    /// </summary>
    private bool GuiIndexFitsMod(GuiWorkspace workspace, string? modRoot)
    {
        var fits = modRoot is null ? workspace.Options.ModRoot is null : EditorPane.PathsEqual(workspace.Options.ModRoot, modRoot);
        if (!fits)
        {
            _ = UpdateGuiWorkspaceAsync(force: false);
        }

        return fits;
    }

    /// <summary>
    /// Quelltext für Entwurf und Übernahme. Klassisch (HOI IV, EU IV) per <see cref="GuiTextReader"/> mit erkannter Kodierung; Jomini wie der Jomini-Index
    /// mit <see cref="File.ReadAllText(string)"/> und ohne Kodierungsnamen (keine Verhaltensänderung für ck3, eu5, vic3).
    /// </summary>
    private (string Text, string? EncodingName) ReadGuiSource(string path) =>
        CurrentGuiLayout.IsClassic ? GuiTextReader.Read(path) : (File.ReadAllText(path), null);

    /// <summary>Text einer Zieldatei des Planers (null, wenn es sie nicht gibt), nach derselben Regel wie <see cref="ReadGuiSource"/>.</summary>
    private string? ReadGuiTarget(string path) => File.Exists(path) ? ReadGuiSource(path).Text : null;

    private static GuiSourceFile? VanillaSourceOf(GuiWorkspace workspace, string path) =>
        workspace.Index.Files.FirstOrDefault(f => f.Layer == GuiLayer.Vanilla && EditorPane.PathsEqual(f.FullPath, path));

    private static bool HasUtf8Bom(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var head = new byte[3];
            return stream.Read(head, 0, 3) == 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}
