using System.ComponentModel;
using System.IO;
using System.Text;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ParadoxModTool.Categories;

namespace ParadoxModTool.Editor;

/// <summary>
/// Ein einzelnes geöffnetes Dokument innerhalb eines Arbeitsbereichs (Abschnitt 5,
/// Split-View: "Dokumente ... verschiebbar"). Kapselt den eigentlichen Textinhalt in
/// einem eigenständigen AvalonEdit-<see cref="TextDocument"/>, damit ein Dokument beim
/// Verschieben zwischen zwei Arbeitsbereichen (<see cref="EditorPane.AdoptDocument"/>/
/// <see cref="EditorPane.ReleaseDocument"/>) seinen Inhalt — inklusive AvalonEdits
/// eigenem Text-Undo-Verlauf — unverändert mitnimmt, statt neu eingelesen werden zu müssen.
///
/// Implementiert seit Runde 4 <see cref="INotifyPropertyChanged"/>, damit die an den Tab
/// gebundenen, farbabhängigen Eigenschaften (<see cref="CategoryColor"/>, <see cref="TabBackground"/>)
/// sofort neu gezeichnet werden, wenn sich die Kategorie eines Dokuments oder eine globale
/// Farbeinstellung ändert — vorher (Runde 1-3) war das nicht nötig, da sich die Kategorie eines
/// Dokuments nie zur Laufzeit änderte.
/// </summary>
public sealed class OpenDocument : INotifyPropertyChanged
{
    public OpenDocument(string? filePath, string initialText, IHighlightingDefinition? highlighting)
    {
        FilePath = filePath;
        Document = new TextDocument(initialText);
        Highlighting = highlighting;
        AutoCategory = FileCategoryClassifier.Classify(filePath);
        Origin = FileOriginTracker.Classify(filePath);
        RefreshLock();
        RefreshErrorMark();

        // Änderungsstatus (Nutzer-Feedback: Warnung bei ungespeicherten Änderungen): AvalonEdits
        // Undo-Stack weiß selbst, ob der aktuelle Stand dem zuletzt als "Original" markierten
        // entspricht - auch nach Rückgängig bis zum Ausgangstext wird das Dokument wieder "sauber".
        Document.UndoStack.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Document.UndoStack.IsOriginalFile))
            {
                OnPropertyChanged(nameof(IsModified));
            }
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Global für ALLE Arbeitsbereiche gültige Einstellung aus dem "Einstellungen"-Menü
    /// (Nutzer-Feedback Runde 4: "Schaffe die Möglichkeit ... dass die Farbcodes oben in der
    /// Dokumentenliste den ganzen Reiter einfärben."). MainWindow setzt dieses Feld beim Umschalten
    /// der Einstellung bzw. beim Wiederherstellen der Session und ruft anschließend
    /// <see cref="NotifyColorSettingsChanged"/> auf allen offenen Dokumenten auf, damit bereits
    /// offene Tabs sofort neu zeichnen, statt erst beim nächsten Tabwechsel.
    /// </summary>
    public static bool ColorizeWholeTabEnabled { get; set; }

    /// <summary>Pfad der Datei auf der Platte, oder null für ein noch nicht gespeichertes neues Dokument.</summary>
    public string? FilePath { get; private set; }

    /// <summary>
    /// Anhand der Dateiendung automatisch ermittelte Kategorie (siehe
    /// <see cref="FileCategoryClassifier.Classify"/>). Bleibt auch bei gesetzter
    /// <see cref="ManualCategory"/> im Hintergrund erhalten, damit sie beim Zurücksetzen der
    /// manuellen Zuweisung (<see cref="SetManualCategory"/> mit <see langword="null"/>) wieder
    /// greift, ohne die Datei erneut klassifizieren zu müssen.
    /// </summary>
    private string AutoCategory { get; set; }

    /// <summary>
    /// Vom Nutzer manuell zugewiesene Kategorie-Kennung (Nutzer-Feedback Runde 4, seit den freien Kategorien
    /// für jede Kategorie möglich) oder <see langword="null"/> (keine manuelle Zuweisung, es gilt die
    /// automatisch ermittelte Kategorie). Eine manuelle Zuweisung überschreibt bewusst IMMER die
    /// automatische. Eine Kennung, die nicht mehr existiert (Kategorie gelöscht), wirkt wie
    /// "Sonstige" - siehe <see cref="FileCategoryClassifier.Find"/>.
    /// </summary>
    public string? ManualCategory { get; private set; }

    /// <summary>Die tatsächlich wirksame Kategorie: die manuelle Zuweisung, falls vorhanden, sonst die automatisch ermittelte.</summary>
    public string Category
    {
        get
        {
            var manual = ManualCategory;
            return manual is not null && FileCategoryClassifier.Exists(manual) ? manual : AutoCategory;
        }
    }

    /// <summary>Für die Farbmarkierung am Tab (WPF-Bindung kann keine statischen Methoden aufrufen).</summary>
    public System.Windows.Media.Brush CategoryColor => FileCategoryClassifier.ColorFor(Category);

    /// <summary>Für den Tooltip am Tab.</summary>
    public string CategoryDisplayName => FileCategoryClassifier.DisplayName(Category);

    /// <summary>
    /// Hintergrundfarbe des GESAMTEN Tab-Eintrags (Nutzer-Feedback Runde 4), halbtransparent
    /// (siehe <see cref="FileCategoryClassifier.TabBackgroundFor"/>) — oder vollständig transparent,
    /// solange <see cref="ColorizeWholeTabEnabled"/> nicht aktiviert ist (Standardverhalten wie in
    /// Runde 1-3: nur der schmale farbige Balken links am Tab).
    /// </summary>
    public System.Windows.Media.Brush TabBackground => ColorizeWholeTabEnabled
        ? FileCategoryClassifier.TabBackgroundFor(Category)
        : System.Windows.Media.Brushes.Transparent;

    /// <summary>Der eigentliche Textinhalt samt AvalonEdit-eigenem Undo-Verlauf.</summary>
    public TextDocument Document { get; }

    public IHighlightingDefinition? Highlighting { get; set; }

    /// <summary>
    /// Zeichenposition des Cursors — wird von <see cref="EditorPane"/> beim Wegschalten von diesem
    /// Tab aktualisiert und beim Zurückschalten wiederhergestellt (inkl. Sprung zur zugehörigen
    /// Zeile), sowie von der Session-Wiederherstellung (Abschnitt 12) gesetzt.
    /// </summary>
    public int CaretOffset { get; set; }

    /// <summary>Eindeutige Kennung dieses Dokuments in der laufenden Sitzung - Dateiname seiner Wiederherstellungs-Sicherung.</summary>
    public string RecoveryId { get; } = Guid.NewGuid().ToString("N");

    /// <summary>Ob der Text seit dem Laden bzw. letzten Speichern verändert wurde (Anzeige "●" im Tab).</summary>
    public bool IsModified => !Document.UndoStack.IsOriginalFile;

    /// <summary>Markiert den aktuellen Textstand als gespeichert (nach Speichern bzw. Neuladen von der Platte).</summary>
    public void MarkSaved()
    {
        Document.UndoStack.MarkAsOriginalFile();
        OnPropertyChanged(nameof(IsModified));
    }

    /// <summary>Anzeigename für den Tab: Dateiname, oder "Unbenannt" für ein neues, ungespeichertes Dokument.</summary>
    public string DisplayName => FilePath is null ? EditorTexts.Untitled : Path.GetFileName(FilePath);

    /// <summary>
    /// Ob zu dieser Datei aktuell eine Notiz existiert (Nutzer-Feedback Runde 5: "kleines Icon
    /// ... das im Dateienreiter anzeigt, dass es Notizen zu dieser Datei gibt"). Dieses Dokument
    /// greift bewusst NIE selbst auf ParadoxModTool.Notes.FileNoteStore zu (das würde eine
    /// Projektabhängigkeit von Editor auf Notes einführen) - MainWindow aktualisiert diesen Wert
    /// stattdessen über <see cref="SetHasNote"/>, sobald eine Datei geladen bzw. eine Notiz
    /// gespeichert/gelöscht wird (siehe MainWindow.UpdateHasNote).
    /// </summary>
    public bool HasNote { get; private set; }

    /// <summary>
    /// Ob in dieser Datei mindestens ein Lesezeichen (Strg+F2) gesetzt ist - Anzeige eines kleinen
    /// Icons im Tab. Wie <see cref="HasNote"/> von MainWindow gesetzt, da Editor nicht auf Notes verweist.
    /// </summary>
    public bool HasBookmark { get; private set; }

    /// <summary>Tooltip des Lesezeichen-Icons im Tab: Zeilen und Kommentare der Lesezeichen dieser Datei.</summary>
    public string BookmarkTooltip { get; private set; } = EditorTexts.BookmarkTip;

    public void SetHasBookmark(bool hasBookmark, string? tooltip = null)
    {
        if (tooltip is not null && BookmarkTooltip != tooltip)
        {
            BookmarkTooltip = tooltip;
            OnPropertyChanged(nameof(BookmarkTooltip));
        }

        if (HasBookmark == hasBookmark)
        {
            return;
        }

        HasBookmark = hasBookmark;
        OnPropertyChanged(nameof(HasBookmark));
        OnPropertyChanged(nameof(BookmarkIconVisible));
    }

    /// <summary>
    /// Ob diese Datei mindestens einen OFFENEN Bug hat (Paket 12c) - Symbol im Tab und Grundlage des Filters
    /// "Bug". Wie <see cref="HasNote"/> von MainWindow gesetzt, da Editor nicht auf Notes verweist.
    /// </summary>
    public bool HasBug { get; private set; }

    /// <summary>Tooltip des Bug-Symbols im Tab: Titel der offenen Bugs dieser Datei.</summary>
    public string BugTooltip { get; private set; } = EditorTexts.BugTip;

    public void SetHasBug(bool hasBug, string? tooltip = null)
    {
        if (tooltip is not null && BugTooltip != tooltip)
        {
            BugTooltip = tooltip;
            OnPropertyChanged(nameof(BugTooltip));
        }

        if (HasBug == hasBug)
        {
            return;
        }

        HasBug = hasBug;
        OnPropertyChanged(nameof(HasBug));
        OnPropertyChanged(nameof(BugIconVisible));
    }

    /// <summary>
    /// Herkunft der Datei (Vanilla, Mod, Extern), aus dem Pfad und den Wurzelordnern in
    /// <see cref="FileOriginTracker"/> ermittelt. Grundlage der Herkunfts-Symbole und des Herkunftsfilters.
    /// </summary>
    public FileOrigin Origin { get; private set; }

    /// <summary>Tooltip der Herkunfts-Symbole im Tab.</summary>
    public string OriginTooltip => Origin switch
    {
        FileOrigin.Vanilla => EditorTexts.OriginVanillaTip,
        FileOrigin.Mod => EditorTexts.OriginModTip,
        _ => string.Empty,
    };

    /// <summary>Ermittelt die Herkunft neu, z. B. nachdem sich Wurzelordner oder der Spielpfad geändert haben.</summary>
    public void RefreshOrigin()
    {
        var origin = FileOriginTracker.Classify(FilePath);
        if (origin == Origin)
        {
            return;
        }

        Origin = origin;
        RaiseOriginChanged();
    }

    private void RaiseOriginChanged()
    {
        OnPropertyChanged(nameof(Origin));
        OnPropertyChanged(nameof(OriginTooltip));
        OnPropertyChanged(nameof(VanillaIconVisible));
        OnPropertyChanged(nameof(ModIconVisible));
    }

    // Sichtbarkeit der Tab-Symbole: vorhanden UND in den Einstellungen (TabIconSettings) eingeschaltet.
    public bool NoteIconVisible => HasNote && TabIconSettings.IsVisible(TabIconKind.Note);

    public bool BookmarkIconVisible => HasBookmark && TabIconSettings.IsVisible(TabIconKind.Bookmark);

    public bool BugIconVisible => HasBug && TabIconSettings.IsVisible(TabIconKind.Bug);

    public bool VanillaIconVisible => Origin == FileOrigin.Vanilla && TabIconSettings.IsVisible(TabIconKind.Vanilla);

    public bool ModIconVisible => Origin == FileOrigin.Mod && TabIconSettings.IsVisible(TabIconKind.Mod);

    /// <summary>Tooltip der Sperre dieser Datei (Datei, Ordner oder Mod gesperrt); <see langword="null"/>, wenn nichts sperrt.</summary>
    public string? LockTooltip { get; private set; }

    private bool _isGuiDraft;

    /// <summary>
    /// GUI-Editor: Die Vanilla-.gui-Datei ist ein Entwurf für die Mod ("Für Mod bearbeiten"). Nur dann lässt sie sich bearbeiten; gespeichert
    /// wird sie nie, sondern über "In Mod übernehmen" in die Mod geschrieben.
    /// </summary>
    public bool IsGuiDraft
    {
        get => _isGuiDraft;
        set
        {
            if (_isGuiDraft != value)
            {
                _isGuiDraft = value;
                OnPropertyChanged(nameof(IsGuiDraft));
            }
        }
    }

    /// <summary>Text des Entwurfs bei "Für Mod bearbeiten" (Grundlage für "In Mod übernehmen"); null bei einem wiederhergestellten Entwurf.</summary>
    public string? GuiDraftBase { get; set; }

    public bool LockIconVisible => LockTooltip is not null && TabIconSettings.IsVisible(TabIconKind.Lock);

    /// <summary>Fragt den Sperrzustand der Datei neu ab (siehe <see cref="TabLockProvider"/>).</summary>
    public void RefreshLock()
    {
        var tooltip = FilePath is { } path ? TabLockProvider.Provider?.Invoke(path) : null;
        if (tooltip == LockTooltip)
        {
            return;
        }

        LockTooltip = tooltip;
        OnPropertyChanged(nameof(LockTooltip));
        OnPropertyChanged(nameof(LockIconVisible));
    }

    /// <summary>Meldungen aus dem Fehlerprotokoll zu dieser Datei (siehe <see cref="TabErrorProvider"/>); <see langword="null"/> = keine.</summary>
    public TabErrorMark? ErrorMark { get; private set; }

    /// <summary>Text des Fehler-Symbols im Tab, z.B. "⚠3".</summary>
    public string ErrorBadge => ErrorMark is { } mark ? "⚠" + mark.Count.ToString(System.Globalization.CultureInfo.CurrentCulture) : string.Empty;

    public string? ErrorTooltip => ErrorMark?.Tooltip;

    public bool ErrorIconVisible => ErrorMark is not null && TabIconSettings.IsVisible(TabIconKind.ErrorLog);

    /// <summary>Fragt die Fehlermarkierung neu ab (nach jedem neu geladenen oder geänderten Lauf, siehe <see cref="TabErrorProvider"/>).</summary>
    public void RefreshErrorMark()
    {
        var mark = FilePath is { } path ? TabErrorProvider.Provider?.Invoke(path) : null;
        if (mark == ErrorMark)
        {
            return;
        }

        ErrorMark = mark;
        OnPropertyChanged(nameof(ErrorMark));
        OnPropertyChanged(nameof(ErrorBadge));
        OnPropertyChanged(nameof(ErrorTooltip));
        OnPropertyChanged(nameof(ErrorIconVisible));
    }

    /// <summary>Löst die Aktualisierung aller Symbol-Sichtbarkeiten aus, nachdem sich <see cref="TabIconSettings"/> geändert hat.</summary>
    public void NotifyIconSettingsChanged()
    {
        OnPropertyChanged(nameof(NoteIconVisible));
        OnPropertyChanged(nameof(BookmarkIconVisible));
        OnPropertyChanged(nameof(BugIconVisible));
        OnPropertyChanged(nameof(VanillaIconVisible));
        OnPropertyChanged(nameof(ModIconVisible));
        OnPropertyChanged(nameof(LockIconVisible));
        OnPropertyChanged(nameof(ErrorIconVisible));
    }

    public static OpenDocument NewEmpty(IHighlightingDefinition? highlighting) =>
        new(null, string.Empty, highlighting);

    /// <summary>Lädt eine Datei standardmäßig als UTF-8 (siehe Architektur-Spezifikation, Abschnitt 5h).</summary>
    public static OpenDocument LoadFromFile(string path, IHighlightingDefinition? highlighting)
    {
        var text = File.ReadAllText(path, Encoding.UTF8);
        var document = new OpenDocument(path, text, highlighting);
        document.RefreshDiskStamp();
        return document;
    }

    /// <summary>
    /// Schreibzeitpunkt (UTC) der Datei auf der Platte zum Zeitpunkt des letzten Ladens bzw.
    /// Speicherns - Grundlage, um Änderungen durch andere Programme zu erkennen.
    /// </summary>
    public DateTime? DiskWriteTimeUtc { get; private set; }

    /// <summary>Übernimmt den aktuellen Schreibzeitpunkt der Datei als "bekannten" Stand.</summary>
    public void RefreshDiskStamp()
    {
        DiskWriteTimeUtc = FilePath is not null && File.Exists(FilePath)
            ? File.GetLastWriteTimeUtc(FilePath)
            : null;
    }

    /// <summary>
    /// Lädt den Inhalt neu von der Platte (nach einer Änderung durch ein anderes Programm). Läuft über den
    /// Undo-Stack, sodass der vorherige Stand mit Strg+Z zurückgeholt werden kann; danach gilt das Dokument
    /// als gespeichert.
    /// </summary>
    public void ReloadFromDisk()
    {
        if (FilePath is null)
        {
            return;
        }

        var text = File.ReadAllText(FilePath, Encoding.UTF8);
        Document.Text = text;
        MarkSaved();
        RefreshDiskStamp();
    }

    /// <summary>Setzt den Dateipfad nachträglich — z. B. nach "Speichern unter…" für ein bislang ungespeichertes Dokument.</summary>
    public void SetFilePath(string path)
    {
        FilePath = path;
        AutoCategory = FileCategoryClassifier.Classify(path);
        Origin = FileOriginTracker.Classify(path);
        RaiseCategoryChanged();
        RaiseOriginChanged();
        OnPropertyChanged(nameof(DisplayName));
        RefreshLock();
        RefreshErrorMark();
    }

    /// <summary>
    /// Setzt oder entfernt (<see langword="null"/>) die manuelle Kategorie-Zuweisung dieses Dokuments.
    /// Ohne Wirkung (kein Ereignis), falls bereits genau diese Kennung gesetzt ist.
    /// </summary>
    public void SetManualCategory(string? categoryId)
    {
        if (ManualCategory == categoryId)
        {
            return;
        }

        ManualCategory = categoryId;
        RaiseCategoryChanged();
    }

    /// <summary>Ermittelt die automatische Kategorie neu, z. B. nachdem die Kategorie-Regeln geändert wurden.</summary>
    public void RefreshAutoCategory()
    {
        AutoCategory = FileCategoryClassifier.Classify(FilePath);
        RaiseCategoryChanged();
    }

    /// <summary>
    /// Setzt, ob zu dieser Datei aktuell eine Notiz existiert (Nutzer-Feedback Runde 5), und löst
    /// bei einer tatsächlichen Änderung eine Aktualisierung des daran gebundenen Icons im
    /// Dateienreiter aus. Ohne Wirkung (kein Ereignis), falls sich der Wert nicht ändert.
    /// </summary>
    public void SetHasNote(bool hasNote)
    {
        if (HasNote == hasNote)
        {
            return;
        }

        HasNote = hasNote;
        OnPropertyChanged(nameof(HasNote));
        OnPropertyChanged(nameof(NoteIconVisible));
    }

    /// <summary>
    /// Löst die Aktualisierung der farbabhängigen, an den Tab gebundenen Eigenschaften erneut aus,
    /// OHNE die Kategorie selbst zu ändern — genutzt, wenn sich global die Kategorie-Farbzuordnung
    /// (Kategorie-Farben-Dialog) oder die "ganzen Reiter einfärben"-Einstellung ändert, damit
    /// bereits offene Tabs sofort die neue Farbe zeigen, statt erst beim nächsten Tabwechsel oder
    /// Programmstart (siehe MainWindow.RefreshAllDocumentColors).
    /// </summary>
    public void NotifyColorSettingsChanged()
    {
        OnPropertyChanged(nameof(CategoryColor));
        OnPropertyChanged(nameof(TabBackground));
    }

    private void RaiseCategoryChanged()
    {
        OnPropertyChanged(nameof(Category));
        OnPropertyChanged(nameof(CategoryColor));
        OnPropertyChanged(nameof(CategoryDisplayName));
        OnPropertyChanged(nameof(TabBackground));
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
