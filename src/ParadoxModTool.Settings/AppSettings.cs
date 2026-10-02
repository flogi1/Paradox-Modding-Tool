using System.Runtime.CompilerServices;
using ParadoxModTool.Session;

namespace ParadoxModTool.Settings;

/// <summary>
/// Einstellungen des Hauptfensters, die früher nur im Häkchen der Menüeinträge lebten. Einzige Quelle der Wahrheit für Werkzeugleisten-
/// Schalter, Menü und Einstellungsfenster: Jede Änderung meldet <see cref="Changed"/> mit dem Namen der Eigenschaft, das Hauptfenster
/// wendet sie an. Editor-Einstellungen (Scope, Klammern, Script-Hilfen, GUI) bleiben in ihren eigenen Klassen im Editor-Projekt.
/// </summary>
public sealed class AppSettings
{
    private bool _splitViewEnabled;
    private bool _explorerVisible = true;
    private bool _resultsPanelOnTop;
    private bool _colorizeWholeTab;
    private bool _multiRowTabs;
    private bool _pathBarVisible = true;
    private bool _replaceWarningsInWindow;
    private bool _notesPanelVisible = true;
    private bool _notesPanelAlwaysVisible;
    private bool _documentBarVisible = true;
    private bool _workspaceFilterButtonVisible = true;
    private bool _workspaceIconButtonVisible = true;
    private bool _checkForUpdatesOnStartup = true;
    private bool _compatchCautiousWrite;
    private bool _compatchSkipLocked;
    private bool _compatchResultRight;
    private bool _compatchMainAsBase;
    private bool _compatchFillMissing;
    private bool _compatchHideFilled;
    private bool _compatchHideReplaced;
    private bool _compatchHideDecided;
    private bool _compatchUncertainOnly;
    private bool _compatchKindsSideBySide;
    private bool _explorerButtonsOnLeft;
    private bool _explorerShowErrorMarks = true;
    private bool _explorerSearchFoldersOnly;
    private bool _explorerHideUgcReferences = true;
    private string _compatchDefaultView = "A";
    private readonly HashSet<string> _hiddenToolbarItems = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, string>> _iconColors = new(StringComparer.Ordinal);
    private string _uiLanguage = LanguageCatalog.Normalize(null);
    private string _settingsWindowCategory = nameof(SettingsCategory.General);
    private WindowBoundsState? _settingsWindowBounds;
    private bool _errorLogStandalone;
    private WindowBoundsState? _errorLogWindowBounds;
    private bool _saveExplorerStandalone;
    private WindowBoundsState? _saveExplorerWindowBounds;
    private string _myModsView = "Tiles";
    private string _myModsGrouping = "Games";
    private string _myModsSort = "Name";
    private bool _myModsSortDescending;
    private bool _myModsOnlyPublished;
    private bool _myModsOnlyOpenBugs;
    private bool _myModsOnlyWarnings;
    private List<string> _myModsGameOrder = new();
    private List<string> _myModsHiddenGames = new();
    private List<string> _myModsHiddenAlertKinds = new();
    private bool _myModsStartMaximized = true;
    private bool _myModsAlertsCollapsed;
    private WindowBoundsState? _myModsWindowBounds;

    /// <summary>Eine einzelne Einstellung hat sich geändert; das Argument ist der Name der Eigenschaft.</summary>
    public event EventHandler<string>? Changed;

    /// <summary>Alle Werte wurden neu geladen (<see cref="Load"/>); einmal alles anwenden statt Eigenschaft für Eigenschaft.</summary>
    public event EventHandler? Reloaded;

    /// <summary>Zweiter Arbeitsbereich sichtbar (pro Spiel gespeichert).</summary>
    public bool SplitViewEnabled { get => _splitViewEnabled; set => Set(ref _splitViewEnabled, value); }

    /// <summary>Verzeichnis-Übersicht sichtbar. Wird nicht gespeichert; beim Start immer sichtbar.</summary>
    public bool ExplorerVisible { get => _explorerVisible; set => Set(ref _explorerVisible, value); }

    /// <summary>Suchergebnisleiste über statt unter dem Arbeitsbereich (pro Spiel gespeichert).</summary>
    public bool ResultsPanelOnTop { get => _resultsPanelOnTop; set => Set(ref _resultsPanelOnTop, value); }

    public bool ColorizeWholeTab { get => _colorizeWholeTab; set => Set(ref _colorizeWholeTab, value); }

    public bool MultiRowTabs { get => _multiRowTabs; set => Set(ref _multiRowTabs, value); }

    /// <summary>Pfadleiste über dem Editor in beiden Arbeitsbereichen.</summary>
    public bool PathBarVisible { get => _pathBarVisible; set => Set(ref _pathBarVisible, value); }

    /// <summary>Warnungen nach dem Ersetzen (OR-/NOR-/AND-Blöcke mit höchstens einem Eintrag) in einem eigenen Fenster statt im Notiz-Panel. Standard: aus.</summary>
    public bool ReplaceWarningsInWindow { get => _replaceWarningsInWindow; set => Set(ref _replaceWarningsInWindow, value); }

    /// <summary>Notiz/Lesezeichen-Panel eingeschaltet.</summary>
    public bool NotesPanelVisible { get => _notesPanelVisible; set => Set(ref _notesPanelVisible, value); }

    /// <summary>Panel auch bei Dateien ohne Notiz und Lesezeichen offen halten.</summary>
    public bool NotesPanelAlwaysVisible { get => _notesPanelAlwaysVisible; set => Set(ref _notesPanelAlwaysVisible, value); }

    public bool DocumentBarVisible { get => _documentBarVisible; set => Set(ref _documentBarVisible, value); }

    /// <summary>Knopf "Filter ▾" in der Werkzeugleiste jedes Arbeitsbereichs.</summary>
    public bool WorkspaceFilterButtonVisible { get => _workspaceFilterButtonVisible; set => Set(ref _workspaceFilterButtonVisible, value); }

    /// <summary>Knopf "Symbole ▾" in der Werkzeugleiste jedes Arbeitsbereichs.</summary>
    public bool WorkspaceIconButtonVisible { get => _workspaceIconButtonVisible; set => Set(ref _workspaceIconButtonVisible, value); }

    public bool CheckForUpdatesOnStartup { get => _checkForUpdatesOnStartup; set => Set(ref _checkForUpdatesOnStartup, value); }

    public bool CompatchCautiousWrite { get => _compatchCautiousWrite; set => Set(ref _compatchCautiousWrite, value); }

    public bool CompatchSkipLocked { get => _compatchSkipLocked; set => Set(ref _compatchSkipLocked, value); }

    /// <summary>Kompatch-Merge: Ergebnis-Spalte ganz rechts (A | B ‖ Ergebnis) statt in der Mitte.</summary>
    public bool CompatchResultRight { get => _compatchResultRight; set => Set(ref _compatchResultRight, value); }

    /// <summary>Kompatch, Standard für neue Arbeitsstände: Hauptmod als Basis. Ein offener Arbeitsstand behält seinen eigenen Wert.</summary>
    public bool CompatchMainAsBase { get => _compatchMainAsBase; set => Set(ref _compatchMainAsBase, value); }

    /// <summary>Kompatch, Standard für neue Arbeitsstände: Fehlendes aus dem Hauptmod ergänzen. Ein offener Arbeitsstand behält seinen eigenen Wert.</summary>
    public bool CompatchFillMissing { get => _compatchFillMissing; set => Set(ref _compatchFillMissing, value); }

    /// <summary>Kompatch, Standard für neue Arbeitsstände: Ergänzte fehlende Blöcke ausblenden. Ein offener Arbeitsstand behält seinen eigenen Wert.</summary>
    public bool CompatchHideFilled { get => _compatchHideFilled; set => Set(ref _compatchHideFilled, value); }

    /// <summary>Kompatch, Standard für neue Arbeitsstände: Einfach ersetzte doppelte Dateien ausblenden. Ein offener Arbeitsstand behält seinen eigenen Wert.</summary>
    public bool CompatchHideReplaced { get => _compatchHideReplaced; set => Set(ref _compatchHideReplaced, value); }

    /// <summary>Kompatch, Standard für neue Arbeitsstände: Erledigte ausblenden. Ein offener Arbeitsstand behält seinen eigenen Wert.</summary>
    public bool CompatchHideDecided { get => _compatchHideDecided; set => Set(ref _compatchHideDecided, value); }

    /// <summary>Kompatch, Standard für neue Arbeitsstände: Nur unsichere Stellen zeigen. Ein offener Arbeitsstand behält seinen eigenen Wert.</summary>
    public bool CompatchUncertainOnly { get => _compatchUncertainOnly; set => Set(ref _compatchUncertainOnly, value); }

    /// <summary>Kompatch-Startfenster: Dateiarten nebeneinander statt als Liste untereinander. Standard: aus (Liste).</summary>
    public bool CompatchKindsSideBySide { get => _compatchKindsSideBySide; set => Set(ref _compatchKindsSideBySide, value); }

    /// <summary>Verzeichnis-Übersicht: Lupe und Arbeitsbereich-Knöpfe links vor dem Namen.</summary>
    public bool ExplorerButtonsOnLeft { get => _explorerButtonsOnLeft; set => Set(ref _explorerButtonsOnLeft, value); }

    /// <summary>Verzeichnis-Übersicht: Bug-Markierungen (Meldungen des letzten Spiellaufs) zeigen. Standard: an.</summary>
    public bool ExplorerShowErrorMarks { get => _explorerShowErrorMarks; set => Set(ref _explorerShowErrorMarks, value); }

    /// <summary>Verzeichnis-Übersicht: Namenssuche nur nach Ordnern.</summary>
    public bool ExplorerSearchFoldersOnly { get => _explorerSearchFoldersOnly; set => Set(ref _explorerSearchFoldersOnly, value); }

    /// <summary>Verzeichnis-Übersicht: Workshop-Verweise <c>ugc_*.mod</c> ausblenden. Standard: an.</summary>
    public bool ExplorerHideUgcReferences { get => _explorerHideUgcReferences; set => Set(ref _explorerHideUgcReferences, value); }

    /// <summary>Standardansicht des Komp-Patch Tools: "A" (Aktionen), "B" (Dokument) oder "C" (Karten); Unbekanntes wird "A".</summary>
    public string CompatchDefaultView { get => _compatchDefaultView; set => Set(ref _compatchDefaultView, NormalizeView(value)); }

    /// <summary>Oberflächensprache (Code wie <c>de</c>, <c>en</c>); wirkt beim nächsten Start.</summary>
    public string UiLanguage { get => _uiLanguage; set => Set(ref _uiLanguage, LanguageCatalog.Normalize(value)); }

    /// <summary>Zuletzt gezeigte Kategorie des Einstellungsfensters (Name aus <see cref="SettingsCategory"/>); Unbekanntes wird "General".</summary>
    public string SettingsWindowCategory { get => _settingsWindowCategory; set => Set(ref _settingsWindowCategory, SettingsCategories.Parse(value).ToString()); }

    /// <summary>Position und Größe des Einstellungsfensters. Liefert und übernimmt Kopien.</summary>
    public WindowBoundsState? SettingsWindowBounds
    {
        get => Copy(_settingsWindowBounds);
        set
        {
            if (SameBounds(_settingsWindowBounds, value))
            {
                return;
            }

            _settingsWindowBounds = Copy(value);
            Changed?.Invoke(this, nameof(SettingsWindowBounds));
        }
    }

    /// <summary>Fehlerprotokoll als eigenes Fenster statt über dem Hauptfenster (Standard aus); gilt ab dem nächsten Öffnen.</summary>
    public bool ErrorLogStandalone { get => _errorLogStandalone; set => Set(ref _errorLogStandalone, value); }

    /// <summary>Position und Größe des Fensters "Fehlerprotokoll". Liefert und übernimmt Kopien.</summary>
    public WindowBoundsState? ErrorLogWindowBounds
    {
        get => Copy(_errorLogWindowBounds);
        set
        {
            if (SameBounds(_errorLogWindowBounds, value))
            {
                return;
            }

            _errorLogWindowBounds = Copy(value);
            Changed?.Invoke(this, nameof(ErrorLogWindowBounds));
        }
    }

    /// <summary>Spielstand-Explorer als eigenes Fenster statt über dem Hauptfenster (Standard aus); gilt ab dem nächsten Öffnen.</summary>
    public bool SaveExplorerStandalone { get => _saveExplorerStandalone; set => Set(ref _saveExplorerStandalone, value); }

    /// <summary>Position und Größe des Fensters "Spielstand-Explorer". Liefert und übernimmt Kopien.</summary>
    public WindowBoundsState? SaveExplorerWindowBounds
    {
        get => Copy(_saveExplorerWindowBounds);
        set
        {
            if (SameBounds(_saveExplorerWindowBounds, value))
            {
                return;
            }

            _saveExplorerWindowBounds = Copy(value);
            Changed?.Invoke(this, nameof(SaveExplorerWindowBounds));
        }
    }

    /// <summary>Meine Mods: "Tiles" oder "List"; Unbekanntes wird "Tiles".</summary>
    public string MyModsView { get => _myModsView; set => Set(ref _myModsView, OneOf(value, "Tiles", "List")); }

    /// <summary>Meine Mods: "Games" oder "All"; Unbekanntes wird "Games".</summary>
    public string MyModsGrouping { get => _myModsGrouping; set => Set(ref _myModsGrouping, OneOf(value, "Games", "All")); }

    /// <summary>Meine Mods: "Name", "Subscribers", "OpenBugs" oder "LastRelease"; Unbekanntes wird "Name".</summary>
    public string MyModsSort { get => _myModsSort; set => Set(ref _myModsSort, OneOf(value, "Name", "Subscribers", "OpenBugs", "LastRelease")); }

    public bool MyModsSortDescending { get => _myModsSortDescending; set => Set(ref _myModsSortDescending, value); }

    public bool MyModsOnlyPublished { get => _myModsOnlyPublished; set => Set(ref _myModsOnlyPublished, value); }

    public bool MyModsOnlyOpenBugs { get => _myModsOnlyOpenBugs; set => Set(ref _myModsOnlyOpenBugs, value); }

    public bool MyModsOnlyWarnings { get => _myModsOnlyWarnings; set => Set(ref _myModsOnlyWarnings, value); }

    /// <summary>Meine Mods: Reihenfolge der Spiele. Übernimmt eine Kopie; meldet nur eine echte Änderung.</summary>
    public IReadOnlyList<string> MyModsGameOrder
    {
        get => _myModsGameOrder;
        set => SetList(ref _myModsGameOrder, value, nameof(MyModsGameOrder));
    }

    /// <summary>Meine Mods: ausgeblendete Spiele. Übernimmt eine Kopie; meldet nur eine echte Änderung.</summary>
    public IReadOnlyList<string> MyModsHiddenGames
    {
        get => _myModsHiddenGames;
        set => SetList(ref _myModsHiddenGames, value, nameof(MyModsHiddenGames));
    }

    /// <summary>Meine Mods: ausgeblendete Hinweisarten. Übernimmt eine Kopie; meldet nur eine echte Änderung.</summary>
    public IReadOnlyList<string> MyModsHiddenAlertKinds
    {
        get => _myModsHiddenAlertKinds;
        set => SetList(ref _myModsHiddenAlertKinds, value, nameof(MyModsHiddenAlertKinds));
    }

    /// <summary>Meine Mods: beim Öffnen als Vollbild (maximiert); aus = gemerkte Fenstergröße.</summary>
    public bool MyModsStartMaximized { get => _myModsStartMaximized; set => Set(ref _myModsStartMaximized, value); }

    public bool MyModsAlertsCollapsed { get => _myModsAlertsCollapsed; set => Set(ref _myModsAlertsCollapsed, value); }

    /// <summary>Position und Größe des Fensters "Meine Mods". Liefert und übernimmt Kopien.</summary>
    public WindowBoundsState? MyModsWindowBounds
    {
        get => Copy(_myModsWindowBounds);
        set
        {
            if (SameBounds(_myModsWindowBounds, value))
            {
                return;
            }

            _myModsWindowBounds = Copy(value);
            Changed?.Invoke(this, nameof(MyModsWindowBounds));
        }
    }

    /// <summary>Ausgeblendete Einträge der allgemeinen Werkzeugleiste (Kennungen aus <see cref="ToolbarItemIds"/>).</summary>
    public IReadOnlyCollection<string> HiddenToolbarItems => _hiddenToolbarItems;

    public bool IsToolbarItemVisible(string id) => !_hiddenToolbarItems.Contains(id);

    /// <summary>Blendet einen Eintrag ein oder aus; meldet <see cref="Changed"/> mit <c>HiddenToolbarItems</c>, wenn sich etwas ändert.</summary>
    public void SetToolbarItemVisible(string id, bool visible)
    {
        if (!ToolbarCatalog.IsKnown(id))
        {
            throw new ArgumentException($"Unbekannter Werkzeugleisten-Eintrag '{id}'.", nameof(id));
        }

        var changed = visible ? _hiddenToolbarItems.Remove(id) : _hiddenToolbarItems.Add(id);
        if (changed)
        {
            Changed?.Invoke(this, nameof(HiddenToolbarItems));
        }
    }

    /// <summary>Eigene Symbolfarben pro Theme (Theme-Kennung → Symbol → Hex).</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> IconColorsByTheme =>
        _iconColors.ToDictionary(t => t.Key, t => (IReadOnlyDictionary<string, string>)t.Value, StringComparer.Ordinal);

    /// <summary>Eigene Farbe eines Symbols im Theme, <see langword="null"/> = Standardfarbe.</summary>
    public string? GetIconColor(string themeId, string iconId) =>
        _iconColors.TryGetValue(themeId, out var colors) && colors.TryGetValue(iconId, out var hex) ? hex : null;

    /// <summary>Setzt die Farbe eines Symbols im Theme; <see langword="null"/> setzt auf die Standardfarbe zurück.</summary>
    public void SetIconColor(string themeId, string iconId, string? hex)
    {
        if (!IconIds.IsKnown(iconId))
        {
            throw new ArgumentException($"Unbekanntes Symbol '{iconId}'.", nameof(iconId));
        }

        if (hex is not null && !IconIds.IsValidColor(hex))
        {
            throw new ArgumentException($"Ungültige Farbe '{hex}'.", nameof(hex));
        }

        if (GetIconColor(themeId, iconId) == hex)
        {
            return;
        }

        if (hex is null)
        {
            _iconColors[themeId].Remove(iconId);
        }
        else
        {
            if (!_iconColors.TryGetValue(themeId, out var colors))
            {
                colors = new Dictionary<string, string>(StringComparer.Ordinal);
                _iconColors[themeId] = colors;
            }

            colors[iconId] = hex;
        }

        Changed?.Invoke(this, nameof(IconColorsByTheme));
    }

    /// <summary>
    /// Übernimmt die Werte aus der Sitzung und meldet einmal <see cref="Reloaded"/>. <paramref name="themeIds"/> sind die beim Start
    /// bekannten Themes (für die einmalige Übernahme der alten Notiz-Farbe).
    /// </summary>
    public void Load(AppSessionState state, IReadOnlyCollection<string> themeIds)
    {
        _splitViewEnabled = state.SplitViewEnabled;
        _resultsPanelOnTop = state.ResultsPanelOnTop;
        _colorizeWholeTab = state.ColorizeWholeTab;
        _multiRowTabs = state.MultiRowTabs;
        _pathBarVisible = state.PathBarVisible;
        _replaceWarningsInWindow = state.ReplaceWarningsInWindow;
        _notesPanelVisible = state.NotesPanelVisible;
        _notesPanelAlwaysVisible = state.NotesPanelAlwaysVisible;
        _documentBarVisible = state.DocumentBarVisible;
        _workspaceFilterButtonVisible = state.WorkspaceFilterButtonVisible;
        _workspaceIconButtonVisible = state.WorkspaceIconButtonVisible;
        _checkForUpdatesOnStartup = state.CheckForUpdatesOnStartup;
        _compatchCautiousWrite = state.CompatchCautiousWrite;
        _compatchSkipLocked = state.CompatchSkipLocked;
        _compatchResultRight = state.CompatchResultRight;
        _compatchMainAsBase = state.CompatchMainAsBase;
        _compatchFillMissing = state.CompatchFillMissing;
        _compatchHideFilled = state.CompatchHideFilled;
        _compatchHideReplaced = state.CompatchHideReplaced;
        _compatchHideDecided = state.CompatchHideDecided;
        _compatchUncertainOnly = state.CompatchUncertainOnly;
        _compatchKindsSideBySide = state.CompatchKindsSideBySide;
        _explorerButtonsOnLeft = state.ExplorerButtonsOnLeft;
        _explorerShowErrorMarks = state.ExplorerShowErrorMarks;
        _explorerSearchFoldersOnly = state.ExplorerSearchFoldersOnly;
        _explorerHideUgcReferences = state.ExplorerHideUgcReferences;
        _compatchDefaultView = NormalizeView(state.CompatchDefaultView);
        _uiLanguage = LanguageCatalog.Normalize(state.UiLanguage);
        _settingsWindowCategory = SettingsCategories.Parse(state.SettingsWindowCategory).ToString();
        _settingsWindowBounds = Copy(state.SettingsWindowBounds);
        _errorLogStandalone = state.ErrorLogStandalone;
        _errorLogWindowBounds = Copy(state.ErrorLogWindowBounds);
        _saveExplorerStandalone = state.SaveExplorerStandalone;
        _saveExplorerWindowBounds = Copy(state.SaveExplorerWindowBounds);
        _myModsView = OneOf(state.MyModsView, "Tiles", "List");
        _myModsGrouping = OneOf(state.MyModsGrouping, "Games", "All");
        _myModsSort = OneOf(state.MyModsSort, "Name", "Subscribers", "OpenBugs", "LastRelease");
        _myModsSortDescending = state.MyModsSortDescending;
        _myModsOnlyPublished = state.MyModsOnlyPublished;
        _myModsOnlyOpenBugs = state.MyModsOnlyOpenBugs;
        _myModsOnlyWarnings = state.MyModsOnlyWarnings;
        _myModsGameOrder = CleanList(state.MyModsGameOrder);
        _myModsHiddenGames = CleanList(state.MyModsHiddenGames);
        _myModsHiddenAlertKinds = CleanList(state.MyModsHiddenAlertKinds);
        _myModsStartMaximized = state.MyModsStartMaximized;
        _myModsAlertsCollapsed = state.MyModsAlertsCollapsed;
        _myModsWindowBounds = Copy(state.MyModsWindowBounds);
        LoadToolbar(state);
        LoadIconColors(state, themeIds);
        Reloaded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Schreibt die Werte in <paramref name="state"/>; alle anderen Felder bleiben unberührt.</summary>
    public void WriteTo(AppSessionState state)
    {
        state.SplitViewEnabled = _splitViewEnabled;
        state.ResultsPanelOnTop = _resultsPanelOnTop;
        state.ColorizeWholeTab = _colorizeWholeTab;
        state.MultiRowTabs = _multiRowTabs;
        state.PathBarVisible = _pathBarVisible;
        state.ReplaceWarningsInWindow = _replaceWarningsInWindow;
        state.NotesPanelVisible = _notesPanelVisible;
        state.NotesPanelAlwaysVisible = _notesPanelAlwaysVisible;
        state.DocumentBarVisible = _documentBarVisible;
        state.WorkspaceFilterButtonVisible = _workspaceFilterButtonVisible;
        state.WorkspaceIconButtonVisible = _workspaceIconButtonVisible;
        state.CheckForUpdatesOnStartup = _checkForUpdatesOnStartup;
        state.CompatchCautiousWrite = _compatchCautiousWrite;
        state.CompatchSkipLocked = _compatchSkipLocked;
        state.CompatchResultRight = _compatchResultRight;
        state.CompatchMainAsBase = _compatchMainAsBase;
        state.CompatchFillMissing = _compatchFillMissing;
        state.CompatchHideFilled = _compatchHideFilled;
        state.CompatchHideReplaced = _compatchHideReplaced;
        state.CompatchHideDecided = _compatchHideDecided;
        state.CompatchUncertainOnly = _compatchUncertainOnly;
        state.CompatchKindsSideBySide = _compatchKindsSideBySide;
        state.ExplorerButtonsOnLeft = _explorerButtonsOnLeft;
        state.ExplorerShowErrorMarks = _explorerShowErrorMarks;
        state.ExplorerSearchFoldersOnly = _explorerSearchFoldersOnly;
        state.ExplorerHideUgcReferences = _explorerHideUgcReferences;
        state.CompatchDefaultView = _compatchDefaultView;
        state.UiLanguage = _uiLanguage;
        state.SettingsWindowCategory = _settingsWindowCategory;
        state.SettingsWindowBounds = Copy(_settingsWindowBounds);
        state.ErrorLogStandalone = _errorLogStandalone;
        state.ErrorLogWindowBounds = Copy(_errorLogWindowBounds);
        state.SaveExplorerStandalone = _saveExplorerStandalone;
        state.SaveExplorerWindowBounds = Copy(_saveExplorerWindowBounds);
        state.MyModsView = _myModsView;
        state.MyModsGrouping = _myModsGrouping;
        state.MyModsSort = _myModsSort;
        state.MyModsSortDescending = _myModsSortDescending;
        state.MyModsOnlyPublished = _myModsOnlyPublished;
        state.MyModsOnlyOpenBugs = _myModsOnlyOpenBugs;
        state.MyModsOnlyWarnings = _myModsOnlyWarnings;
        state.MyModsGameOrder = new List<string>(_myModsGameOrder);
        state.MyModsHiddenGames = new List<string>(_myModsHiddenGames);
        state.MyModsHiddenAlertKinds = new List<string>(_myModsHiddenAlertKinds);
        state.MyModsStartMaximized = _myModsStartMaximized;
        state.MyModsAlertsCollapsed = _myModsAlertsCollapsed;
        state.MyModsWindowBounds = Copy(_myModsWindowBounds);
        state.HiddenToolbarItems = _hiddenToolbarItems.OrderBy(id => id, StringComparer.Ordinal).ToList();
        state.ScriptHintButtonsVisible = IsToolbarItemVisible(ToolbarItemIds.AutoComplete) && IsToolbarItemVisible(ToolbarItemIds.Hover);
        state.IconColorsByTheme = _iconColors
            .Where(t => t.Value.Count > 0)
            .ToDictionary(t => t.Key, t => new Dictionary<string, string>(t.Value), StringComparer.Ordinal);
    }

    /// <summary>
    /// Ohne gespeicherte Symbolfarben (Sitzung vor der UI-Reorganisation) wird die alte, globale Notiz-Farbe einmalig in jedes bekannte
    /// Theme übernommen. "Zurücksetzen" in einem Theme führt danach zur eingebauten Standardfarbe. Ungültige Einträge fallen weg.
    /// </summary>
    private void LoadIconColors(AppSessionState state, IReadOnlyCollection<string> themeIds)
    {
        _iconColors.Clear();
        if (state.IconColorsByTheme is null)
        {
            if (IconIds.IsValidColor(state.NoteIconColorHex))
            {
                foreach (var theme in themeIds)
                {
                    _iconColors[theme] = new Dictionary<string, string>(StringComparer.Ordinal) { [IconIds.Note] = state.NoteIconColorHex! };
                }
            }

            return;
        }

        foreach (var (theme, colors) in state.IconColorsByTheme)
        {
            var valid = colors.Where(c => IconIds.IsKnown(c.Key) && IconIds.IsValidColor(c.Value))
                .ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal);
            if (valid.Count > 0)
            {
                _iconColors[theme] = valid;
            }
        }
    }

    /// <summary>
    /// Ohne gespeicherte Liste (Sitzung vor der UI-Reorganisation) gilt der alte Schalter "Vorschläge und Tooltips in der Werkzeugleiste":
    /// war er aus, sind beide Einträge ausgeblendet. Danach gilt nur noch die Liste; unbekannte Kennungen fallen weg.
    /// </summary>
    private void LoadToolbar(AppSessionState state)
    {
        _hiddenToolbarItems.Clear();
        if (state.HiddenToolbarItems is null)
        {
            if (!state.ScriptHintButtonsVisible)
            {
                _hiddenToolbarItems.Add(ToolbarItemIds.AutoComplete);
                _hiddenToolbarItems.Add(ToolbarItemIds.Hover);
            }

            return;
        }

        foreach (var id in state.HiddenToolbarItems.Where(ToolbarCatalog.IsKnown))
        {
            _hiddenToolbarItems.Add(id);
        }
    }

    private static WindowBoundsState? Copy(WindowBoundsState? bounds) => bounds is null
        ? null
        : new WindowBoundsState { Left = bounds.Left, Top = bounds.Top, Width = bounds.Width, Height = bounds.Height, IsMaximized = bounds.IsMaximized };

    private static bool SameBounds(WindowBoundsState? a, WindowBoundsState? b) =>
        a is null || b is null
            ? a is null && b is null
            : a.Left == b.Left && a.Top == b.Top && a.Width == b.Width && a.Height == b.Height && a.IsMaximized == b.IsMaximized;

    private static string NormalizeView(string? view) => view?.Trim().ToUpperInvariant() switch
    {
        "B" => "B",
        "C" => "C",
        _ => "A",
    };

    /// <summary>Der passende erlaubte Wert (Groß-/Kleinschreibung egal), sonst der erste.</summary>
    private static string OneOf(string? value, params string[] allowed) =>
        allowed.FirstOrDefault(a => string.Equals(a, value?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? allowed[0];

    private static List<string> CleanList(IEnumerable<string>? values) =>
        (values ?? Array.Empty<string>()).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct(StringComparer.Ordinal).ToList();

    private void SetList(ref List<string> field, IEnumerable<string>? value, string name)
    {
        var clean = CleanList(value);
        if (field.SequenceEqual(clean, StringComparer.Ordinal))
        {
            return;
        }

        field = clean;
        Changed?.Invoke(this, name);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string name = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        Changed?.Invoke(this, name);
    }
}
