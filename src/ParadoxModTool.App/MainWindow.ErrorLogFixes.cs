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

        var writes = files
            .Select(f => new ModWrite(f.Path, f.NewText, new FileWriteUndoableAction(description + ": " + Path.GetFileName(f.Path), f.Path, f.NewText, new UTF8Encoding(f.HasBom))))
            .ToList();
        var folders = plans.Where(p => p.Files.Count > 0).Select(p => p.Folder).ToList();
        return WriteModActions(folders, writes, description, snapshotNote, failed)
            ?? new FixOutcome(true, done(files.Sum(f => f.Count), files.Count));
    }

    /// <summary>
    /// ENC-001 ohne error.log: das BOM aus Dateien eigener Mods entfernen, sonst Byte für Byte gleich (<see cref="BomCheck.WithoutBom"/>).
    /// Gelesen wird erst jetzt; Dateien, die inzwischen kein BOM mehr haben oder seit der Prüfung gelöscht bzw. verschoben wurden, bleiben
    /// unberührt. Ablauf wie <see cref="ApplyModEdits"/>: eine Sicherung je betroffener Mod, alles ein Schritt im Aktionsverlauf.
    /// Läuft im Hintergrund (Fehlerprotokoll, "Alle entfernen" bei vielen Dateien); <see cref="WriteModActions"/> holt sich den UI-Thread selbst.
    /// Weil der Editor solange bedienbar bleibt: vor dem Schreiben noch einmal die Prüfung auf ungespeicherte Dateien, eine seit dem Lesen
    /// gespeicherte Datei (Änderungszeit oder Länge anders) bleibt unberührt und steht in der Meldung, geänderte Tabs werden nicht neu geladen.
    /// </summary>
    private FixOutcome RemoveBoms(IReadOnlyList<BomFinding> findings)
    {
        var changes = new List<(BomFinding Finding, byte[] Content, BomFileStamp Stamp)>();
        foreach (var finding in findings)
        {
            try
            {
                if (BomCheck.ReadWithoutBom(finding.FullPath, out var stamp) is { } content)
                {
                    changes.Add((finding, content, stamp));
                }
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                // Seit der Prüfung gelöscht oder verschoben: nichts zu tun.
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new FixOutcome(false, ErrorLogTexts.BomRemoveFailed(ex.Message));
            }
        }

        if (changes.Count == 0)
        {
            return new FixOutcome(false, ErrorLogTexts.BomNothing);
        }

        var description = ErrorLogTexts.BomRemoveAction(changes.Count);
        var writes = changes
            .Select(c => new ModWrite(c.Finding.FullPath, null, new BinaryWriteUndoableAction(description + ": " + Path.GetFileName(c.Finding.FullPath), c.Finding.FullPath, c.Content)))
            .ToList();
        var stamps = changes.ToDictionary(c => c.Finding.FullPath, c => c.Stamp, StringComparer.OrdinalIgnoreCase);
        var guard = new BackgroundWriteGuard(w => BomCheck.Unchanged(w.Path, stamps[w.Path]));
        var folders = changes.Select(c => c.Finding.ModRoot).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (WriteModActions(folders, writes, description, ErrorLogTexts.BomSnapshotNote, ErrorLogTexts.BomRemoveFailed, guard) is { } problem)
        {
            return problem;
        }

        var skipped = changes
            .Where(c => guard.Skipped.Contains(c.Finding.FullPath, StringComparer.OrdinalIgnoreCase))
            .Select(c => ErrorLogTexts.BomLine(c.Finding.ModName, c.Finding.RelativePath))
            .ToList();
        var skippedNote = skipped.Count > 0 ? ErrorLogTexts.BomSkipped(ErrorLogTexts.BomFileList(skipped)) : null;
        var removed = changes.Count - skipped.Count;
        if (removed == 0)
        {
            return new FixOutcome(false, skippedNote!);
        }

        var done = ErrorLogTexts.BomRemoveDone(removed);
        return new FixOutcome(true, skippedNote is null ? done : done + Environment.NewLine + Environment.NewLine + skippedNote);
    }

    /// <summary>
    /// Schutz für Schreiben aus einem Hintergrund-Thread (<see cref="RemoveBoms"/>): <see cref="StillCurrent"/> entscheidet unmittelbar vor jeder
    /// Datei, ob sie noch geschrieben werden darf; übersprungene Pfade landen in <see cref="Skipped"/>.
    /// </summary>
    private sealed record BackgroundWriteGuard(Func<ModWrite, bool> StillCurrent)
    {
        public List<string> Skipped { get; } = new();
    }

    /// <summary>
    /// Gemeinsamer Kern der Korrekturen an Mod-Dateien: Ungespeicherte offene Dateien brechen ab; je Mod-Ordner eine Sicherung, dann alle
    /// Schreibaktionen als ein Schritt im Aktionsverlauf. Scheitert eine, werden die schon geschriebenen zurückgenommen (keine Teiländerung).
    /// <see cref="ModWrite.Text"/>: neuer Inhalt für den GUI-Index (.gui); <see langword="null"/>: den liest das Neuladen der Tabs von der Platte.
    /// Rückgabe: <see langword="null"/> bei Erfolg, sonst das Ergebnis mit der Fehlermeldung.
    /// Darf aus einem Hintergrund-Thread kommen: offene Dokumente, Aktionsverlauf, Tabs und Fehlerprotokoll laufen über den UI-Thread
    /// (<c>Dispatcher.Invoke</c>, im UI-Thread direkt), Sicherung und Schreiben im aufrufenden Thread. Mit <paramref name="guard"/> (Hintergrund):
    /// nach der Sicherung noch einmal die Prüfung auf ungespeicherte Dateien, je Datei <see cref="BackgroundWriteGuard.StillCurrent"/>, und geänderte
    /// Tabs werden nicht neu geladen. Wird dabei keine Datei geschrieben, gibt es auch keinen Schritt im Aktionsverlauf.
    /// </summary>
    private FixOutcome? WriteModActions(
        IReadOnlyList<string> folders,
        IReadOnlyList<ModWrite> writes,
        string description,
        string snapshotNote,
        Func<string, string> failed,
        BackgroundWriteGuard? guard = null)
    {
        FixOutcome? Unsaved()
        {
            var unsavedPath = Dispatcher.Invoke(() => AllPanes.SelectMany(p => p.Documents)
                .FirstOrDefault(d => d.IsModified && writes.Any(w => EditorPane.PathsEqual(d.FilePath, w.Path)))?.FilePath);
            return unsavedPath is null ? null : new FixOutcome(false, ErrorLogTexts.RenameUnsaved(Path.GetFileName(unsavedPath)));
        }

        if (Unsaved() is { } unsaved)
        {
            return unsaved;
        }

        foreach (var folder in folders)
        {
            try
            {
                new ModVersionManager(folder, ErrorLogSnapshotDirectory(folder)).CreateSnapshot(snapshotNote);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                return new FixOutcome(false, ErrorLogTexts.RenameSnapshotFailed(ex.Message));
            }
        }

        // Die Sicherung kann dauern; im Hintergrund kann der Nutzer inzwischen eine der Dateien geändert haben.
        if (guard is not null && Unsaved() is { } unsavedNow)
        {
            return unsavedNow;
        }

        var written = new List<IUndoableAction>();
        var performed = new List<ModWrite>();
        try
        {
            foreach (var write in writes)
            {
                if (guard is not null && !guard.StillCurrent(write))
                {
                    guard.Skipped.Add(write.Path);
                    continue;
                }

                write.Action.Do();
                written.Add(write.Action);
                performed.Add(write);
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

            Dispatcher.Invoke(() => ReloadOpenTabsAffectedBy(new CompositeUndoableAction(string.Empty, written), keepModified: guard is not null));
            return new FixOutcome(false, failed(ex.Message));
        }

        if (performed.Count == 0)
        {
            return null;
        }

        var action = new CompositeUndoableAction(description, performed.Select(w => w.Action).ToList());
        Dispatcher.Invoke(() =>
        {
            _history.Record(action);
            ReloadOpenTabsAffectedBy(action, keepModified: guard is not null);
            foreach (var write in performed)
            {
                if (write.Text is { } text && write.Path.EndsWith(".gui", StringComparison.OrdinalIgnoreCase))
                {
                    NotifyGuiFileSaved(write.Path, text);
                }
            }

            _errorLog?.RefreshSources();
        });
        return null;
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
        var launcherGame = PlaysetStore.IsLauncherDatabase(_playsetsFilePath);
        if (launcherGame && !EnsureGameExecutable())
        {
            return new FixOutcome(false, ParadoxModTool.Localization.CommonTexts.Cancelled);
        }

        // Launcher-Spiele laufen über die Playset-Verwaltung (Datenbank, Sicherung, Schutz vor laufendem Launcher), playsets.json wie bisher über PlaysetEdits.
        var result = launcherGame && CreatePlaysetManager() is { } manager
            ? manager.AddMod(playset, new PlaysetModEntry(mod.Directory, mod.Name, true, null), own)
            : PlaysetEdits.AddMod(_playsetsFilePath, playset, mod.Directory, own, () => GameProcess.IsRunning(_gameExecutablePath));
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
            PlaysetEditStatus.WriteFailed when launcherGame => new FixOutcome(false, result.Detail ?? string.Empty),
            _ => new FixOutcome(false, ErrorLogTexts.AddModWriteFailed(result.Detail ?? string.Empty)),
        };
    }
}

/// <summary>Eine Schreibaktion einer Korrektur: Zieldatei, neuer Text für den GUI-Index (bei Binärinhalt <see langword="null"/>) und die Aktion.</summary>
internal sealed record ModWrite(string Path, string? Text, IUndoableAction Action);
