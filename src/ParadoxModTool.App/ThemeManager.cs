using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using ParadoxModTool.Session;
using ParadoxModTool.Theming;
// Wie überall in diesem Projekt: WPF + Windows Forms machen einige Typnamen mehrdeutig (CS0104).
using Application = System.Windows.Application;
using Control = System.Windows.Controls.Control;
using SystemColors = System.Windows.SystemColors;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace ParadoxModTool.App;

/// <summary>
/// Zentrale Theme-Verwaltung (Paket 12b): hält die Auswahl (Hell, Dunkel, "wie Windows" oder ein eigenes Theme), die
/// eigenen Themes und die persönlichen Kategorie-Farb-Anpassungen an den eingebauten Themes und schreibt das
/// wirksame Theme als Ressourcen "Theme.&lt;Token&gt;" in die Anwendungsressourcen. Alle Stile und Fenster verwenden
/// <c>DynamicResource</c>, ein Wechsel wirkt daher sofort. Code, der Farben selbst zeichnet, liest
/// <see cref="GetBrush"/>/<see cref="GetColor"/> und aktualisiert sich bei <see cref="ThemeChanged"/>.
/// </summary>
public static class ThemeManager
{
    private const string ResourcePrefix = "Theme.";

    private static ThemeDefinition _current = BuiltInThemes.CreateLight();
    private static string _selectionId = ThemeSelection.SystemId;
    private static List<ThemeDefinition> _customThemes = new();
    private static Dictionary<string, Dictionary<string, string>> _overridesByTheme = new();
    private static bool _initialized;

    /// <summary>Wird nach jedem Anwenden eines Themes ausgelöst (Auswahl, Bearbeiten, Windows-Wechsel).</summary>
    public static event EventHandler? ThemeChanged;

    public static ThemeDefinition Current => _current;

    /// <summary>Kennung der Auswahl: "system", eine eingebaute oder eine eigene Theme-Kennung.</summary>
    public static string SelectionId => _selectionId;

    public static IReadOnlyList<ThemeDefinition> CustomThemes => _customThemes;

    /// <summary>Wirksame Kategorie-Farben (Schlüssel = <c>FileCategory</c>-Name) des aktuellen Themes samt persönlichen Anpassungen.</summary>
    public static Dictionary<string, string> EffectiveCategoryColors { get; private set; } = new();

    public static Brush GetBrush(string token) =>
        Application.Current.Resources[ResourcePrefix + token] as Brush
        ?? throw new KeyNotFoundException($"Theme-Token '{token}' ist nicht definiert.");

    public static Color GetColor(string token) => GetBrush(token) is SolidColorBrush solid ? solid.Color : Colors.Transparent;

    /// <summary>
    /// Lädt Auswahl, eigene Themes und Anpassungen aus der Session (kann <see langword="null"/> sein) und wendet das Theme an.
    /// Muss vor dem Erzeugen der ersten Fenster aufgerufen werden. Eine alte Session mit global gespeicherten
    /// Kategorie-Farben wird einmalig zu den Anpassungen des Themes "Hell" übernommen.
    /// </summary>
    public static void Initialize(AppSessionState? state)
    {
        if (!_initialized)
        {
            _initialized = true;
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, _) => Attach(sender as Window)));
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (e.Category == UserPreferenceCategory.General && _selectionId == ThemeSelection.SystemId)
                {
                    Application.Current?.Dispatcher.BeginInvoke(new Action(Reapply));
                }
            };
        }

        _customThemes = ThemeStore.LoadAll(ThemeStore.GetDefaultDirectory());
        _selectionId = state?.ThemeId ?? ThemeSelection.SystemId;
        _overridesByTheme = state?.CategoryColorOverridesByTheme
            .ToDictionary(kv => kv.Key, kv => new Dictionary<string, string>(kv.Value))
            ?? new Dictionary<string, Dictionary<string, string>>();

        if (state is { CategoryColorOverrides.Count: > 0 } && !_overridesByTheme.ContainsKey(ThemeDefinition.LightId))
        {
            _overridesByTheme[ThemeDefinition.LightId] = new Dictionary<string, string>(state.CategoryColorOverrides);
        }

        Reapply();
    }

    /// <summary>Schreibt Auswahl und Anpassungen in die Session (die alte globale Kategorie-Farb-Liste wird geleert).</summary>
    public static void WriteTo(AppSessionState state)
    {
        state.ThemeId = _selectionId;
        state.CategoryColorOverridesByTheme = _overridesByTheme
            .Where(kv => kv.Value.Count > 0)
            .ToDictionary(kv => kv.Key, kv => new Dictionary<string, string>(kv.Value));
        state.CategoryColorOverrides = new Dictionary<string, string>();
    }

    public static void SelectTheme(string selectionId)
    {
        _selectionId = selectionId;
        Reapply();
    }

    /// <summary>Wendet ein noch nicht gespeichertes Arbeitsexemplar als Live-Vorschau an (Auswahl bleibt unverändert).</summary>
    public static void Preview(ThemeDefinition theme)
    {
        Apply(theme);
    }

    /// <summary>Wendet die aktuelle Auswahl neu an (nach Abbrechen einer Vorschau, nach Windows-Wechsel).</summary>
    public static void Reapply() => Apply(ThemeSelection.Resolve(_selectionId, SystemIsDark(), _customThemes));

    public static ThemeDefinition? FindCustom(string id) => _customThemes.FirstOrDefault(t => t.Id == id);

    /// <summary>Speichert ein eigenes Theme (neu oder geändert) und lädt die Liste neu; bei aktiver Auswahl wird es sofort angewendet.</summary>
    public static void SaveCustom(ThemeDefinition theme)
    {
        ThemeStore.Save(theme, ThemeStore.GetDefaultDirectory());
        _customThemes = ThemeStore.LoadAll(ThemeStore.GetDefaultDirectory());
        Reapply();
    }

    public static void DeleteCustom(string id)
    {
        ThemeStore.Delete(id, ThemeStore.GetDefaultDirectory());
        _customThemes = ThemeStore.LoadAll(ThemeStore.GetDefaultDirectory());
        _overridesByTheme.Remove(id);
        Reapply();
    }

    /// <summary>
    /// Übernimmt geänderte Kategorie-Farben für das aktive Theme: bei einem eigenen Theme in dessen Datei, bei Hell/Dunkel als
    /// persönliche Anpassung (nur die vom Theme abweichenden Werte).
    /// </summary>
    public static void SetCategoryColors(IReadOnlyDictionary<string, string> colors)
    {
        if (_current.IsBuiltIn)
        {
            var defaults = BuiltInThemes.TryGet(_current.Id)!.CategoryColors;
            _overridesByTheme[_current.Id] = colors
                .Where(kv => !defaults.TryGetValue(kv.Key, out var d) || !string.Equals(d, kv.Value, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            Reapply();
            return;
        }

        var edited = _current.Clone();
        foreach (var (key, value) in colors)
        {
            edited.CategoryColors[key] = value;
        }

        SaveCustom(edited);
    }

    /// <summary>Kategorie-Farben zurück auf die Werte des Themes (verwirft persönliche Anpassungen an Hell/Dunkel).</summary>
    public static void ResetCategoryColors()
    {
        _overridesByTheme.Remove(_current.Id);
        Reapply();
    }

    /// <summary>Als neue eigene Kopie speichern, inklusive der aktuell wirksamen Kategorie-Farben.</summary>
    public static ThemeDefinition CreateCustomCopy(ThemeDefinition source, string name)
    {
        var copy = source.Clone();
        copy.Id = ThemeStore.NewCustomId();
        copy.Name = name;
        if (source.Id == _current.Id)
        {
            copy.CategoryColors = new Dictionary<string, string>(EffectiveCategoryColors);
        }

        return copy;
    }

    public static bool SystemIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    private static void Apply(ThemeDefinition theme)
    {
        _current = theme;
        _overridesByTheme.TryGetValue(theme.Id, out var overrides);
        EffectiveCategoryColors = ThemeSelection.EffectiveCategoryColors(theme, overrides);

        var resources = Application.Current.Resources;
        foreach (var token in ThemeTokens.All)
        {
            resources[ResourcePrefix + token.Key] = Freeze(ParseColor(theme.Colors[token.Key]));
        }

        MapSystemColors(resources);

        foreach (Window window in Application.Current.Windows)
        {
            ApplyTitleBar(window);
        }

        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Überschreibt die Windows-Systemfarben, auf die die Standardvorlagen (z. B. Textauswahl, Bildlaufleisten-Ecke)
    /// zurückgreifen, damit auch nicht eigens gestaltete Stellen zum Theme passen.
    /// </summary>
    private static void MapSystemColors(ResourceDictionary resources)
    {
        void Set(ResourceKey brushKey, ResourceKey colorKey, string token)
        {
            var color = ParseColor(ThemeColor(token));
            resources[brushKey] = Freeze(color);
            resources[colorKey] = color;
        }

        Set(SystemColors.WindowBrushKey, SystemColors.WindowColorKey, "PanelBackground");
        Set(SystemColors.WindowTextBrushKey, SystemColors.WindowTextColorKey, "Text");
        Set(SystemColors.ControlBrushKey, SystemColors.ControlColorKey, "WindowBackground");
        Set(SystemColors.ControlTextBrushKey, SystemColors.ControlTextColorKey, "Text");
        Set(SystemColors.ControlDarkBrushKey, SystemColors.ControlDarkColorKey, "ControlBorder");
        Set(SystemColors.ControlLightBrushKey, SystemColors.ControlLightColorKey, "ControlBackground");
        Set(SystemColors.HighlightBrushKey, SystemColors.HighlightColorKey, "Accent");
        Set(SystemColors.HighlightTextBrushKey, SystemColors.HighlightTextColorKey, "AccentText");
        resources[SystemColors.InactiveSelectionHighlightBrushKey] = Freeze(ParseColor(ThemeColor("InactiveSelection")));
        resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = Freeze(ParseColor(ThemeColor("Text")));
        Set(SystemColors.GrayTextBrushKey, SystemColors.GrayTextColorKey, "TextDisabled");
        Set(SystemColors.MenuBrushKey, SystemColors.MenuColorKey, "PanelBackground");
        Set(SystemColors.MenuTextBrushKey, SystemColors.MenuTextColorKey, "Text");
        Set(SystemColors.InfoBrushKey, SystemColors.InfoColorKey, "ControlBackground");
        Set(SystemColors.InfoTextBrushKey, SystemColors.InfoTextColorKey, "Text");
    }

    private static string ThemeColor(string token) => _current.Colors[token];

    private static Color ParseColor(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    // ----- Fenster: Hintergrund/Vordergrund aus dem Theme, dunkle Windows-Titelleiste -----

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, object> Attached = new();

    /// <summary>
    /// Verbindet ein Fenster mit dem Theme (Hintergrund, Textfarbe, dunkle Titelleiste). Läuft für jedes Fenster automatisch beim
    /// Laden; das Hauptfenster ruft es zusätzlich früh selbst auf, damit beim Start nichts im falschen Theme aufblitzt.
    /// Mehrfaches Aufrufen ist unschädlich.
    /// </summary>
    public static void Attach(Window? window)
    {
        if (window is null or SplashWindow || Attached.TryGetValue(window, out _))
        {
            return;
        }

        Attached.Add(window, new object());

        // Nur setzen, wenn das Fenster keine eigenen Werte im XAML hat.
        if (window.ReadLocalValue(Control.BackgroundProperty) == DependencyProperty.UnsetValue)
        {
            window.SetResourceReference(Control.BackgroundProperty, ResourcePrefix + "WindowBackground");
        }

        if (window.ReadLocalValue(Control.ForegroundProperty) == DependencyProperty.UnsetValue)
        {
            window.SetResourceReference(Control.ForegroundProperty, ResourcePrefix + "Text");
        }

        window.SourceInitialized += (_, _) => ApplyTitleBar(window);
        ApplyTitleBar(window);
    }

    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private static void ApplyTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var dark = _current.IsDark ? 1 : 0;
        if (DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(handle, DwmUseImmersiveDarkModeBefore20H1, ref dark, sizeof(int));
        }
    }
}
