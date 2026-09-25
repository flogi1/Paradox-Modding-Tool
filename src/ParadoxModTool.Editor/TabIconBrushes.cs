using System.Windows;
using System.Windows.Media;

namespace ParadoxModTool.Editor;

/// <summary>
/// Farben der Tab-Symbole als Anwendungs-Ressourcen (<c>{DynamicResource Icon.Note}</c> usw.), gemeinsam für beide Arbeitsbereiche und alle
/// Stellen, die dasselbe Symbol zeigen. Das Hauptfenster setzt sie beim Start, beim Theme-Wechsel und bei einer eigenen Farbe mit
/// <see cref="Set"/>; jede Änderung ersetzt den Pinsel (ein Pinsel in einer Vorlage wird beim Versiegeln der Vorlage eingefroren und
/// ließe sich nicht mehr umfärben). Die Farbe liegt jeweils auf einer Plakette HINTER dem Emoji, nicht auf dem Emoji selbst: WPF färbt
/// farbige Emoji-Glyphen über Foreground nicht zuverlässig um.
/// </summary>
public static class TabIconBrushes
{
    /// <summary>Plakette hinter 🗒 im Tab und "Notiz anzeigen"-Button.</summary>
    public const string NoteKey = "Icon.Note";

    /// <summary>Plakette hinter 🔖 im Tab, Lesezeichen-Rand und Übersichtsbalken im Vergleich. Standard: Theme-Token "Bookmark".</summary>
    public const string BookmarkKey = "Icon.Bookmark";

    /// <summary>Rand der Bug-Plakette. Standard: Theme-Token "Danger".</summary>
    public const string BugKey = "Icon.Bug";

    /// <summary>Füllung der Vanilla-Raute.</summary>
    public const string VanillaKey = "Icon.Vanilla";

    /// <summary>Plakette hinter 🛠. Standard: durchsichtig (wie bisher ohne Plakette).</summary>
    public const string ModKey = "Icon.Mod";

    public static readonly Color DefaultNote = Color.FromRgb(0xD9, 0xA6, 0x2E);
    public static readonly Color DefaultVanilla = Color.FromRgb(0xE6, 0xB4, 0x22);

    /// <summary>Setzt die Farbe eines Symbols (neuer, eingefrorener Pinsel unter <paramref name="key"/>).</summary>
    public static void Set(string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        Application.Current.Resources[key] = brush;
    }

    /// <summary>Aktueller Pinsel eines Symbols (für Code, der selbst zeichnet); durchsichtig, solange noch keiner gesetzt ist.</summary>
    public static Brush Get(string key) => Application.Current?.Resources[key] as Brush ?? Brushes.Transparent;

    /// <summary>Aktuelle Farbe eines Symbols.</summary>
    public static Color CurrentColor(string key) => Get(key) is SolidColorBrush solid ? solid.Color : Colors.Transparent;
}
