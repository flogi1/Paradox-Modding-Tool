using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
// UseWindowsForms ist im .csproj dieses Projekts aktiviert (für ColorDialog/FolderBrowserDialog,
// siehe die entsprechenden Methoden weiter unten), wodurch - wie schon bei ParadoxModTool.App
// (Lessons Learned Punkt 7/12) - mehrere gleichnamige Typen zwischen WPF und WinForms mehrdeutig
// werden (CS0104). "UserControl" ist als Basisklasse dieser Datei betroffen (System.Windows.
// Controls.UserControl vs. System.Windows.Forms.UserControl), da beide Frameworks eine eigene
// UserControl-Klasse mitbringen - anders als bei den übrigen WinForms-Aufrufen in dieser Datei
// (dort bewusst vollqualifiziert statt aliasiert, siehe AssignColor_Click/AddRoot_Click) lässt
// sich eine Basisklassen-Deklaration nicht bequem vollqualifiziert schreiben, daher hier
// stattdessen ein expliziter using-Alias. "MessageBox" (neu: Delete_Click) und "KeyEventArgs"
// (neu: ExplorerPanel_PreviewKeyDown, Alt+F-Hotkey) sind aus demselben Grund betroffen
// (System.Windows.MessageBox vs. System.Windows.Forms.MessageBox bzw. System.Windows.Input.
// KeyEventArgs vs. System.Windows.Forms.KeyEventArgs) - dieselben beiden Kollisionen, die
// MainWindow.xaml.cs bereits kennt (siehe dortiger using-Block).
using UserControl = System.Windows.Controls.UserControl;
using MessageBox = System.Windows.MessageBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.Explorer;

/// <summary>Ziel-Arbeitsbereich für "Im ... Arbeitsbereich öffnen" (Nutzer-Feedback: "Man sollte
/// direkt in dieser Verzeichnis Übersicht auswählen können, ob man die Datei im linken oder
/// rechten Arbeitsbereich öffnet. Zwei verschiedene Buttons sollen das ermöglichen."). Der Wert
/// <see cref="Active"/> kam mit dem Doppelklick-Feature hinzu (Nutzer-Feedback: "Doppelklick auf
/// eine Datei in der Verzeichnis Übersicht sollte die Datei in der gerade aktiven Arbeitsumgebung
/// öffnen.") - anders als bei den ◀/▶-Buttons kennt der Doppelklick kein festes Ziel, sondern
/// meint immer den GERADE aktiven Arbeitsbereich, egal ob das links oder rechts ist.</summary>
public enum ExplorerOpenTarget
{
    Left,
    Right,
    Active,
}

public sealed class ExplorerOpenFileEventArgs : EventArgs
{
    public required string FilePath { get; init; }
    public required ExplorerOpenTarget Target { get; init; }
}

/// <summary>
/// Von <see cref="ExplorerPanel"/> ausgelöst, wenn der Nutzer über den "Neue Datei anlegen…"-
/// Dialog eine neue Moddatei angelegt haben möchte. Die eigentliche Datei wird bewusst NICHT
/// hier geschrieben, sondern von <c>MainWindow</c> über den programmweiten Aktionsverlauf
/// (<c>FileWriteUndoableAction</c>), damit "Neue Datei anlegen" wie jede andere Programmaktion
/// rückgängig gemacht werden kann (Konzept: "alle Prozesse ... rückgängig machen").
/// </summary>
public sealed class ExplorerNewFileEventArgs : EventArgs
{
    public required string FolderPath { get; init; }
    public required string FileName { get; init; }
}

/// <summary>
/// Von <see cref="ExplorerPanel"/> ausgelöst, wenn der Nutzer über "Neuen Ordner anlegen…" einen
/// neuen Unterordner haben möchte (Nutzer-Feedback: "Es sollte auch die Möglichkeit geben über
/// die Verzeichnis Übersicht einen neuen Ordner zu erstellen"). Genau wie bei
/// <see cref="ExplorerNewFileEventArgs"/> wird der Ordner NICHT hier, sondern von MainWindow
/// angelegt (dort über einen <c>DelegateUndoableAction</c> im programmweiten Aktionsverlauf).
/// </summary>
public sealed class ExplorerNewFolderEventArgs : EventArgs
{
    public required string ParentFolderPath { get; init; }
    public required string FolderName { get; init; }
}

/// <summary>
/// Von <see cref="ExplorerPanel"/> ausgelöst, wenn der Nutzer über "Löschen…" eine Datei/einen
/// Ordner löschen möchte (Nutzer-Feedback-Kontext: "Das Löschen der Datei funktioniert auch,
/// allerdings wird die Verzeichnis Übersicht nach einer Löschung nicht aktualisiert" - siehe
/// ausführlicher Kommentar zum "Löschen…"-Menüpunkt in ExplorerPanel.xaml). Die eigentliche
/// Löschung übernimmt bewusst MainWindow (Microsoft.VisualBasic.FileIO.FileSystem, Papierkorb),
/// damit dieselbe zentrale Stelle danach auch offene Tabs derselben Datei schließen und den
/// betroffenen Ordnerknoten aktualisieren kann.
/// </summary>
public sealed class ExplorerDeleteEventArgs : EventArgs
{
    public required string FullPath { get; init; }
    public required bool IsDirectory { get; init; }
    public required string ParentFolderPath { get; init; }
}

public partial class ExplorerPanel : UserControl
{
    private readonly ObservableCollection<ExplorerEntry> _roots = new();
    private readonly ObservableCollection<ExplorerEntry> _favoriteEntries = new();
    private readonly ExpandedPathSet _expanded = new();
    private readonly Dictionary<string, string> _colors;

    // Stand von Farben und Favoriten beim letzten Laden/Speichern: Grundlage, um beim Speichern nur die eigenen Änderungen
    // mit dem Stand eines anderen gleichzeitig offenen Fensters zusammenzuführen.
    private Dictionary<string, string> _colorsBaseline = new();
    private List<string> _favoritesBaseline = new();
    private readonly List<string> _favoritePaths = new();

    // Favoriten gelten je Spiel (siehe SetGameProfile); solange kein Spiel bekannt ist, null - dann wird nichts gespeichert.
    private string? _favoritesFilePath;
    private GameProfile? _activeProfile;

    /// <summary>Best-effort-Vorschlag für den Startordner des "Ordner hinzufügen…"-Dialogs (siehe
    /// <see cref="SetSuggestedInitialFolder"/>) - <see langword="null"/>, solange MainWindow noch
    /// keinen ermitteln konnte, dann öffnet der Dialog einfach ohne Vorauswahl.</summary>
    private string? _suggestedInitialFolder;

    public ExplorerPanel()
    {
        InitializeComponent();
        Tree.ItemsSource = _roots;
        _roots.CollectionChanged += (_, _) => RootsChanged?.Invoke(this, EventArgs.Empty);
        FavoritesTree.ItemsSource = _favoriteEntries;

        _colors = ExplorerColorStore.TryLoad(ExplorerColorStore.GetDefaultColorsFilePath());
        _colorsBaseline = new Dictionary<string, string>(_colors);

        // Nutzer-Feedback (Runde 10, weitere Verbesserungen): "Füge für Favoriten hinzufügen den
        // Hotkey Alt + F hinzu" - PreviewKeyDown (statt KeyDown) auf dem UserControl selbst, damit
        // der Hotkey unabhängig davon greift, welches Kind-Element (Hauptbaum oder Favoriten-Baum)
        // gerade den Tastaturfokus hat, nach demselben Muster wie MainWindow.MainWindow_PreviewKeyDown.
        PreviewKeyDown += ExplorerPanel_PreviewKeyDown;

        InitializeSearch();
    }

    public event EventHandler<ExplorerOpenFileEventArgs>? OpenFileRequested;
    public event EventHandler<ExplorerNewFileEventArgs>? NewFileRequested;
    public event EventHandler<ExplorerNewFolderEventArgs>? NewFolderRequested;
    public event EventHandler<ExplorerDeleteEventArgs>? DeleteRequested;

    /// <summary>Liefert für einen Pfad die Ziel-Mods des Projekts (Untermenüs "In andere Mod kopieren/verschieben", "Mit anderer Mod vergleichen"); <see langword="null"/> = Pfad gehört zu keinem Projekt.</summary>
    public Func<string, ExplorerBranchTargets?>? BranchTargetsProvider { get; set; }

    public event EventHandler<ExplorerBranchActionEventArgs>? BranchActionRequested;

    /// <summary>Liefert den Sperrzustand eines Pfads (siehe <see cref="ExplorerLockState"/>); <see langword="null"/> = Pfad gehört zu keiner Mod.</summary>
    public Func<string, ExplorerLockState?>? PathLockStateProvider { get; set; }

    public event EventHandler<ExplorerPathLockEventArgs>? PathLockRequested;

    /// <summary>Liefert den Tooltip des Schloss-Symbols für einen Eintrag (Pfad, ist Ordner); <see langword="null"/> = kein Symbol.</summary>
    public Func<string, bool, string?>? LockMarkProvider { get; set; }

    /// <summary>Liefert die Fehlermarkierung eines Eintrags (Pfad, ist Ordner); <see langword="null"/> = keine. Das Hauptfenster kennt das Fehlerprotokoll.</summary>
    public Func<string, bool, ExplorerErrorMark?>? ErrorMarkProvider { get; set; }

    /// <summary>Klick auf die Fehlermarkierung; der Wert ist der Pfad des Eintrags.</summary>
    public event EventHandler<string>? ErrorMarkClicked;

    public static readonly DependencyProperty ButtonsOnLeftProperty =
        DependencyProperty.Register(nameof(ButtonsOnLeft), typeof(bool), typeof(ExplorerPanel), new PropertyMetadata(false, OnOptionChanged));

    /// <summary>Lupe und die Knöpfe für den linken und rechten Arbeitsbereich stehen links vor dem Namen statt rechts dahinter.</summary>
    public bool ButtonsOnLeft
    {
        get => (bool)GetValue(ButtonsOnLeftProperty);
        set => SetValue(ButtonsOnLeftProperty, value);
    }

    public static readonly DependencyProperty ShowErrorMarksProperty =
        DependencyProperty.Register(nameof(ShowErrorMarks), typeof(bool), typeof(ExplorerPanel), new PropertyMetadata(true, OnOptionChanged));

    /// <summary>Fehlermarkierungen (Meldungen des letzten Spiellaufs) hinter Dateien und Ordnern zeigen. Standard: an.</summary>
    public bool ShowErrorMarks
    {
        get => (bool)GetValue(ShowErrorMarksProperty);
        set => SetValue(ShowErrorMarksProperty, value);
    }

    /// <summary>Ein Schalter der Übersicht (Buttons links, Bug-Markierungen, Nur Ordner) wurde geändert; das Hauptfenster übernimmt ihn in die Einstellungen.</summary>
    public event EventHandler? OptionsChanged;

    private static void OnOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ExplorerPanel)d).OptionsChanged?.Invoke(d, EventArgs.Empty);

    /// <summary>Workshop-Verweise <c>ugc_*.mod</c> (<see cref="UgcReference"/>) weder im Baum noch in der Namenssuche zeigen. Ändern liest den Baum neu ein.</summary>
    public bool HideUgcReferences
    {
        get => _hideUgcReferences;
        set
        {
            if (_hideUgcReferences != value)
            {
                _hideUgcReferences = value;
                RefreshAll();
            }
        }
    }

    private bool _hideUgcReferences = true;

    /// <summary>Namenssuche nur nach Ordnern.</summary>
    public bool SearchFoldersOnly
    {
        get => FoldersOnlyCheckBox.IsChecked == true;
        set => FoldersOnlyCheckBox.IsChecked = value;
    }

    /// <summary>Liefert die Zahl der Sperren (Dateien und Ordner) im Ordner; 0 = Menüpunkt "Alle Sperren darunter aufheben" bleibt aus.</summary>
    public Func<string, int>? LockCountBelowProvider { get; set; }

    /// <summary>Wird ausgelöst, wenn der Nutzer "Alle Sperren darunter aufheben" wählt; der Wert ist der Ordnerpfad.</summary>
    public event EventHandler<string>? UnlockBelowRequested;

    /// <summary>
    /// Wird ausgelöst, wenn der Nutzer die Lupe neben einem Ordner anklickt oder im Kontextmenü "In Dateien suchen" wählt; der
    /// Wert ist der Ordner- oder Dateipfad.
    /// </summary>
    public event EventHandler<string>? SearchInPathRequested;

    /// <summary>Fragt die Schloss-Symbole aller geladenen Einträge neu ab (nach Sperren, Entsperren und Änderungen an Mods).</summary>
    public void RefreshLockMarks()
    {
        foreach (var root in _roots)
        {
            RefreshLockMarks(root);
        }

        foreach (var favorite in _favoriteEntries)
        {
            RefreshLockMarks(favorite);
        }
    }

    private void RefreshLockMarks(ExplorerEntry entry)
    {
        if (entry.IsPlaceholder)
        {
            return;
        }

        entry.LockTooltip = LockMarkProvider?.Invoke(entry.FullPath, entry.IsDirectory);
        foreach (var child in entry.Children)
        {
            RefreshLockMarks(child);
        }
    }

    /// <summary>Fragt die Fehlermarkierungen aller geladenen Einträge neu ab (nach jeder Änderung im Fehlerprotokoll).</summary>
    public void RefreshErrorMarks()
    {
        foreach (var root in _roots)
        {
            RefreshErrorMarks(root);
        }

        foreach (var favorite in _favoriteEntries)
        {
            RefreshErrorMarks(favorite);
        }
    }

    private void RefreshErrorMarks(ExplorerEntry entry)
    {
        if (entry.IsPlaceholder)
        {
            return;
        }

        ApplyErrorMark(entry);
        foreach (var child in entry.Children)
        {
            RefreshErrorMarks(child);
        }
    }

    private void ApplyErrorMark(ExplorerEntry entry)
    {
        var mark = entry.IsPlaceholder ? null : ErrorMarkProvider?.Invoke(entry.FullPath, entry.IsDirectory);
        entry.SetErrorMark(mark?.Badge, mark?.Tooltip);
    }

    private void ErrorMark_Click(object sender, RoutedEventArgs e)
    {
        // Nur das Fehlerprotokoll öffnen; der Eintrag selbst soll nicht zusätzlich aufklappen oder ausgewählt werden.
        e.Handled = true;
        if (EntryFrom(sender) is { IsPlaceholder: false } entry)
        {
            ErrorMarkClicked?.Invoke(this, entry.FullPath);
        }
    }

    private void SearchFolder_Click(object sender, RoutedEventArgs e)
    {
        if (EntryFrom(sender) is { IsDirectory: true, IsPlaceholder: false } entry)
        {
            SearchInPathRequested?.Invoke(this, entry.FullPath);
        }
    }

    private void SearchInPath_Click(object sender, RoutedEventArgs e)
    {
        if (EntryFrom(sender) is { IsPlaceholder: false } entry)
        {
            SearchInPathRequested?.Invoke(this, entry.FullPath);
        }
    }

    /// <summary>Rechtsklick "Als Filter anlegen" auf einen Ordner; der Wert ist der Ordnerpfad.</summary>
    public event EventHandler<string>? FolderFilterRequested;

    private void CreateFolderFilter_Click(object sender, RoutedEventArgs e)
    {
        if (EntryFrom(sender) is { IsDirectory: true, IsPlaceholder: false } entry)
        {
            FolderFilterRequested?.Invoke(this, entry.FullPath);
        }
    }

    /// <summary>Baut beim Öffnen des Kontextmenüs die Untermenüs der Projekt-Aktionen auf; ohne Projekt bleiben sie samt Trennlinie ausgeblendet.</summary>
    private void EntryContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        var menu = (System.Windows.Controls.ContextMenu)sender;
        var entry = (menu.PlacementTarget as FrameworkElement)?.DataContext as ExplorerEntry;
        var targets = entry is { IsPlaceholder: false, IsRoot: false } ? BranchTargetsProvider?.Invoke(entry.FullPath) : null;

        var lockState = entry is { IsPlaceholder: false, IsRoot: false } ? PathLockStateProvider?.Invoke(entry.FullPath) : null;
        var unlockCount = entry is { IsDirectory: true, IsPlaceholder: false } ? LockCountBelowProvider?.Invoke(entry.FullPath) ?? 0 : 0;

        foreach (var item in menu.Items.OfType<FrameworkElement>().Where(i => i.Tag is string tag && tag != "Lock" && tag != "UnlockBelow" && tag != "SearchInPath"))
        {
            var visible = false;
            if (item is System.Windows.Controls.MenuItem parent)
            {
                var action = Enum.Parse<ExplorerBranchAction>((string)parent.Tag);
                var list = action switch
                {
                    ExplorerBranchAction.Copy => targets?.Copy,
                    ExplorerBranchAction.Move => targets?.Move,
                    _ => entry is { IsDirectory: false } ? targets?.Compare : null,
                };

                parent.Items.Clear();
                if (entry is not null && list is { Count: > 0 })
                {
                    foreach (var target in list)
                    {
                        var child = new System.Windows.Controls.MenuItem { Header = target.Name };
                        var capturedTarget = target;
                        child.Click += (_, _) => BranchActionRequested?.Invoke(this, new ExplorerBranchActionEventArgs
                        {
                            Path = entry.FullPath,
                            IsDirectory = entry.IsDirectory,
                            Action = action,
                            TargetModId = capturedTarget.ModId,
                        });
                        parent.Items.Add(child);
                    }

                    visible = true;
                }
            }
            else
            {
                visible = targets is not null || lockState is not null || unlockCount > 0;
            }

            item.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        if (menu.Items.OfType<System.Windows.Controls.MenuItem>().FirstOrDefault(i => i.Tag is "Lock") is { } lockItem)
        {
            if (lockState is null || entry is null)
            {
                lockItem.Visibility = Visibility.Collapsed;
            }
            else
            {
                lockItem.Visibility = Visibility.Visible;
                lockItem.Header = lockState.Kind == ExplorerLockKind.Unlocked
                    ? (entry.IsDirectory ? ExplorerTexts.LockFolder : ExplorerTexts.LockFile)
                    : (entry.IsDirectory ? ExplorerTexts.UnlockFolder : ExplorerTexts.UnlockFile);
                lockItem.IsEnabled = lockState.Kind != ExplorerLockKind.LockedByParent;
                lockItem.ToolTip = lockState.Kind == ExplorerLockKind.LockedByParent ? ExplorerTexts.LockedByParent(lockState.ByPath) : null;
                lockItem.DataContext = (entry, lockState.Kind == ExplorerLockKind.Unlocked);
                lockItem.Click -= LockItem_Click;
                lockItem.Click += LockItem_Click;
            }
        }

        if (menu.Items.OfType<System.Windows.Controls.MenuItem>().FirstOrDefault(i => i.Tag is "UnlockBelow") is { } unlockItem)
        {
            unlockItem.Visibility = unlockCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            unlockItem.Header = ExplorerTexts.UnlockBelow(unlockCount);
            unlockItem.Click -= UnlockBelowItem_Click;
            unlockItem.Click += UnlockBelowItem_Click;
        }

        // "In Dateien suchen" bei Ordnern, "In Datei suchen" bei einer einzelnen Datei.
        if (menu.Items.OfType<System.Windows.Controls.MenuItem>().FirstOrDefault(i => i.Tag is "SearchInPath") is { } searchItem)
        {
            searchItem.Header = entry is { IsDirectory: false } ? ExplorerTexts.SearchInFile : ExplorerTexts.SearchInFiles;
        }
    }

    private void UnlockBelowItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem { DataContext: ExplorerEntry entry })
        {
            UnlockBelowRequested?.Invoke(this, entry.FullPath);
        }
    }

    private void LockItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem { DataContext: ValueTuple<ExplorerEntry, bool> data })
        {
            PathLockRequested?.Invoke(this, new ExplorerPathLockEventArgs { Path = data.Item1.FullPath, IsDirectory = data.Item1.IsDirectory, Lock = data.Item2 });
        }
    }

    /// <summary>Sind aktuell keine Wurzeln in der Übersicht vorhanden? Von MainWindow beim ersten
    /// Start genutzt, um den Mod-Ordner aus dem aktiven Spielprofil automatisch vorzuschlagen
    /// (Konzept Abschnitt 1d: "Grundeinstellungen möglichst automatisch vorschlagen").</summary>
    public bool HasAnyRoots => _roots.Count > 0;

    /// <summary>Wird ausgelöst, wenn eine Wurzel hinzukommt, entfernt oder die Liste neu gesetzt wird.</summary>
    public event EventHandler? RootsChanged;

    /// <summary>
    /// Setzt das aktive Spielprofil (für die Dateityp-Auswahl beim Anlegen neuer Dateien sowie
    /// die Standardfarben je Ordnerkategorie, siehe <see cref="FolderCategoryColorResolver"/>)
    /// und färbt bereits geladene Ordner ohne EXPLIZITE Nutzerfarbe entsprechend neu ein. Lädt außerdem die Favoriten dieses
    /// Spiels; <paramref name="allProfiles"/> braucht die einmalige Übernahme aus der alten gemeinsamen Favoritendatei, um deren
    /// Einträge den Spielen zuzuordnen (siehe <see cref="ExplorerFavoritesStore.LoadForGame"/>).
    /// </summary>
    public void SetGameProfile(GameProfile? profile, IReadOnlyList<GameProfile> allProfiles)
    {
        _activeProfile = profile;
        LoadFavoritesForGame(profile, allProfiles);
        foreach (var root in _roots)
        {
            ReapplyDefaultFolderColors(root);
            ReapplyFavoriteState(root);
        }
    }

    private void LoadFavoritesForGame(GameProfile? profile, IReadOnlyList<GameProfile> allProfiles)
    {
        _favoritePaths.Clear();
        _favoritesFilePath = null;
        if (profile is not null)
        {
            try
            {
                _favoritePaths.AddRange(ExplorerFavoritesStore.LoadForGame(profile.Id, allProfiles));
                _favoritesFilePath = ExplorerFavoritesStore.GetFavoritesFilePath(profile.Id);
            }
            catch (IOException)
            {
                // Ohne lesbare Favoritendatei bleibt die Liste leer und wird nicht gespeichert (sonst überschriebe sie die Datei). Das gilt auch, wenn die
                // Übernahme aus der alten gemeinsamen Datei scheitert: sie wird dann beim nächsten Start wiederholt.
            }
            catch (UnauthorizedAccessException)
            {
                // Wie oben.
            }
        }

        _favoritesBaseline = new List<string>(_favoritePaths);
        RebuildFavoritesDisplay();
    }

    private void ReapplyFavoriteState(ExplorerEntry entry)
    {
        if (entry.IsPlaceholder)
        {
            return;
        }

        var key = PathNormalization.NormalizeKey(entry.FullPath);
        entry.IsFavorite = _favoritePaths.Any(p => PathNormalization.NormalizeKey(p) == key);
        foreach (var child in entry.Children)
        {
            ReapplyFavoriteState(child);
        }
    }

    private void ReapplyDefaultFolderColors(ExplorerEntry entry)
    {
        if (entry.IsPlaceholder)
        {
            return;
        }

        if (entry.IsDirectory && !_colors.ContainsKey(PathNormalization.NormalizeKey(entry.FullPath)))
        {
            entry.ColorHex = FolderCategoryColorResolver.DefaultColorFor(entry.Name, _activeProfile?.FolderCategories);
        }

        foreach (var child in entry.Children)
        {
            ReapplyDefaultFolderColors(child);
        }
    }

    /// <summary>Fügt eine neue Wurzel hinzu (Nutzer-Feedback: "weitere Ordnerstrukturen zu diesem
    /// Arbeitsbereich hinzufügen können"). Bereits vorhandene Wurzeln (nach normalisiertem Pfad)
    /// werden stillschweigend übersprungen statt doppelt angezeigt.</summary>
    public void AddRoot(ExplorerRootDescriptor descriptor)
    {
        var key = PathNormalization.NormalizeKey(descriptor.FullPath);
        if (_roots.Any(r => PathNormalization.NormalizeKey(r.FullPath) == key))
        {
            return;
        }

        var entry = new ExplorerEntry
        {
            // Gemischte Schrägstriche (aus Profilvorlage oder alter Sitzung) werden hier vereinheitlicht: alle Einträge darunter erben den Pfad,
            // und "Ordner öffnen (Explorer)" scheitert mit ihnen (öffnet "Dokumente").
            FullPath = PathNormalization.ToWindowsPath(descriptor.FullPath),
            Name = descriptor.DisplayName,
            IsDirectory = true,
            IsRoot = true,
            RootKind = descriptor.Kind,
        };
        ApplyColorAndFavoriteState(entry);
        entry.Children.Add(CreatePlaceholder());
        _roots.Add(entry);
        RestoreExpansion(entry);
    }

    /// <summary>Setzt die gemerkten aufgeklappten Ordner (aus der Session). Vor <see cref="SetRoots"/> aufrufen, damit die Wurzeln gleich aufgeklappt erscheinen.</summary>
    public void SetExpandedPaths(IEnumerable<string> paths) => _expanded.Load(paths);

    /// <summary>Die aufgeklappten Ordner zum Speichern in der Session; verschwundene Ordner entfallen.</summary>
    public IReadOnlyList<string> CaptureExpandedPaths() => _expanded.Capture();

    /// <summary>Klappt einen Ordner auf, wenn er als aufgeklappt gemerkt ist: lädt seine Kinder sofort (ohne auf das Ereignis zu warten) und setzt sich über <see cref="LoadChildren"/> in den Kindern fort.</summary>
    private void RestoreExpansion(ExplorerEntry entry)
    {
        if (!entry.IsDirectory || entry.IsPlaceholder || !_expanded.Contains(entry.FullPath))
        {
            return;
        }

        LoadChildren(entry);
        entry.IsExpanded = true;
    }

    private bool IsInMainTree(DependencyObject item)
    {
        for (var current = item; current is not null; current = System.Windows.Media.VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, Tree))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Liefert alle aktuellen Wurzeln zum Speichern in der Session (siehe
    /// <c>AppSessionState.ExplorerRoots</c> in <c>ParadoxModTool.Session</c>).</summary>
    public IReadOnlyList<ExplorerRootDescriptor> CaptureRoots() =>
        _roots.Select(r => new ExplorerRootDescriptor(r.FullPath, r.Name, r.RootKind)).ToList();

    /// <summary>Stellt zuvor gespeicherte Wurzeln wieder her (Sitzungswiederherstellung). Eine
    /// seit dem letzten Beenden gelöschte/umbenannte Wurzel wird stillschweigend übersprungen,
    /// statt die Wiederherstellung der übrigen Wurzeln abzubrechen (dasselbe Prinzip wie bei
    /// MainWindow.RestorePane für offene Dokumente).</summary>
    public void SetRoots(IEnumerable<ExplorerRootDescriptor> descriptors)
    {
        _roots.Clear();
        foreach (var descriptor in descriptors)
        {
            if (Directory.Exists(descriptor.FullPath))
            {
                AddRoot(descriptor);
            }
        }
    }

    /// <summary>
    /// Setzt einen unverbindlichen Vorschlag für den Startordner des "Ordner hinzufügen…"-Dialogs
    /// (<see cref="AddRoot_Click"/>) - von MainWindow anhand des Pfads zur Spiel-.exe ermittelt
    /// (siehe MainWindow.ComputeGameFilesGuess), da es anders als beim Mod-Ordner-Template keine
    /// über die Wiki bestätigte feste Konvention dafür gibt, wo die Spieldateien liegen. Bewusst
    /// NUR eine Dialog-Vorauswahl, KEINE automatisch hinzugefügte Wurzel.
    /// </summary>
    public void SetSuggestedInitialFolder(string? folderPath) => _suggestedInitialFolder = folderPath;

    /// <summary>Liest alle Wurzeln komplett neu ein (Button "🔄").</summary>
    public void RefreshAll()
    {
        foreach (var root in _roots)
        {
            root.ChildrenLoaded = false;
            root.Children.Clear();
            root.Children.Add(CreatePlaceholder());
            RestoreExpansion(root);
        }
    }

    /// <summary>Von MainWindow nach erfolgreichem Anlegen einer neuen Datei aufgerufen (siehe
    /// <see cref="NewFileRequested"/>), damit der betroffene Ordnerknoten die neue Datei sofort
    /// anzeigt, ohne dass der Nutzer manuell "Ordner aktualisieren" auswählen muss.</summary>
    public void NotifyFolderChanged(string folderPath)
    {
        var key = PathNormalization.NormalizeKey(folderPath);
        foreach (var root in _roots)
        {
            RefreshFolderIfMatches(root, key);
        }
    }

    private void RefreshFolderIfMatches(ExplorerEntry entry, string normalizedFolderKey)
    {
        if (entry.IsPlaceholder || !entry.IsDirectory)
        {
            return;
        }

        if (PathNormalization.NormalizeKey(entry.FullPath) == normalizedFolderKey && entry.ChildrenLoaded)
        {
            LoadChildren(entry);
            return;
        }

        foreach (var child in entry.Children.ToList())
        {
            RefreshFolderIfMatches(child, normalizedFolderKey);
        }
    }

    private static ExplorerEntry CreatePlaceholder() => new()
    {
        FullPath = string.Empty,
        Name = ExplorerTexts.Loading,
        IsDirectory = false,
        IsPlaceholder = true,
    };

    private void ApplyColorAndFavoriteState(ExplorerEntry entry)
    {
        var key = PathNormalization.NormalizeKey(entry.FullPath);

        if (_colors.TryGetValue(key, out var hex))
        {
            entry.ColorHex = hex;
        }
        else if (entry.IsDirectory)
        {
            entry.ColorHex = FolderCategoryColorResolver.DefaultColorFor(entry.Name, _activeProfile?.FolderCategories);
        }

        entry.IsFavorite = _favoritePaths.Any(p => PathNormalization.NormalizeKey(p) == key);
        entry.LockTooltip = entry.IsPlaceholder ? null : LockMarkProvider?.Invoke(entry.FullPath, entry.IsDirectory);
        ApplyErrorMark(entry);
    }

    private void LoadChildren(ExplorerEntry folder)
    {
        folder.Children.Clear();
        foreach (var child in FolderScanner.GetChildren(folder.FullPath, HideUgcReferences))
        {
            ApplyColorAndFavoriteState(child);
            if (child.IsDirectory)
            {
                child.Children.Add(CreatePlaceholder());
            }

            folder.Children.Add(child);
            RestoreExpansion(child);
        }

        folder.ChildrenLoaded = true;
    }

    /// <summary>
    /// Löst das Lazy-Loading beim Aufklappen aus (siehe <see cref="ExplorerEntry.Children"/>).
    /// Bewusst über <c>e.OriginalSource</c> statt <c>sender</c>: "Expanded" ist ein bubbelndes
    /// Ereignis, das über den <see cref="Style"/>-EventSetter an JEDEM TreeViewItem-Container
    /// hängt — ohne <c>e.Handled = true</c> würde derselbe Aufklapp-Vorgang beim Hochblubbern
    /// zusätzlich an jedem übergeordneten Container erneut (fälschlich für DESSEN eigenen
    /// Ordner) ausgelöst.
    /// </summary>
    private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: ExplorerEntry entry } item && entry.IsDirectory)
        {
            if (!entry.ChildrenLoaded)
            {
                LoadChildren(entry);
            }

            if (!entry.IsPlaceholder && IsInMainTree(item))
            {
                _expanded.Add(entry.FullPath);
            }
        }

        e.Handled = true;
    }

    /// <summary>Merkt sich das Zuklappen eines Ordners im Hauptbaum (siehe <see cref="ExpandedPathSet"/>). Die Ordner darunter behalten ihren Zustand.</summary>
    private void TreeViewItem_Collapsed(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: ExplorerEntry entry } item && entry.IsDirectory && !entry.IsPlaceholder && IsInMainTree(item))
        {
            _expanded.Remove(entry.FullPath);
        }

        e.Handled = true;
    }

    private static ExplorerEntry? EntryFrom(object sender) => (sender as FrameworkElement)?.DataContext as ExplorerEntry;

    private void OpenLeft_Click(object sender, RoutedEventArgs e) => RequestOpen(sender, ExplorerOpenTarget.Left);

    private void OpenRight_Click(object sender, RoutedEventArgs e) => RequestOpen(sender, ExplorerOpenTarget.Right);

    private void RequestOpen(object sender, ExplorerOpenTarget target)
    {
        if (EntryFrom(sender) is { IsDirectory: false, IsPlaceholder: false } entry)
        {
            OpenFileRequested?.Invoke(this, new ExplorerOpenFileEventArgs { FilePath = entry.FullPath, Target = target });
        }
    }

    private void NewFile_Click(object sender, RoutedEventArgs e)
    {
        if (EntryFrom(sender) is not { IsDirectory: true, IsPlaceholder: false } folder)
        {
            return;
        }

        var knownExtensions = _activeProfile?.FileTypes.Select(t => t.Extension).ToList();

        // Nutzer-Feedback: "Im gui Ordner sollte standardmäßig die GUI Datei ausgewählt sein. In
        // den localitzation Ordner standardmäßig yml; für die anderen Ordner ist .txt die
        // richtige Option" - siehe GuessPreferredExtension weiter unten.
        var preferredExtension = GuessPreferredExtension(folder.Name);

        var dialog = new NewFileWindow(folder.FullPath, knownExtensions, preferredExtension)
        {
            Owner = Window.GetWindow(this),
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        NewFileRequested?.Invoke(this, new ExplorerNewFileEventArgs { FolderPath = folder.FullPath, FileName = dialog.ResultFileName });
    }

    /// <summary>
    /// Schätzt die naheliegendste Dateiendung anhand des ZIEL-ORDNERNAMENS (Nutzer-Feedback: "Im
    /// gui Ordner sollte standardmäßig die GUI Datei ausgewählt sein. In den localitzation Ordner
    /// standardmäßig yml; für die anderen Ordner ist .txt die richtige Option"). Bewusst NUR der
    /// unmittelbare Ordnername (nicht der volle Pfad) - dieselbe bewusste Vereinfachung wie bei
    /// <see cref="FolderCategoryColorResolver"/> (siehe dort). Case-insensitiv, da Windows-
    /// Ordnernamen nicht groß-/kleinschreibungssensitiv sind. Liefert <see langword="null"/> für
    /// jeden nicht erkannten Ordnernamen (z. B. "common", "events") - <see cref="NewFileWindow"/>
    /// fällt in diesem Fall auf .txt (Index 0 der Endungsliste) zurück, wie bisher.
    /// </summary>
    private static string? GuessPreferredExtension(string folderName) => folderName.ToLowerInvariant() switch
    {
        "gui" => ".gui",
        "interface" => ".gui",
        "localization" or "localisation" => ".yml",
        _ => null,
    };

    private void NewFolder_Click(object sender, RoutedEventArgs e)
    {
        if (EntryFrom(sender) is not { IsDirectory: true, IsPlaceholder: false } folder)
        {
            return;
        }

        // Microsoft.VisualBasic.Interaction.InputBox: einfacher, native Text-Eingabeaufforderung
        // (Teil des .NET-Windows-Desktop-Runtime, kein zusätzliches NuGet-Paket nötig - dieselbe
        // "nur zertifizierte/unbedenkliche Bibliotheken"-Anforderung aus dem Konzept wie beim
        // Papierkorb-Löschen weiter unten in MainWindow) - spart einen kompletten eigenen
        // WPF-Dialog für diesen einen Texteingabe-Fall. Bekannte Einschränkung: folgt (noch) nicht
        // dem künftigen Light-/Dark-Mode-Theming des restlichen Programms (siehe README, bekannte
        // Unsicherheiten) - für eine spätere Ausbaustufe ggf. durch einen eigenen, themefähigen
        // WPF-Dialog ersetzen.
        var name = Microsoft.VisualBasic.Interaction.InputBox(
            ExplorerTexts.NewFolderPrompt, ExplorerTexts.NewFolder, ExplorerTexts.NewFolderDefaultName);

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        NewFolderRequested?.Invoke(this, new ExplorerNewFolderEventArgs { ParentFolderPath = folder.FullPath, FolderName = name.Trim() });
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (EntryFrom(sender) is not { IsPlaceholder: false, IsRoot: false } entry)
        {
            return;
        }

        var result = MessageBox.Show(
            entry.IsDirectory ? ExplorerTexts.DeleteConfirmFolder(entry.Name) : ExplorerTexts.DeleteConfirmFile(entry.Name),
            ExplorerTexts.DeleteConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var trimmedPath = entry.FullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parentFolder = Path.GetDirectoryName(trimmedPath) ?? entry.FullPath;

        DeleteRequested?.Invoke(this, new ExplorerDeleteEventArgs
        {
            FullPath = entry.FullPath,
            IsDirectory = entry.IsDirectory,
            ParentFolderPath = parentFolder,
        });
    }

    /// <summary>
    /// Doppelklick auf eine Datei im Haupt- ODER Favoriten-Baum öffnet sie im gerade aktiven
    /// Arbeitsbereich (Nutzer-Feedback: "Doppelklick auf eine Datei in der Verzeichnis Übersicht
    /// sollte die Datei in der gerade aktiven Arbeitsumgebung öffnen."). Bewusst über
    /// <c>e.OriginalSource</c> (nicht <c>sender</c>) ausgelesen wie bei
    /// <see cref="TreeViewItem_Expanded"/>, da MouseDoubleClick ein bubbelndes Ereignis ist und
    /// sonst z. B. ein Doppelklick auf ein KIND-Element fälschlich dem übergeordneten Ordner
    /// zugeordnet würde. Ein Doppelklick auf einen Ordner löst hier bewusst NICHTS aus (das
    /// Auf-/Zuklappen übernimmt TreeView bereits von sich aus per Doppelklick).
    /// </summary>
    private void Entry_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement { DataContext: ExplorerEntry { IsDirectory: false, IsPlaceholder: false } entry })
        {
            return;
        }

        OpenFileRequested?.Invoke(this, new ExplorerOpenFileEventArgs { FilePath = entry.FullPath, Target = ExplorerOpenTarget.Active });
        e.Handled = true;
    }

    /// <summary>Alt+F (Nutzer-Feedback: "Füge für Favoriten hinzufügen den Hotkey Alt + F hinzu")
    /// pinnt den im Haupt- oder Favoriten-Baum gerade ausgewählten Eintrag an - siehe
    /// <see cref="PinToFavorites"/> (dieselbe Logik wie <see cref="PinFavorite_Click"/> im
    /// Kontextmenü).</summary>
    private void ExplorerPanel_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Alt || e.Key != Key.F)
        {
            return;
        }

        var selected = (Tree.SelectedItem as ExplorerEntry) ?? (FavoritesTree.SelectedItem as ExplorerEntry);
        if (selected is { IsPlaceholder: false })
        {
            PinToFavorites(selected);
        }

        e.Handled = true;
    }

    private void RefreshFolder_Click(object sender, RoutedEventArgs e)
    {
        if (EntryFrom(sender) is { IsDirectory: true, IsPlaceholder: false } entry)
        {
            entry.ChildrenLoaded = false;
            LoadChildren(entry);
        }
    }

    private void AssignColor_Click(object sender, RoutedEventArgs e)
    {
        if (EntryFrom(sender) is not { IsPlaceholder: false } entry)
        {
            return;
        }

        var colorDialog = new System.Windows.Forms.ColorDialog();
        if (!string.IsNullOrEmpty(entry.ColorHex))
        {
            try
            {
                var current = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(entry.ColorHex)!;
                colorDialog.Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B);
            }
            catch (FormatException)
            {
                // Ungültiger gespeicherter Hex-Code - Dialog startet einfach ohne Vorauswahl.
            }
        }

        if (colorDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var c = colorDialog.Color;
        var hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        entry.ColorHex = hex;
        _colors[PathNormalization.NormalizeKey(entry.FullPath)] = hex;
        SaveColors();
    }

    private void ClearColor_Click(object sender, RoutedEventArgs e)
    {
        if (EntryFrom(sender) is not { IsPlaceholder: false } entry)
        {
            return;
        }

        entry.ColorHex = entry.IsDirectory
            ? FolderCategoryColorResolver.DefaultColorFor(entry.Name, _activeProfile?.FolderCategories)
            : null;
        _colors.Remove(PathNormalization.NormalizeKey(entry.FullPath));
        SaveColors();
    }

    private void SaveColors()
    {
        try
        {
            var merged = ExplorerColorStore.SaveMerged(_colorsBaseline, _colors, ExplorerColorStore.GetDefaultColorsFilePath());
            _colors.Clear();
            foreach (var (key, value) in merged)
            {
                _colors[key] = value;
            }

            _colorsBaseline = new Dictionary<string, string>(merged);
        }
        catch (IOException)
        {
            // Farbe bleibt für die laufende Sitzung wirksam, wird nur nicht dauerhaft gespeichert.
        }
        catch (UnauthorizedAccessException)
        {
            // Farbe bleibt für die laufende Sitzung wirksam, wird nur nicht dauerhaft gespeichert.
        }
    }

    private void PinFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (EntryFrom(sender) is not { IsPlaceholder: false } entry)
        {
            return;
        }

        PinToFavorites(entry);
    }

    /// <summary>Gemeinsame Pin-Logik für das Kontextmenü (<see cref="PinFavorite_Click"/>) UND den
    /// neuen Alt+F-Hotkey (<see cref="ExplorerPanel_PreviewKeyDown"/>).</summary>
    private void PinToFavorites(ExplorerEntry entry)
    {
        var key = PathNormalization.NormalizeKey(entry.FullPath);
        if (!_favoritePaths.Any(p => PathNormalization.NormalizeKey(p) == key))
        {
            _favoritePaths.Add(entry.FullPath);
            SaveFavorites();
        }

        entry.IsFavorite = true;
        RebuildFavoritesDisplay();
    }

    private void UnpinFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (EntryFrom(sender) is not { } entry)
        {
            return;
        }

        var key = PathNormalization.NormalizeKey(entry.FullPath);
        _favoritePaths.RemoveAll(p => PathNormalization.NormalizeKey(p) == key);
        SaveFavorites();
        entry.IsFavorite = false;
        RebuildFavoritesDisplay();
    }

    private void SaveFavorites()
    {
        if (_favoritesFilePath is not { } path)
        {
            // Noch kein Spiel bekannt: Favoriten wirken nur in der laufenden Sitzung.
            return;
        }

        try
        {
            var merged = ExplorerFavoritesStore.SaveMerged(_favoritesBaseline, _favoritePaths, path);
            _favoritePaths.Clear();
            _favoritePaths.AddRange(merged);
            _favoritesBaseline = new List<string>(merged);
        }
        catch (IOException)
        {
            // Favorit bleibt für die laufende Sitzung wirksam, wird nur nicht dauerhaft gespeichert.
        }
        catch (UnauthorizedAccessException)
        {
            // Favorit bleibt für die laufende Sitzung wirksam, wird nur nicht dauerhaft gespeichert.
        }
    }

    /// <summary>
    /// Baut die Anzeige-Liste der Favoritenleiste aus <see cref="_favoritePaths"/> neu auf. Ein
    /// zwischenzeitlich gelöschter/umbenannter Favorit wird dabei übersprungen (bleibt aber in
    /// <see cref="_favoritePaths"/> gespeichert, falls er später wieder auftaucht) - bekannte,
    /// bewusste Vereinfachung: er verschwindet dadurch nicht dauerhaft aus der gespeicherten
    /// Liste, bis er explizit entfernt wird.
    ///
    /// Seit der Umstellung von ListBox auf TreeView (Nutzer-Feedback: Favoriten sollen sich wie
    /// normale Ordner auf-/zuklappen lassen) bekommt ein favorisierter ORDNER hier zusätzlich
    /// einen Platzhalter-Kindknoten (siehe <see cref="CreatePlaceholder"/>) - GENAU wie ein
    /// Ordner im Hauptbaum, damit TreeView den Aufklapp-Pfeil anzeigt und
    /// <see cref="TreeViewItem_Expanded"/> beim ersten Aufklappen automatisch die echten Kinder
    /// nachlädt (diese Methode ist bereits rein über ExplorerEntry.FullPath generisch, unabhängig
    /// davon, aus welchem Baum der Knoten stammt - keine Änderung dort nötig). Über
    /// <see cref="ApplyColorAndFavoriteState"/> bekommt der Favorit außerdem dieselbe Farbe wie im
    /// Hauptbaum angezeigt (vorher zeigte die flache Favoritenliste gar keine Farbe an).
    /// </summary>
    private void RebuildFavoritesDisplay()
    {
        _favoriteEntries.Clear();
        foreach (var path in _favoritePaths)
        {
            var isDirectory = Directory.Exists(path);
            if (!isDirectory && !File.Exists(path))
            {
                continue;
            }

            var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(trimmed);

            var entry = new ExplorerEntry
            {
                FullPath = path,
                Name = string.IsNullOrEmpty(name) ? path : name,
                IsDirectory = isDirectory,
            };

            ApplyColorAndFavoriteState(entry);
            entry.IsFavorite = true;

            if (isDirectory)
            {
                entry.Children.Add(CreatePlaceholder());
            }

            _favoriteEntries.Add(entry);
        }
    }

    private void RemoveRoot_Click(object sender, RoutedEventArgs e)
    {
        if (EntryFrom(sender) is { IsRoot: true } entry)
        {
            _roots.Remove(entry);
        }
    }

    private void RevealInExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (EntryFrom(sender) is not { IsPlaceholder: false } entry)
        {
            return;
        }

        try
        {
            // Der Explorer versteht nur Pfade mit Backslashes (sonst öffnet er "Dokumente"); auch ein alter, gemischt gespeicherter Pfad geht so.
            var path = PathNormalization.ToWindowsPath(entry.FullPath);
            if (entry.IsDirectory)
            {
                if (Directory.Exists(path))
                {
                    Process.Start("explorer.exe", $"\"{path}\"");
                }
            }
            else if (File.Exists(path))
            {
                // "/select," markiert die Datei im geöffneten Ordnerfenster, statt den Ordner
                // nur allgemein zu öffnen - dieselbe Umsetzung wie
                // ParadoxModTool.Editor.EditorPane.OpenContainingFolder.
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Explorer konnte nicht gestartet werden - kein kritischer Pfad, stillschweigend ignorieren.
        }
    }

    private void AddRoot_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (_suggestedInitialFolder is { } suggested && Directory.Exists(suggested))
        {
            dialog.SelectedPath = suggested;
        }

        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("explorer.addRoot");
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var path = dialog.SelectedPath;
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var displayName = Path.GetFileName(trimmed);
        AddRoot(new ExplorerRootDescriptor(path, string.IsNullOrEmpty(displayName) ? path : displayName, ExplorerRootKind.Custom));
    }

    private void RefreshAll_Click(object sender, RoutedEventArgs e) => RefreshAll();
}
