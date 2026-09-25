using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.SearchReplace;
// WPF bringt über System.Windows.Controls bereits eine eigene Klasse "TextSearch" mit
// (Such-Unterstützung in ListBox/ComboBox) - das kollidiert mit unserer gleichnamigen
// ParadoxModTool.SearchReplace.TextSearch (CS0104). Alias löst das eindeutig zugunsten
// unserer eigenen Klasse auf, da Alias-Direktiven Vorrang vor Namespace-Imports haben.
using TextSearch = ParadoxModTool.SearchReplace.TextSearch;
// UseWindowsForms ist im .csproj dieses Projekts aktiviert (FolderBrowserDialog, siehe
// Browse_Click) - wie schon an anderer Stelle in diesem Programm dokumentiert (Lessons Learned
// Punkt 7/12 der Architektur-Dokumentation) werden dadurch mehrere gleichnamige Typen zwischen
// WPF und WinForms mehrdeutig (CS0104). Neu betroffen durch die Schnellzugriff-Buttons (siehe
// BuildFixedQuickPathButtons/BuildCustomQuickPathButtons weiter unten, die WPF-Controls per Code
// statt XAML erzeugen): "Button" (System.Windows.Controls.Button vs. System.Windows.Forms.Button)
// und "Orientation" (System.Windows.Controls.Orientation vs. System.Windows.Forms.Orientation).
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;

namespace ParadoxModTool.App;

/// <summary>
/// Vereinheitlichtes Suchen-&amp;-Ersetzen-Fenster (Abschnitt 5b): "Suchen und Ersetzen und
/// Suchen in Dateien kann in einem Funktionskasten vereinheitlicht werden [...] es wäre gut,
/// wenn es einfach ist zwischen den Modi hin und her zu wechseln." Für den Modus "Aktuelles
/// Dokument" arbeitet dieses Fenster ausschließlich über die vom Hauptfenster übergebenen
/// Delegates (damit es selbst keine Kenntnis von EditorPane/ActionHistory braucht); für den
/// Modus "Ordner" ruft es die reinen Lesevorgänge aus ParadoxModTool.SearchReplace direkt auf,
/// nur das tatsächliche Schreiben beim Ersetzen läuft über den Delegate ins Hauptfenster (damit
/// es dort als EIN Schritt im programmweiten Aktionsverlauf landet, siehe ParadoxModTool.UndoRedo).
/// </summary>
public partial class SearchReplaceWindow : Window
{
    private readonly Func<string> _getActiveDocumentText;
    private readonly Action<int, int> _selectInActiveDocument;
    private readonly Action<string> _replaceActiveDocumentText;
    private readonly Func<IReadOnlyList<FileReplacementPlan>, string, int> _executeFileReplacements;
    private readonly Action<string, int, int> _openSearchResult;
    private readonly Func<bool> _getHideDescription;
    private readonly Action<bool> _setHideDescription;

    private readonly string _favoritesPath = SearchFavoritesStore.GetDefaultFavoritesFilePath();
    private readonly ObservableCollection<string> _favorites = new();

    // Stand der Favoriten beim letzten Laden/Speichern (siehe SearchFavoritesStore.SaveMerged).
    private List<string> _favoritesBaseline = new();

    /// <summary>Aktives Spielprofil (für die 4 FESTEN Schnellzugriff-Buttons - Mod-Ordner/in_game/
    /// main_menu/loading_screen, siehe <see cref="BuildFixedQuickPathButtons"/>) sowie der
    /// Callback zum Auflösen des Mod-Ordner-Pfads (dieselbe Logik wie beim automatischen
    /// Vorschlag der Verzeichnis-Übersicht, siehe MainWindow.ResolveDefaultModRoot - hier als
    /// Delegate übergeben, damit dieses Fenster keine eigene Kopie der Auflösungslogik braucht).</summary>
    private readonly GameProfile? _activeProfile;
    private readonly Func<string?> _resolveDefaultModRoot;

    /// <summary>Liefert den Ordner mit den eigentlichen Vanilla-Spieldateien (common/gui/in_game/...,
    /// z. B. ".../Europa Universalis V/game") - Basis für die in_game/main_menu/loading_screen-
    /// Buttons. Bei einer echten EU5-Installation liegt dieser "game"-Ordner als GESCHWISTER neben
    /// "binaries" (mit der .exe), nicht darunter - vom Nutzer am 2026-09-17 bestätigt, siehe
    /// MainWindow.ComputeGameContentRoot. <see langword="null"/>, solange kein Spielpfad eingestellt
    /// ist oder der "game"-Ordner (noch) nicht existiert.</summary>
    private readonly Func<string?> _resolveGameContentRoot;

    private readonly string _quickPathSettingsPath = QuickPathStore.GetDefaultFilePath();
    private readonly QuickPathSettings _quickPathSettings;

    /// <summary>
    /// Die Trefferliste wird vom Hauptfenster übergeben und geteilt (Nutzer-Feedback: "Ich hätte
    /// auch gern noch die Option die Suchergebnisse aus dem Suchfenster [...] in einem Fenster
    /// über meinem Arbeitsbereich anzeigen zu lassen [...]. Die Ergebnisse im Suchfenster können
    /// aber auf jeden Fall bleiben!") — ein einzelnes <see cref="ObservableCollection{T}"/>-Objekt,
    /// das sowohl an <see cref="ResultsList"/> hier als auch an die neue Ergebnisleiste im
    /// Hauptfenster gebunden ist, sodass beide Anzeigen automatisch synchron bleiben, ohne dass
    /// zwei getrennte Kopien der Treffer gepflegt werden müssten.
    /// </summary>
    private readonly ObservableCollection<SearchResultRow> _results;

    /// <summary>Verhindert eine Endlosschleife, wenn das Hauptfenster den Checkbox-Zustand hier synchronisiert (siehe <see cref="SetHideDescriptionCheckboxState"/>).</summary>
    private bool _suppressHideDescriptionEvent;

    public SearchReplaceWindow(
        Func<string> getActiveDocumentText,
        Action<int, int> selectInActiveDocument,
        Action<string> replaceActiveDocumentText,
        Func<IReadOnlyList<FileReplacementPlan>, string, int> executeFileReplacements,
        Action<string, int, int> openSearchResult,
        ObservableCollection<SearchResultRow> sharedResults,
        Func<bool> getHideDescription,
        Action<bool> setHideDescription,
        GameProfile? activeProfile,
        Func<string?> resolveDefaultModRoot,
        Func<string?> resolveGameContentRoot)
    {
        InitializeComponent();

        _getActiveDocumentText = getActiveDocumentText;
        _selectInActiveDocument = selectInActiveDocument;
        _replaceActiveDocumentText = replaceActiveDocumentText;
        _executeFileReplacements = executeFileReplacements;
        _openSearchResult = openSearchResult;
        _results = sharedResults;
        _getHideDescription = getHideDescription;
        _setHideDescription = setHideDescription;
        _activeProfile = activeProfile;
        _resolveDefaultModRoot = resolveDefaultModRoot;
        _resolveGameContentRoot = resolveGameContentRoot;

        ResultsList.ItemsSource = _results;
        FavoritesList.ItemsSource = _favorites;
        HideDescriptionCheckBox.IsChecked = _getHideDescription();

        foreach (var folder in SearchFavoritesStore.TryLoad(_favoritesPath))
        {
            _favorites.Add(folder);
        }

        _favoritesBaseline = _favorites.ToList();

        _quickPathSettings = QuickPathStore.TryLoad(_quickPathSettingsPath);
        BuildFixedQuickPathButtons();
        BuildCustomQuickPathButtons();
        ApplyQuickPathVisibility(_quickPathSettings.Visible);

        UpdateModeVisibility();
    }

    /// <summary>
    /// Baut die 4 FESTEN, spielprofil-abhängigen Schnellzugriff-Buttons (Nutzer-Feedback: "es
    /// sollten für das Spiel 4 Default Buttons angezeigt werden ... Für EU V sollte das der mod
    /// Ordner sein, der in_game Ordner, der main_menu Ordner und der loading_screen Ordner."). Der
    /// Mod-Ordner-Button liegt unter <see cref="_resolveDefaultModRoot"/>; die anderen drei sind
    /// die VANILLA-Gegenstücke im Spiel-Installationsordner - also Unterordner von
    /// <see cref="_resolveGameContentRoot"/> (dem "game"-Ordner, der bei einer echten EU5-Installation
    /// als GESCHWISTER-Ordner neben "binaries" liegt, nicht dessen Unterordner - vom Nutzer am
    /// 2026-09-17 anhand seiner echten Installation bestätigt, nachdem eine erste Annahme
    /// fälschlich den "binaries"-Ordner selbst annahm), NICHT vom Mod-Ordner. Ist die jeweilige
    /// Basis nicht auflösbar (kein Profil/kein Spielpfad eingestellt), werden die Buttons
    /// deaktiviert statt versteckt, damit für den Nutzer sichtbar bleibt, DASS es diese
    /// Schnellzugriffe gibt (nur eben gerade nicht nutzbar).
    /// </summary>
    private void BuildFixedQuickPathButtons()
    {
        FixedQuickPathButtons.Children.Clear();

        var modRoot = _resolveDefaultModRoot();
        AddFixedQuickPathButton("Mod-Ordner", modRoot, "Der lokale Mod-Ordner aus dem aktiven Spielprofil.");

        if (_activeProfile is null)
        {
            return;
        }

        var gameContentRoot = _resolveGameContentRoot();

        foreach (var categoryPath in new[] { "in_game", "main_menu", "loading_screen" })
        {
            var category = _activeProfile.FolderCategories
                .FirstOrDefault(c => string.Equals(c.Path, categoryPath, StringComparison.OrdinalIgnoreCase));
            if (category is null)
            {
                continue;
            }

            var resolvedPath = gameContentRoot is null ? null : Path.Combine(gameContentRoot, category.Path);
            AddFixedQuickPathButton(category.Label, resolvedPath, category.Description ?? $"{category.Label} ({category.Path})");
        }
    }

    private void AddFixedQuickPathButton(string label, string? resolvedPath, string description)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 4, 4),
            IsEnabled = resolvedPath is not null,
            ToolTip = resolvedPath is null
                ? $"{description}\n\n(Derzeit nicht ermittelbar - Mod-Ordner nicht gefunden.)"
                : $"{description}\n\n{resolvedPath}",
        };

        button.Click += (_, _) =>
        {
            if (resolvedPath is not null)
            {
                FolderTextBox.Text = resolvedPath;
            }
        };

        FixedQuickPathButtons.Children.Add(button);
    }

    /// <summary>
    /// Baut die 4 FREI konfigurierbaren Schnellzugriff-Buttons (Nutzer-Feedback: "4 weitere
    /// Buttons, wo man den Pfad frei konfigurieren kann, den man speichern will") - jeder Button
    /// kommt mit einem kleinen "✎"-Knopf zum Umbenennen/Pfad-Festlegen (siehe
    /// <see cref="ConfigureCustomQuickPathButton"/>), da für 4 freie Buttons ein eigener
    /// Verwaltungsdialog unverhältnismäßig wäre.
    /// </summary>
    private void BuildCustomQuickPathButtons()
    {
        CustomQuickPathButtons.Children.Clear();

        for (var i = 0; i < _quickPathSettings.CustomButtons.Count; i++)
        {
            var index = i; // für die Click-Closures unten eingefangen (Schleifenvariable selbst würde sich sonst ändern)
            var state = _quickPathSettings.CustomButtons[index];

            var container = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 6, 4) };

            var mainButton = new Button
            {
                Content = state.Label,
                Padding = new Thickness(6, 2, 6, 2),
                IsEnabled = state.Path is not null,
                ToolTip = state.Path is null
                    ? "Noch kein Pfad festgelegt - über \"✎\" konfigurieren."
                    : state.Path,
            };
            mainButton.Click += (_, _) =>
            {
                if (_quickPathSettings.CustomButtons[index].Path is { } path)
                {
                    FolderTextBox.Text = path;
                }
            };

            var editButton = new Button
            {
                Content = "✎",
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(2, 0, 0, 0),
                ToolTip = "Name und Pfad dieses Schnellzugriff-Buttons festlegen",
            };
            editButton.Click += (_, _) => ConfigureCustomQuickPathButton(index);

            container.Children.Add(mainButton);
            container.Children.Add(editButton);
            CustomQuickPathButtons.Children.Add(container);
        }
    }

    /// <summary>
    /// Fragt Name UND Pfad für einen freien Schnellzugriff-Button ab (Microsoft.VisualBasic.
    /// Interaction.InputBox für den Namen, klassischer FolderBrowserDialog für den Pfad -
    /// dieselbe pragmatische Wahl wie bei ExplorerPanel.NewFolder_Click bzw. dem bereits
    /// vorhandenen "Durchsuchen…"-Button oben in diesem Fenster). Ein Abbruch des Namens-Dialogs
    /// bricht die GESAMTE Aktion ab; ein Abbruch NUR des Ordner-Dialogs behält dagegen den
    /// bisherigen Pfad bei (falls schon einer gesetzt war) und übernimmt trotzdem die neue
    /// Beschriftung - reines Umbenennen soll nicht zwingend einen Ordner-Dialog erzwingen.
    /// </summary>
    private void ConfigureCustomQuickPathButton(int index)
    {
        var current = _quickPathSettings.CustomButtons[index];

        var newLabel = Microsoft.VisualBasic.Interaction.InputBox(
            "Name für diesen Schnellzugriff-Button:", "Schnellzugriff-Button konfigurieren", current.Label);

        if (string.IsNullOrWhiteSpace(newLabel))
        {
            return;
        }

        current.Label = newLabel.Trim();

        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = $"Ordner für \"{current.Label}\" auswählen (Abbrechen behält den bisherigen Pfad bei)",
        };
        if (!string.IsNullOrEmpty(current.Path) && Directory.Exists(current.Path))
        {
            dialog.SelectedPath = current.Path;
        }

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            current.Path = dialog.SelectedPath;
        }

        SaveQuickPathSettings();
        BuildCustomQuickPathButtons();
    }

    /// <summary>Nutzer-Feedback: "Man sollte auch die Möglichkeit haben die Buttons
    /// auszublenden." - Zustand wird über <see cref="QuickPathSettings.Visible"/> dauerhaft
    /// gespeichert (siehe <see cref="QuickPathVisibilityToggle_Click"/>).</summary>
    private void ApplyQuickPathVisibility(bool visible)
    {
        QuickPathVisibilityToggle.IsChecked = visible;
        QuickPathButtonsPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        QuickPathVisibilityToggle.Content = visible ? "▾ Ausblenden" : "▸ Einblenden";
    }

    private void QuickPathVisibilityToggle_Click(object sender, RoutedEventArgs e)
    {
        var visible = QuickPathVisibilityToggle.IsChecked == true;
        ApplyQuickPathVisibility(visible);
        _quickPathSettings.Visible = visible;
        SaveQuickPathSettings();
    }

    private void SaveQuickPathSettings()
    {
        try
        {
            QuickPathStore.Save(_quickPathSettings, _quickPathSettingsPath);
        }
        catch (IOException)
        {
            // Einstellung bleibt für die laufende Sitzung wirksam, wird nur nicht dauerhaft gespeichert.
        }
        catch (UnauthorizedAccessException)
        {
            // Einstellung bleibt für die laufende Sitzung wirksam, wird nur nicht dauerhaft gespeichert.
        }
    }

    /// <summary>Vom Hauptfenster aufgerufen, um den Checkbox-Zustand hier zu spiegeln, ohne den Toggle erneut auszulösen (bidirektionale Synchronisierung mit der Ergebnisleiste im Hauptfenster).</summary>
    public void SetHideDescriptionCheckboxState(bool hide)
    {
        _suppressHideDescriptionEvent = true;
        try
        {
            HideDescriptionCheckBox.IsChecked = hide;
        }
        finally
        {
            _suppressHideDescriptionEvent = false;
        }
    }

    private void HideDescriptionCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressHideDescriptionEvent)
        {
            return;
        }

        _setHideDescription(HideDescriptionCheckBox.IsChecked == true);
    }

    private SearchOptions CurrentOptions => new()
    {
        MatchCase = MatchCaseCheckBox.IsChecked == true,
        WholeWord = WholeWordCheckBox.IsChecked == true,
        IgnoreFormatting = IgnoreFormattingCheckBox.IsChecked == true,
    };

    /// <summary>Stellt die Suche auf "Ordner (Dateien)" mit diesem Ordner ein und setzt den Cursor in das Suchfeld (Lupe in der Verzeichnis-Übersicht).</summary>
    public void SearchInFolder(string folder)
    {
        FilesModeRadio.IsChecked = true;
        FolderTextBox.Text = folder;
        UpdateModeVisibility();
        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
    }

    private bool IsFilesMode => FilesModeRadio.IsChecked == true;

    private void ModeRadio_Checked(object sender, RoutedEventArgs e) => UpdateModeVisibility();

    private void UpdateModeVisibility()
    {
        // FolderPanel ist beim ALLERERSTEN Auslösen von ModeRadio_Checked (durch
        // IsChecked="True" auf CurrentDocumentModeRadio direkt in der XAML) unter Umständen
        // noch nicht angelegt, da es in der XAML-Reihenfolge NACH den RadioButtons steht -
        // daher defensiv prüfen, statt eine NullReferenceException zu riskieren.
        if (FolderPanel is null)
        {
            return;
        }

        FolderPanel.Visibility = IsFilesMode ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        // Bewusst der klassische WinForms-Ordnerauswahldialog (siehe UseWindowsForms im
        // .csproj) statt des neueren Microsoft.Win32.OpenFolderDialog aus .NET 8 - Letzterer
        // konnte in dieser Cloud-Umgebung ohne .NET-SDK nicht gegen die tatsächlich installierte
        // SDK-Version verifiziert werden, FolderBrowserDialog ist seit Jahren stabil verfügbar.
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Suchordner auswählen",
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            FolderTextBox.Text = dialog.SelectedPath;
        }
    }

    private void AddFavorite_Click(object sender, RoutedEventArgs e)
    {
        var folder = FolderTextBox.Text.Trim();
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        if (!_favorites.Contains(folder, StringComparer.OrdinalIgnoreCase))
        {
            _favorites.Add(folder);
            SaveFavoritesMerged();
        }
    }

    private void RemoveFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (FavoritesList.SelectedItem is string selected)
        {
            _favorites.Remove(selected);
            SaveFavoritesMerged();
        }
    }

    /// <summary>Speichert die Favoriten und übernimmt dabei Einträge, die ein anderes Fenster inzwischen ergänzt hat.</summary>
    private void SaveFavoritesMerged()
    {
        var merged = SearchFavoritesStore.SaveMerged(_favoritesBaseline, _favorites.ToList(), _favoritesPath);
        _favorites.Clear();
        foreach (var folder in merged)
        {
            _favorites.Add(folder);
        }

        _favoritesBaseline = merged.ToList();
    }

    private void FavoritesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FavoritesList.SelectedItem is string selected)
        {
            FolderTextBox.Text = selected;
        }
    }

    private void FindAll_Click(object sender, RoutedEventArgs e)
    {
        var query = SearchTextBox.Text;
        if (string.IsNullOrEmpty(query))
        {
            StatusText.Text = "Bitte einen Suchbegriff eingeben.";
            return;
        }

        _results.Clear();

        if (IsFilesMode)
        {
            var folder = NormalizeFolderPath(FolderTextBox.Text.Trim());
            if (!Directory.Exists(folder))
            {
                StatusText.Text = "Der angegebene Ordner wurde nicht gefunden.";
                return;
            }

            var matches = FileSearchService.Search(folder, query, CurrentOptions);
            foreach (var match in matches)
            {
                _results.Add(SearchResultRow.FromFileMatch(match, _getHideDescription()));
            }

            var fileCount = matches.Select(m => m.FilePath).Distinct().Count();
            StatusText.Text = $"{matches.Count} Treffer in {fileCount} Datei(en).";
        }
        else
        {
            var text = _getActiveDocumentText();
            var matches = TextSearch.FindAll(text, query, CurrentOptions);
            foreach (var match in matches)
            {
                _results.Add(SearchResultRow.FromDocumentMatch(match, _getHideDescription()));
            }

            StatusText.Text = $"{matches.Count} Treffer im aktuellen Dokument.";
        }
    }

    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        var query = SearchTextBox.Text;
        if (string.IsNullOrEmpty(query))
        {
            StatusText.Text = "Bitte einen Suchbegriff eingeben.";
            return;
        }

        var replacement = ReplaceTextBox.Text;

        if (IsFilesMode)
        {
            var folder = NormalizeFolderPath(FolderTextBox.Text.Trim());
            if (!Directory.Exists(folder))
            {
                StatusText.Text = "Der angegebene Ordner wurde nicht gefunden.";
                return;
            }

            var files = FileSearchService.EnumerateSearchableFiles(folder);
            var plans = FileReplaceService.ComputeReplacements(files, query, replacement, CurrentOptions);

            if (plans.Count == 0)
            {
                StatusText.Text = "Keine Treffer - es wurde nichts ersetzt.";
                return;
            }

            var totalReplacements = plans.Sum(p => p.ReplacementCount);
            var description = $"Suchen & Ersetzen in Dateien: \"{query}\" → \"{replacement}\" ({plans.Count} Datei(en))";
            var changedFiles = _executeFileReplacements(plans, description);

            StatusText.Text = $"{totalReplacements} Ersetzung(en) in {changedFiles} Datei(en) durchgeführt.";

            _results.Clear();
            foreach (var plan in plans)
            {
                _results.Add(SearchResultRow.FromReplacementPlan(plan, _getHideDescription()));
            }
        }
        else
        {
            var text = _getActiveDocumentText();
            var (newText, count) = TextSearch.ReplaceAll(text, query, replacement, CurrentOptions);

            if (count == 0)
            {
                StatusText.Text = "Keine Treffer im aktuellen Dokument - es wurde nichts ersetzt.";
                return;
            }

            _replaceActiveDocumentText(newText);
            StatusText.Text = $"{count} Ersetzung(en) im aktuellen Dokument durchgeführt (Strg+Z im Textbereich macht dies rückgängig, solange noch nicht gespeichert).";
            _results.Clear();
        }
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsList.SelectedItem is not SearchResultRow row)
        {
            return;
        }

        if (row.FilePath is { } filePath)
        {
            _openSearchResult(filePath, row.Offset, row.Length);
        }
        else
        {
            _selectInActiveDocument(row.Offset, row.Length);
        }
    }

    /// <summary>
    /// Normalisiert den eingegebenen/durchsuchten Ordnerpfad über <see cref="Path.GetFullPath"/>,
    /// bevor er für die Dateiaufzählung verwendet wird (Nutzer-Feedback: "nach Find und Replace in
    /// einem Ordner wird die geöffnete Datei nicht aktualisiert"). Ursache: ein z. B. aus dem
    /// Windows-Explorer-Adressfeld eingefügter Ordnerpfad endet häufig auf einen Backslash — ohne
    /// Normalisierung würde <see cref="Directory.EnumerateFiles(string)"/> dadurch Dateipfade mit
    /// doppeltem Backslash liefern, die rein textuell nicht mehr zu dem über den Dateidialog
    /// geöffneten (bereits kanonischen) Pfad derselben Datei passen — der Tab-Abgleich in
    /// <see cref="ParadoxModTool.Editor.EditorPane.ReloadIfOpen"/> schlägt dann fehl, obwohl es
    /// dieselbe Datei ist. Bei einem ungültigen Pfad wird der unveränderte, nur getrimmte Wert
    /// zurückgegeben, damit die anschließende <see cref="Directory.Exists"/>-Prüfung wie gewohnt
    /// eine verständliche Fehlermeldung liefert, statt hier eine Ausnahme durchschlagen zu lassen.
    /// </summary>
    private static string NormalizeFolderPath(string folder)
    {
        if (string.IsNullOrEmpty(folder))
        {
            return folder;
        }

        try
        {
            return Path.GetFullPath(folder);
        }
        catch (ArgumentException)
        {
            return folder;
        }
        catch (NotSupportedException)
        {
            return folder;
        }
        catch (PathTooLongException)
        {
            return folder;
        }
    }
}

/// <summary>
/// Eine Zeile in der Trefferliste — sowohl für Dokument- als auch Dateisuche/-ersetzung
/// verwendet, UND von zwei Anzeigen gleichzeitig gebunden (dem modalen Suchen-Fenster sowie der
/// Ergebnisleiste im Hauptfenster, siehe <see cref="SearchReplaceWindow"/>). Implementiert
/// <see cref="INotifyPropertyChanged"/>, damit das Umschalten der "Nur gefundenen Code anzeigen"-
/// Option (Nutzer-Feedback) bereits vorhandene Zeilen live aktualisiert, statt die komplette
/// Liste neu aufbauen zu müssen.
/// </summary>
public sealed class SearchResultRow : INotifyPropertyChanged
{
    private bool _hideDescription;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? FilePath { get; private init; }

    public int LineNumber { get; private init; }

    public string LineText { get; private init; } = string.Empty;

    /// <summary>
    /// Der exakt gefundene Code (siehe <see cref="TextMatch.MatchedText"/>) — Nutzer-Feedback:
    /// "nur den Code, der tatsächlich gefunden wurde, anzeigen ... damit man es rauskopieren und
    /// verwenden kann". Bei einer reinen Ersetzungs-Zusammenfassung (<see cref="FromReplacementPlan"/>)
    /// gibt es keinen einzelnen gefundenen Text, daher dort leer.
    /// </summary>
    public string MatchedText { get; private init; } = string.Empty;

    public int Offset { get; private init; }

    public int Length { get; private init; }

    /// <summary>Die beschreibende Zeile ("Datei — Zeile N: ..."), ausblendbar über <see cref="HideDescription"/>.</summary>
    public string Description => FilePath is null
        ? $"Zeile {LineNumber}: {LineText}"
        : LineNumber > 0
            ? $"{Path.GetFileName(FilePath)} — Zeile {LineNumber}: {LineText}"
            : $"{Path.GetFileName(FilePath)} — {LineText}";

    /// <summary>Ob die Beschreibungszeile ausgeblendet ist (Nutzer-Feedback: Toggle für "nur den gefundenen Code anzeigen").</summary>
    public bool HideDescription
    {
        get => _hideDescription;
        private set
        {
            if (_hideDescription == value)
            {
                return;
            }

            _hideDescription = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HideDescription)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DescriptionVisibility)));
        }
    }

    public Visibility DescriptionVisibility => HideDescription ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Wird vom Hauptfenster auf ALLEN Zeilen der geteilten Liste aufgerufen, wenn der Toggle umgeschaltet wird.</summary>
    public void SetHideDescription(bool hide) => HideDescription = hide;

    public static SearchResultRow FromDocumentMatch(TextMatch match, bool hideDescription) => new()
    {
        FilePath = null,
        LineNumber = match.LineNumber,
        LineText = match.LineText,
        MatchedText = match.MatchedText,
        Offset = match.Offset,
        Length = match.Length,
        HideDescription = hideDescription,
    };

    public static SearchResultRow FromFileMatch(FileSearchMatch match, bool hideDescription) => new()
    {
        FilePath = match.FilePath,
        LineNumber = match.Match.LineNumber,
        LineText = match.Match.LineText,
        MatchedText = match.Match.MatchedText,
        Offset = match.Match.Offset,
        Length = match.Match.Length,
        HideDescription = hideDescription,
    };

    public static SearchResultRow FromReplacementPlan(FileReplacementPlan plan, bool hideDescription) => new()
    {
        FilePath = plan.FilePath,
        LineNumber = 0,
        LineText = $"{plan.ReplacementCount} Ersetzung(en)",
        MatchedText = string.Empty,
        Offset = 0,
        Length = 0,
        HideDescription = hideDescription,
    };
}
