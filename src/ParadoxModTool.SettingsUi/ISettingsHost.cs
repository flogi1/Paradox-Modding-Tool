using System.Windows;
using System.Windows.Media;
using ParadoxModTool.Localization;
using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi;

/// <summary>Ein wählbares Theme: Auswahl-Kennung (auch "system") und Anzeigename.</summary>
public sealed record ThemeChoice(string Id, string Name);

/// <summary>Ergebnis der Discord-Prüfung für die Einstellungsseite (ohne Token).</summary>
public sealed record DiscordSetupState(bool TokenSaved, bool TokenValid, string BotName, bool IntentEnabled, string InviteUrl, IReadOnlyList<string> Servers, string? Error);

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

    /// <summary>Programm- und User-Ordner der Sprachdateien (Statusanzeige der Sprachseite).</summary>
    LocaleSources LocaleSources { get; }

    /// <summary>Legt den User-Ordner der Sprachdateien bei Bedarf an und öffnet ihn im Explorer.</summary>
    void OpenTranslationFolder();

    /// <summary>https-Adresse der Anleitung für Übersetzer (TRANSLATING.md auf GitHub).</summary>
    string TranslationGuideUrl { get; }

    /// <summary>Schließt dieses Fenster auf dem normalen Weg und startet es danach für dasselbe Spiel neu (etwa für eine neue Sprache).</summary>
    void RestartApp();

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

    /// <summary>Bugtracker: Status von gh (gefunden, angemeldet) als Anzeigetext.</summary>
    Task<string> CheckGitHubStatusAsync();

    /// <summary>Startet <c>gh auth login</c> in einem eigenen Fenster und liefert den Hinweistext für die Seite.</summary>
    string StartGitHubLogin();

    bool HasDiscordToken { get; }

    /// <summary>Speichert den Bot-Token verschlüsselt; liefert den Hinweistext für die Seite.</summary>
    string SaveDiscordToken(string token);

    void RemoveDiscordToken();

    /// <summary>Nur für "Anzeigen" auf der Seite.</summary>
    string? ReadDiscordToken();

    Task<DiscordSetupState> CheckDiscordSetupAsync();

    /// <summary>Öffnet eine https-Adresse im Browser.</summary>
    void OpenUrl(string url);

    bool UpdatesAvailable { get; }

    Task CheckForUpdatesAsync();
}
