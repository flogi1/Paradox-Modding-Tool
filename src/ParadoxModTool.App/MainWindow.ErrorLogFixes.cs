using System.IO;
using System.Text;
using ParadoxModTool.Editor;
using ParadoxModTool.ErrorLog;
using ParadoxModTool.LaunchControl;
using ParadoxModTool.UndoRedo;
using ParadoxModTool.VersionManager;

namespace ParadoxModTool.App;

/// <summary>Fehlerprotokoll Plan 3: die Korrekturen, die Dateien ändern (Kandidat einsetzen, überall umbenennen, Mod ins Playset aufnehmen).</summary>
public partial class MainWindow
{
    /// <summary>Sicherungsordner einer Mod; beim Umbenennen ausgenommen, auch wenn er im Mod-Ordner liegt (Plan-3-Ruling 3).</summary>
    private string ErrorLogSnapshotDirectory(string modFolder) => StorageLocations.ResolveSnapshotsDirectory(modFolder, _storageSettings);

    /// <summary>
    /// Plan-3-Ruling 1: ersetzt den Namen in der Zeile im Editor, als rückgängig machbare Bearbeitung, ohne zu speichern. Rückgabe: Meldung,
    /// wenn der Name dort nicht (mehr) steht; sonst <see langword="null"/>.
    /// </summary>
    private string? ReplaceNameAt(string path, int line, string name, string replacement)
    {
        JumpToBookmarkInFile(path, line);
        var document = AllPanes.SelectMany(p => p.Documents).FirstOrDefault(d => EditorPane.PathsEqual(d.FilePath, path));
        if (document is null || line < 1 || line > document.Document.LineCount)
        {
            return ErrorLogTexts.FixNameNotOnLine(name, line);
        }

        var documentLine = document.Document.GetLineByNumber(line);
        if (NameEdits.FindInLine(document.Document.GetText(documentLine), name) is not int column)
        {
            return ErrorLogTexts.FixNameNotOnLine(name, line);
        }

        document.Document.Replace(documentLine.Offset + column, name.Length, replacement);
        return null;
    }

    /// <summary>Plan-3-Ruling 4: Umbenennen schreiben (Ablauf siehe <see cref="ApplyModEdits"/>).</summary>
    private FixOutcome ApplyRename(IReadOnlyList<ModRenamePlan> plans, string oldName, string newName) =>
        ApplyModEdits(plans, ErrorLogTexts.RenameAction(oldName, newName), ErrorLogTexts.RenameSnapshotNote, ErrorLogTexts.RenameNothing(oldName), ErrorLogTexts.RenameFailed, ErrorLogTexts.RenameDone);

    /// <summary>Spielstand-Explorer Plan 2b: <c>set_variable</c> auskommentieren schreiben, wie Umbenennen mit Sicherung und einem Schritt "Rückgängig".</summary>
    private FixOutcome ApplyCommentOut(IReadOnlyList<ModRenamePlan> plans, string name, string effect) =>
        ApplyModEdits(plans, ErrorLogTexts.CommentOutAction(effect, name), ErrorLogTexts.CommentOutSnapshotNote(effect), ErrorLogTexts.CommentOutNothing(effect, name), ErrorLogTexts.CommentOutFailed, ErrorLogTexts.CommentOutDone);

    /// <summary>
    /// Ersetzungsliste aus dem Compare und Update Tool schreiben (Spec 2026-09-30-replace-lists, Abschnitt 3). Hat sich eine Datei seit der
    /// Vorschau geändert, wird nichts geschrieben; sonst wie Umbenennen: Sicherung der Mod, ein Schritt im Aktionsverlauf.
    /// </summary>
    private FixOutcome ApplyReplaceList(IReadOnlyList<ParadoxModTool.SearchReplace.ReplaceLists.ReplaceFileChange> changes, string modFolder, string listName)
    {
        if (changes.FirstOrDefault(c => !ParadoxModTool.SearchReplace.ReplaceLists.ReplaceListRunner.StillCurrent(c)) is { } stale)
        {
            return new FixOutcome(false, ReplaceListTexts.Stale(Path.GetFileName(stale.Path)));
        }

        var files = changes.Select(c => new RenameFile(c.Path, c.NewText, c.Count, Array.Empty<RenameLine>(), c.HasBom)).ToList();
        var plan = new ModRenamePlan(ParadoxModTool.VersionManager.StorageLocations.GetModName(modFolder), modFolder, files);
        return ApplyModEdits(new[] { plan }, ReplaceListTexts.UndoAction(listName), ReplaceListTexts.SnapshotNote(listName), ReplaceListTexts.NothingToReplace, ReplaceListTexts.Failed, ReplaceListTexts.Done);
    }

    /// <summary>
    /// Änderungen an Mod-Dateien schreiben (Umbenennen, Auskommentieren). Ungespeicherte offene Dateien brechen ab; je Mod eine Sicherung, dann
    /// alle Dateien als ein Schritt im Aktionsverlauf. Scheitert eine Datei, werden die schon geschriebenen zurückgenommen (keine Teiländerung).
    /// </summary>
    private FixOutcome ApplyModEdits(IReadOnlyList<ModRenamePlan> plans, string description, string snapshotNote, string nothing, Func<string, string> failed, Func<int, int, string> done)
    {
        var files = plans.SelectMany(p => p.Files).ToList();
        if (files.Count == 0)
        {
            return new FixOutcome(false, nothing);
        }

        var unsaved = AllPanes.SelectMany(p => p.Documents)
            .FirstOrDefault(d => d.IsModified && files.Any(f => EditorPane.PathsEqual(d.FilePath, f.Path)));
        if (unsaved?.FilePath is { } unsavedPath)
        {
            return new FixOutcome(false, ErrorLogTexts.RenameUnsaved(Path.GetFileName(unsavedPath)));
        }

        foreach (var plan in plans.Where(p => p.Files.Count > 0))
        {
            try
            {
                new ModVersionManager(plan.Folder, ErrorLogSnapshotDirectory(plan.Folder)).CreateSnapshot(snapshotNote);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                return new FixOutcome(false, ErrorLogTexts.RenameSnapshotFailed(ex.Message));
            }
        }

        var writes = files
            .Select(f => (IUndoableAction)new FileWriteUndoableAction(description + ": " + Path.GetFileName(f.Path), f.Path, f.NewText, new UTF8Encoding(f.HasBom)))
            .ToList();
        var written = new List<IUndoableAction>();
        try
        {
            foreach (var write in writes)
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
                    // Weiter mit den übrigen; die Sicherung von eben enthält den alten Stand.
                }
            }

            ReloadOpenTabsAffectedBy(new CompositeUndoableAction(string.Empty, written));
            return new FixOutcome(false, failed(ex.Message));
        }

        var action = new CompositeUndoableAction(description, writes);
        _history.Record(action);
        ReloadOpenTabsAffectedBy(action);
        foreach (var file in files.Where(f => f.Path.EndsWith(".gui", StringComparison.OrdinalIgnoreCase)))
        {
            NotifyGuiFileSaved(file.Path, file.NewText);
        }

        _errorLog?.RefreshSources();
        return new FixOutcome(true, done(files.Sum(f => f.Count), files.Count));
    }

    /// <summary>Plan-3-Rulings 6 und 7: definierende Mod ins gewählte Playset; danach Playset-Auswahl und Quellen neu laden.</summary>
    private FixOutcome AddModToPlaysetFor(ModSource mod)
    {
        var service = _errorLog;
        if (service?.PlaysetName is not { } playset)
        {
            return new FixOutcome(false, ErrorLogTexts.AddModNoPlayset);
        }

        var own = _mods?.Registry.Mods.SelectMany(m => m.Paths).ToList() ?? new List<string>();
        var result = PlaysetEdits.AddMod(_playsetsFilePath, playset, mod.Directory, own, () => GameProcess.IsRunning(_gameExecutablePath));
        if (result.Changed)
        {
            RefreshPlaysetsCombo();
            service.RefreshSources();
        }

        return result.Status switch
        {
            PlaysetEditStatus.Added => new FixOutcome(true, ErrorLogTexts.AddModAdded(mod.Name, playset)),
            PlaysetEditStatus.Enabled => new FixOutcome(true, ErrorLogTexts.AddModEnabled(mod.Name, playset)),
            PlaysetEditStatus.AlreadyPresent => new FixOutcome(true, ErrorLogTexts.AddModAlreadyPresent(mod.Name, playset)),
            PlaysetEditStatus.GameRunning => new FixOutcome(false, ErrorLogTexts.AddModGameRunning),
            PlaysetEditStatus.FileMissing => new FixOutcome(false, ErrorLogTexts.AddModFileMissing),
            PlaysetEditStatus.PlaysetMissing => new FixOutcome(false, ErrorLogTexts.AddModPlaysetMissing(playset)),
            PlaysetEditStatus.InvalidFile => new FixOutcome(false, ErrorLogTexts.AddModInvalidFile),
            PlaysetEditStatus.LauncherDatabase => new FixOutcome(false, PlaysetTexts.LauncherDatabaseReadOnly),
            _ => new FixOutcome(false, ErrorLogTexts.AddModWriteFailed(result.Detail ?? string.Empty)),
        };
    }
}
