using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;
using ParadoxModTool.Editor;
using ParadoxModTool.Session;

namespace ParadoxModTool.App;

/// <summary>
/// Schutz vor Datenverlust durch ungespeicherte Änderungen (Nutzer-Feedback): Rückfrage beim
/// Schließen eines geänderten Tabs, dazu ein Wiederherstellungsspeicher (<see cref="RecoveryStore"/>),
/// der beim Beenden und alle 30 Sekunden (Absturzschutz) den Stand geänderter Dokumente sichert und
/// beim nächsten Start wieder einspielt.
/// </summary>
public partial class MainWindow
{
    private static readonly TimeSpan RecoveryInterval = TimeSpan.FromSeconds(30);

    private readonly RecoveryStore _recoveryStore = new(GameContext.Repository.GameRecoveryDirectory(GameContext.GameId));
    private DispatcherTimer? _recoveryTimer;

    private IEnumerable<EditorPane> AllPanes => new[] { LeftEditor, RightEditor };

    /// <summary>Fragt bei ungespeicherten Änderungen nach (Speichern / Verwerfen / Abbrechen), bevor der Tab geschlossen wird.</summary>
    private void CloseWithPrompt(EditorPane pane, OpenDocument document)
    {
        if (document.IsModified)
        {
            var answer = MessageBox.Show(
                this,
                MainTexts.CloseUnsaved(document.DisplayName),
                MainTexts.CloseUnsavedTitle,
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning,
                MessageBoxResult.Yes);

            if (answer == MessageBoxResult.Cancel)
            {
                return;
            }

            if (answer == MessageBoxResult.Yes)
            {
                pane.ActivateDocument(document);
                SaveActiveDocumentOf(pane);

                // Speichern abgebrochen (Dateidialog) oder fehlgeschlagen: Tab bleibt offen.
                if (document.IsModified)
                {
                    return;
                }
            }
        }

        pane.CloseDocument(document);
        SyncRecovery();
    }

    /// <summary>Schreibt den Stand aller ungespeicherten Dokumente in den Wiederherstellungsspeicher und räumt Sicherungen erledigter Dokumente auf.</summary>
    private void SyncRecovery()
    {
        var entries = new List<RecoveryEntry>();

        foreach (var document in AllPanes.SelectMany(pane => pane.Documents).Where(d => d.IsModified))
        {
            long? diskTicks = null;
            if (document.FilePath is not null && File.Exists(document.FilePath))
            {
                diskTicks = File.GetLastWriteTimeUtc(document.FilePath).Ticks;
            }

            entries.Add(new RecoveryEntry
            {
                Id = document.RecoveryId,
                FilePath = document.FilePath,
                Text = document.Document.Text,
                DiskWriteTimeUtcTicks = diskTicks,
            });
        }

        try
        {
            _recoveryStore.Sync(entries);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sicherung nicht möglich (z. B. kein Schreibzugriff auf %APPDATA%) - darf das Arbeiten nicht stören.
        }
    }

    private void StartRecoveryTimer()
    {
        _recoveryTimer = new DispatcherTimer { Interval = RecoveryInterval };
        _recoveryTimer.Tick += (_, _) => SyncRecovery();
        _recoveryTimer.Start();
    }

    /// <summary>
    /// Spielt gesicherte, ungespeicherte Stände aus der letzten Sitzung bzw. einem Absturz wieder ein.
    /// Läuft NACH <c>RestoreSession</c>: Dateien, die dort schon geöffnet wurden, erhalten den
    /// gesicherten Text (Tab wieder als geändert markiert), alle anderen werden im linken Arbeitsbereich
    /// neu geöffnet. Hat sich die Datei seit der Sicherung außerhalb des Programms verändert, entscheidet
    /// der Nutzer.
    /// </summary>
    private void ApplyRecoveries()
    {
        IReadOnlyList<RecoveryEntry> entries;
        try
        {
            entries = _recoveryStore.LoadAll();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var entry in entries)
        {
            try
            {
                ApplyRecovery(entry);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Datei unlesbar - Sicherung bleibt erhalten und wird beim nächsten Start erneut versucht.
                continue;
            }

            _recoveryStore.Delete(entry.Id);
        }

        // Sofort unter den neuen Dokument-Kennungen neu sichern, damit die Wiederherstellung nicht
        // erst nach dem ersten Timer-Tick wieder abgesichert ist.
        SyncRecovery();
    }

    private void ApplyRecovery(RecoveryEntry entry)
    {
        OpenDocument? document = null;
        EditorPane? pane = null;

        if (entry.FilePath is not null)
        {
            foreach (var candidatePane in AllPanes)
            {
                document = candidatePane.Documents.FirstOrDefault(d => EditorPane.PathsEqual(d.FilePath, entry.FilePath));
                if (document is not null)
                {
                    pane = candidatePane;
                    break;
                }
            }
        }

        var isNew = document is null;

        if (entry.FilePath is not null && File.Exists(entry.FilePath))
        {
            var currentTicks = File.GetLastWriteTimeUtc(entry.FilePath).Ticks;
            if (entry.DiskWriteTimeUtcTicks is long savedTicks && savedTicks != currentTicks && !AskUseRecovery(entry.FilePath))
            {
                return;
            }
        }

        if (isNew)
        {
            document = entry.FilePath is null
                ? OpenDocument.NewEmpty(_activeHighlighting)
                : File.Exists(entry.FilePath)
                    ? OpenDocument.LoadFromFile(entry.FilePath, _activeHighlighting)
                    : new OpenDocument(entry.FilePath, string.Empty, _activeHighlighting);
            pane = LeftEditor;
        }

        if (document!.Document.Text == entry.Text)
        {
            // Sicherung entspricht dem Stand auf der Platte - nichts wiederherzustellen.
            if (isNew)
            {
                pane!.AddDocument(document, select: false);
            }

            return;
        }

        // Der Textwechsel läuft über den Undo-Stack: der Tab gilt als geändert, und Strg+Z führt zum Plattenstand zurück.
        document.Document.Text = entry.Text;

        if (isNew)
        {
            pane!.AddDocument(document, select: false);
            UpdateHasNote(document);
        }
    }

    private bool _checkingExternalChanges;

    /// <summary>
    /// Erkennt Änderungen an offenen Dateien durch andere Programme (Nutzer-Feedback), sobald das Fenster
    /// wieder aktiviert wird. Ungeänderte Dokumente werden still neu geladen; hat das Dokument selbst
    /// ungespeicherte Änderungen, entscheidet der Nutzer (Strg+Z holt den eigenen Stand zurück).
    /// </summary>
    private void CheckExternalChanges()
    {
        // Ein Meldungsfenster löst selbst wieder Aktivierungen aus - Wiedereintritt verhindern.
        if (_checkingExternalChanges)
        {
            return;
        }

        _checkingExternalChanges = true;
        try
        {
            foreach (var pane in AllPanes)
            {
                foreach (var document in pane.Documents.ToList())
                {
                    CheckExternalChange(pane, document);
                }
            }
        }
        finally
        {
            _checkingExternalChanges = false;
        }
    }

    private void CheckExternalChange(EditorPane pane, OpenDocument document)
    {
        if (document.FilePath is not { } path || document.DiskWriteTimeUtc is not { } known)
        {
            return;
        }

        try
        {
            if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) == known)
            {
                return;
            }

            if (document.IsModified)
            {
                var reload = MessageBox.Show(
                    this,
                    MainTexts.ExternalChanged(document.DisplayName),
                    MainTexts.ExternalChangedTitle,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No) == MessageBoxResult.Yes;

                if (!reload)
                {
                    // Nicht erneut nachfragen, bis die Datei wieder geändert wird.
                    document.RefreshDiskStamp();
                    return;
                }
            }

            pane.ReloadFromDisk(document);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Datei gerade vom anderen Programm gesperrt - beim nächsten Aktivieren erneut versuchen.
        }
    }

    private static bool AskUseRecovery(string filePath) =>
        MessageBox.Show(
            MainTexts.RecoveryChanged(Path.GetFileName(filePath)),
            MainTexts.RecoveryTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
}
