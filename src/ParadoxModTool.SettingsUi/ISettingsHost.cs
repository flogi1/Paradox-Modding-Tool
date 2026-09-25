using System.Windows;
using System.Windows.Media;
using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi;

/// <summary>Ein wählbares Theme: Auswahl-Kennung (auch "system") und Anzeigename.</summary>
public sealed record ThemeChoice(string Id, string Name);

/// <summary>
/// Was das Einstellungsfenster vom Hauptfenster braucht: das Einstellungs-Modell und alles, was nur die App kann (Dialoge, Theme, Farbwahl,
/// Neu-Lernen). Die Editor-Einstellungen (Scope, Klammern, Script-Hilfen, GUI) liest und schreibt die Seite direkt.
/// </summary>
public interface ISettingsHost
{
    AppSettings Settings { get; }

    string? GameExecutablePath { get; }

    string? PlaysetsFilePath { get; }

    void ChooseGameExecutable();

    void ChoosePlaysetsFile();

    void OpenStorageSettings();

    Task RelearnLocalizationProfilesAsync();

    Task RelearnScopeRulesAsync();

    /// <summary>Öffnet "Kategorien verwalten" (modal); danach kann sich die Kategorienliste geändert haben.</summary>
    void OpenCategories();

    /// <summary>GUI-Hauptschalter: baut den Index auf bzw. gibt ihn frei und schließt offene GUI-Fenster.</summary>
    void SetGuiToolsEnabled(bool enabled);

    Task RebuildGuiIndexAsync();

    string CurrentThemeId { get; }

    string CurrentThemeName { get; }

    /// <summary>Wirksame Farbe eines Symbols (<see cref="IconIds"/>) im aktuellen Theme.</summary>
    Color IconColor(string iconId);

    /// <summary>Farbwahl-Dialog; <see langword="null"/> = abgebrochen.</summary>
    Color? PickColor(Color current);

    IReadOnlyList<ThemeChoice> ThemeChoices();

    string SelectedThemeId { get; }

    void SelectTheme(string selectionId);

    void OpenThemeEditor(Window? owner);

    /// <summary>Theme gewechselt oder ein Theme geändert (auch Name).</summary>
    event EventHandler? ThemeChanged;

    IReadOnlyList<UiLanguageOption> Languages { get; }

    /// <summary>Spiel-Anbindung (Plan 4): Anzeigename des Spiels dieses Fensters.</summary>
    string GameDisplayName { get; }

    /// <summary>Es gibt eine pdx_settings.json, in die PMT sich eintragen kann.</summary>
    bool CanRegisterGameEditor { get; }

    /// <summary>Welcher Editor im Spiel eingetragen ist, als Anzeigetext.</summary>
    string GameEditorStatus { get; }

    /// <summary>"PMT als Editor eintragen": prüft, sichert, trägt ein und meldet das Ergebnis selbst.</summary>
    void RegisterGameEditor();

    /// <summary>"Alten Editor wiederherstellen".</summary>
    void RestoreGameEditor();

    bool UpdatesAvailable { get; }

    Task CheckForUpdatesAsync();
}
