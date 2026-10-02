using System.IO;
using System.Windows;
using ParadoxModTool.Localization;
using ParadoxModTool.Mods;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Was das Hauptfenster über offene Editor-Tabs weiß: <paramref name="IsModifiedInEditor"/> (Datei offen mit ungespeicherten Änderungen),
/// <paramref name="TargetWritten"/> (Datei wurde geschrieben, offener unveränderter Tab neu laden), <paramref name="SourceRemoved"/> (Datei wurde verschoben, Tab schließen).
/// </summary>
public sealed record BranchHooks(Func<string, bool> IsModifiedInEditor, Action<string> TargetWritten, Action<string> SourceRemoved);

/// <summary>Führt Übertragungen zwischen Mods eines Projekts mit Rückfragen und Fehlermeldungen aus (Logik in <see cref="BranchTransfer"/>).</summary>
public static class BranchActions
{
    private static string Caption => MainTexts.ProjectCaption;

    /// <summary>Überträgt die Dateien (relativ zum Mod-Ordner der Quelle). Gibt einen kurzen Ergebnistext für die Statusleiste zurück; Rückfragen und Fehler zeigt die Methode selbst.</summary>
    public static string Transfer(Window owner, ModRegistry registry, ModInfo from, ModInfo to, IReadOnlyList<string> relativePaths, TransferMode mode, BranchHooks hooks)
    {
        var check = BranchTransfer.Check(registry, from, to, mode);
        if (!check.Allowed)
        {
            MessageBox.Show(owner, check.Reason, Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
            return string.Empty;
        }

        var fromFolder = BranchTransfer.WorkingFolder(from);
        var toFolder = BranchTransfer.WorkingFolder(to);
        if (fromFolder is null || toFolder is null)
        {
            MessageBox.Show(owner, MainTexts.ModFolderMissing(fromFolder is null ? from.Name : to.Name), Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
            return string.Empty;
        }

        var files = relativePaths.Where(p => File.Exists(Path.Combine(fromFolder, p.Replace('/', Path.DirectorySeparatorChar)))).ToList();
        if (files.Count == 0)
        {
            return MainTexts.TransferNoFiles;
        }

        var blocked = files
            .Where(p => hooks.IsModifiedInEditor(Path.Combine(toFolder, p.Replace('/', Path.DirectorySeparatorChar)))
                        || (mode == TransferMode.Move && hooks.IsModifiedInEditor(Path.Combine(fromFolder, p.Replace('/', Path.DirectorySeparatorChar)))))
            .ToList();
        if (blocked.Count > 0)
        {
            MessageBox.Show(owner, MainTexts.TransferUnsaved(string.Join("\n", blocked.Take(10)) + (blocked.Count > 10 ? "\n…" : string.Empty)), Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
            return string.Empty;
        }

        var existing = files.Where(p => BranchTransfer.TargetExists(to, p)).ToList();
        var overwrite = false;
        if (existing.Count == 1)
        {
            var answer = MessageBox.Show(owner, MainTexts.TransferExistsOne(existing[0], to.Name), Caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                return CommonTexts.Cancelled;
            }

            overwrite = true;
        }
        else if (existing.Count > 1)
        {
            var answer = MessageBox.Show(owner, MainTexts.TransferExistsMany(existing.Count, to.Name), Caption, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel)
            {
                return CommonTexts.Cancelled;
            }

            overwrite = answer == MessageBoxResult.Yes;
        }

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        var done = 0;
        var skipped = 0;
        var locked = 0;
        try
        {
            foreach (var path in files)
            {
                var result = BranchTransfer.Execute(registry, from, to, path, mode, overwrite, stamp);
                if (result.Outcome is TransferOutcome.TargetLocked or TransferOutcome.SourceLocked)
                {
                    locked++;
                    continue;
                }

                if (result.Outcome != TransferOutcome.Done)
                {
                    skipped++;
                    continue;
                }

                done++;
                hooks.TargetWritten(Path.Combine(toFolder, path.Replace('/', Path.DirectorySeparatorChar)));
                if (mode == TransferMode.Move)
                {
                    hooks.SourceRemoved(Path.Combine(fromFolder, path.Replace('/', Path.DirectorySeparatorChar)));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show(owner, MainTexts.TransferFailed(ex.Message), Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
            return MainTexts.TransferPartial(done);
        }

        var text = mode == TransferMode.Move ? MainTexts.TransferMoved(done, to.Name) : MainTexts.TransferCopied(done, to.Name);
        if (skipped > 0)
        {
            text += MainTexts.TransferSkipped(skipped);
        }

        if (locked > 0)
        {
            text += MainTexts.TransferLocked(locked);
        }

        return text + ".";
    }
}
