using System.Runtime.InteropServices;
using System.Windows;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Layout;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Bildschirm der Vorschau "wie im Spiel" für Designer und Inspektor: klassisch die <c>settings.txt</c> (<see cref="GuiSettings.GameSettingsPath"/>),
/// Jomini <c>pdx_settings.txt</c> bzw. <c>pdx_settings.json</c> im Benutzerordner (<see cref="GuiSettings.GameUserFolder"/>).
/// </summary>
public static class GuiPreviewScreen
{
    /// <summary>Kleinste erlaubte UI-Skalierung (0 hieße: Teilen durch 0 in der Engine).</summary>
    public const double MinUiScale = 0.25;

    /// <summary>Größte erlaubte UI-Skalierung.</summary>
    public const double MaxUiScale = 4;

    /// <summary>
    /// Auflösung und UI-Skalierung aus den Einstellungen des aktiven Spiels; <see langword="null"/>, wenn sie fehlen oder unlesbar sind. Jomini: nennt die
    /// Datei keine feste Auflösung (randloses Fenster, Victoria 3, Europa Universalis V), gilt die des Hauptbildschirms; die Skalierung wird auf
    /// <see cref="MinUiScale"/>…<see cref="MaxUiScale"/> begrenzt.
    /// </summary>
    public static (int Width, int Height, double UiScale)? FromGame()
    {
        if (GuiSettings.Layout.IsClassic)
        {
            return GuiSettings.GameSettingsPath is { } path ? ClassicScreenSettings.TryRead(path) : null;
        }

        if (GuiSettings.GameUserFolder is not { } folder || JominiGameSettings.TryRead(folder) is not { } settings)
        {
            return null;
        }

        var primary = settings.Width is null || settings.Height is null ? PrimaryScreenPixels() : default;
        return (settings.Width ?? primary.Width, settings.Height ?? primary.Height, Math.Clamp(settings.UiScale, MinUiScale, MaxUiScale));
    }

    /// <summary>Standardsprache und Rückfall der Jomini-Texte in kanonischer Form.</summary>
    public const string DefaultLanguage = "l_english";

    /// <summary>
    /// Sprache des Spiels für die Vorschautexte in kanonischer Form (<c>l_german</c>): die Jomini-Einstellungen im Benutzerordner (<c>pdx_settings</c>),
    /// sonst (klassisch, keine oder unlesbare Einstellungen, keine Sprache darin) <c>l_english</c>.
    /// </summary>
    public static string GameLanguage()
    {
        if (GuiSettings.Layout.IsClassic
            || GuiSettings.GameUserFolder is not { } folder
            || JominiGameSettings.TryRead(folder)?.Language?.Trim().ToLowerInvariant() is not { Length: > 0 } language)
        {
            return DefaultLanguage;
        }

        return language.StartsWith("l_", StringComparison.Ordinal) ? language : "l_" + language;
    }

    /// <summary>Sprachcode ohne das Präfix <c>l_</c>, wie ihn die Leiste zeigt (<c>l_german</c> wird <c>german</c>).</summary>
    public static string LanguageName(string language) => language.StartsWith("l_", StringComparison.Ordinal) ? language[2..] : language;

    /// <summary>
    /// Bildschirm ohne lesbare Spieleinstellungen: klassisch 1920×1080, Jomini der Hauptbildschirm (<see cref="PrimaryScreenPixels"/>); Skalierung jeweils 1.
    /// </summary>
    public static (int Width, int Height, double UiScale) Fallback()
    {
        if (GuiSettings.Layout.IsClassic)
        {
            return (1920, 1080, 1);
        }

        var (width, height) = PrimaryScreenPixels();
        return (width, height, 1);
    }

    /// <summary>Größe des Hauptbildschirms in physischen Pixeln: <see cref="SystemParameters.PrimaryScreenWidth"/> (geräteunabhängig) mal System-DPI / 96.</summary>
    public static (int Width, int Height) PrimaryScreenPixels()
    {
        // Erst SystemParameters lesen: WPF macht den Prozess dabei DPI-bewusst; vorher meldet Windows einem nicht DPI-bewussten Prozess immer 96.
        var width = SystemParameters.PrimaryScreenWidth;
        var height = SystemParameters.PrimaryScreenHeight;
        var scale = SystemDpi() / 96.0;
        return ((int)Math.Round(width * scale), (int)Math.Round(height * scale));
    }

    private static uint SystemDpi()
    {
        try
        {
            return GetDpiForSystem() is var dpi and > 0 ? dpi : 96;
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
            // Ohne die Funktion (vor Windows 10 1607): 96 DPI annehmen.
            return 96;
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
}
