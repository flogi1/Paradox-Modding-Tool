using System.IO;
using System.Windows;
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
    private const string Caption = "Projekt";

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
            MessageBox.Show(owner, $"Der Ordner der Mod \"{(fromFolder is null ? from.Name : to.Name)}\" wurde nicht gefunden.", Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
            return string.Empty;
        }

        var files = relativePaths.Where(p => File.Exists(Path.Combine(fromFolder, p.Replace('/', Path.DirectorySeparatorChar)))).ToList();
        if (files.Count == 0)
        {
            return "Keine Dateien zum Übertragen.";
        }

        var blocked = files
            .Where(p => hooks.IsModifiedInEditor(Path.Combine(toFolder, p.Replace('/', Path.DirectorySeparatorChar)))
                        || (mode == TransferMode.Move && hooks.IsModifiedInEditor(Path.Combine(fromFolder, p.Replace('/', Path.DirectorySeparatorChar)))))
            .ToList();
        if (blocked.Count > 0)
        {
            MessageBox.Show(owner, "Diese Dateien haben ungespeicherte Änderungen im Editor. Speichere oder verwirf sie zuerst:\n\n" + string.Join("\n", blocked.Take(10)) + (blocked.Count > 10 ? "\n…" : string.Empty), Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
            return string.Empty;
        }

        var existing = files.Where(p => BranchTransfer.TargetExists(to, p)).ToList();
        var overwrite = false;
        if (existing.Count == 1)
        {
            var answer = MessageBox.Show(owner, $"Die Datei\n{existing[0]}\nexistiert schon in \"{to.Name}\".\n\nÜberschreiben? Die alte Datei wird vorher gesichert.", Caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                return "Abgebrochen.";
            }

            overwrite = true;
        }
        else if (existing.Count > 1)
        {
            var answer = MessageBox.Show(owner, $"{existing.Count} Dateien existieren schon in \"{to.Name}\".\n\nJa: alle überschreiben (die alten Dateien werden vorher gesichert)\nNein: vorhandene überspringen\nAbbrechen: nichts übertragen", Caption, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel)
            {
                return "Abgebrochen.";
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
            MessageBox.Show(owner, "Die Übertragung wurde abgebrochen:\n" + ex.Message, Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
            return $"{done} Datei(en) übertragen, dann Fehler.";
        }

        var verb = mode == TransferMode.Move ? "verschoben" : "kopiert";
        var text = $"{done} Datei(en) nach {to.Name} {verb}";
        if (skipped > 0)
        {
            text += $", {skipped} übersprungen";
        }

        if (locked > 0)
        {
            text += $", {locked} gesperrt";
        }

        return text + ".";
    }
}
